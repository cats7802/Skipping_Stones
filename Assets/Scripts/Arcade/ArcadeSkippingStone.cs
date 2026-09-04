using System;
using System.Collections;
using UnityEngine;
using SkippingStones.Arcade.Buffs;
using SkippingStones.Gameplay;

namespace SkippingStones.Arcade
{
    /// <summary>
    /// 🌊 리듬 아케이드 모드 전용 돌 비행 및 판정 엔진 (모듈화 완료)
    /// - 디렉터 확정 리듬 룰:
    ///   1) 포물선 높이 1.8m 통통 귀엽게 완전 고정
    ///   2) 기본 1박(60 BPM) 거리 기반 판정별 증감 및 미스 시 Base 거리 즉시 롤백
    ///   3) 프리셋 3종(아기자기 10m / 스탠다드 12m / 스피드 15m / Custom) 지원
    ///   4) BPM 기반 고정 주기(60~120 BPM) 가속 및 사운드/모멘텀 연동
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class ArcadeSkippingStone : MonoBehaviour
    {
        public enum RhythmPresetType
        {
            Cute_10m,      // 🟢 아기자기 10m 시작
            Standard_12m,  // 🔵 스탠다드 12m 시작
            Speed_15m,     // 🟣 스피드 15m 시작
            Custom         // ⚙️ 커스텀 자유 튜닝
        }

        [System.Serializable]
        public struct PresetData
        {
            public float baseDistance;
            public float perfectDelta;
            public float greatDelta;
            public float goodDelta;
            public float lateDelta;
            public float tooLateDelta;

            public PresetData(float bDist, float pD, float grD, float goD, float lD, float tlD)
            {
                baseDistance = bDist;
                perfectDelta = pD;
                greatDelta = grD;
                goodDelta = goD;
                lateDelta = lD;
                tooLateDelta = tlD;
            }
        }

        [Header("🎛️ 리듬 밸런스 프리셋 및 실시간 튜닝")]
        public RhythmPresetType activePreset = RhythmPresetType.Cute_10m;
        public PresetData customPreset = new PresetData(10.0f, +0.5f, +0.2f, 0.0f, -0.3f, -0.6f);

        [Header("🌊 포물선 형상 (고정 높이)")]
        public float fixedBounceArcHeight = 1.8f;
        public float currentBounceDistance = 10.0f;
        public float waterLevel = 16.0f;

        [Header("🌊 리듬 BPM 및 타이밍 설정")]
        public float baseBPM = 60f;
        public int currentCombo = 0;
        public float currentBPM = 60f;
        public float currentCycleDuration = 1.00f; // BPM 60 = 1.00s
        public bool enableComboAcceleration = true;

        [Header("🌊 모멘텀 (스태미나/라이프)")]
        public float currentMomentum = 60f;
        public float maxMomentum = 100f;

        [Header("상태 모니터링")]
        public float initialLaunchPower = 1.0f;
        public bool isThrown = false;
        public bool isSunk = false;
        public bool isCrashed = false;
        public bool isSkimming = false;
        public int skipCount = 0;
        public float totalDistance = 0f;
        public float skimDistance = 0f;

        [Header("비주얼 및 트레일")]
        public TrailRenderer trail;
        public RhythmRingIndicator rhythmRing;

        [Header("🌀 랜덤 링 (Random Ring) 상태 및 버프")]
        public bool isInRandomRing = false;
        public IRandomRingBuff currentActiveBuff = null;
        public int activeBuffRemainingBounces = 0;
        public float steerAngleBonus = 0f;
        public bool isInvincibleToObstacles = false;
        public float speedMultiplierBonus = 1.0f;

        public event Action<int, string> OnSkipBounced;
        public event Action<float> OnStoneSunk;

        [Header("🎛️ 캐릭터 특성/패시브 고도 오프셋")]
        public float characterHeightModifier = 0f;

        public Vector3 CycleStartPosition => cycleStartPos;
        public Vector3 CycleEndPosition => cycleEndPos;
        public float CycleElapsedTime => cycleElapsedTime;
        public Vector3 CurrentForwardDirection => currentForwardDir;
        public float CurrentBounceArcHeight => fixedBounceArcHeight + characterHeightModifier + (isInvincibleToObstacles ? 1.2f : 0f);

        private Rigidbody rb;
        private Vector3 cycleStartPos;
        private Vector3 cycleEndPos;
        private Vector3 currentForwardDir = Vector3.forward;
        private float cycleElapsedTime = 0f;
        private bool hasTappedInCycle = false;
        private int earlyRetryCount = 0;
        private float pendingSteerAngle = 0f;
        private string pendingGrade = "";

        // 🌟 공용 수면 반사 그림자 컨트롤러 통합
        private readonly WaterReflectionShadowController shadowController = new WaterReflectionShadowController();

        private static readonly PresetData CuteData = new PresetData(10.0f, +0.5f, +0.2f, 0.0f, -0.3f, -0.6f);
        private static readonly PresetData StandardData = new PresetData(12.0f, +0.6f, +0.3f, 0.0f, -0.4f, -0.8f);
        private static readonly PresetData SpeedData = new PresetData(15.0f, +1.0f, +0.5f, 0.0f, -0.5f, -1.0f);

        public PresetData GetCurrentPresetData()
        {
            switch (activePreset)
            {
                case RhythmPresetType.Cute_10m: return CuteData;
                case RhythmPresetType.Standard_12m: return StandardData;
                case RhythmPresetType.Speed_15m: return SpeedData;
                case RhythmPresetType.Custom: return customPreset;
                default: return CuteData;
            }
        }

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = true;
                rb.useGravity = false;
            }
            EnsureTrail();
            EnsureRhythmRing();
            shadowController.Setup();
        }

        private void EnsureTrail()
        {
            if (trail == null) trail = GetComponent<TrailRenderer>();
            if (trail == null) trail = gameObject.AddComponent<TrailRenderer>();

            trail.time = 0.35f;
            trail.startWidth = 0.05f;
            trail.endWidth = 0.005f;
            trail.minVertexDistance = 0.05f;
            trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            trail.receiveShadows = false;

            Material trailMat = Resources.Load<Material>("StoneTrail_Mat");
            if (trailMat == null)
            {
                Shader s = Shader.Find("Universal Render Pipeline/Particles/Unlit")
                           ?? Shader.Find("Sprites/Default");
                if (s != null) trailMat = new Material(s);
            }
            trail.material = trailMat;
        }

        private void EnsureRhythmRing()
        {
            if (rhythmRing == null)
            {
                rhythmRing = FindAnyObjectByType<RhythmRingIndicator>();
            }
            if (rhythmRing == null)
            {
                GameObject ringObj = new GameObject("[RhythmRingIndicator_WorldEffect]");
                rhythmRing = ringObj.AddComponent<RhythmRingIndicator>();
            }
            if (rhythmRing != null)
            {
                rhythmRing.arcadeStone = this;
                rhythmRing.stone = null;
            }
        }

        public void Launch(Vector3 forwardDirection, float powerMultiplier)
        {
            UpdateWaterLevel();
            isThrown = true;
            isSunk = false;
            isCrashed = false;
            isSkimming = false;
            skipCount = 0;
            totalDistance = 0f;
            currentCombo = 0;
            
            PresetData preset = GetCurrentPresetData();
            initialLaunchPower = Mathf.Clamp(powerMultiplier, 0.5f, 2.0f);
            currentBounceDistance = preset.baseDistance * initialLaunchPower;
            currentMomentum = Mathf.Clamp(60f * initialLaunchPower, 30f, maxMomentum);
            UpdateBPM();

            currentForwardDir = new Vector3(forwardDirection.x, 0f, forwardDirection.z).normalized;
            if (currentForwardDir.sqrMagnitude < 0.01f) currentForwardDir = Vector3.forward;

            cycleStartPos = transform.position;
            cycleEndPos = cycleStartPos + currentForwardDir * currentBounceDistance;
            cycleEndPos.y = waterLevel;

            cycleElapsedTime = 0f;
            hasTappedInCycle = false;
            earlyRetryCount = 0;
            pendingGrade = "";
            pendingSteerAngle = 0f;

            isInRandomRing = false;
            if (currentActiveBuff != null)
            {
                currentActiveBuff.OnRemove(this);
                currentActiveBuff = null;
            }
            activeBuffRemainingBounces = 0;
            steerAngleBonus = 0f;
            isInvincibleToObstacles = false;
            speedMultiplierBonus = 1.0f;
        }

        private void Update()
        {
#if ENABLE_INPUT_SYSTEM
            if (UnityEngine.InputSystem.Keyboard.current != null && UnityEngine.InputSystem.Keyboard.current.f2Key.wasPressedThisFrame)
            {
                showDebugHUD = !showDebugHUD;
            }
#else
            if (Input.GetKeyDown(KeyCode.F2))
            {
                showDebugHUD = !showDebugHUD;
            }
#endif

            if (!isThrown || isSunk || isCrashed || isSkimming || isInRandomRing) return;

            cycleElapsedTime += Time.deltaTime;

            var traj = ArcadeRhythmTrajectoryCalculator.EvaluateFlightPosition(
                cycleStartPos, cycleEndPos, CurrentBounceArcHeight, waterLevel, cycleElapsedTime, currentCycleDuration
            );

            // 🪨 지형 및 바위 충돌 검사
            if (!isInvincibleToObstacles)
            {
                Collider[] hits = Physics.OverlapSphere(traj.position, 0.12f, ~0, QueryTriggerInteraction.Ignore);
                for (int i = 0; i < hits.Length; i++)
                {
                    Collider col = hits[i];
                    if (col == null || col.gameObject == gameObject || col.transform.IsChildOf(transform)) continue;

                    if (col.GetComponent<WaterSurface>() != null || col.GetComponentInParent<WaterSurface>() != null) continue;
                    string colName = col.name.ToLower();
                    if (colName.Contains("water") || colName.Contains("surface") || colName.Contains("river") 
                        || colName.Contains("pier") || colName.Contains("platform") 
                        || colName.Contains("character") || colName.Contains("thrower")) continue;

                    bool isTerrain = col.GetComponent<TerrainCollider>() != null || col.GetComponent<UnityEngine.Terrain>() != null || col.GetComponent<MeshCollider>() != null;
                    bool isObstacle = colName.Contains("rock") || colName.Contains("obstacle") || colName.Contains("ground") || colName.Contains("bank");

                    if (isTerrain || isObstacle)
                    {
                        CrashOnLand(traj.position, Vector3.up, col);
                        return;
                    }
                }
            }

            transform.position = traj.position;
            if (traj.rotation != Quaternion.identity)
            {
                transform.rotation = traj.rotation;
            }

            if (traj.isCycleComplete)
            {
                string gradeToExecute = hasTappedInCycle ? pendingGrade : "MISS";
                ExecuteSurfaceImpact(gradeToExecute, pendingSteerAngle);
            }
        }

        private void LateUpdate()
        {
            shadowController.UpdateShadow(transform.position, waterLevel, isThrown && !isSunk && !isCrashed);
        }

        private void OnDestroy()
        {
            if (rhythmRing != null)
            {
                Destroy(rhythmRing.gameObject);
                rhythmRing = null;
            }

            shadowController.Cleanup();
        }

        public bool TryRhythmTap(float steerAngleDegrees, out string resultGrade)
        {
            if (!isThrown || isSunk || isCrashed || isSkimming)
            {
                resultGrade = "NOT IN FLIGHT";
                return false;
            }

            if (hasTappedInCycle)
            {
                if (Mathf.Abs(steerAngleDegrees) > 0.1f)
                {
                    pendingSteerAngle = steerAngleDegrees;
                }
                resultGrade = "ALREADY TAPPED";
                return false;
            }

            float timeRemaining = currentCycleDuration - cycleElapsedTime;

            if (timeRemaining > ArcadeRhythmTrajectoryCalculator.WINDOW_EARLY_RETRY)
            {
                resultGrade = "💦 TOO EARLY (너무 이름)";
                return false;
            }

            if (timeRemaining > ArcadeRhythmTrajectoryCalculator.WINDOW_GOOD)
            {
                if (earlyRetryCount == 0)
                {
                    earlyRetryCount++;
                    resultGrade = "💦 TOO EARLY (재도전 기회 1회 잔여)";
                    return false;
                }
                else
                {
                    hasTappedInCycle = true;
                    pendingGrade = "MISS";
                    pendingSteerAngle = steerAngleDegrees;
                    resultGrade = "❌ TOO EARLY MISS";
                    return true;
                }
            }

            hasTappedInCycle = true;
            string grade = ArcadeRhythmTrajectoryCalculator.EvaluateTimingGrade(timeRemaining);

            pendingGrade = grade;
            pendingSteerAngle = steerAngleDegrees;
            resultGrade = grade;

            if (AudioManager.Instance != null)
            {
                if (grade.Contains("PERFECT")) AudioManager.Instance.Play(SoundType.BouncePerfect);
                else if (grade.Contains("GREAT") || grade.Contains("GOOD")) AudioManager.Instance.Play(SoundType.BounceGood);
                else AudioManager.Instance.Play(SoundType.BounceWater);
            }
            if (rhythmRing != null)
            {
                rhythmRing.PlayHitFeedback(grade);
            }

            return true;
        }

        private void ExecuteSurfaceImpact(string grade, float steerAngle = 0f)
        {
            hasTappedInCycle = true;
            skipCount++;

            float finalSteerAngle = steerAngle;
            if (Mathf.Abs(finalSteerAngle) > 0.1f && steerAngleBonus > 0.01f)
            {
                float sign = Mathf.Sign(finalSteerAngle);
                finalSteerAngle += sign * steerAngleBonus;
            }

            if (Mathf.Abs(finalSteerAngle) > 0.1f)
            {
                currentForwardDir = Quaternion.Euler(0f, finalSteerAngle, 0f) * currentForwardDir;
            }

            PresetData preset = GetCurrentPresetData();
            float distanceDelta = 0f;
            float momentumDelta = 0f;

            if (grade.Contains("PERFECT"))
            {
                distanceDelta = preset.perfectDelta;
                momentumDelta = +20f;
                currentCombo++;
            }
            else if (grade.Contains("GREAT"))
            {
                distanceDelta = preset.greatDelta;
                momentumDelta = +10f;
                currentCombo++;
            }
            else if (grade.Contains("GOOD"))
            {
                distanceDelta = preset.goodDelta;
                momentumDelta = +5f;
                currentCombo++;
            }
            else if (grade.Contains("TOO LATE"))
            {
                distanceDelta = preset.tooLateDelta;
                momentumDelta = -20f;
                currentCombo = 0;
            }
            else if (grade.Contains("LATE"))
            {
                distanceDelta = preset.lateDelta;
                momentumDelta = -10f;
                currentCombo = 0;
            }
            else
            {
                currentBounceDistance = preset.baseDistance * initialLaunchPower;
                distanceDelta = 0f;
                momentumDelta = -30f;
                currentCombo = 0;
            }

            if (!grade.Contains("MISS"))
            {
                currentBounceDistance = Mathf.Max(3.0f, currentBounceDistance + distanceDelta);
            }

            if (currentActiveBuff != null)
            {
                if (activeBuffRemainingBounces > 0)
                {
                    activeBuffRemainingBounces--;
                    currentActiveBuff.OnBounceTick(this, activeBuffRemainingBounces);

                    if (activeBuffRemainingBounces <= 0)
                    {
                        currentActiveBuff.OnRemove(this);
                        currentActiveBuff = null;
                    }
                }
            }

            currentMomentum = Mathf.Clamp(currentMomentum + momentumDelta, 0f, maxMomentum);
            totalDistance += currentBounceDistance;
            UpdateBPM();

            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.SetBGMPitchByBPM(currentBPM, 60f);
            }

            OnSkipBounced?.Invoke(skipCount, grade);

            if (currentMomentum <= 0.1f)
            {
                StartCoroutine(CoSkimmingFinish());
            }
            else
            {
                cycleStartPos = transform.position;
                cycleStartPos.y = waterLevel;
                float nextDistance = currentBounceDistance * speedMultiplierBonus;
                cycleEndPos = cycleStartPos + currentForwardDir * nextDistance;
                cycleEndPos.y = waterLevel;

                cycleElapsedTime = 0f;
                hasTappedInCycle = false;
                earlyRetryCount = 0;
                pendingGrade = "";
                pendingSteerAngle = 0f;
            }
        }

        private IEnumerator CoSkimmingFinish()
        {
            isSkimming = true;
            if (AudioManager.Instance != null) AudioManager.Instance.Play(SoundType.SkimSlide);

            float duration = 1.4f;
            float elapsed = 0f;
            float splashTimer = 0f;
            Vector3 skimStart = transform.position;
            skimStart.y = waterLevel;
            Vector3 skimTarget = skimStart + currentForwardDir * (currentBounceDistance * 0.7f);

            GameController gc = FindAnyObjectByType<GameController>();

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                float ease = 1f - (1f - t) * (1f - t);
                
                float bobbingY = waterLevel + 0.025f + Mathf.Sin(elapsed * 35f) * 0.015f;
                Vector3 curHoriz = Vector3.Lerp(skimStart, skimTarget, ease);
                transform.position = new Vector3(curHoriz.x, bobbingY, curHoriz.z);
                transform.Rotate(0f, 720f * Time.deltaTime * (1f - t), 0f);

                skimDistance = Vector3.Distance(skimStart, new Vector3(curHoriz.x, waterLevel, curHoriz.z));

                splashTimer += Time.deltaTime;
                if (splashTimer >= 0.12f && (1f - t) > 0.15f)
                {
                    splashTimer = 0f;
                    if (SplashEffectSpawner.Instance != null)
                    {
                        SplashEffectSpawner.Instance.SpawnSplash(transform.position, Mathf.Lerp(0.35f, 0.8f, 1f - t));
                    }
                }

                if (gc != null)
                {
                    gc.bannerNotificationText = $"🌊 도로록~ 스키밍 피니시! (+{skimDistance:F1}m 보너스)";
                    gc.lastSkimBonusDist = skimDistance;
                }

                yield return null;
            }

            totalDistance += skimDistance;
            SinkStone();
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!isThrown || isSunk || isCrashed) return;

            if (other.GetComponent<WaterSurface>() != null || other.GetComponentInParent<WaterSurface>() != null) return;
            string colName = other.name.ToLower();
            if (colName.Contains("water") || colName.Contains("surface") || colName.Contains("river") || colName.Contains("lake") || colName.Contains("stream")) return;

            bool isTerrain = other.GetComponent<TerrainCollider>() != null || other.GetComponent<UnityEngine.Terrain>() != null || other.GetComponent<MeshCollider>() != null;
            bool isRock = colName.Contains("rock") || colName.Contains("obstacle");
            bool isGround = colName.Contains("ground") || colName.Contains("bank");

            if (isTerrain || isRock || isGround)
            {
                CrashOnLand(transform.position, Vector3.up, other);
            }
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (!isThrown || isSunk || isCrashed) return;

            if (collision.gameObject.GetComponent<WaterSurface>() != null || collision.gameObject.GetComponentInParent<WaterSurface>() != null) return;
            string hitName = collision.gameObject.name.ToLower();
            if (hitName.Contains("water") || hitName.Contains("surface") || hitName.Contains("river") || hitName.Contains("lake") || hitName.Contains("stream")) return;

            CrashOnLand(collision.contacts.Length > 0 ? collision.contacts[0].point : transform.position, collision.contacts.Length > 0 ? collision.contacts[0].normal : Vector3.up, collision.collider);
        }

        public void CrashOnLand(Vector3 hitPoint, Vector3 hitNormal, Collider hitCollider = null)
        {
            if (isCrashed || isSunk) return;
            isCrashed = true;
            isThrown = false;
            isSkimming = false;

            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.Play(SoundType.ButtonClick);
                AudioManager.Instance.StopBGMFadeOut(1.0f);
            }

            if (SplashEffectSpawner.Instance != null)
            {
                SplashEffectSpawner.Instance.SpawnCrashDustFX(hitPoint, 1.2f);
            }

            string colName = hitCollider != null ? hitCollider.name : "Unknown";
            Debug.Log($"💥 <color=#FF3366>[ArcadeStone Crash]</color> 충돌 오브젝트: '{colName}' | 위치: {hitPoint} | 총 비행거리: {totalDistance:F1}m");

            StartCoroutine(CoCrashTumble(hitPoint, hitNormal));
        }

        private IEnumerator CoCrashTumble(Vector3 hitPoint, Vector3 hitNormal)
        {
            Vector3 reboundVel = (hitNormal + Vector3.up * 0.5f).normalized * 2.5f;
            Vector3 currentPos = hitPoint;
            float elapsed = 0f;
            float duration = 1.0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                reboundVel += Physics.gravity * Time.deltaTime;
                currentPos += reboundVel * Time.deltaTime;

                if (currentPos.y <= waterLevel)
                {
                    currentPos.y = waterLevel;
                    transform.position = currentPos;
                    if (SplashEffectSpawner.Instance != null)
                    {
                        SplashEffectSpawner.Instance.SpawnSplash(currentPos, 0.8f);
                    }
                    break;
                }

                transform.position = currentPos;
                transform.Rotate(360f * Time.deltaTime, 180f * Time.deltaTime, 0f);
                yield return null;
            }

            OnStoneSunk?.Invoke(totalDistance);
        }

        public void SinkStone()
        {
            if (isSunk) return;
            isSunk = true;
            isSkimming = false;
            if (AudioManager.Instance != null) AudioManager.Instance.Play(SoundType.StoneSink);

            OnStoneSunk?.Invoke(totalDistance);
        }

        public void ApplySteerAngle(float angle)
        {
            if (isSunk || isCrashed) return;
            pendingSteerAngle = angle;
        }

        private void UpdateBPM()
        {
            currentBPM = ArcadeRhythmTrajectoryCalculator.CalculateBPM(totalDistance, baseBPM, enableComboAcceleration);
            currentCycleDuration = 60f / currentBPM;
        }

        public void UpdateWaterLevel()
        {
            WaterSurface ws = FindAnyObjectByType<WaterSurface>();
            if (ws != null)
            {
                BoxCollider col = ws.GetComponent<BoxCollider>();
                waterLevel = (col != null) ? col.bounds.max.y : ws.transform.position.y;
            }
            else
            {
                waterLevel = 16.0f;
            }
        }

        [Header("🛠️ 실시간 디버그 HUD 및 비트 메트로놈")]
        public bool showDebugHUD = false;

        private void OnGUI()
        {
            if (!showDebugHUD || !isThrown || isSunk) return;

            Rect safe = Screen.safeArea;
            float scale = Mathf.Max(0.85f, Screen.height / 1080f * 0.9f);
            
            float width = 240f * scale;
            float height = 130f * scale;
            float xPos = safe.xMin + 16f * scale;
            float yPos = safe.yMin + 20f * scale;

            GUIStyle boxStyle = new GUIStyle(GUI.skin.box);
            boxStyle.fontSize = Mathf.RoundToInt(11 * scale);
            boxStyle.normal.textColor = Color.white;
            boxStyle.alignment = TextAnchor.UpperLeft;
            boxStyle.padding = new RectOffset((int)(8 * scale), (int)(8 * scale), (int)(6 * scale), (int)(6 * scale));

            GUIStyle labelStyle = new GUIStyle(GUI.skin.label);
            labelStyle.fontSize = Mathf.RoundToInt(11 * scale);
            labelStyle.normal.textColor = Color.white;
            labelStyle.richText = true;

            GUILayout.BeginArea(new Rect(xPos, yPos, width, height), boxStyle);
            GUILayout.Label($"<b>🎵 [리듬 HUD] (F2 토글)</b>", labelStyle);
            
            float progress = (currentCycleDuration > 0.001f) ? Mathf.Clamp01(cycleElapsedTime / currentCycleDuration) : 0f;
            string pulse = (progress > 0.85f) ? "<color=#FF3366>● [쿵!]</color>" : "<color=#00E5FF>○ [비행]</color>";
            GUILayout.Label($"⏱️ {cycleElapsedTime:F2}s / {currentCycleDuration:F2}s ({progress * 100f:F0}%) {pulse}", labelStyle);

            GUILayout.Label($"🏃 {currentBPM:F0} BPM | 콤보: {currentCombo}", labelStyle);
            GUILayout.Label($"📏 목표: {currentBounceDistance:F1}m | 모멘텀: {currentMomentum:F0}", labelStyle);
            GUILayout.EndArea();
        }

        #region 🌀 랜덤 링 (Random Ring) 인터랙션 & 버프 시퀀스

        public void EnterRandomRing(RandomRing ring)
        {
            if (isInRandomRing || isSunk || isCrashed || !isThrown) return;
            StartCoroutine(CoProcessRandomRingSequence(ring));
        }

        private IEnumerator CoProcessRandomRingSequence(RandomRing ring)
        {
            isInRandomRing = true;

            DualCameraSetup cam = FindAnyObjectByType<DualCameraSetup>();
            if (cam != null)
            {
                cam.SetRingHoldCinematic(true);
            }

            float beatDuration = currentCycleDuration;
            float holdDuration = beatDuration * 2.0f;

            Vector3 ringCenter = ring.transform.position;
            Vector3 snapStartPos = transform.position;
            float snapTime = Mathf.Min(0.55f, beatDuration * 0.5f);
            float snapElapsed = 0f;

            ring.PlayBeatPulse(2, beatDuration);

            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlaySound(SoundType.ThrowWhoosh, 1.1f);
            }

            while (snapElapsed < snapTime)
            {
                snapElapsed += Time.deltaTime;
                float st = Mathf.Clamp01(snapElapsed / snapTime);
                float smoothT = 1f - Mathf.Pow(1f - st, 3f);
                transform.position = Vector3.Lerp(snapStartPos, ringCenter, smoothT);
                transform.Rotate(Vector3.up, 720f * Time.deltaTime, Space.World);
                yield return null;
            }

            float remainingHold = holdDuration - snapTime;
            float holdElapsed = 0f;
            while (holdElapsed < remainingHold)
            {
                holdElapsed += Time.deltaTime;
                Vector3 jitter = UnityEngine.Random.insideUnitSphere * 0.035f;
                transform.position = ringCenter + jitter;
                transform.Rotate(Vector3.up, 1440f * Time.deltaTime, Space.World);
                yield return null;
            }

            if (currentActiveBuff != null)
            {
                currentActiveBuff.OnRemove(this);
            }

            IRandomRingBuff selectedBuff = RandomRingBuffManager.RollRandomBuff();
            currentActiveBuff = selectedBuff;
            activeBuffRemainingBounces = selectedBuff.DurationBounces;
            selectedBuff.OnApply(this);

            string buffMessage = selectedBuff.BuffName;
            GameController gc = FindAnyObjectByType<GameController>();
            if (gc != null)
            {
                gc.lastTimingText = buffMessage;
                gc.bannerNotificationText = $"[RANDOM RING] {buffMessage}";
            }
            Debug.Log($"<color=#00FFFF>[🌀 Random Ring Roll]</color> {buffMessage}");

            float launchDuration = beatDuration * 1.0f;
            float launchDistance = currentBounceDistance * 3.0f;

            if (cam != null)
            {
                cam.SetRingHoldCinematic(false);
                cam.TriggerWarpSpeedFOV(launchDuration, 105f);
            }

            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlaySound(SoundType.BoostPad, 1.4f);
            }
            HapticFeedbackHelper.TriggerPerfectImpact();

            ring.DisappearAndDestroy();

            Vector3 launchStart = transform.position;
            Vector3 launchEnd = launchStart + currentForwardDir * launchDistance;
            launchEnd.y = waterLevel;

            bool hasRiverPath = false;
            float startRiverDist = 0f;
            float targetRiverDist = 0f;
            Vector3 targetRiverCenter = launchEnd;
            Vector3 targetRiverTangent = currentForwardDir;

            if (SkippingStones.Terrain.GlobalRiverPath.Instance != null)
            {
                if (SkippingStones.Terrain.GlobalRiverPath.Instance.GetClosestPointOnRiver(launchStart, out _, out _, out startRiverDist))
                {
                    targetRiverDist = startRiverDist + launchDistance;
                    if (SkippingStones.Terrain.GlobalRiverPath.Instance.EvaluateAtDistance(targetRiverDist, out targetRiverCenter, out targetRiverTangent, out _, out float rWaterY))
                    {
                        hasRiverPath = true;
                        launchEnd = targetRiverCenter;
                        launchEnd.y = (rWaterY > 0.01f) ? rWaterY : waterLevel;
                    }
                }
            }

            float launchElapsed = 0f;
            while (launchElapsed < launchDuration)
            {
                launchElapsed += Time.deltaTime;
                float lt = Mathf.Clamp01(launchElapsed / launchDuration);
                float curvedT = Mathf.Sin(lt * Mathf.PI * 0.5f);

                Vector3 curPosXZ;
                Vector3 curTangent = currentForwardDir;

                if (hasRiverPath && SkippingStones.Terrain.GlobalRiverPath.Instance != null)
                {
                    float curRiverDist = Mathf.Lerp(startRiverDist, targetRiverDist, curvedT);
                    if (SkippingStones.Terrain.GlobalRiverPath.Instance.EvaluateAtDistance(curRiverDist, out Vector3 riverPoint, out curTangent, out _, out _))
                    {
                        curPosXZ = riverPoint;
                    }
                    else
                    {
                        curPosXZ = Vector3.Lerp(launchStart, launchEnd, curvedT);
                    }
                }
                else
                {
                    curPosXZ = Vector3.Lerp(launchStart, launchEnd, curvedT);
                }

                float arcY = Mathf.Lerp(launchStart.y, waterLevel, lt) + Mathf.Sin(lt * Mathf.PI) * 1.5f;
                transform.position = new Vector3(curPosXZ.x, Mathf.Max(waterLevel, arcY), curPosXZ.z);

                if (curTangent.sqrMagnitude > 0.001f)
                {
                    currentForwardDir = new Vector3(curTangent.x, 0f, curTangent.z).normalized;
                    transform.rotation = Quaternion.LookRotation(currentForwardDir, Vector3.up);
                }
                yield return null;
            }

            transform.position = launchEnd;
            if (hasRiverPath && targetRiverTangent.sqrMagnitude > 0.001f)
            {
                currentForwardDir = new Vector3(targetRiverTangent.x, 0f, targetRiverTangent.z).normalized;
            }
            isInRandomRing = false;

            if (SplashEffectSpawner.Instance != null)
            {
                SplashEffectSpawner.Instance.SpawnSplash(launchEnd, 2.5f);
            }

            ExecuteSurfaceImpact("PERFECT (WARP LAUNCH)", 0f);
        }

        #endregion

        private void OnDrawGizmos()
        {
            if (!isThrown || isSunk) return;

            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(cycleEndPos, 0.4f);

            Gizmos.color = Color.yellow;
            Vector3 prev = cycleStartPos;
            for (int i = 1; i <= 20; i++)
            {
                float t = i / 20f;
                Vector3 horiz = Vector3.Lerp(cycleStartPos, cycleEndPos, t);
                float y = waterLevel + 4f * fixedBounceArcHeight * t * (1f - t);
                Vector3 cur = new Vector3(horiz.x, Mathf.Max(waterLevel, y), horiz.z);
                Gizmos.DrawLine(prev, cur);
                prev = cur;
            }
        }
    }
}
