using UnityEngine;
using System.Collections;

namespace SkippingStones.Gameplay
{
    /// <summary>
    /// 🪷 수면 연잎/연꽃 오브젝트 컴포넌트
    /// 돌이 수면 위 연잎에 착수(Bounce)했을 때 살짝 눌리는 물리 반응 및 이펙트 연출을 담당합니다.
    /// </summary>
    public class LilyPad : MonoBehaviour
    {
        [Header("연잎 설정")]
        [Tooltip("연잎 착수 판정 반경 (m)")]
        public float detectionRadius = 1.4f;

        private Vector3 initialLocalPos;
        private Vector3 initialLocalScale;
        private Coroutine squishCoroutine;

        private void Awake()
        {
            initialLocalPos = transform.localPosition;
            initialLocalScale = transform.localScale;
        }

        /// <summary>
        /// 돌이 연잎에 닿았을 때 통~ 하고 눌렸다가 부드럽게 복원되는 스프링 연출
        /// </summary>
        public void OnStepped()
        {
            if (squishCoroutine != null) StopCoroutine(squishCoroutine);
            squishCoroutine = StartCoroutine(SquishRoutine());
        }

        private IEnumerator SquishRoutine()
        {
            float elapsed = 0f;
            float duration = 0.45f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;

                float damp = Mathf.Exp(-5f * t);
                float wave = Mathf.Sin(t * Mathf.PI * 5f);
                float offset = damp * wave * 0.12f;

                transform.localPosition = initialLocalPos - new Vector3(0f, offset, 0f);
                transform.localScale = new Vector3(
                    initialLocalScale.x * (1f + offset * 0.5f),
                    initialLocalScale.y * (1f - offset),
                    initialLocalScale.z * (1f + offset * 0.5f)
                );

                yield return null;
            }

            transform.localPosition = initialLocalPos;
            transform.localScale = initialLocalScale;
            squishCoroutine = null;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.2f, 1f, 0.4f, 0.35f);
            Gizmos.DrawWireSphere(transform.position, detectionRadius);
        }
    }
}
