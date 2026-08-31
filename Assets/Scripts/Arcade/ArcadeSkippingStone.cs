using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SkippingStones.Arcade
{
    /// <summary>
    /// 🌊 리듬 아케이드 모드 전용 돌 비행 및 판정 엔진 (독립 구현)
    /// - BPM 기반 고정 주기(60~120 BPM) 수학적 포물선 비행
    /// - 수면 착수 7단계 판정 (PERFECT, GREAT, GOOD, LATE, TOO LATE, TOO EARLY, MISS)
    /// - 콤보 가속, 3방향 조향(A/D/S), 모멘텀 스태미나 소진 시 스키밍 피니시
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class ArcadeSkippingStone : MonoBehaviour
    {
        [Header("🌊 리듬 BPM 및 타이밍 설정")]
        public float baseBPM = 60f;
        public int currentCombo = 0;
        public float currentBPM = 60f;
        public float currentCycleDuration = 1.00f; // BPM 60 = 1.00s

        [Header("🌊 모멘텀 (스태미나/라이프)")]
        public float currentMomentum = 100f;
        public float maxMomentum = 100f;

        [Header("🌊 비행 파라미터")]
        public float bounceArcHeight = 2.6f;
        public float bounceDistance = 30.0f;
        public float waterLevel = 16.0f;
        public float currentSteerAngle = 0f;

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
        public ArcadeRhythmRing rhythmRing;

        public event Action<int, string> OnSkipBounced;
        public event Action<float> OnStoneSunk;

        private Rigidbody rb;
        private Vector3 cycleStartPos;
        private Vector3 cycleEndPos;
        private Vector3 currentForwardDir = Vector3.forward;
        private float cycleElapsedTime = 0f;
        private bool hasTappedInCycle = false;
        private int earlyRetryCount = 0;

        // 판정 기준 윈도우 (착수 전 잔여 시간 초)
        private const float WINDOW_PERFECT = 0.100f; // ±100ms
        private const float WINDOW_GREAT = 0.220f;   // ±220ms
        private const float WINDOW_GOOD = 0.380f;    // ±380ms
        private const float WINDOW_EARLY_RETRY = 0.600f; // 380~600ms (기회 1회 보존)

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
                GameObject ringObj = new GameObject("ArcadeRhythmRing");
                rhythmRing = ringObj.AddComponent<ArcadeRhythmRing>();
                rhythmRing.Hide();
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

            cycleStartPos = transform.position;
            bounceDistance = 25.0f * Mathf.Clamp(powerMultiplier, 0.8f, 1.3f);
            bounceArcHeight = 2.4f;

            cycleEndPos = cycleStartPos + currentForwardDir * bounceDistance;
            cycleEndPos.y = waterLevel;

            cycleElapsedTime = 0f;
            hasTappedInCycle = false;
            earlyRetryCount = 0;

            if (rhythmRing != null)
            {
                rhythmRing.Show(cycleEndPos);
            }
        }

        private void Update()
        {
            if (!isThrown || isSunk || isCrashed || isSkimming) return;

            cycleElapsedTime += Time.deltaTime;
            float t = cycleElapsedTime / currentCycleDuration;

            // 1. 수학적 포물선 위치 계산
            Vector3 horizPos = Vector3.Lerp(cycleStartPos, cycleEndPos, Mathf.Clamp01(t));
            // y = waterLevel + 4 * H * t * (1 - t)
            float yPos = waterLevel + 4f * bounceArcHeight * t * (1f - t);
            transform.position = new Vector3(horizPos.x, Mathf.Max(waterLevel, yPos), horizPos.z);

            // 2. 비행 방향 및 회전
            Vector3 vel = (cycleEndPos - cycleStartPos).normalized;
            transform.rotation = Quaternion.LookRotation(vel, Vector3.up) * Quaternion.Euler(-15f, 0f, 0f);

            // 3. 리듬 링 프로그레스 업데이트
            if (rhythmRing != null)
            {
                float timeRemaining = currentCycleDuration - cycleElapsedTime;
                float normRemaining = timeRemaining / (currentCycleDuration * 0.5f); // 하강 구간에서 수축
                rhythmRing.UpdateProgress(normRemaining);
            }

            // 4. 착수 시점(t >= 1.0) 도달 시 자연 판정 (미입력 = MISS 처리)
            if (cycleElapsedTime >= currentCycleDuration)
            {
                if (!hasTappedInCycle)
                {
                    ResolveImpact("MISS");
                }
            }
        }

        /// <summary>
        /// 🎮 플레이어 터치/키보드 입력 시 판정 평가
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

            // 상공 너무 이름 (600ms 이상)
            if (timeRemaining > WINDOW_EARLY_RETRY)
            {
                resultGrade = "💦 TOO EARLY (너무 이름)";
                return false;
            }

            // 재도전 기회 보존 구간 (380ms ~ 600ms)
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
                    // 연타 막누름 시 기회 소진
                    hasTappedInCycle = true;
                    ResolveImpact("MISS", steerAngleDegrees);
                    resultGrade = "❌ TOO EARLY MISS";
                    return true;
                }
            }

            // 정밀 판정 구간 (±380ms 이내)
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
            else if (timeRemaining <= WINDOW_GOOD)
            {
                grade = "👍 GOOD";
            }
            else
            {
                grade = "⚠️ LATE";
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

            float nextDist = 20.0f;
            float nextHeight = 2.0f;
            float momentumDelta = 0f;

            if (grade.Contains("PERFECT"))
            {
                nextDist = 30.0f;
                nextHeight = 2.6f;
                momentumDelta = +20f;
                currentCombo++;
            }
            else if (grade.Contains("GREAT"))
            {
                nextDist = 22.0f;
                nextHeight = 2.1f;
                momentumDelta = +10f;
                currentCombo++;
            }
            else if (grade.Contains("GOOD"))
            {
                nextDist = 16.0f;
                nextHeight = 1.7f;
                momentumDelta = +5f;
                currentCombo++;
            }
            else if (grade.Contains("LATE"))
            {
                nextDist = 14.0f;
                nextHeight = 1.4f;
                momentumDelta = -10f;
                currentCombo = 0; // 콤보 리셋
            }
            else // MISS
            {
                nextDist = 6.0f;
                nextHeight = 0.8f;
                momentumDelta = -30f;
                currentCombo = 0;
            }

            currentMomentum = Mathf.Clamp(currentMomentum + momentumDelta, 0f, maxMomentum);
            totalDistance += bounceDistance;
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
                // 다음 바운스 포물선 설정
                cycleStartPos = transform.position;
                cycleStartPos.y = waterLevel;
                bounceDistance = nextDist;
                bounceArcHeight = nextHeight;
                cycleEndPos = cycleStartPos + currentForwardDir * bounceDistance;
                cycleEndPos.y = waterLevel;

                cycleElapsedTime = 0f;
                hasTappedInCycle = false;
                earlyRetryCount = 0;

                if (rhythmRing != null)
                {
                    rhythmRing.Show(cycleEndPos);
                }
            }
        }

        private IEnumerator CoSkimmingFinish()
        {
            isSkimming = true;
            if (rhythmRing != null) rhythmRing.Hide();
            if (AudioManager.Instance != null) AudioManager.Instance.Play(SoundType.SkimSlide);

            float duration = 1.2f;
            float elapsed = 0f;
            Vector3 skimStart = transform.position;
            skimStart.y = waterLevel;
            Vector3 skimTarget = skimStart + currentForwardDir * 8.0f;

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
            if (rhythmRing != null) rhythmRing.Hide();
            if (AudioManager.Instance != null) AudioManager.Instance.Play(SoundType.StoneSink);

            OnStoneSunk?.Invoke(totalDistance);
        }

        public void ApplySteerAngle(float angle)
        {
            if (isSunk || isCrashed) return;
            currentForwardDir = Quaternion.Euler(0f, angle, 0f) * currentForwardDir;
            cycleEndPos = cycleStartPos + currentForwardDir * bounceDistance;
            cycleEndPos.y = waterLevel;
            if (rhythmRing != null) rhythmRing.transform.position = cycleEndPos;
        }

        private void UpdateBPM()
        {
            if (currentCombo >= 20) currentBPM = 120f;
            else if (currentCombo >= 15) currentBPM = 100f;
            else if (currentCombo >= 10) currentBPM = 85f;
            else if (currentCombo >= 5) currentBPM = 72f;
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
    }
}
