using System;
using System.Collections;
using UnityEngine;

namespace SkippingStones.RhythmArcade
{
    /// <summary>
    /// 🎵 [RhythmArcadeStone] 물리(Rigidbody/충돌)를 배제한 순수 BPM 기반 결정론적 포물선 비행 컨트롤러
    /// - 1회 바운스 체공 시간: BPM에 따라 수학적 고정 (BPM 60 = 1.00s, BPM 120 = 0.50s)
    /// - 1회 바운스 수평 이동 거리: 타이밍 판정에 따라 차등 부여 (PERFECT: 30m, GREAT: 20m, EARLY/LATE: 12m, MISS: 5m)
    /// - 수학적 포물선: Y(t) = WaterLevel + 4 * PeakHeight * t * (1 - t)
    /// </summary>
    public class RhythmArcadeStone : MonoBehaviour
    {
        [Header("상태")]
        public bool isFlying = false;
        public bool isFinished = false;
        public float totalDistance = 0f;
        public int skipCount = 0;
        public int comboCount = 0;

        [Header("현재 비트 바운스 파라미터")]
        public float currentBPM = 60f;
        public float beatDuration = 1.0f;       // 60 / BPM
        public float stepDistance = 20.0f;      // 현재 스텝 수평 비행 거리
        public float peakHeight = 1.2f;         // 포물선 정점 높이
        public Vector3 moveDirection = Vector3.forward;

        private Vector3 stepStartPos;
        private Vector3 stepTargetPos;
        private float stepElapsed = 0f;
        private float waterLevel = 0f;

        [Header("비주얼 및 수면 반사 그림자")]
        private GameObject waterShadowObj;
        private MeshRenderer waterShadowRenderer;
        private Material waterShadowMat;

        public event Action<Vector3, float> OnBounceTriggered; // 착수 위치, 비트 주기
        public event Action<float> OnFlightProgress;          // 진행률 (0~1)
        public event Action OnFlightFinished;

        public void Initialize(Vector3 startPos, Vector3 initialDirection, float initBPM, float initDistance)
        {
            UpdateWaterLevel();
            transform.position = new Vector3(startPos.x, waterLevel + 0.1f, startPos.z);
            moveDirection = initialDirection.normalized;
            currentBPM = initBPM;
            beatDuration = 60f / Mathf.Max(30f, currentBPM);
            stepDistance = initDistance;
            totalDistance = 0f;
            skipCount = 0;
            comboCount = 0;
            isFlying = false;
            isFinished = false;

            SetupWaterShadow();
        }

        public void StartFlight()
        {
            isFlying = true;
            isFinished = false;
            StartNewBounceStep(stepDistance, moveDirection);
        }

        public void SetNextBounceStep(float nextStepDist, float steerAngleDegrees, float nextBPM)
        {
            if (isFinished) return;

            currentBPM = nextBPM;
            beatDuration = 60f / Mathf.Max(30f, currentBPM);
            stepDistance = nextStepDist;

            if (Mathf.Abs(steerAngleDegrees) > 0.01f)
            {
                Quaternion rot = Quaternion.Euler(0f, steerAngleDegrees, 0f);
                moveDirection = (rot * moveDirection).normalized;
            }

            StartNewBounceStep(stepDistance, moveDirection);
        }

        private void StartNewBounceStep(float distance, Vector3 dir)
        {
            skipCount++;
            stepStartPos = transform.position;
            stepStartPos.y = waterLevel; // 수면 시작점 고정

            stepTargetPos = stepStartPos + (dir * distance);
            stepTargetPos.y = waterLevel;

            stepElapsed = 0f;

            // 착수 파문 및 링 이벤트 발생
            OnBounceTriggered?.Invoke(stepTargetPos, beatDuration);
        }

