using System.Collections;
using UnityEngine;

namespace SkippingStones.Arcade
{
    /// <summary>
    /// 🌀 리듬 아케이드 모드 전용 랜덤 링 (Random Ring)
    /// - 평소 대기 시: 위치(Z좌표) 및 개체별 랜덤 위상(Phase Offset)을 반영하여 제각각 자연스러운 웨이브로 둥실거림 (칼군무 방지)
    /// - 돌 접근 시(35m 이내): 돌의 비행 높이로 자석처럼 부드럽게 스냅 전환되어 돌이 링 밑으로 빠져나가지 않음
    /// - 링 크기(지름 1.76m, 반경 0.88m)에 100% 칼같이 일치하는 원판형(Cylinder) 트리거 콜라이더 및 기즈모
    /// - 링 안착 시 2박자 동안 음악 비트에 맞춘 둥-둥- 스케일 펄스(Beat Pulse)
    /// </summary>
    public class RandomRing : MonoBehaviour
    {
        [Header("🎛️ 비주얼 & 애니메이션 설정")]
        [Tooltip("평소 대기 시 상하 바운스 진폭(m)")]
        public float bobbingAmplitude = 0.5f;
        [Tooltip("외부 링 자전 속도 (도/초)")]
        public float outerRotationSpeed = 45f;
        [Tooltip("내부 링 자전 속도 (도/초, 음수면 역방향)")]
        public float innerRotationSpeed = -65f;
        [Tooltip("펄스 연출 시 최대 스케일 배율")]
        public float pulseMaxScale = 1.3f;
        
        [Header("📏 링 크기 배율")]
        [Tooltip("외부 링 기본 크기 배율 (8.0f = 외경 지름 약 1.76m, 반경 약 0.88m)")]
        public float outerScaleMultiplier = 8.0f; 
        [Tooltip("내부 링 기본 크기 배율")]
        public float innerScaleMultiplier = 4.0f; 

        [Header("🎯 인터랙션 영역 (링 외곽에 1:1 완벽 정렬)")]
        [Tooltip("링 모델 외경 대비 감지 여유율 (1.0 = 모델 외경에 칼같이 일치)")]
        public float radiusMarginFactor = 1.0f;
        [Tooltip("링 앞뒤 감지 두께(m)")]
        public float planeThickness = 0.5f;

        [Header("✨ 이펙트 슬롯 (추후 연동용)")]
        public GameObject portalVFX;
        public GameObject speedlinesVFX;

        // 내부 상태
        private Transform outerRingTrans;
        private Transform innerRingTrans;
        private Vector3 initialOuterScale = Vector3.one;
        private Vector3 initialInnerScale = Vector3.one;
        private float waterLevel = 16.0f;
        private bool isTriggered = false;
        private float bobbingTimer = 0f;
        private float phaseOffset = 0f;
        private CapsuleCollider triggerCollider;
        private float calculatedRadius = 0.88f;
        private ArcadeSkippingStone cachedStone;

        public bool IsTriggered => isTriggered;
        public float TriggerRadius => calculatedRadius;

        private void Awake()
        {
            // 위치 기반 및 랜덤 위상 오프셋 부여 (모든 링이 동시에 춤추지 않고 자연스럽게 물결치듯 분산)
            phaseOffset = (transform.position.z * 0.15f) + Random.Range(0f, Mathf.PI * 2f);
            bobbingTimer = phaseOffset;

            SetupVisualMesh();
            SetupTriggerCollider();
        }

        private void Start()
        {
            UpdateWaterLevel();
            cachedStone = FindAnyObjectByType<ArcadeSkippingStone>();
        }

