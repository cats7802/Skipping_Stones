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

    private float waterLevel = 0f;
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
            mainCam.transform.position = new Vector3(centerPos.x, 8.0f, centerPos.z);
            mainCam.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        }
        RenderSettings.fog = false; // 🌟 탑다운 리플레이 중 안개 표백(하얗게 덮임) 완전 차단
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

    [Header("3단계: 비행 추적 카메라 (돌과 리듬 링 세로 9:16 위에서 3번째 55~60% 구간 배치)")]
    public float flightDistBack = 4.8f;
    public float flightHeight = 1.85f;
    public float flightLookForward = 8.5f;
    public float flightLookHeight = -0.45f;
    [Tooltip("조작 직후 돌이 먼저 꺾인 뒤 다음 바운스까지 카메라가 정후방으로 돌아오는 보간 속도")]
    public float headingCatchupSpeed = 4.2f;
    private Vector3 smoothedFlightHeading = Vector3.forward;

    [Header("2번 가이드 카메라 (PIP 정측면 뷰)")]
    [Tooltip("세컨드 가이드 카메라(PIP 뷰) 활성화 여부 (현재 OFF)")]
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
        flightDistBack = 4.8f;
        flightHeight = 1.85f;
        flightLookForward = 8.5f;
        flightLookHeight = -0.45f;
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
        waterLevel = (waterSurfaceCache != null) ? waterSurfaceCache.transform.position.y : 0f;
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

        Vector3 forwardDir = (targetCharacter != null) ? targetCharacter.forward : Vector3.right;
        Vector3 backDir = -forwardDir;

        Vector3 targetOffset = Vector3.zero;
        Vector3 targetLookOffset = Vector3.zero;

        switch (currentMode)
        {
            case CameraMode.TopDownPosition:
                // 0단계: 캐릭터 뒤편 상단에서 강변과 수면을 함께 조망
                targetOffset = charPos + (backDir * topDownDistBack) + (Vector3.up * topDownHeight);
                targetLookOffset = charPos + (forwardDir * topDownLookForward) + (Vector3.up * topDownLookHeight);
                break;

            case CameraMode.ShoulderAim:
                // 1~2단계: 캐릭터의 어깨 너머로 조준선과 물 건너편을 정밀 조준
                targetOffset = charPos + (backDir * shoulderDistBack) + (Vector3.up * shoulderHeight);
                targetLookOffset = charPos + (forwardDir * shoulderLookForward) + (Vector3.up * shoulderLookHeight);
                break;

            case CameraMode.LaunchLeadIn:
                // 2.5단계: 45~55프레임 발사 예정 앵커 위치를 기준으로 정면 축을 향해 완만하게 선행 가속
                Vector3 leadInDir = (leadInForwardDir.sqrMagnitude > 0.01f) ? leadInForwardDir.normalized : forwardDir;
                Vector3 anchorXZ = new Vector3(leadInAnchorPos.x, 0f, leadInAnchorPos.z);
                float anchorCamY = (Mathf.Max(0f, leadInAnchorPos.y) * 0.80f) + flightHeight;
                float anchorLookY = (Mathf.Max(0f, leadInAnchorPos.y) * 0.40f) + flightLookHeight;

                targetOffset = anchorXZ - (leadInDir * flightDistBack) + (Vector3.up * anchorCamY);
                targetLookOffset = anchorXZ + (leadInDir * flightLookForward) + (Vector3.up * anchorLookY);
                break;

            case CameraMode.DynamicFlight:
            default:
                // 3단계: 비행하는 돌의 진행 방향 뒤쪽에서 부드럽게 추적
                Vector3 targetHeading = forwardDir;

                if (targetStone != null)
                {
                    Rigidbody stoneRb = targetStone.GetComponent<Rigidbody>();
                    if (stoneRb != null && !stoneRb.isKinematic)
                    {
                        Vector3 velXZ = new Vector3(stoneRb.linearVelocity.x, 0f, stoneRb.linearVelocity.z);
                        if (velXZ.sqrMagnitude > 0.4f)
                        {
                            Vector3 rawHeading = velXZ.normalized;
                            // 🌟 충돌 시 옆/뒤로 꺾이지 않도록 forwardDir(+Z) 기준 ±22도 이내로 강력 클램핑
                            float dot = Vector3.Dot(rawHeading, forwardDir);
                            if (dot > 0.30f)
                            {
                                targetHeading = Vector3.RotateTowards(forwardDir, rawHeading, Mathf.Deg2Rad * 22f, 0f);
                            }
                            else
                            {
                                targetHeading = forwardDir;
                            }
                        }
                    }
                    else
                    {
                        // 🌟 Kinematic / 갓모드 비행 시 강줄기 +Z 정면 추적 보장
                        targetHeading = forwardDir;
                    }
                }

                if (smoothedFlightHeading.sqrMagnitude < 0.01f) smoothedFlightHeading = targetHeading;

                // 🌟 충돌 후에도 0.3~0.4초 내에 돌의 정후방으로 신속하고 자연스럽게 복귀 회전!
                float dynamicCatchupSpeed = Mathf.Max(headingCatchupSpeed, 14f);
                smoothedFlightHeading = Vector3.Slerp(smoothedFlightHeading, targetHeading, Time.deltaTime * dynamicCatchupSpeed);
                Vector3 moveDir = smoothedFlightHeading.normalized;

                // 🌟 다이내믹 Y축 바운스 추적
                float stoneY = Mathf.Max(0f, stonePos.y);
                float dynamicCamY = (stoneY * 0.75f) + flightHeight;
                float dynamicLookY = (stoneY * 0.35f) + flightLookHeight;

                Vector3 stoneXZ = new Vector3(stonePos.x, 0f, stonePos.z);
                targetOffset = stoneXZ - (moveDir * flightDistBack) + (Vector3.up * dynamicCamY);
                targetLookOffset = stoneXZ + (forwardDir * flightLookForward) + (Vector3.up * dynamicLookY);
                break;
        }

        if (currentMode == CameraMode.TopDownReplay)
        {
            if (mainCam != null)
            {
                mainCam.orthographic = true;
                mainCam.orthographicSize = replayOrthoSize;
                mainCam.transform.position = new Vector3(replayCenterPos.x, 80f, replayCenterPos.z);
                mainCam.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            }
            return;
        }

        // 1. 메인 3D 카메라 추적
        if (mainCam != null)
        {
            if (mainCam.orthographic) mainCam.orthographic = false;

            if (currentMode == CameraMode.TopDownPosition)
            {
                // 0단계 위치 선정: 캐릭터 Z/X 이동에 즉각 밀착하여 1:1로 함께 이동
                mainCam.transform.position = targetOffset;
                mainCam.transform.rotation = Quaternion.LookRotation((targetLookOffset - mainCam.transform.position).normalized);
            }
            else if (targetStone != null && (targetStone.GetComponent<SkippingStone>()?.isGodMode ?? false))
            {
                // 🌟 갓모드(104m/s) 초고속 비행 시 카메라 1:1 즉각 밀착 추적 (지연 0%)
                mainCam.transform.position = targetOffset;
                mainCam.transform.rotation = Quaternion.LookRotation((targetLookOffset - mainCam.transform.position).normalized);
            }
            else
            {
                mainCam.transform.position = Vector3.Lerp(mainCam.transform.position, targetOffset, Time.deltaTime * followSmoothSpeed);
                Quaternion desiredRot = Quaternion.LookRotation((targetLookOffset - mainCam.transform.position).normalized);
                mainCam.transform.rotation = Quaternion.Slerp(mainCam.transform.rotation, desiredRot, Time.deltaTime * followSmoothSpeed);
            }
        }

        // 2. 가이드 2D 정측면 카메라 추적 (enableGuideCamera 활성화 시에만 동작)
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
            RenderSettings.fog = true; // 🌟 일반 인게임 모드 복귀 시 안개 정상 복원
        }
    }

    public void SetupCameras()
    {
        if (mainCam == null)
        {
            mainCam = Camera.main;
            if (mainCam == null)
            {
                GameObject camObj = new GameObject("Main3DCamera");
                mainCam = camObj.AddComponent<Camera>();
                mainCam.tag = "MainCamera";
            }
        }
        mainCam.orthographic = false;
        mainCam.fieldOfView = 60f;
        mainCam.depth = 0;
        mainCam.rect = new Rect(0, 0, 1, 1);

        if (guideCam == null)
        {
            Transform guideTrans = transform.Find("GuidePIP_Camera");
            if (guideTrans != null)
            {
                guideCam = guideTrans.GetComponent<Camera>();
            }
            else
            {
                GameObject guideObj = new GameObject("GuidePIP_Camera");
                guideObj.transform.SetParent(transform);
                guideCam = guideObj.AddComponent<Camera>();
            }
        }

        if (guideCam != null)
        {
            guideCam.orthographic = true;
            guideCam.orthographicSize = guideOrthoSize;
            guideCam.depth = 10;
            guideCam.rect = new Rect(pipX, pipY, pipWidth, pipHeight);
            guideCam.clearFlags = CameraClearFlags.SolidColor;
            guideCam.backgroundColor = new Color(0.08f, 0.12f, 0.18f, 1f);
            guideCam.gameObject.SetActive(enableGuideCamera);
        }

        SetCameraMode(CameraMode.TopDownPosition);
    }
}