        private void Update()
        {
            if (!isFlying || isFinished) return;

            stepElapsed += Time.deltaTime;
            float t = Mathf.Clamp01(stepElapsed / beatDuration);

            // 1. 수평 XZ 보간
            Vector3 currentXZ = Vector3.Lerp(stepStartPos, stepTargetPos, t);

            // 2. 수직 Y 포물선 계산 (t=0 -> 0, t=0.5 -> peakHeight, t=1.0 -> 0)
            float arcY = 4f * peakHeight * t * (1f - t);
            float currentY = waterLevel + arcY;

            transform.position = new Vector3(currentXZ.x, currentY, currentXZ.z);

            // 3. 진행 방향을 바라보는 부드러운 틸트 회전
            if (moveDirection.sqrMagnitude > 0.001f)
            {
                float pitchAngle = Mathf.Lerp(18f, -18f, t); // 상승 시 들림(+), 하강 시 숙임(-)
                Quaternion baseRot = Quaternion.LookRotation(moveDirection, Vector3.up);
                transform.rotation = baseRot * Quaternion.Euler(pitchAngle, 0f, 0f);
            }

            // 4. 수면 그림자 위치 동기화
            UpdateWaterShadow(currentXZ, arcY);

            // 5. 누적 거리 갱신
            totalDistance = Vector3.Distance(new Vector3(stepStartPos.x, 0f, stepStartPos.z), new Vector3(currentXZ.x, 0f, currentXZ.z)) + ((skipCount - 1) * stepDistance);

            OnFlightProgress?.Invoke(t);

            // 6. 스텝 완료 (착수 순간 도달 시 다음 스텝 자동 진행 or 마감)
            if (t >= 1.0f)
            {
                OnStepCompleted();
            }
        }

        private void OnStepCompleted()
        {
            transform.position = stepTargetPos;
            // 타이밍 입력이 없었을 경우 기본 바운스 처리 (Miss 판정으로 감속)
            StartNewBounceStep(stepDistance * 0.6f, moveDirection);
        }

        public void FinishFlight()
        {
            isFlying = false;
            isFinished = true;
            if (waterShadowObj != null) waterShadowObj.SetActive(false);
            OnFlightFinished?.Invoke();
        }

        private void UpdateWaterLevel()
        {
            WaterSurface ws = FindAnyObjectByType<WaterSurface>();
            if (ws != null)
            {
                Collider c = ws.GetComponent<Collider>();
                waterLevel = (c != null) ? c.bounds.max.y : ws.transform.position.y;
                return;
            }
            GameObject water = GameObject.Find("WaterSurface") ?? GameObject.Find("Water_Surface");
            if (water != null)
            {
                Collider col = water.GetComponent<Collider>();
                waterLevel = (col != null) ? col.bounds.max.y : water.transform.position.y;
            }
        }

        private void SetupWaterShadow()
        {
            if (waterShadowObj != null) Destroy(waterShadowObj);

            waterShadowObj = GameObject.CreatePrimitive(PrimitiveType.Quad);
            waterShadowObj.name = "[RhythmArcade_Shadow]";
            Destroy(waterShadowObj.GetComponent<Collider>());
            waterShadowObj.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

            waterShadowRenderer = waterShadowObj.GetComponent<MeshRenderer>();
            Shader unlit = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
            waterShadowMat = (unlit != null) ? new Material(unlit) : new Material(Shader.Find("Standard"));

            Texture2D softShadowTex = new Texture2D(32, 32, TextureFormat.RGBA32, false);
            Vector2 center = new Vector2(15.5f, 15.5f);
            float radius = 15.5f;
            for (int y = 0; y < 32; y++)
            {
                for (int x = 0; x < 32; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), center);
                    float alpha = Mathf.Pow(Mathf.SmoothStep(1.0f, 0.0f, dist / radius), 1.8f);
                    softShadowTex.SetPixel(x, y, new Color(0.02f, 0.08f, 0.16f, alpha * 0.4f));
                }
            }
            softShadowTex.Apply();

            waterShadowMat.mainTexture = softShadowTex;
            waterShadowRenderer.material = waterShadowMat;
            waterShadowRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            waterShadowRenderer.receiveShadows = false;
        }

        private void UpdateWaterShadow(Vector3 stoneXZ, float currentHeight)
        {
            if (waterShadowObj == null) return;
            waterShadowObj.SetActive(true);
            waterShadowObj.transform.position = new Vector3(stoneXZ.x, waterLevel + 0.02f, stoneXZ.z);
            float scale = Mathf.Lerp(0.35f, 0.2f, Mathf.Clamp01(currentHeight / peakHeight));
            waterShadowObj.transform.localScale = new Vector3(scale, scale, 1.0f);
        }

        private void OnDestroy()
        {
            if (waterShadowObj != null) Destroy(waterShadowObj);
            if (waterShadowMat != null) Destroy(waterShadowMat);
        }
    }
}
