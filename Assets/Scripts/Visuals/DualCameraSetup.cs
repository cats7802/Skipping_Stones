using UnityEngine;

[ExecuteAlways]
public class DualCameraSetup : MonoBehaviour
{
    public enum CameraMode
    {
        TopDownPosition, // 0단계: 탑다운 뷰 (위치 선정)
        ShoulderAim,     // 1~2단계: 숄더 뷰 (방향 조준 & 파워 와인드업)
        LaunchLeadIn,    // 2.5단계: 45~55프레임 발사 앵커 선행 가속
        DynamicFlight,   // 3단계: 다이내믹 쿼터뷰 (비행 및 리듬 바운스)
        TopDownReplay    // 3.5단계: 90도 수직 직교(Orthographic) 탑다운 리플레이
    }

    [Header("타깃 참조")]
    public Transform targetStone;
    public Transform targetCharacter;

    private Vector3 leadInAnchorPos;
    private Vector3 leadInForwardDir;
    private Vector3 replayCenterPos;
    private float replayOrthoSize = 25f;

    [Header("수면 기준 높이")]
    public float waterLevel = 0f;
    private WaterSurface waterSurfaceCache;

    public void SetReplayTopDownView(Vector3 centerPos, float orthoSize)
    {
        replayCenterPos = centerPos;
        replayOrthoSize = orthoSize;
        currentMode = CameraMode.TopDownReplay;

        if (mainCam != null)
        {
            mainCam.orthographic = true;
            mainCam.orthographicSize = orthoSize;
            // 🌟 수면 및 타깃 높이 기준 +80m 위에서 수직 조망
            float targetY = Mathf.Max(centerPos.y, waterLevel);
            mainCam.transform.position = new Vector3(centerPos.x, targetY + 80f, centerPos.z);
            mainCam.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        }
        RenderSettings.fog = false;
    }

    public void StartLaunchLeadIn(Vector3 anchorPos, Vector3 forwardDir)
    {
        leadInAnchorPos = anchorPos;
        leadInForwardDir = forwardDir;
        currentMode = CameraMode.LaunchLeadIn;
    }

    [Header("현재 카메라 모드")]
    public CameraMode currentMode = CameraMode.TopDownPosition;

    [Header("1번 메인 카메라 (3D)")]
    public Camera mainCam;
    public float followSmoothSpeed = 8f;

    [Header("모드별 카메라 오프셋 (세로 9:16 최적화)")]
    public float topDownDistBack = 9.0f;
    public float topDownHeight = 5.6f;
    public float topDownLookForward = 10.0f;
    public float topDownLookHeight = 1.5f;

    public float shoulderDistBack = 3.4f;
    public float shoulderHeight = 2.2f;
    public float shoulderLookForward = 14.0f;
    public float shoulderLookHeight = 1.3f;

    [Header("3단계: 비행 추적 카메라 (돌과 리듬 링 세로 9:16 위에서 3번째 3/6 구간 배치)")]
    public float flightDistBack = 5.5f;
    public float flightHeight = 2.4f;
    public float flightLookForward = 7.5f;
    public float flightLookHeight = -2.2f;
    [Tooltip("조작 직후 돌이 먼저 꺾인 뒤 다음 바운스까지 카메라가 정후방으로 돌아오는 보간 속도")]
    public float headingCatchupSpeed = 4.2f;
    private Vector3 smoothedFlightHeading = Vector3.forward;

    [Header("2번 가이드 카메라 (PIP 정측면 뷰)")]
    public bool enableGuideCamera = false;
    public Camera guideCam;
    public Vector3 guideOffset = new Vector3(0f, 1.0f, 8f);
    public float guideOrthoSize = 2.8f;
    [Range(0f, 1f)] public float pipX = 0.65f;
    [Range(0f, 1f)] public float pipY = 0.60f;
    [Range(0.1f, 0.5f)] public float pipWidth = 0.32f;
    [Range(0.1f, 0.5f)] public float pipHeight = 0.35f;

    private void Awake()
    {
        flightDistBack = 5.5f;
        flightHeight = 2.4f;
        flightLookForward = 7.5f;
        flightLookHeight = -2.2f;
        EnsureReferences();
    }

    private void Start()
    {
        EnsureReferences();
        SnapCameraImmediate();
    }