        private void OnValidate()
        {
            CalculateExactRadius();
            if (triggerCollider != null)
            {
                triggerCollider.radius = calculatedRadius;
                triggerCollider.height = planeThickness;
            }
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

        private void CalculateExactRadius()
        {
            float rawMeshRadius = 0.11f;
            calculatedRadius = rawMeshRadius * outerScaleMultiplier * radiusMarginFactor;
        }

        private void SetupTriggerCollider()
        {
            CalculateExactRadius();

            triggerCollider = GetComponent<CapsuleCollider>();
            if (triggerCollider == null) triggerCollider = gameObject.AddComponent<CapsuleCollider>();

            triggerCollider.isTrigger = true;
            triggerCollider.direction = 2; // Z-Axis
            triggerCollider.radius = calculatedRadius;
            triggerCollider.height = planeThickness;
            triggerCollider.center = Vector3.zero;
        }

        private void SetupVisualMesh()
        {
            GameObject modelPrefab = null;
#if UNITY_EDITOR
            modelPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/3D/Ingame_Object/Random_Ring.fbx");
#endif
            if (modelPrefab == null)
            {
                modelPrefab = Resources.Load<GameObject>("Random_Ring");
            }

            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                Destroy(transform.GetChild(i).gameObject);
            }

            // 1. 🌀 외부 링
            GameObject outerObj = (modelPrefab != null) ? Instantiate(modelPrefab, transform) : GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            outerObj.name = "OuterRing";
            outerRingTrans = outerObj.transform;
            outerRingTrans.localPosition = Vector3.zero;
            outerRingTrans.localRotation = (modelPrefab == null) ? Quaternion.Euler(90f, 0f, 0f) : Quaternion.identity;
            outerRingTrans.localScale = Vector3.one * outerScaleMultiplier;
            initialOuterScale = outerRingTrans.localScale;

            // 2. 🌀 내부 링
            GameObject innerObj = (modelPrefab != null) ? Instantiate(modelPrefab, transform) : GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            innerObj.name = "InnerRing";
            innerRingTrans = innerObj.transform;
            innerRingTrans.localPosition = Vector3.zero;
            innerRingTrans.localRotation = (modelPrefab == null) ? Quaternion.Euler(90f, 0f, 0f) : Quaternion.identity;
            innerRingTrans.localScale = Vector3.one * innerScaleMultiplier;
            initialInnerScale = innerRingTrans.localScale;

            Collider[] cols = GetComponentsInChildren<Collider>();
            for (int i = 0; i < cols.Length; i++)
            {
                if (cols[i] != triggerCollider && cols[i].gameObject != gameObject)
                {
                    Destroy(cols[i]);
                }
            }
        }

        private void Update()
        {
            if (isTriggered) return;

            // 1. 🎯 평소에는 개별 위상으로 둥실둥실 ➔ 돌이 다가오면 돌 높이와 실시간 스냅 동기화
            UpdateAdaptiveHeight();

            // 2. 외부 링 정방향 회전 & 내부 링 역방향 회전
            if (outerRingTrans != null)
            {
                outerRingTrans.Rotate(Vector3.forward, outerRotationSpeed * Time.deltaTime, Space.Self);
            }
            if (innerRingTrans != null)
            {
                innerRingTrans.Rotate(Vector3.forward, innerRotationSpeed * Time.deltaTime, Space.Self);
            }

            // 3. 근접 돌 트리거 감지
            CheckStoneProximity();
        }

        /// <summary>
        /// 🌊 평소: 수면 위 1.8m 기준 개체별 고유 위상으로 둥실둥실(Bobbing Wave)
        /// 🚀 돌 접근 시(35m 이내): 둥실거림을 점진적으로 돌 높이(stoneY)로 자석처럼 부드럽게 스냅 전환
        /// </summary>
        private void UpdateAdaptiveHeight()
        {
            if (cachedStone == null)
            {
                cachedStone = FindAnyObjectByType<ArcadeSkippingStone>();
            }

            bobbingTimer += Time.deltaTime * 2.5f;
            // 개체별 고유 위상(phaseOffset)이 더해져 각 링마다 다른 타이밍에 둥실거림
            float idleBobbingOffset = Mathf.Sin(bobbingTimer + phaseOffset) * bobbingAmplitude;

            float stoneArcHeight = (cachedStone != null) ? cachedStone.CurrentBounceArcHeight : 1.8f;
            float defaultIdleY = waterLevel + stoneArcHeight + idleBobbingOffset;

            float targetY = defaultIdleY;

            if (cachedStone != null && cachedStone.isThrown && !cachedStone.isSunk && !cachedStone.isCrashed && !cachedStone.isSkimming)
            {
                float distZ = transform.position.z - cachedStone.transform.position.z;

                // 돌이 링 전방 35m 이내로 진입했을 때
                if (distZ > -3f && distZ < 35f)
                {
                    float approachFactor = Mathf.Clamp01(1.0f - (distZ / 35f));
                    float stoneActualY = Mathf.Max(waterLevel + 0.4f, cachedStone.transform.position.y);

                    // 평소 둥실 높이와 돌의 실제 비행 높이를 자연스럽게 블렌딩하여 스냅
                    targetY = Mathf.Lerp(defaultIdleY, stoneActualY, approachFactor);
                }
            }

            Vector3 curPos = transform.position;
            curPos.y = Mathf.Lerp(curPos.y, targetY, Time.deltaTime * 10.0f);
            transform.position = curPos;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (isTriggered) return;
            ArcadeSkippingStone stone = other.GetComponent<ArcadeSkippingStone>() ?? other.GetComponentInParent<ArcadeSkippingStone>();
            if (stone != null && stone.isThrown && !stone.isSunk && !stone.isCrashed && !stone.isSkimming)
            {
                TriggerRing(stone);
            }
        }

