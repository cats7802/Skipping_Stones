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
        leadInForwardDir = (forwardDir.sqrMagnitude > 0.01f) ? forwardDir.normalized : Vector3.forward;
        smoothedFlightHeading = leadInForwardDir; // 🌟 55프레임 발사 방향과 100% 일치시켜 좌우 요동침 원천 차단
        currentMode = CameraMode.LaunchLeadIn;
    }

    [Header("현재 카메라 모드")]
    public CameraMode currentMode = CameraMode.TopDownPosition;

    [Header("1번 메인 카메라 (3D)")]
    public Camera mainCam;
    public float followSmoothSpeed = 8f;
    [Tooltip("기본/조준 시 FOV")]
    public float defaultFOV = 60f;
    [Tooltip("비행 중 광각 FOV (배경 전경 확장 및 원근감 극대화)")]
    public float flightFOV = 80f;
    [Tooltip("FOV 전환 보간 속도")]
    public float fovTransitionSpeed = 6.0f;

    [Header("모드별 카메라 오프셋 (세로 9:16 최적화)")]
    public float topDownDistBack = 9.0f;
    public float topDownHeight = 5.6f;
    public float topDownLookForward = 10.0f;
    public float topDownLookHeight = 1.5f;

    public float shoulderDistBack = 3.4f;
    public float shoulderHeight = 2.2f;
    public float shoulderLookForward = 14.0f;
    public float shoulderLookHeight = 1.3f;

    [Header("3단계: 비행 추적 카메라 (상향 쿼터뷰 / 착수 및 전방 시야 최적화)")]
    [Tooltip("체크 시 돌의 포물선/바운스에 따라 카메라가 위아래로 같이 움직입니다.")]
    public bool followBounceY = true;
    [Tooltip("카메라 리그 전체 높이 오프셋")]
    public float flightPivotOffsetY = -1.5f;
    public float flightDistBack = 1.5f;
    public float flightHeight = 2.5f;
    public float flightLookForward = 10.0f;
    [Tooltip("카메라 자체의 상하 피치 각도 조절용 시선 높이 (음수일수록 아래를 내려다봄)")]
    public float flightLookHeight = -5.5f;
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

    [System.Serializable]
    public struct CameraProfile
    {
        public float defaultFOV;
        public float flightFOV;
        public float topDownDistBack;
        public float topDownHeight;
        public float topDownLookForward;
        public float topDownLookHeight;
        public float shoulderDistBack;
        public float shoulderHeight;
        public float shoulderLookForward;
        public float shoulderLookHeight;
        public bool followBounceY;
        public float flightPivotOffsetY;
        public float flightDistBack;
        public float flightHeight;
        public float flightLookForward;
        public float flightLookHeight;
        public float headingCatchupSpeed;

        public static CameraProfile DefaultLongDistance()
        {
            return new CameraProfile
            {
                defaultFOV = 60f,
                flightFOV = 80f,
                topDownDistBack = 9.0f,
                topDownHeight = 5.6f,
                topDownLookForward = 10.0f,
                topDownLookHeight = 1.5f,
                shoulderDistBack = 3.4f,
                shoulderHeight = 2.2f,
                shoulderLookForward = 14.0f,
                shoulderLookHeight = 1.3f,
                followBounceY = true,
                flightPivotOffsetY = -1.5f,
                flightDistBack = 1.5f,
                flightHeight = 2.5f,
                flightLookForward = 10.0f,
                flightLookHeight = -5.5f,
                headingCatchupSpeed = 4.2f
            };
        }

        public static CameraProfile DefaultRhythmArcade()
        {
            return new CameraProfile
            {
                defaultFOV = 60f,
                flightFOV = 80f,
                topDownDistBack = 9.0f,
                topDownHeight = 5.6f,
                topDownLookForward = 10.0f,
                topDownLookHeight = 1.5f,
                shoulderDistBack = 3.4f,
                shoulderHeight = 2.2f,
                shoulderLookForward = 14.0f,
                shoulderLookHeight = 1.3f,
                followBounceY = true,
                flightPivotOffsetY = -1.5f,
                flightDistBack = 1.5f,
                flightHeight = 2.5f,
                flightLookForward = 10.0f,
                flightLookHeight = -5.5f,
                headingCatchupSpeed = 4.2f
            };
        }
    }

    [Header("모드별 독립 카메라 프로필")]
    public CameraProfile longDistanceProfile = CameraProfile.DefaultLongDistance();

    public void ApplyProfileForMode(GameController.GameMode mode)
    {
        // 🎵 리듬 아케이드 모드: 디렉터님이 인스펙터에서 자유롭게 튜닝/실험 중이므로 강제 덮어쓰기 금지!
        if (mode == GameController.GameMode.RhythmArcade)
        {
            return;
        }

        // 🌊 롱디스턴스 물리 모드: 디렉터 확정 골든 수치(-1.5, 1.5, 2.5, 10, -5.5, 4.2) 완벽 고정
        CameraProfile p = longDistanceProfile;

        defaultFOV = p.defaultFOV;
        flightFOV = p.flightFOV;
        topDownDistBack = p.topDownDistBack;
        topDownHeight = p.topDownHeight;
        topDownLookForward = p.topDownLookForward;
        topDownLookHeight = p.topDownLookHeight;
        shoulderDistBack = p.shoulderDistBack;
        shoulderHeight = p.shoulderHeight;
        shoulderLookForward = p.shoulderLookForward;
        shoulderLookHeight = p.shoulderLookHeight;
        followBounceY = p.followBounceY;
        flightPivotOffsetY = p.flightPivotOffsetY;
        flightDistBack = p.flightDistBack;
        flightHeight = p.flightHeight;
        flightLookForward = p.flightLookForward;
        flightLookHeight = p.flightLookHeight;
        headingCatchupSpeed = p.headingCatchupSpeed;
    }

    private void Awake()
    {
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
                // 🌟 55프레임 발사 예정 고정 앵커 위치(leadInAnchorPos) 및 발사각(leadInForwardDir) 기준
                Vector3 leadInDir = (leadInForwardDir.sqrMagnitude > 0.01f) ? leadInForwardDir.normalized : forwardDir;
                float relativeLeadInY = Mathf.Max(0f, leadInAnchorPos.y - waterLevel);
                float leadInCamY = waterLevel + (relativeLeadInY * 0.75f) + flightHeight + flightPivotOffsetY;
                float leadInLookY = waterLevel + (relativeLeadInY * 0.35f) + flightLookHeight + flightPivotOffsetY;

                Vector3 leadInAnchorXZ = new Vector3(leadInAnchorPos.x, 0f, leadInAnchorPos.z);
                targetOffset = leadInAnchorXZ - (leadInDir * flightDistBack) + (Vector3.up * leadInCamY);
                targetLookOffset = leadInAnchorXZ + (leadInDir * flightLookForward) + (Vector3.up * leadInLookY);
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

                // 🌟 Y축 고정/연동 제어 & 가상 피벗 높이(flightPivotOffsetY) 평행 오프셋
                float relativeStoneY = followBounceY ? Mathf.Max(0f, stonePos.y - waterLevel) : 0f;
                float dynamicCamY = waterLevel + (relativeStoneY * 0.75f) + flightHeight + flightPivotOffsetY;
                float dynamicLookY = waterLevel + (relativeStoneY * 0.35f) + flightLookHeight + flightPivotOffsetY;

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

            // 🌟 FOV 다이내믹 전환: 비행 중에는 광각(flightFOV), 그 외에는 defaultFOV
            float targetFOV = (currentMode == CameraMode.DynamicFlight || currentMode == CameraMode.LaunchLeadIn) ? flightFOV : defaultFOV;
            if (Mathf.Abs(mainCam.fieldOfView - targetFOV) > 0.05f)
            {
                mainCam.fieldOfView = Mathf.Lerp(mainCam.fieldOfView, targetFOV, Time.deltaTime * fovTransitionSpeed);
            }

            if (currentMode == CameraMode.TopDownPosition)
            {
                mainCam.transform.position = targetOffset;
                mainCam.transform.rotation = Quaternion.LookRotation((targetLookOffset - mainCam.transform.position).normalized);
            }
            else if (currentMode == CameraMode.LaunchLeadIn)
            {
                // 🌟 45~55프레임 리드인: 숄더뷰에서 55프레임 발사 대기 위치로 부드럽게 쑥 빨려 들어가며 대기
                mainCam.transform.position = Vector3.Lerp(mainCam.transform.position, targetOffset, Time.deltaTime * 18f);
                Quaternion desiredRot = Quaternion.LookRotation((targetLookOffset - mainCam.transform.position).normalized);
                mainCam.transform.rotation = Quaternion.Slerp(mainCam.transform.rotation, desiredRot, Time.deltaTime * 18f);
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