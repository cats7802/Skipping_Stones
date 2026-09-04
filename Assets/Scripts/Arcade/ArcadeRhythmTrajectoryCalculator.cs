using UnityEngine;

namespace SkippingStones.Arcade
{
    /// <summary>
    /// 🎵 리듬 아케이드 모드의 고정 포물선 궤적, BPM 템포 가속 및 판정별 거리/모멘텀 증감 계산기
    /// </summary>
    public static class ArcadeRhythmTrajectoryCalculator
    {
        public const float WINDOW_PERFECT = 0.100f;   // ±100ms
        public const float WINDOW_GREAT = 0.220f;     // ±220ms
        public const float WINDOW_GOOD = 0.380f;      // ±380ms
        public const float WINDOW_LATE = 0.480f;      // 착수 직후 100ms
        public const float WINDOW_EARLY_RETRY = 0.600f; // 380~600ms (재도전 기회 1회)

        public struct TrajectoryPositionResult
        {
            public Vector3 position;
            public Quaternion rotation;
            public bool isCycleComplete;
        }

        public static TrajectoryPositionResult EvaluateFlightPosition(
            Vector3 startPos,
            Vector3 endPos,
            float arcHeight,
            float waterLevel,
            float elapsedTime,
            float cycleDuration)
        {
            float t = (cycleDuration > 0.0001f) ? (elapsedTime / cycleDuration) : 1f;
            Vector3 horizPos = Vector3.Lerp(startPos, endPos, Mathf.Clamp01(t));
            float yPos = waterLevel + 4f * arcHeight * t * (1f - t);
            Vector3 nextPos = new Vector3(horizPos.x, Mathf.Max(waterLevel, yPos), horizPos.z);

            Vector3 vel = (endPos - startPos).normalized;
            Quaternion rot = (vel.sqrMagnitude > 0.001f)
                ? Quaternion.LookRotation(vel, Vector3.up) * Quaternion.Euler(-15f, 0f, 0f)
                : Quaternion.identity;

            return new TrajectoryPositionResult
            {
                position = nextPos,
                rotation = rot,
                isCycleComplete = (elapsedTime >= cycleDuration)
            };
        }

        public static float CalculateBPM(float totalDistance, float baseBPM, bool enableComboAcceleration)
        {
            if (!enableComboAcceleration) return baseBPM;

            if (totalDistance >= 1600f) return 120f;     // 1,600m 이상: 0.50초 (극강 1박 피버)
            if (totalDistance >= 1000f) return 100f;     // 1,000m 이상: 0.60초 (스피디 쾌속)
            if (totalDistance >= 500f) return 85f;       // 500m 이상: 0.70초 (경쾌한 가속)
            if (totalDistance >= 200f) return 72f;       // 200m 이상: 0.83초 (적응 단계)
            return baseBPM;                              // 0 ~ 200m: 1.00초 (기본 60 BPM)
        }

        public static string EvaluateTimingGrade(float timeRemaining)
        {
            if (timeRemaining <= WINDOW_PERFECT && timeRemaining >= -0.06f) return "✨ PERFECT ✨";
            if (timeRemaining <= WINDOW_GREAT && timeRemaining >= -0.12f) return "🌟 GREAT";
            if (timeRemaining <= WINDOW_GOOD && timeRemaining >= 0f) return "👍 GOOD";
            if (timeRemaining < 0f && timeRemaining >= -0.18f) return "⚠️ LATE";
            if (timeRemaining < -0.18f) return "🚨 TOO LATE";
            return "MISS";
        }
    }
}