        private void CheckStoneProximity()
        {
            if (cachedStone == null) return;
            if (!cachedStone.isThrown || cachedStone.isSunk || cachedStone.isCrashed || cachedStone.isSkimming) return;

            Vector3 localStonePos = transform.InverseTransformPoint(cachedStone.transform.position);

            bool isWithinThickness = Mathf.Abs(localStonePos.z) <= (planeThickness * 0.5f);
            float radialDist = new Vector2(localStonePos.x, localStonePos.y).magnitude;
            bool isWithinRadius = radialDist <= calculatedRadius;

            if (isWithinThickness && isWithinRadius)
            {
                TriggerRing(cachedStone);
            }
        }

        private void TriggerRing(ArcadeSkippingStone stone)
        {
            if (isTriggered) return;
            isTriggered = true;

            stone.EnterRandomRing(this);
        }

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
                while (elapsed < halfTime)
                {
                    elapsed += Time.deltaTime;
                    float t = Mathf.Clamp01(elapsed / halfTime);
                    float curve = Mathf.Sin(t * Mathf.PI * 0.5f);
                    if (outerRingTrans != null) outerRingTrans.localScale = Vector3.Lerp(initialOuterScale * pulseMaxScale, initialOuterScale, curve);
                    if (innerRingTrans != null) innerRingTrans.localScale = Vector3.Lerp(initialInnerScale, initialInnerScale * pulseMaxScale, curve);
                    yield return null;
                }
            }

            if (outerRingTrans != null) outerRingTrans.localScale = initialOuterScale;
            if (innerRingTrans != null) innerRingTrans.localScale = initialInnerScale;
        }

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
            CalculateExactRadius();

            Gizmos.color = new Color(0.1f, 0.9f, 1f, 0.9f);
            Matrix4x4 oldMat = Gizmos.matrix;
            Gizmos.matrix = transform.localToWorldMatrix;

            int segments = 32;
            float angleStep = 360f / segments;
            float halfThick = planeThickness * 0.5f;

            for (int i = 0; i < segments; i++)
            {
                float a1 = i * angleStep * Mathf.Deg2Rad;
                float a2 = (i + 1) * angleStep * Mathf.Deg2Rad;

                Vector3 p1Front = new Vector3(Mathf.Cos(a1) * calculatedRadius, Mathf.Sin(a1) * calculatedRadius, halfThick);
                Vector3 p2Front = new Vector3(Mathf.Cos(a2) * calculatedRadius, Mathf.Sin(a2) * calculatedRadius, halfThick);
                Gizmos.DrawLine(p1Front, p2Front);

                Vector3 p1Back = new Vector3(Mathf.Cos(a1) * calculatedRadius, Mathf.Sin(a1) * calculatedRadius, -halfThick);
                Vector3 p2Back = new Vector3(Mathf.Cos(a2) * calculatedRadius, Mathf.Sin(a2) * calculatedRadius, -halfThick);
                Gizmos.DrawLine(p1Back, p2Back);

                if (i % (segments / 4) == 0)
                {
                    Gizmos.DrawLine(p1Front, p1Back);
                }
            }

            Gizmos.matrix = oldMat;
        }
    }
}
