using UnityEngine;

namespace SkippingStones.Gameplay
{
    /// <summary>
    /// ⏱️ 돌의 착수 시점 고도 및 타이밍을 판정하고 모멘텀/속도/바운스 배율을 산출하는 핸들러
    /// </summary>
    public static class StoneTimingEvaluator
    {
        public struct TimingEvaluationResult
        {
            public bool isSuccess;
            public bool isEarlyRetry;
            public string grade;
            public float momentumDelta;
            public float bounceForceMultiplier;
            public float speedMultiplier;
            public float fixedBounceForce; // 0이 아니면 이 값 우선
        }

        public static TimingEvaluationResult Evaluate(float distToWater, float baseBounceForce)
        {
            TimingEvaluationResult res = new TimingEvaluationResult
            {
                isSuccess = true,
                isEarlyRetry = false,
                fixedBounceForce = 0f
            };

            if (distToWater > 0.85f)
            {
                res.isSuccess = false;
                res.isEarlyRetry = true;
                res.grade = "💦 TOO EARLY";
                return res;
            }
            else if (distToWater > 0.48f)
            {
                res.grade = "✨ GOOD (+0.5)";
                res.momentumDelta = +0.5f;
                res.bounceForceMultiplier = 0.95f;
                res.speedMultiplier = 0.98f;
            }
            else if (distToWater > 0.18f)
            {
                res.grade = "⚡ GREAT! ⚡ (+1.0)";
                res.momentumDelta = +1.0f;
                res.bounceForceMultiplier = 1.10f;
                res.speedMultiplier = 1.02f;
            }
            else if (distToWater >= -0.09f)
            {
                res.grade = "🔥 PERFECT! 🔥 (+2.0)";
                res.momentumDelta = +2.0f;
                res.bounceForceMultiplier = 1.25f;
                res.speedMultiplier = 1.08f;
            }
            else if (distToWater >= -0.20f)
            {
                res.grade = "⚠️ LATE (-1.0)";
                res.momentumDelta = -1.0f;
                res.bounceForceMultiplier = 0.82f;
                res.speedMultiplier = 0.88f;
            }
            else if (distToWater >= -0.32f)
            {
                res.grade = "🚨 TOO LATE (-1.5)";
                res.momentumDelta = -1.5f;
                res.bounceForceMultiplier = 0.65f;
                res.speedMultiplier = 0.78f;
            }
            else
            {
                res.grade = "💥 BAD (-3.0)";
                res.momentumDelta = -3.0f;
                res.fixedBounceForce = Mathf.Max(baseBounceForce * 0.50f, 2.8f);
                res.speedMultiplier = 0.70f;
            }

            return res;
        }
    }
}
