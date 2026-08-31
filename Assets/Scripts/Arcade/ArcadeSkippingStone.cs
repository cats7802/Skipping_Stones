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

        [Header("🌊 모멘텀 (스태미나/라이프)")]
        public float currentMomentum = 100f;
        public float maxMomentum = 100f;

        [Header("상태 모니터링")]
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

        // 판정 기준 윈도우 (착수 전 잔여 시간 초)
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
                rhythmRing = GetComponentInChildren<RhythmRingIndicator>();
            }
            if (rhythmRing == null)
            {
                GameObject ringObj = new GameObject("RhythmRingIndicator");
                ringObj.transform.SetParent(transform, false);
                rhythmRing = ringObj.AddComponent<RhythmRingIndicator>();
            }
            if (rhythmRing != null)
            {
                rhythmRing.arcadeStone = this;
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
            currentMomentum = maxMomentum;
            UpdateBPM();

            currentForwardDir = new Vector3(forwardDirection.x, 0f, forwardDirection.z).normalized;
            if (currentForwardDir.sqrMagnitude < 0.01f) currentForwardDir = Vector3.forward;

            // 초기 기준 거리 설정
            PresetData preset = GetCurrentPresetData();
            currentBounceDistance = preset.baseDistance;

            cycleStartPos = transform.position;
            cycleEndPos = cycleStartPos + currentForwardDir * currentBounceDistance;
            cycleEndPos.y = waterLevel;

            cycleElapsedTime = 0f;
            hasTappedInCycle = false;
            earlyRetryCount = 0;
        }

        private void Update()
        {
            if (!isThrown || isSunk || isCrashed || isSkimming) return;

            cycleElapsedTime += Time.deltaTime;
            float t = cycleElapsedTime / currentCycleDuration;

            // 1. 디렉터 확정: 고정 높이 1.8m 기반 수학적 포물선
            Vector3 horizPos = Vector3.Lerp(cycleStartPos, cycleEndPos, Mathf.Clamp01(t));
            // y = waterLevel + 4 * H * t * (1 - t)
            float yPos = waterLevel + 4f * fixedBounceArcHeight * t * (1f - t);
            transform.position = new Vector3(horizPos.x, Mathf.Max(waterLevel, yPos), horizPos.z);

            // 2. 비행 방향 및 회전
            Vector3 vel = (cycleEndPos - cycleStartPos).normalized;
            if (vel.sqrMagnitude > 0.001f)
            {
                transform.rotation = Quaternion.LookRotation(vel, Vector3.up) * Quaternion.Euler(-15f, 0f, 0f);
            }

            // 3. 착수 시점(t >= 1.0) 도달 시 자연 판정 (미입력 = MISS 처리)
            if (cycleElapsedTime >= currentCycleDuration)
            {
                if (!hasTappedInCycle)
                {
                    ResolveImpact("MISS");
                }
            }
        }

        /// <summary>
        /// 🎮 플레이어 터치/키보드 입력 시 6단계 판정 평가
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
                    // 연타 막누름 시 기회 소진 후 MISS 처리
                    hasTappedInCycle = true;
                    ResolveImpact("MISS", steerAngleDegrees);
                    resultGrade = "❌ TOO EARLY MISS";
                    return true;
                }
            }

            // 3. 정밀 판정 구간
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

            ResolveImpact(grade, steerAngleDegrees);
            resultGrade = grade;
            return true;
        }

        private void ResolveImpact(string grade, float steerAngle = 0f)
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
                // 디렉터 확정: 미스 시 처음 설정된 기본 거리로 즉시 롤백
                currentBounceDistance = preset.baseDistance;
                distanceDelta = 0f;
                momentumDelta = -30f;
                currentCombo = 0; // 콤보 리셋
            }

            // 📋 디렉터 요청 디버그 로깅: 1.0초 정밀 착수 시간 및 오차 콘솔 출력
            float expectedDuration = currentCycleDuration;
            float actualElapsed = cycleElapsedTime;
            float timingError = actualElapsed - expectedDuration;
            Debug.Log($"<color=#00FFAA>[🎵 Rhythm Precision]</color> <b>Skip #{skipCount} ({grade})</b> | 경과: {actualElapsed:F3}s / 목표: {expectedDuration:F2}s (오차: {(timingError >= 0 ? "+" : "")}{timingError:F3}s) | 거리: {currentBounceDistance:F1}m | BPM: {currentBPM:F0}");

            // 거리 누적 증감 (미스가 아닐 때만 증감 적용, 최소치 보장)
            if (!grade.Contains("MISS"))
            {
                currentBounceDistance = Mathf.Max(3.0f, currentBounceDistance + distanceDelta);
            }

            currentMomentum = Mathf.Clamp(currentMomentum + momentumDelta, 0f, maxMomentum);
            totalDistance += currentBounceDistance;
            UpdateBPM();

            if (rhythmRing != null)
            {
                rhythmRing.PlayHitFeedback(grade);
            }

            // 🎵 오디오 사운드 재생
            if (AudioManager.Instance != null)
            {
                if (grade.Contains("PERFECT")) AudioManager.Instance.Play(SoundType.BouncePerfect);
                else if (grade.Contains("GREAT") || grade.Contains("GOOD")) AudioManager.Instance.Play(SoundType.BounceGood);
                else AudioManager.Instance.Play(SoundType.BounceWater);

                AudioManager.Instance.SetBGMPitchByBPM(currentBPM, 60f);
            }

            OnSkipBounced?.Invoke(skipCount, grade);

            // 모멘텀 고갈 시 스키밍 피니시 진입
            if (currentMomentum <= 0.1f || (grade.Contains("MISS") && skipCount > 2))
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
            }
        }

        private IEnumerator CoSkimmingFinish()
        {
            isSkimming = true;
            if (AudioManager.Instance != null) AudioManager.Instance.Play(SoundType.SkimSlide);

            float duration = 1.2f;
            float elapsed = 0f;
            Vector3 skimStart = transform.position;
            skimStart.y = waterLevel;
            Vector3 skimTarget = skimStart + currentForwardDir * (currentBounceDistance * 0.5f);

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                // 감속 곡선 (EaseOutQuad)
                float ease = 1f - (1f - t) * (1f - t);
                transform.position = Vector3.Lerp(skimStart, skimTarget, ease);
                skimDistance = Vector3.Distance(skimStart, transform.position);
                yield return null;
            }

            totalDistance += skimDistance;
            SinkStone();
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
            currentForwardDir = Quaternion.Euler(0f, angle, 0f) * currentForwardDir;
            cycleEndPos = cycleStartPos + currentForwardDir * currentBounceDistance;
            cycleEndPos.y = waterLevel;
        }

        private void UpdateBPM()
        {
            // 🎵 디렉터 확정: 10콤보 단위 점진적 가속 테이블
            if (currentCombo >= 40) currentBPM = 120f;
            else if (currentCombo >= 30) currentBPM = 100f;
            else if (currentCombo >= 20) currentBPM = 85f;
            else if (currentCombo >= 10) currentBPM = 72f;
            else currentBPM = 60f;

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
        public bool showDebugHUD = true;

        private void OnGUI()
        {
            if (!showDebugHUD || !isThrown || isSunk) return;

            GUIStyle boxStyle = new GUIStyle(GUI.skin.box);
            boxStyle.fontSize = 13;
            boxStyle.normal.textColor = Color.white;
            boxStyle.alignment = TextAnchor.UpperLeft;

            GUILayout.BeginArea(new Rect(20, 120, 310, 165), boxStyle);
            GUILayout.Label($"<b>🎵 [리듬 정밀 타이밍 HUD]</b>");
            
            // 1. 실시간 0.001초 스톱워치 & 진행 바
            float progress = (currentCycleDuration > 0.001f) ? Mathf.Clamp01(cycleElapsedTime / currentCycleDuration) : 0f;
            GUILayout.Label($"⏱️ 비행 시간: <b><color=#00FFAA>{cycleElapsedTime:F3}s</color> / {currentCycleDuration:F2}s</b> ({progress * 100f:F0}%)");

            // 2. 비트 메트로놈 펄스 (착수 직전 85% 이상일 때 황금/레드로 깜빡임)
            string pulse = (progress > 0.85f) ? "<color=#FF3366>● [착수 비트 쿵!]</color>" : "<color=#00E5FF>○ [상공 비행중]</color>";
            GUILayout.Label($"🥁 비트 펄스: <b>{pulse}</b>");

            GUILayout.Label($"🏃 현재 속도: <b>{currentBPM:F0} BPM</b> (콤보: {currentCombo})");
            GUILayout.Label($"📏 1바운스 목표: <b>{currentBounceDistance:F1}m</b> (높이: {fixedBounceArcHeight:F1}m 고정)");
            GUILayout.Label($"🎛️ 적용 프리셋: <b>{activePreset}</b>");
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
