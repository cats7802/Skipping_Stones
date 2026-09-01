using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SkippingStones.Arcade
{
    /// <summary>
    /// 🌊 리듬 아케이드 모드 전용 돌 비행 및 판정 엔진 (독립 구현)
    /// - 디렉터 확정 리듬 룰:
    ///   1) 포물선 높이 1.8m 통통 귀엽게 완전 고정
    ///   2) 기본 1박(60 BPM) 거리 기반 판정별 증감 및 미스 시 Base 거리 즉시 롤백
    ///   3) 실시간 실험을 위한 프리셋 3종(아기자기 10m / 스탠다드 12m / 스피드 15m / Custom) 지원 (단축키 1,2,3 지원)
    ///   4) BPM 기반 고정 주기(60~120 BPM) 가속 및 사운드/모멘텀 연동
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class ArcadeSkippingStone : MonoBehaviour
    {
        public enum RhythmPresetType
        {
            Cute_10m,      // 🟢 아기자기 10m 시작 (퍼펙트 +0.5m, 그레이트 +0.2m, 굿 0m, 레이트 -0.3m, 투레이트 -0.6m, 미스 10m 롤백)
            Standard_12m,  // 🔵 스탠다드 12m 시작 (퍼펙트 +0.6m, 그레이트 +0.3m, 굿 0m, 레이트 -0.4m, 투레이트 -0.8m, 미스 12m 롤백)
            Speed_15m,     // 🟣 스피드 15m 시작 (퍼펙트 +1.0m, 그레이트 +0.5m, 굿 0m, 레이트 -0.5m, 투레이트 -1.0m, 미스 15m 롤백)
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
        [Tooltip("인스펙터에서 직접 튜닝할 때 사용할 커스텀 수치")]
        public PresetData customPreset = new PresetData(10.0f, +0.5f, +0.2f, 0.0f, -0.3f, -0.6f);

        [Header("🌊 포물선 형상 (고정 높이)")]
        [Tooltip("디렉터 확정: 통통 귀엽게 튀는 고정 포물선 정점 높이")]
        public float fixedBounceArcHeight = 1.8f;
        [Tooltip("현재 바운스 1회 이동 목표 거리")]
        public float currentBounceDistance = 10.0f;
        public float waterLevel = 16.0f;

        [Header("🌊 리듬 BPM 및 타이밍 설정")]
        public float baseBPM = 60f;
        public int currentCombo = 0;
        public float currentBPM = 60f;
        public float currentCycleDuration = 1.00f; // BPM 60 = 1.00s
        [Tooltip("콤보에 따른 점진적 BPM 가속 활성화 여부 (음악은 원곡 유지, 돌 타이밍만 가속)")]
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

        public event Action<int, string> OnSkipBounced;
        public event Action<float> OnStoneSunk;

        public Vector3 CycleEndPosition => cycleEndPos;
        public float CycleElapsedTime => cycleElapsedTime;

        private Rigidbody rb;
        private Vector3 cycleStartPos;
        private Vector3 cycleEndPos;
        private Vector3 currentForwardDir = Vector3.forward;
        private float cycleElapsedTime = 0f;
        private bool hasTappedInCycle = false;
        private int earlyRetryCount = 0;
        private float pendingSteerAngle = 0f;
        private string pendingGrade = "";

        // 판정 기준 윈도우 (착수 전 잔여 시간 초 - 60 BPM / 1.00초 주기 맞춤)
        private const float WINDOW_PERFECT = 0.100f;   // ±100ms
        private const float WINDOW_GREAT = 0.220f;     // ±220ms
        private const float WINDOW_GOOD = 0.380f;      // ±380ms
        private const float WINDOW_LATE = 0.480f;      // 착수 직후 100ms
        private const float WINDOW_EARLY_RETRY = 0.600f; // 380~600ms (기회 1회 보존)

        // 사전 정의 프리셋 테이블
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

        [Header("🌊 수면 대칭 반사 그림자 (Water Reflection Shadow)")]
        private GameObject waterReflectionObj;
        private MeshRenderer waterReflectionRenderer;
        private Material waterReflectionMat;

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
            SetupWaterReflectionShadow();
        }

        private void SetupWaterReflectionShadow()
        {
            if (waterReflectionObj != null) Destroy(waterReflectionObj);

            waterReflectionObj = GameObject.CreatePrimitive(PrimitiveType.Quad);
            waterReflectionObj.name = "[Water_Reflection_Shadow]";
            Destroy(waterReflectionObj.GetComponent<Collider>());
            waterReflectionObj.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

            waterReflectionRenderer = waterReflectionObj.GetComponent<MeshRenderer>();
            Shader unlit = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
            waterReflectionMat = (unlit != null) ? new Material(unlit) : new Material(Shader.Find("Standard"));

            if (waterReflectionMat.HasProperty("_Surface"))
            {
                waterReflectionMat.SetFloat("_Surface", 1.0f); // Transparent
                waterReflectionMat.SetFloat("_Blend", 0.0f);   // Alpha
                waterReflectionMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                waterReflectionMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                waterReflectionMat.SetInt("_ZWrite", 0);
                waterReflectionMat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent + 10;
            }

            Texture2D softShadowTex = new Texture2D(64, 64, TextureFormat.RGBA32, false);
            softShadowTex.wrapMode = TextureWrapMode.Clamp;
            Vector2 center = new Vector2(31.5f, 31.5f);
            float radius = 31.5f;

            for (int y = 0; y < 64; y++)
            {
                for (int x = 0; x < 64; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), center);
                    float normDist = Mathf.Clamp01(dist / radius);
                    float alpha = Mathf.SmoothStep(1.0f, 0.0f, normDist);
                    alpha = Mathf.Pow(alpha, 1.8f);
                    softShadowTex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }
            softShadowTex.Apply();

            waterReflectionMat.mainTexture = softShadowTex;
            waterReflectionMat.color = new Color(0.02f, 0.08f, 0.16f, 0.35f);
            if (waterReflectionMat.HasProperty("_BaseColor"))
            {
                waterReflectionMat.SetColor("_BaseColor", new Color(0.02f, 0.08f, 0.16f, 0.35f));
            }

            waterReflectionRenderer.material = waterReflectionMat;
            waterReflectionRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            waterReflectionRenderer.receiveShadows = false;

            waterReflectionObj.transform.localScale = new Vector3(0.25f, 0.25f, 1.0f);
            waterReflectionObj.SetActive(false);
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
                // 🌟 돌의 자식이 아닌 독립된 월드 루트 오브젝트로 생성
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
            // 초기 기준 거리 및 모멘텀 (발사 파워 게이지 반영)
            PresetData preset = GetCurrentPresetData();
            initialLaunchPower = Mathf.Clamp(powerMultiplier, 0.5f, 2.0f);
            currentBounceDistance = preset.baseDistance * initialLaunchPower;
            currentMomentum = Mathf.Clamp(60f * initialLaunchPower, 30f, maxMomentum); // 파워에 비례한 시작 모멘텀
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

            if (!isThrown || isSunk || isCrashed || isSkimming) return;

            cycleElapsedTime += Time.deltaTime;
            float t = cycleElapsedTime / currentCycleDuration;

            // 1. 디렉터 확정: 고정 높이 1.8m 기반 수학적 포물선 (1.00초 정박 동안 끝까지 수면으로 비행)
            Vector3 horizPos = Vector3.Lerp(cycleStartPos, cycleEndPos, Mathf.Clamp01(t));
            // y = waterLevel + 4 * H * t * (1 - t)
            float yPos = waterLevel + 4f * fixedBounceArcHeight * t * (1f - t);
            Vector3 nextPos = new Vector3(horizPos.x, Mathf.Max(waterLevel, yPos), horizPos.z);

            // 🪨 지형 및 바위 충돌 검사 (Kinematic 돌의 연속 충돌 검출)
            Collider[] hits = Physics.OverlapSphere(nextPos, 0.12f, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < hits.Length; i++)
            {
                Collider col = hits[i];
                if (col == null || col.gameObject == gameObject || col.transform.IsChildOf(transform)) continue;

                // 수면, 캐릭터, 발판 제외
                if (col.GetComponent<WaterSurface>() != null || col.GetComponentInParent<WaterSurface>() != null) continue;
                string colName = col.name.ToLower();
                if (colName.Contains("water") || colName.Contains("surface") || colName.Contains("river") 
                    || colName.Contains("pier") || colName.Contains("platform") 
                    || colName.Contains("character") || colName.Contains("thrower")) continue;

                // 지형(Terrain/MeshCollider) 또는 바위(Rock/Obstacle/Ground/Bank) 감지
                bool isTerrain = col.GetComponent<TerrainCollider>() != null || col.GetComponent<UnityEngine.Terrain>() != null || col.GetComponent<MeshCollider>() != null;
                bool isObstacle = colName.Contains("rock") || colName.Contains("obstacle") || colName.Contains("ground") || colName.Contains("bank");

                if (isTerrain || isObstacle)
                {
                    CrashOnLand(nextPos, Vector3.up, col);
                    return;
                }
            }

            transform.position = nextPos;

            // 2. 비행 방향 및 회전
            Vector3 vel = (cycleEndPos - cycleStartPos).normalized;
            if (vel.sqrMagnitude > 0.001f)
            {
                transform.rotation = Quaternion.LookRotation(vel, Vector3.up) * Quaternion.Euler(-15f, 0f, 0f);
            }

            // 4. 🎯 핵심: 정확히 수면 표면(t >= 1.0)에 닿는 순간 다음 바운스 실행!
            if (cycleElapsedTime >= currentCycleDuration)
            {
                string gradeToExecute = hasTappedInCycle ? pendingGrade : "MISS";
                ExecuteSurfaceImpact(gradeToExecute, pendingSteerAngle);
            }
        }

        private void LateUpdate()
        {
            UpdateWaterReflectionShadow();
        }

        private void UpdateWaterReflectionShadow()
        {
            if (waterReflectionObj == null) return;

            if (!isThrown || isSunk || isCrashed)
            {
                if (waterReflectionObj.activeSelf) waterReflectionObj.SetActive(false);
                return;
            }

            float dist = transform.position.y - waterLevel;

            // 돌이 수면 위 3.5m 이내로 진입했을 때 그림자 추적 활성화
            if (dist >= -0.35f && dist <= 3.5f)
            {
                if (!waterReflectionObj.activeSelf) waterReflectionObj.SetActive(true);

                // 수면 높이에 납작하게 밀착 배치
                waterReflectionObj.transform.position = new Vector3(transform.position.x, waterLevel + 0.008f, transform.position.z);
                waterReflectionObj.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

                // 공중 상공: 0.30m(알파 0.08) -> 수면 밀착: 0.14m(알파 0.35)
                float closeness = Mathf.Clamp01(1f - (dist / 2.8f));
                float shadowScale = Mathf.Lerp(0.30f, 0.14f, closeness);
                waterReflectionObj.transform.localScale = new Vector3(shadowScale, shadowScale, 1.0f);

                if (waterReflectionMat != null)
                {
                    float shadowAlpha = Mathf.Lerp(0.08f, 0.35f, closeness);
                    waterReflectionMat.color = new Color(0.03f, 0.10f, 0.20f, shadowAlpha);
                }
            }
            else
            {
                if (waterReflectionObj.activeSelf) waterReflectionObj.SetActive(false);
            }
        }

        private void OnDestroy()
        {
            if (rhythmRing != null)
            {
                Destroy(rhythmRing.gameObject);
                rhythmRing = null;
            }

            if (waterReflectionObj != null)
            {
                if (waterReflectionMat != null)
                {
                    if (waterReflectionMat.mainTexture != null)
                    {
                        Destroy(waterReflectionMat.mainTexture);
                    }
                    Destroy(waterReflectionMat);
                }
                Destroy(waterReflectionObj);
                waterReflectionObj = null;
            }
        }

        /// <summary>
        /// 🎮 플레이어 터치/키보드 입력 시 판정 즉시 평가 (사운드/이펙트 즉시 피드백, 착수는 수면에서 자연 실행)
        /// </summary>
        public bool TryRhythmTap(float steerAngleDegrees, out string resultGrade)
        {
            if (!isThrown || isSunk || isCrashed || isSkimming)
            {
                resultGrade = "NOT IN FLIGHT";
                return false;
            }

            if (hasTappedInCycle)
            {
                // 🌟 이미 판정 확정 후라도 더블탭/스와이프 조향 각도가 들어오면 조향 각도 즉시 갱신 반영 (+3° 추가 꺾임)
                if (Mathf.Abs(steerAngleDegrees) > 0.1f)
                {
                    pendingSteerAngle = steerAngleDegrees;
                }
                resultGrade = "ALREADY TAPPED";
                return false;
            }

            float timeRemaining = currentCycleDuration - cycleElapsedTime;

            // 1. 상공 너무 이름 (600ms 이상)
            if (timeRemaining > WINDOW_EARLY_RETRY)
            {
                resultGrade = "💦 TOO EARLY (너무 이름)";
                return false;
            }

            // 2. 재도전 기회 보존 구간 (380ms ~ 600ms)
            if (timeRemaining > WINDOW_GOOD)
            {
                if (earlyRetryCount == 0)
                {
                    earlyRetryCount++;
                    resultGrade = "💦 TOO EARLY (재도전 기회 1회 잔여)";
                    return false;
                }
                else
                {
                    // 연타 막누름 시 기회 소진 후 MISS 예약
                    hasTappedInCycle = true;
                    pendingGrade = "MISS";
                    pendingSteerAngle = steerAngleDegrees;
                    resultGrade = "❌ TOO EARLY MISS";
                    return true;
                }
            }

            // 3. 정밀 판정 구간 (누른 즉시 사운드/판정 텍스트 피드백 발생)
            hasTappedInCycle = true;
            string grade;
            if (timeRemaining <= WINDOW_PERFECT && timeRemaining >= -0.06f)
            {
                grade = "✨ PERFECT ✨";
            }
            else if (timeRemaining <= WINDOW_GREAT && timeRemaining >= -0.12f)
            {
                grade = "🌟 GREAT";
            }
            else if (timeRemaining <= WINDOW_GOOD && timeRemaining >= 0f)
            {
                grade = "👍 GOOD";
            }
            else if (timeRemaining < 0f && timeRemaining >= -0.18f)
            {
                grade = "⚠️ LATE";
            }
            else if (timeRemaining < -0.18f)
            {
                grade = "🚨 TOO LATE";
            }
            else
            {
                grade = "MISS";
            }

            pendingGrade = grade;
            pendingSteerAngle = steerAngleDegrees;
            resultGrade = grade;

            // 🎵 누른 순간 사운드 & 링 버스트 피드백 즉시 폭발!
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

        /// <summary>
        /// 🌊 돌이 1.00초 정박에 정확히 수면에 닿았을 때 다음 바운스로 자연 튀어오름
        /// </summary>
        private void ExecuteSurfaceImpact(string grade, float steerAngle = 0f)
        {
            hasTappedInCycle = true;
            skipCount++;

            // 조향 적용
            if (Mathf.Abs(steerAngle) > 0.1f)
            {
                currentForwardDir = Quaternion.Euler(0f, steerAngle, 0f) * currentForwardDir;
            }

            PresetData preset = GetCurrentPresetData();
            float distanceDelta = 0f;
            float momentumDelta = 0f;

            // 📋 디렉터 확정 판정별 거리 증감 & 모멘텀 변동
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
                currentCombo = 0; // 콤보 리셋
            }
            else if (grade.Contains("LATE"))
            {
                distanceDelta = preset.lateDelta;
                momentumDelta = -10f;
                currentCombo = 0; // 콤보 리셋
            }
            else // MISS
            {
                // 디렉터 확정: 미스 시 파워 보너스가 반영된 초기 기준 거리로 롤백
                currentBounceDistance = preset.baseDistance * initialLaunchPower;
                distanceDelta = 0f;
                momentumDelta = -30f;
                currentCombo = 0; // 콤보 리셋
            }

            // 📋 디렉터 요청 디버그 로깅: 1.0초 정밀 착수 시간, 오차 및 조향 각도(5°/8°) 콘솔 출력
            float expectedDuration = currentCycleDuration;
            float actualElapsed = cycleElapsedTime;
            float timingError = actualElapsed - expectedDuration;
            string steerStr = (Mathf.Abs(steerAngle) > 0.1f) 
                ? $" | 조향: <b><color=#FFD700>{(steerAngle > 0 ? "+" : "")}{steerAngle:F0}°</color></b>" 
                : " | 조향: 0° (직진)";
            Debug.Log($"<color=#00FFAA>[🎵 Rhythm Precision]</color> <b>Skip #{skipCount} ({grade})</b> | 경과: {actualElapsed:F3}s / 목표: {expectedDuration:F2}s (오차: {(timingError >= 0 ? "+" : "")}{timingError:F3}s){steerStr} | 거리: {currentBounceDistance:F1}m | BPM: {currentBPM:F0}");

            // 거리 누적 증감 (미스가 아닐 때만 증감 적용, 최소치 보장)
            if (!grade.Contains("MISS"))
            {
                currentBounceDistance = Mathf.Max(3.0f, currentBounceDistance + distanceDelta);
            }

            currentMomentum = Mathf.Clamp(currentMomentum + momentumDelta, 0f, maxMomentum);
            totalDistance += currentBounceDistance;
            UpdateBPM();

            // 🎵 BGM 피치는 착수 후 다음 바운스 템포에 맞춰 갱신
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.SetBGMPitchByBPM(currentBPM, 60f);
            }

            OnSkipBounced?.Invoke(skipCount, grade);

            // 모멘텀 고갈 시 스키밍 피니시 진입 (모멘텀이 남아있으면 미스 발생 시에도 계속 비행 유지)
            if (currentMomentum <= 0.1f)
            {
                StartCoroutine(CoSkimmingFinish());
            }
            else
            {
                // 다음 바운스 포물선 설정 (고정 높이 1.8m 유지)
                cycleStartPos = transform.position;
                cycleStartPos.y = waterLevel;
                cycleEndPos = cycleStartPos + currentForwardDir * currentBounceDistance;
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
                // 감속 곡선 (EaseOutQuad)
                float ease = 1f - (1f - t) * (1f - t);
                
                // 수면 도로록 찰랑임 (Bobbing)
                float bobbingY = waterLevel + 0.025f + Mathf.Sin(elapsed * 35f) * 0.015f;
                Vector3 curHoriz = Vector3.Lerp(skimStart, skimTarget, ease);
                transform.position = new Vector3(curHoriz.x, bobbingY, curHoriz.z);
                transform.Rotate(0f, 720f * Time.deltaTime * (1f - t), 0f);

                skimDistance = Vector3.Distance(skimStart, new Vector3(curHoriz.x, waterLevel, curHoriz.z));

                // 도로록 물보라 이펙트 연속 방출
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

            // 수면 트리거는 무시
            if (other.GetComponent<WaterSurface>() != null || other.GetComponentInParent<WaterSurface>() != null) return;
            string colName = other.name.ToLower();
            if (colName.Contains("water") || colName.Contains("surface") || colName.Contains("river") || colName.Contains("lake") || colName.Contains("stream")) return;

            // 실제 지형(Terrain/MeshCollider) 또는 바위(Rock/Obstacle) 콜라이더 접촉 시에만 충돌
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

        /// <summary>
        /// 💥 지형, 강변 또는 바위 충돌 시 즉시 크래시 처리
        /// </summary>
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
            // 허공 멈춤 방지: 충돌 후 튕겨서 바닥/수면으로 툭 떨어지는 텀블링 애니메이션
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
            // 🌊 디렉터 확정: 공중 궤적을 억지로 꺾지 않고, 다음 수면 착수(바운스) 시 박차고 나갈 각도로 예약!
            pendingSteerAngle = angle;
            Debug.Log($"<color=#FFD700>[🎮 Steer Reserved]</color> 수면 착수 시 튕겨나갈 조향 예약: <b>{(angle > 0 ? "+" : "")}{angle:F0}°</b>");
        }

        private void UpdateBPM()
        {
            if (enableComboAcceleration)
            {
                // 🌊 디렉터 확정: 도달 거리(m) 기반 점진적 코스 템포 가속
                // - 먼 강줄기로 멀리 나아갈수록 코스의 속도감이 자연스럽게 빨라짐
                if (totalDistance >= 1600f) currentBPM = 120f;     // 1,600m 이상: 0.50초 (극강의 1박 질주 피버)
                else if (totalDistance >= 1000f) currentBPM = 100f;// 1,000m 이상: 0.60초 (스피디 쾌속)
                else if (totalDistance >= 500f) currentBPM = 85f;  // 500m 이상: 0.70초 (경쾌한 가속)
                else if (totalDistance >= 200f) currentBPM = 72f;  // 200m 이상: 0.83초 (적응 단계)
                else currentBPM = baseBPM;                         // 0 ~ 200m: 1.00초 (기본 60 BPM 편안한 출발)
            }
            else
            {
                currentBPM = baseBPM;
            }

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

            // 모바일 및 PC 화면 대응 콤팩트 좌측 상단 HUD (화면을 가리지 않는 1/3 슬림 사이즈)
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
            
            // 1. 실시간 스톱워치 & 비트
            float progress = (currentCycleDuration > 0.001f) ? Mathf.Clamp01(cycleElapsedTime / currentCycleDuration) : 0f;
            string pulse = (progress > 0.85f) ? "<color=#FF3366>● [쿵!]</color>" : "<color=#00E5FF>○ [비행]</color>";
            GUILayout.Label($"⏱️ {cycleElapsedTime:F2}s / {currentCycleDuration:F2}s ({progress * 100f:F0}%) {pulse}", labelStyle);

            GUILayout.Label($"🏃 {currentBPM:F0} BPM | 콤보: {currentCombo}", labelStyle);
            GUILayout.Label($"📏 목표: {currentBounceDistance:F1}m | 모멘텀: {currentMomentum:F0}", labelStyle);
            GUILayout.EndArea();
        }

        private void OnDrawGizmos()
        {
            if (!isThrown || isSunk) return;

            // 씬 뷰에 착수 예정 지점 녹색 펄스 기즈모 표시
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(cycleEndPos, 0.4f);

            // 포물선 궤적 미리보기 라인
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