    public void EnsureReferences()
    {
        if (mainCam == null)
        {
            mainCam = Camera.main;
            if (mainCam == null)
            {
                var cams = FindObjectsByType<Camera>(FindObjectsInactive.Include);
                foreach (var c in cams)
                {
                    if (c.name.IndexOf("map", System.StringComparison.OrdinalIgnoreCase) < 0 &&
                        c.name.IndexOf("sample", System.StringComparison.OrdinalIgnoreCase) < 0)
                    {
                        mainCam = c;
                        break;
                    }
                }
            }
            if (mainCam == null)
            {
                GameObject camObj = new GameObject("Main Camera");
                mainCam = camObj.AddComponent<Camera>();
                camObj.tag = "MainCamera";
                camObj.AddComponent<AudioListener>();
            }
        }

        if (targetCharacter == null)
        {
            var chr = FindAnyObjectByType<StoneThrowerCharacter>();
            if (chr != null) targetCharacter = chr.transform;
        }

        if (targetStone == null)
        {
            var st = FindAnyObjectByType<SkippingStone>();
            if (st != null) targetStone = st.transform;
        }

        if (waterSurfaceCache == null)
        {
            waterSurfaceCache = FindAnyObjectByType<WaterSurface>();
            if (waterSurfaceCache == null)
            {
                var waterObj = GameObject.Find("WaterSurface");
                if (waterObj != null) waterSurfaceCache = waterObj.GetComponent<WaterSurface>();
            }
        }

        // 🌟 콜라이더 Bounds 상단 기준 또는 트랜스폼 높이로 수면 Y값 동적 취득
        if (waterSurfaceCache != null)
        {
            Collider col = waterSurfaceCache.GetComponent<Collider>();
            waterLevel = (col != null) ? col.bounds.max.y : waterSurfaceCache.transform.position.y;
        }
    }

    public void SnapCameraImmediate()
    {
        EnsureReferences();

        if (mainCam != null && targetCharacter != null)
        {
            Vector3 charPos = targetCharacter.position;
            Vector3 forwardDir = targetCharacter.forward;
            Vector3 backDir = -forwardDir;
            mainCam.transform.position = charPos + (backDir * topDownDistBack) + (Vector3.up * topDownHeight);
            Vector3 lookTarget = charPos + (forwardDir * topDownLookForward) + (Vector3.up * topDownLookHeight);
            mainCam.transform.rotation = Quaternion.LookRotation((lookTarget - mainCam.transform.position).normalized);
            mainCam.fieldOfView = 60f;
        }
    }

