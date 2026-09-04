using UnityEngine;

namespace SkippingStones.Gameplay.Calculators
{
    /// <summary>
    /// 🪨 돌의 수면 바운스 물리, 공기 역학 및 감쇠 연산을 전담하는 순수 물리 계산기
    /// </summary>
    public static class StonePhysicsCalculator
    {
        /// <summary>
        /// 스킵 횟수에 따른 점진적 수면 바운스 상승력 감쇠 계산
        /// </summary>
        public static float GetDynamicBounceForce(float baseBounceUpForce, int currentSkip)
        {
            float progress = Mathf.Clamp01((currentSkip - 1) / 32f);
            float decayFactor = Mathf.Lerp(1.0f, 0.38f, Mathf.Sqrt(progress));
            return baseBounceUpForce * decayFactor;
        }

        /// <summary>
        /// 비행 중 피치 회전 및 스핀 각도 연산
        /// </summary>
        public static void CalculateFlightRotation(Vector3 linearVelocity, float currentPitch, float currentSpin, float deltaTime, out float newPitch, out float newSpin, out Quaternion finalRotation)
        {
            Vector3 hVel = new Vector3(linearVelocity.x, 0f, linearVelocity.z);
            if (hVel.sqrMagnitude > 0.05f)
            {
                Vector3 hDir = hVel.normalized;
                float vy = linearVelocity.y;
                float targetPitch = Mathf.Clamp(vy * 6.5f, -36f, 45f);
                newPitch = Mathf.Lerp(currentPitch, targetPitch, deltaTime * 14f);
                newSpin = (currentSpin + 1440f * deltaTime) % 360f;

                Quaternion headingRot = Quaternion.LookRotation(hDir, Vector3.up);
                Quaternion pitchRot = Quaternion.Euler(-newPitch, 0f, 0f);
                Quaternion spinRot = Quaternion.Euler(0f, newSpin, 0f);

                finalRotation = headingRot * pitchRot * spinRot;
            }
            else
            {
                newPitch = currentPitch;
                newSpin = currentSpin;
                finalRotation = Quaternion.identity;
            }
        }

        /// <summary>
        /// 조향 각도를 현재 수평 속도 벡터에 적용
        /// </summary>
        public static Vector3 ApplySteerToVelocity(Vector3 linearVelocity, float steerAngleDegrees)
        {
            Vector2 hVel = new Vector2(linearVelocity.x, linearVelocity.z);
            float spd = hVel.magnitude;
            if (spd < 0.1f) return linearVelocity;

            Quaternion rot = Quaternion.Euler(0f, steerAngleDegrees, 0f);
            Vector3 rotated3D = rot * new Vector3(hVel.x, 0f, hVel.y);
            Vector2 newHDir = new Vector2(rotated3D.x, rotated3D.z).normalized;

            return new Vector3(newHDir.x * spd, linearVelocity.y, newHDir.y * spd);
        }
    }
}
