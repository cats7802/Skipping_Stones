using System.Collections;
using UnityEngine;

namespace SkippingStones.Arcade
{
    /// <summary>
    /// 🌀 리듬 아케이드 모드 전용 랜덤 링 (Random Ring)
    /// - 3D/Ingame_Object/Random_Ring.fbx 메쉬 자동 연동
    /// - 물수제비 포물선 높이 및 박자(BPM)와 동기화된 공중 상하 바운스(Bobbing)
    /// - 링 안착 시 2박자 동안 음악 비트에 맞춘 둥-둥- 스케일 펄스(Beat Pulse)
    /// - 돌 흡입 & 런치 트리거 및 추후 포탈/집중선 VFX 확장 슬롯 제공
    /// </summary>
    public class RandomRing : MonoBehaviour
    {
        [Header("🎛️ 비주얼 & 애니메이션 설정")]
        [Tooltip("물수제비 포물선 정점과 싱크되는 상하 바운스 진폭(m)")]
        public float bobbingAmplitude = 0.9f;
        [Tooltip("링의 기본 중심 높이 (수면 위)")]
        public float baseHeightOffset = 1.8f;
        [Tooltip("대기 상태 자전 회전 속도 (도/초)")]
        public float idleRotationSpeed = 45f;
        [Tooltip("펄스 연출 시 최대 스케일 배율")]
        public float pulseMaxScale = 1.3f;
        [Tooltip("링 모델 시각적 기본 크기 배율")]
        public float visualScaleMultiplier = 2.0f;

        [Header("🎯 인터랙션 영역")]
        [Tooltip("돌을 빨아들이는 감지 반경")]
        public float triggerRadius = 2.0f;

        [Header("✨ 이펙트 슬롯 (추후 연동용)")]
        public GameObject portalVFX;
        public GameObject speedlinesVFX;

        // 내부 상태
        private Transform meshTransform;
        private Vector3 initialLocalScale = Vector3.one;
        private float waterLevel = 16.0f;
        private bool isTriggered = false;
        private float bobbingTimer = 0f;

        public bool IsTriggered => isTriggered;

        private void Awake()
        {
            SetupVisualMesh();
        }

        private void Start()
        {
            UpdateWaterLevel();
        }

        private void UpdateWaterLevel()
        {
            WaterSurface ws = FindAnyObjectByType<WaterSurface>();
            if (ws != null)
            {
                waterLevel = ws.transform.position.y;
            }
            else
            {
                waterLevel = 16.0f;
            }
        }

        private void SetupVisualMesh()
        {
            // 이미 자식으로 메쉬가 바인딩되어 있다면 스킵
            if (transform.childCount > 0)
            {
                meshTransform = transform.GetChild(0);
                meshTransform.localScale = meshTransform.localScale * visualScaleMultiplier;
                initialLocalScale = meshTransform.localScale;
                return;
            }

            // 3D/Ingame_Object/Random_Ring.fbx 로드
            GameObject modelPrefab = null;
#if UNITY_EDITOR
            modelPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/3D/Ingame_Object/Random_Ring.fbx");
#endif
            if (modelPrefab == null)
            {
                modelPrefab = Resources.Load<GameObject>("Random_Ring");
            }

            if (modelPrefab != null)
            {
                GameObject meshInstance = Instantiate(modelPrefab, transform);
                meshInstance.name = "RingModel";
                meshTransform = meshInstance.transform;
                meshTransform.localScale = meshTransform.localScale * visualScaleMultiplier;
                initialLocalScale = meshTransform.localScale;
            }
            else
            {
                // 모델이 없을 때의 비주얼 폴백 (토러스/실린더 형태)
                GameObject fallback = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                fallback.name = "RingModel_Fallback";
                fallback.transform.SetParent(transform);
                fallback.transform.localPosition = Vector3.zero;
                fallback.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                fallback.transform.localScale = new Vector3(2.5f * visualScaleMultiplier, 0.15f * visualScaleMultiplier, 2.5f * visualScaleMultiplier);
                Collider col = fallback.GetComponent<Collider>();
                if (col != null) Destroy(col);

                meshTransform = fallback.transform;
                initialLocalScale = meshTransform.localScale;
            }
        }