    private void LateUpdate()
    {
        EnsureReferences();

        Vector3 charPos = (targetCharacter != null) ? targetCharacter.position : (targetStone != null ? targetStone.position : transform.position);
        Vector3 stonePos = (targetStone != null) ? targetStone.position : charPos;

        Vector3 forwardDir = (targetCharacter != null) ? targetCharacter.forward : Vector3.forward;
        Vector3 backDir = -forwardDir;

        Vector3 targetOffset = Vector3.zero;
        Vector3 targetLookOffset = Vector3.zero;

        switch (currentMode)
        {
            case CameraMode.TopDownPosition:
                targetOffset = charPos + (backDir * topDownDistBack) + (Vector3.up * topDownHeight);
                targetLookOffset = charPos + (forwardDir * topDownLookForward) + (Vector3.up * topDownLookHeight);
                break;

            case CameraMode.ShoulderAim:
                targetOffset = charPos + (backDir * shoulderDistBack) + (Vector3.up * shoulderHeight);
                targetLookOffset = charPos + (forwardDir * shoulderLookForward) + (Vector3.up * shoulderLookHeight);
                break;

            case CameraMode.LaunchLeadIn:
                // 🌟 [수정] 0f 하드코딩 제거: anchorPos의 실제 월드 Y값 기준으로 오프셋 계산
                Vector3 leadInDir = (leadInForwardDir.sqrMagnitude > 0.01f) ? leadInForwardDir.normalized : forwardDir;
                float baseAnchorY = Mathf.Max(leadInAnchorPos.y, waterLevel);

                targetOffset = leadInAnchorPos - (leadInDir * flightDistBack) + (Vector3.up * flightHeight);
                targetLookOffset = leadInAnchorPos + (leadInDir * flightLookForward) + (Vector3.up * flightLookHeight);
                break;

            case CameraMode.DynamicFlight:
            default:
                Vector3 targetHeading = forwardDir;

                if (targetStone != null)
                {
                    Rigidbody stoneRb = targetStone.GetComponent<Rigidbody>();
                    if (stoneRb != null && !stoneRb.isKinematic)
                    {
                        Vector3 velXZ = new Vector3(stoneRb.linearVelocity.x, 0f, stoneRb.linearVelocity.z);
                        if (velXZ.sqrMagnitude > 0.4f)
                        {
                            targetHeading = velXZ.normalized;
                        }
                        else
                        {
                            targetHeading = targetStone.forward;
                            targetHeading.y = 0f;
                            if (targetHeading.sqrMagnitude > 0.01f) targetHeading.Normalize();
                            else targetHeading = forwardDir;
                        }
                    }
                    else
                    {
                        targetHeading = targetStone.forward;
                        targetHeading.y = 0f;
                        if (targetHeading.sqrMagnitude > 0.01f) targetHeading.Normalize();
                        else targetHeading = forwardDir;
                    }
                }

                if (smoothedFlightHeading.sqrMagnitude < 0.01f) smoothedFlightHeading = targetHeading;

                // 🌟 돌이 먼저 코너로 꺾인 후 카메라가 시간차를 두고 부드럽게 후방으로 따라오도록 Slerp 보간
                float catchupRate = Mathf.Max(headingCatchupSpeed, 4.5f);
                smoothedFlightHeading = Vector3.Slerp(smoothedFlightHeading, targetHeading, Time.deltaTime * catchupRate);
                Vector3 moveDir = smoothedFlightHeading.normalized;

                // 🌟 과거의 다이내믹 Y축 바운스 추적 공식 보존 (수면 상대 높이 100% 연동)
                float relativeStoneY = Mathf.Max(0f, stonePos.y - waterLevel);
                float dynamicCamY = waterLevel + (relativeStoneY * 0.75f) + flightHeight;
                float dynamicLookY = waterLevel + (relativeStoneY * 0.35f) + flightLookHeight;

                Vector3 stoneXZ = new Vector3(stonePos.x, 0f, stonePos.z);
                targetOffset = stoneXZ - (moveDir * flightDistBack) + (Vector3.up * dynamicCamY);
                targetLookOffset = stoneXZ + (moveDir * flightLookForward) + (Vector3.up * dynamicLookY);
                break;
        }

        if (currentMode == CameraMode.TopDownReplay)
        {
            if (mainCam != null)
            {
                mainCam.orthographic = true;
                mainCam.orthographicSize = replayOrthoSize;
                float repY = Mathf.Max(replayCenterPos.y, waterLevel);
                mainCam.transform.position = new Vector3(replayCenterPos.x, repY + 80f, replayCenterPos.z);
                mainCam.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            }
            return;
        }

        // 메인 3D 카메라 위치 및 회전 갱신
        if (mainCam != null)
        {
            if (mainCam.orthographic) mainCam.orthographic = false;

            if (currentMode == CameraMode.TopDownPosition)
            {
                mainCam.transform.position = targetOffset;
                mainCam.transform.rotation = Quaternion.LookRotation((targetLookOffset - mainCam.transform.position).normalized);
            }
            else if (targetStone != null && (targetStone.GetComponent<SkippingStone>()?.isGodMode ?? false))
            {
                mainCam.transform.position = targetOffset;
                mainCam.transform.rotation = Quaternion.LookRotation((targetLookOffset - mainCam.transform.position).normalized);
            }
            else
            {
                // 🌟 비행 중 카메라가 고속 돌(18m/s)을 쫓아갈 때 Lerp 지연으로 인한 고스팅/떨림(Camera Jitter) 원천 차단
                mainCam.transform.position = targetOffset;
                Quaternion desiredRot = Quaternion.LookRotation((targetLookOffset - mainCam.transform.position).normalized);
                mainCam.transform.rotation = desiredRot;
            }
        }

        // 가이드 PIP 뷰 갱신
        if (guideCam != null)
        {
            if (guideCam.gameObject.activeSelf != enableGuideCamera)
            {
                guideCam.gameObject.SetActive(enableGuideCamera);
            }

            if (enableGuideCamera)
            {
                guideCam.transform.position = new Vector3(stonePos.x + guideOffset.x, stonePos.y + guideOffset.y, stonePos.z);
                guideCam.transform.rotation = Quaternion.Euler(0f, -90f, 0f);
                guideCam.orthographicSize = guideOrthoSize;
                guideCam.rect = new Rect(pipX, pipY, pipWidth, pipHeight);
            }
        }
    }

    public void SetCameraMode(CameraMode mode)
    {
        currentMode = mode;
        if (mainCam != null && mode != CameraMode.TopDownReplay && mainCam.orthographic)
        {
            mainCam.orthographic = false;
        }
        if (mode != CameraMode.TopDownReplay)
        {
            RenderSettings.fog = true;
        }
    }
}