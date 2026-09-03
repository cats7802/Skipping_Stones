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
        [Tooltip("외부 링 자전 속도 (도/초)")]
        public float outerRotationSpeed = 45f;
        [Tooltip("내부 링 자전 속도 (도/초, 음수면 역방향)")]
        public float innerRotationSpeed = -65f;
        [Tooltip("펄스 연출 시 최대 스케일 배율")]
        public float pulseMaxScale = 1.3f;
        [Tooltip("외부 링 기본 크기 배율")]
        public float outerScaleMultiplier = 3.0f; // 기존 2.0f 대비 1.5배 확대 (3.0f)
        [Tooltip("내부 링 기본 크기 배율")]
        public float innerScaleMultiplier = 1.5f; // 기존 1.0f 대비 1.5배 확대 (1.5f)

        [Header("🎯 인터랙션 영역 (원판형 정밀 통과 감지)")]
        [Tooltip("링 통과 감지 원형 반경(m, 링 테두리에 직접 닿았을 때 작동)")]
        public float triggerRadius = 1.6f;
        [Tooltip("링 앞뒤 감지 두께(m, 링에 닿는 순간만 작동)")]
        public float planeThickness = 0.35f;

        [Header("✨ 이펙트 슬롯 (추후 연동용)")]
        public GameObject portalVFX;
        public GameObject speedlinesVFX;

        // 내부 상태 (외부 링 & 내부 링)
        private Transform outerRingTrans;
        private Transform innerRingTrans;
        private Vector3 initialOuterScale = Vector3.one;
        private Vector3 initialInnerScale = Vector3.one;
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
            // 3D/Ingame_Object/Random_Ring.fbx 로드
            GameObject modelPrefab = null;
#if UNITY_EDITOR
            modelPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/3D/Ingame_Object/Random_Ring.fbx");
#endif
            if (modelPrefab == null)
            {
                modelPrefab = Resources.Load<GameObject>("Random_Ring");
            }

            // 기존 자식 정리
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                Destroy(transform.GetChild(i).gameObject);
            }

            // 1. 🌀 외부 링 (Outer Ring) 생성
            GameObject outerObj = (modelPrefab != null) ? Instantiate(modelPrefab, transform) : GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            outerObj.name = "OuterRing";
            outerRingTrans = outerObj.transform;
            outerRingTrans.localPosition = Vector3.zero;
            outerRingTrans.localRotation = (modelPrefab == null) ? Quaternion.Euler(90f, 0f, 0f) : Quaternion.identity;
            outerRingTrans.localScale = Vector3.one * outerScaleMultiplier;
            initialOuterScale = outerRingTrans.localScale;

            // 2. 🌀 내부 링 (Inner Ring) 생성
            GameObject innerObj = (modelPrefab != null) ? Instantiate(modelPrefab, transform) : GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            innerObj.name = "InnerRing";
            innerRingTrans = innerObj.transform;
            innerRingTrans.localPosition = Vector3.zero;
            innerRingTrans.localRotation = (modelPrefab == null) ? Quaternion.Euler(90f, 0f, 0f) : Quaternion.identity;
            innerRingTrans.localScale = Vector3.one * innerScaleMultiplier;
            initialInnerScale = innerRingTrans.localScale;

            // 콜라이더 제거
            Collider[] cols = GetComponentsInChildren<Collider>();
            for (int i = 0; i < cols.Length; i++)
            {
                Destroy(cols[i]);
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

            // 2. 외부 링 정방향 회전 & 내부 링 역방향 회전 (Counter-Rotation)
            if (outerRingTrans != null)
            {
                outerRingTrans.Rotate(Vector3.forward, outerRotationSpeed * Time.deltaTime, Space.Self);
            }
            if (innerRingTrans != null)
            {
                innerRingTrans.Rotate(Vector3.forward, innerRotationSpeed * Time.deltaTime, Space.Self);
            }

            // 3. 근접 돌 트리거 감지 (원판형 통과 감지)
            CheckStoneProximity();
        }

        private void CheckStoneProximity()
        {
            ArcadeSkippingStone stone = FindAnyObjectByType<ArcadeSkippingStone>();
            if (stone == null || !stone.isThrown || stone.isSunk || stone.isCrashed || stone.isSkimming) return;

            // 링의 로컬 좌표계로 돌 위치 변환
            Vector3 localStonePos = transform.InverseTransformPoint(stone.transform.position);

            // 1) 링 앞뒤 두께 검사 (Z축 기준 ±planeThickness 이내)
            bool isWithinThickness = Mathf.Abs(localStonePos.z) <= (planeThickness * 0.5f);

            // 2) 링 원형 반경 검사 (XY 평면 상의 거리)
            float radialDist = new Vector2(localStonePos.x, localStonePos.y).magnitude;
            bool isWithinRadius = radialDist <= triggerRadius;

            if (isWithinThickness && isWithinRadius)
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
                    if (outerRingTrans != null) outerRingTrans.localScale = Vector3.Lerp(initialOuterScale, initialOuterScale * pulseMaxScale, curve);
                    if (innerRingTrans != null) innerRingTrans.localScale = Vector3.Lerp(initialInnerScale, initialInnerScale * pulseMaxScale, curve);
                    yield return null;
                }

                elapsed = 0f;
                // 짝! (수축 복귀)
                while (elapsed < halfTime)
                {
                    elapsed += Time.deltaTime;
                    float t = Mathf.Clamp01(elapsed / halfTime);
                    float curve = Mathf.Sin(t * Mathf.PI * 0.5f);
                    if (outerRingTrans != null) outerRingTrans.localScale = Vector3.Lerp(initialOuterScale * pulseMaxScale, initialOuterScale, curve);
                    if (innerRingTrans != null) innerRingTrans.localScale = Vector3.Lerp(initialInnerScale * pulseMaxScale, initialInnerScale, curve);
                    yield return null;
                }
            }

            if (outerRingTrans != null) outerRingTrans.localScale = initialOuterScale;
            if (innerRingTrans != null) innerRingTrans.localScale = initialInnerScale;
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
            float duration = 0.35f;
            float elapsed = 0f;

            Vector3 startOuter = (outerRingTrans != null) ? outerRingTrans.localScale : Vector3.zero;
            Vector3 startInner = (innerRingTrans != null) ? innerRingTrans.localScale : Vector3.zero;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float easeOut = t * t;
                if (outerRingTrans != null) outerRingTrans.localScale = Vector3.Lerp(startOuter, Vector3.zero, easeOut);
                if (innerRingTrans != null) innerRingTrans.localScale = Vector3.Lerp(startInner, Vector3.zero, easeOut);
                yield return null;
            }

            Destroy(gameObject);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            // 링 평면에 맞춘 원판 기즈모 시각화
            Matrix4x4 oldMat = Gizmos.matrix;
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(Vector3.zero, new Vector3(triggerRadius * 2f, triggerRadius * 2f, planeThickness));
            Gizmos.matrix = oldMat;
        }
    }
}