        private void Update()
        {
            if (isTriggered) return;

            // 1. 박자에 맞춘 상하 부드러운 바운스 (Bobbing)
            bobbingTimer += Time.deltaTime * 2.5f;
            float verticalOffset = Mathf.Sin(bobbingTimer) * bobbingAmplitude;
            Vector3 curPos = transform.position;
            curPos.y = waterLevel + baseHeightOffset + verticalOffset;
            transform.position = curPos;

            // 2. 은은한 자전
            if (meshTransform != null)
            {
                meshTransform.Rotate(Vector3.forward, idleRotationSpeed * Time.deltaTime, Space.Self);
            }

            // 3. 근접 돌 트리거 감지 (리듬 아케이드 돌)
            CheckStoneProximity();
        }

        private void CheckStoneProximity()
        {
            ArcadeSkippingStone stone = FindAnyObjectByType<ArcadeSkippingStone>();
            if (stone == null || !stone.isThrown || stone.isSunk || stone.isCrashed || stone.isSkimming) return;

            Vector3 ringCenter = transform.position;
            Vector3 stonePos = stone.transform.position;

            // 링 영역 진입 판정
            float dist = Vector3.Distance(ringCenter, stonePos);
            if (dist <= triggerRadius)
            {
                TriggerRing(stone);
            }
        }

        private void TriggerRing(ArcadeSkippingStone stone)
        {
            if (isTriggered) return;
            isTriggered = true;

            // 돌에게 링 진입 전달
            stone.EnterRandomRing(this);
        }

        /// <summary>
        /// 🥁 돌이 링에 머무는 동안 음악 비트에 맞춘 둥-둥- 스케일 펄스 연출
        /// </summary>
        public void PlayBeatPulse(int beatCount, float beatDuration)
        {
            StartCoroutine(CoBeatPulse(beatCount, beatDuration));
        }

        private IEnumerator CoBeatPulse(int beatCount, float beatDuration)
        {
            if (meshTransform == null) yield break;

            for (int i = 0; i < beatCount; i++)
            {
                float halfTime = beatDuration * 0.5f;
                float elapsed = 0f;

                // 쿵! (팽창)
                while (elapsed < halfTime)
                {
                    elapsed += Time.deltaTime;
                    float t = Mathf.Clamp01(elapsed / halfTime);
                    float curve = Mathf.Sin(t * Mathf.PI * 0.5f);
                    meshTransform.localScale = Vector3.Lerp(initialLocalScale, initialLocalScale * pulseMaxScale, curve);
                    yield return null;
                }

                elapsed = 0f;
                // 짝! (수축 복귀)
                while (elapsed < halfTime)
                {
                    elapsed += Time.deltaTime;
                    float t = Mathf.Clamp01(elapsed / halfTime);
                    float curve = Mathf.Sin(t * Mathf.PI * 0.5f);
                    meshTransform.localScale = Vector3.Lerp(initialLocalScale * pulseMaxScale, initialLocalScale, curve);
                    yield return null;
                }
            }

            meshTransform.localScale = initialLocalScale;
        }

        /// <summary>
        /// 🚀 돌 발사 후 링 소멸 연출 (축소 및 디졸브)
        /// </summary>
        public void DisappearAndDestroy()
        {
            StartCoroutine(CoDisappear());
        }

        private IEnumerator CoDisappear()
        {
            if (meshTransform != null)
            {
                float duration = 0.35f;
                float elapsed = 0f;
                Vector3 startScale = meshTransform.localScale;

                while (elapsed < duration)
                {
                    elapsed += Time.deltaTime;
                    float t = Mathf.Clamp01(elapsed / duration);
                    meshTransform.localScale = Vector3.Lerp(startScale, Vector3.zero, t * t);
                    yield return null;
                }
            }

            Destroy(gameObject);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, triggerRadius);
        }
    }
}
