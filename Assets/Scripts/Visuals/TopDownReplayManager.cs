using System.Collections;
using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class TopDownReplayManager : MonoBehaviour
{
    public static TopDownReplayManager Instance { get; private set; }

    [Header("참조")]
    public GameController gameController;
    public SkippingStone stone;

    [Header("리플레이 상태")]
    public bool isReplayActive = false;
    public bool isDrawing = false;
    public bool isReplayFinished = false;
    public const float PAGE_DISTANCE = 1500f;
    public int totalPages = 1;
    public int currentPage = 1;

    [Header("궤적 라인 및 비주얼 색상")]
    public float lineWidth = 0.55f;
    public Color pathColor = new Color(0.1f, 0.95f, 1.0f, 0.95f);
    public Color skimLineColor = new Color(1.0f, 0.85f, 0.20f, 0.98f);
    public Color startMarkerColor = new Color(0.2f, 1f, 0.4f, 1f);
    public Color bounceMarkerColor = new Color(0.1f, 0.85f, 1f, 1f);
    public Color perfectMarkerColor = new Color(0.2f, 1f, 0.5f, 1f);
    public Color boostMarkerColor = new Color(0.95f, 0.20f, 1.0f, 1f);
    public Color skimStartMarkerColor = new Color(1.0f, 0.62f, 0.12f, 1f);
    public Color finishMarkerColor = new Color(1.0f, 0.22f, 0.22f, 1f);

    private LineRenderer trajectoryLine;
    private LineRenderer skimLine;
    private GameObject replayStoneAvatar; // 🌟 리플레이 선두에서 실제 돌 맵핑으로 2.5배 퐁퐁퐁 피칭 도약하는 3D 조약돌
    private List<GameObject> markerObjects = new List<GameObject>();
    private List<SkippingStone.BounceRecord> currentHistory = new List<SkippingStone.BounceRecord>();
    private Coroutine drawCoroutine;
    private Coroutine slideCoroutine;
    private float cachedFinalDist = 0f;

    // 🌟 자유 줌/스크롤 네비게이션 상태 및 궤적 경계(Bounding Box)
    private Vector3 currentCamCenter = new Vector3(0f, 8.0f, 0f);
    private float currentOrthoSize = 40f;
    private float targetOrthoSize = 40f;
    private float minOrthoSize = 18f;
    private float maxOrthoSize = 400f;

    private float boundMinX = -120f;
    private float boundMaxX = 120f;
    private float boundMinZ = -15f;
    private float boundMaxZ = 3550f;

    private Vector2 lastMousePos;
    private bool isMouseDragging = false;
    private int lastLoadedTerrainPage = 1;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else if (Instance != this) { Destroy(gameObject); return; }

        if (gameController == null) gameController = FindAnyObjectByType<GameController>();
        if (stone == null) stone = FindAnyObjectByType<SkippingStone>();

        CreateTrajectoryLineRenderers();
        CreateReplayStoneAvatar();
    }

    private void Update()
    {
        if (!isReplayActive) return;

        // 리플레이 궤적 드로잉이 완료된 후 PC 휠/드래그 및 모바일 핀치줌/스크롤 활성화
        if (isReplayFinished && !isDrawing)
        {
            HandleFreeNavigation();
        }
    }

    private void CreateTrajectoryLineRenderers()
    {
        Shader unlitShader = Shader.Find("Universal Render Pipeline/Unlit") 
                             ?? Shader.Find("Sprites/Default") 
                             ?? Shader.Find("Unlit/Color");

        // 1. 일반 공중 비행 궤적선 (시안색)
        if (trajectoryLine == null)
        {
            GameObject lineObj = new GameObject("TopDownReplay_TrajectoryLine");
            lineObj.transform.SetParent(transform);
            trajectoryLine = lineObj.AddComponent<LineRenderer>();

            Material lineMat = (unlitShader != null) ? new Material(unlitShader) : new Material(Shader.Find("Standard"));
            lineMat.color = pathColor;

            trajectoryLine.material = lineMat;
            trajectoryLine.useWorldSpace = true;
            trajectoryLine.positionCount = 0;
            trajectoryLine.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            trajectoryLine.receiveShadows = false;
            trajectoryLine.enabled = false;
        }

        // 2. 도로록 스키밍 활주 궤적선 (황금빛 골드)
        if (skimLine == null)
        {
            GameObject skimLineObj = new GameObject("TopDownReplay_SkimLine");
            skimLineObj.transform.SetParent(transform);
            skimLine = skimLineObj.AddComponent<LineRenderer>();

            Material skimMat = (unlitShader != null) ? new Material(unlitShader) : new Material(Shader.Find("Standard"));
            skimMat.color = skimLineColor;

            skimLine.material = skimMat;
            skimLine.useWorldSpace = true;
            skimLine.positionCount = 0;
            skimLine.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            skimLine.receiveShadows = false;
            skimLine.enabled = false;
        }
    }

    /// <summary>
    /// 🌟 실제 인게임 조약돌 3D 메쉬 및 머티리얼(Stone_Pebble_Mat) 1:1 완벽 연동
    /// </summary>
    private void CreateReplayStoneAvatar()
    {
        if (replayStoneAvatar == null)
        {
            GameObject stonePrefab = Resources.Load<GameObject>("Stone");
#if UNITY_EDITOR
            if (stonePrefab == null)
            {
                stonePrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/3D/prefab/Stone.prefab");
            }
#endif
            if (stonePrefab != null)
            {
                replayStoneAvatar = Instantiate(stonePrefab, transform);
                replayStoneAvatar.name = "TopDownReplay_StoneAvatar";
            }
            else
            {
                replayStoneAvatar = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                replayStoneAvatar.name = "TopDownReplay_StoneAvatar";
                replayStoneAvatar.transform.SetParent(transform);
            }

            // 물리 콜라이더 제거
            foreach (var col in replayStoneAvatar.GetComponentsInChildren<Collider>(true))
            {
                if (Application.isPlaying) Destroy(col);
                else DestroyImmediate(col);
            }

            // 🌟 실제 인게임 조약돌 머티리얼(Stone_Pebble_Mat) 연결
            Material pebbleMat = (stone != null && stone.stoneCustomMaterial != null) 
                                 ? stone.stoneCustomMaterial 
                                 : Resources.Load<Material>("Stone_Pebble_Mat");
            if (pebbleMat != null)
            {
                foreach (var rend in replayStoneAvatar.GetComponentsInChildren<Renderer>(true))
                {
                    rend.material = pebbleMat;
                    rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                }
            }

            replayStoneAvatar.SetActive(false);
        }
    }

    /// <summary>
    /// 🌟 카메라 줌 레벨에 따라 화면상에서 일정한 픽셀 굵기를 유지하도록 실시간 역비례 스케일링
    /// </summary>
    private void UpdateVisualsScale(float orthoSize)
    {
        float dynamicW = Mathf.Clamp(orthoSize * 0.0128f, 0.32f, 6.0f); // 🌟 0.8배 슬림화
        float ringW = Mathf.Clamp(orthoSize * 0.0056f, 0.12f, 2.6f);

        if (trajectoryLine != null)
        {
            trajectoryLine.startWidth = dynamicW;
            trajectoryLine.endWidth = dynamicW;
        }
        if (skimLine != null)
        {
            skimLine.startWidth = dynamicW * 1.25f;
            skimLine.endWidth = dynamicW * 1.25f;
        }

        // 마커 지름 및 테두리 선 굵기 동시 실시간 동기화 (줌인 시 뚱뚱해짐 방지)
        float markerScale = Mathf.Clamp(orthoSize / 390f, 0.15f, 2.5f);
        foreach (var m in markerObjects)
        {
            if (m != null)
            {
                m.transform.localScale = new Vector3(markerScale, 1f, markerScale);
                LineRenderer lr = m.GetComponent<LineRenderer>();
                if (lr != null)
                {
                    lr.startWidth = ringW;
                    lr.endWidth = ringW;
                }
            }
        }

        // 🌟 돌 아바타 크기도 라인 폭 및 줌 레벨에 1:1 일치하여 거리가 멀어져도 동일한 화면 크기 유지
        if (replayStoneAvatar != null && replayStoneAvatar.activeSelf)
        {
            float avatarScale = Mathf.Clamp(orthoSize * 0.28f, 3.5f, 65f);
            replayStoneAvatar.transform.localScale = new Vector3(avatarScale, avatarScale * 0.35f, avatarScale);
        }
    }

    /// <summary>
    /// 🌟 리플레이 시작
    /// </summary>
    public void StartReplay(float finalDist)
    {
        cachedFinalDist = finalDist;
        if (stone == null) stone = FindAnyObjectByType<SkippingStone>();
        if (gameController == null) gameController = FindAnyObjectByType<GameController>();

        // 🌟 1. 리플레이 중 3D 비행 트레일 끄기 및 실제 3D 물리 돌 완전 동결 (백그라운드 충돌/비행 원천 차단)
        if (stone != null)
        {
            if (stone.trail != null)
            {
                stone.trail.enabled = false;
                stone.trail.Clear();
            }

            Rigidbody sRb = stone.GetComponent<Rigidbody>();
            if (sRb != null)
            {
                sRb.linearVelocity = Vector3.zero;
                sRb.angularVelocity = Vector3.zero;
                sRb.useGravity = false;
                sRb.isKinematic = true;
            }
        }

        currentHistory.Clear();
        if (stone != null && stone.bounceHistory != null && stone.bounceHistory.Count > 0)
        {
            currentHistory.AddRange(stone.bounceHistory);
        }
        else
        {
            Vector3 startP = (stone != null) ? stone.transform.position : Vector3.zero;
            currentHistory.Add(new SkippingStone.BounceRecord { position = startP, skipIndex = 0, grade = "START", distance = 0f });
            currentHistory.Add(new SkippingStone.BounceRecord { position = startP + Vector3.forward * finalDist, skipIndex = 1, grade = "FINISH", distance = finalDist });
        }

        CalculateSmartBounds();

        totalPages = Mathf.Max(1, Mathf.CeilToInt(finalDist / PAGE_DISTANCE));
        currentPage = 1;
        isReplayActive = true;
        isReplayFinished = false;

        // 🌟 2. 리플레이 진입 시 자동 비행 즉시 중단 및 테스트 UI 자동 닫힘
        if (EnvironmentTestHelper.Instance != null)
        {
            EnvironmentTestHelper.Instance.StopAutoFly();
            EnvironmentTestHelper.Instance.showTestUI = false;
        }

        // 🌟 3. 시작 나무 발판 및 투구 캐릭터 가시성 복원 (리플레이에서 선명하게 노출)
        StoneThrowerCharacter thrower = FindAnyObjectByType<StoneThrowerCharacter>();
        if (thrower != null)
        {
            thrower.RestoreVisibility();
        }

        GameObject pier = GameObject.Find("Lakeside_WoodenPier");
        if (pier != null)
        {
            pier.SetActive(true);
            var pr = pier.GetComponent<Renderer>();
            if (pr != null) pr.enabled = true;
        }

        // 🌟 4. 1구간 (0m ~ 1,500m) 직교 뷰로 세팅
        SetPageCameraView(1, false);

        // 🌟 5. 궤적 드로잉 애니메이션 시작
        StartDrawingAnimation();
    }

    /// <summary>
    /// 🌟 산맥 이탈 방지 스마트 바운딩 박스 및 줌 한계 계산
    /// </summary>
    private void CalculateSmartBounds()
    {
        float minX = float.MaxValue, maxX = float.MinValue;
        float maxZ = cachedFinalDist;

        foreach (var r in currentHistory)
        {
            if (r.position.x < minX) minX = r.position.x;
            if (r.position.x > maxX) maxX = r.position.x;
            if (r.position.z > maxZ) maxZ = r.position.z;
        }

        if (minX > maxX) { minX = -10f; maxX = 10f; }

        // 🌟 산맥 밖으로 나가지 않도록 호수 수면 영역(-120m ~ +120m) 내로 한정
        boundMinX = Mathf.Max(minX - 35f, -120f);
        boundMaxX = Mathf.Min(maxX + 35f, 120f);
        boundMinZ = -15f;
        boundMaxZ = maxZ + 25f;

        minOrthoSize = 18f; // 최대 줌인 (물보라 초근접)

        float spanX = Mathf.Max(35f, boundMaxX - boundMinX);
        float spanZ = Mathf.Max(50f, boundMaxZ - boundMinZ);
        maxOrthoSize = Mathf.Clamp(Mathf.Max(spanX * 0.95f, spanZ * 0.52f), 35f, 400f);
    }

    public void NextPage()
    {
        if (currentPage < totalPages)
        {
            currentPage++;
            SetPageCameraView(currentPage, true);
        }
    }

    public void PrevPage()
    {
        if (currentPage > 1)
        {
            currentPage--;
            SetPageCameraView(currentPage, true);
        }
    }

    public void GoToPage(int page)
    {
        currentPage = Mathf.Clamp(page, 1, totalPages);
        SetPageCameraView(currentPage, true);
    }

    public void SetPageCameraView(int page, bool smooth)
    {
        DualCameraSetup dualCam = (gameController != null && gameController.dualCamera != null) 
                                  ? gameController.dualCamera 
                                  : FindAnyObjectByType<DualCameraSetup>();
        if (dualCam == null) return;

        Vector3 targetCenter;
        float targetOrtho;

        if (cachedFinalDist <= PAGE_DISTANCE)
        {
            float spanZ = Mathf.Max(35f, boundMaxZ - boundMinZ);
            float spanX = Mathf.Max(25f, boundMaxX - boundMinX);
            float midX = (boundMinX + boundMaxX) * 0.5f;
            targetCenter = new Vector3(midX, 8.0f, (boundMinZ + boundMaxZ) * 0.5f);
            targetOrtho = Mathf.Max(spanZ * 0.55f, spanX * 0.98f, 20f);
        }
        else
        {
            float pageStartZ = (page - 1) * PAGE_DISTANCE;
            float pageEndZ = Mathf.Min(cachedFinalDist, page * PAGE_DISTANCE);
            float pageCenterZ = (pageStartZ + pageEndZ) * 0.5f;
            float pageSpanZ = Mathf.Max(PAGE_DISTANCE, pageEndZ - pageStartZ);

            float pageMinX = float.MaxValue, pageMaxX = float.MinValue;
            foreach (var r in currentHistory)
            {
                if (r.position.z >= pageStartZ - 60f && r.position.z <= pageEndZ + 60f)
                {
                    if (r.position.x < pageMinX) pageMinX = r.position.x;
                    if (r.position.x > pageMaxX) pageMaxX = r.position.x;
                }
            }
            float pageCenterX = (pageMinX <= pageMaxX) ? (pageMinX + pageMaxX) * 0.5f : 0f;
            float pageSpanX = (pageMinX <= pageMaxX) ? Mathf.Max(35f, pageMaxX - pageMinX) : 35f;

            targetCenter = new Vector3(pageCenterX, 8.0f, pageCenterZ);
            targetOrtho = Mathf.Max(pageSpanZ * 0.52f, pageSpanX * 0.98f, 25f);
        }

        currentCamCenter = targetCenter;
        targetOrthoSize = targetOrtho;
        currentOrthoSize = targetOrtho;

        UpdateVisualsScale(targetOrtho);

        // 지형/수면 청크 배치
        lastLoadedTerrainPage = page;
        if (LakeEnvironmentManager.Instance != null) LakeEnvironmentManager.Instance.PlaceTerrainAtPage(page);
        var water = FindAnyObjectByType<WaterSurface>();
        if (water != null) water.PlaceWaterAtPage(page);

        if (smooth && gameObject.activeInHierarchy)
        {
            if (slideCoroutine != null) StopCoroutine(slideCoroutine);
            slideCoroutine = StartCoroutine(SlideCameraRoutine(dualCam, targetCenter, targetOrtho, 0.45f));
        }
        else
        {
            dualCam.SetReplayTopDownView(targetCenter, targetOrtho);
        }
    }

    private IEnumerator SlideCameraRoutine(DualCameraSetup dualCam, Vector3 targetCenter, float targetOrtho, float duration)
    {
        if (dualCam.mainCam == null) yield break;

        Vector3 startPos = dualCam.mainCam.transform.position;
        float startOrtho = dualCam.mainCam.orthographicSize;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);

            Vector3 currentPos = Vector3.Lerp(startPos, new Vector3(targetCenter.x, 8.0f, targetCenter.z), t);
            float ortho = Mathf.Lerp(startOrtho, targetOrtho, t);

            currentCamCenter = currentPos;
            currentOrthoSize = ortho;
            UpdateVisualsScale(ortho);

            dualCam.SetReplayTopDownView(currentPos, ortho);
            yield return null;
        }

        currentCamCenter = targetCenter;
        currentOrthoSize = targetOrtho;
        UpdateVisualsScale(targetOrtho);
        dualCam.SetReplayTopDownView(targetCenter, targetOrtho);
    }

    /// <summary>
    /// 🌟 800m부터 완주 지점 끝까지 1:1 완전 연속 실시간 Z축 추적 카메라 계산 (X=0 정중앙 고정)
    /// </summary>
    private float CalculateCameraZForLeadPosition(float leadZ)
    {
        if (cachedFinalDist <= PAGE_DISTANCE)
        {
            return (boundMinZ + boundMaxZ) * 0.5f;
        }

        // 0m ~ 800m: 출발 지점 조망 (Z = 750m 고정)
        if (leadZ <= 800f)
        {
            return 750f;
        }

        // 🌟 800m ~ 완주 지점 끝까지: 조기 멈춤 없이 3,500m/3,800m까지 시원하게 전진!
        return Mathf.Clamp(leadZ - 50f, 750f, cachedFinalDist);
    }

    /// <summary>
    /// 🌟 리플레이 완료 후 PC 마우스 휠/드래그 & 모바일 핀치줌/스크롤 자유 네비게이션
    /// </summary>
    private void HandleFreeNavigation()
    {
        DualCameraSetup dualCam = (gameController != null && gameController.dualCamera != null) 
                                  ? gameController.dualCamera 
                                  : FindAnyObjectByType<DualCameraSetup>();
        if (dualCam == null || dualCam.mainCam == null) return;

        float screenH = Mathf.Max(Screen.height, 100f);
        float worldPerPixel = (currentOrthoSize * 2f) / screenH;

        // ─────────────────────────────────────────────────────────────
        // 1. 📱 모바일 터치 처리 (New Input System & Legacy 지원)
        // ─────────────────────────────────────────────────────────────
#if ENABLE_INPUT_SYSTEM
        if (Touchscreen.current != null && Touchscreen.current.touches.Count > 0)
        {
            int tCount = 0;
            Vector2 t0Pos = Vector2.zero, t0Delta = Vector2.zero;
            Vector2 t1Pos = Vector2.zero, t1Delta = Vector2.zero;

            for (int i = 0; i < Touchscreen.current.touches.Count; i++)
            {
                var touchControl = Touchscreen.current.touches[i];
                if (touchControl.isInProgress)
                {
                    if (tCount == 0)
                    {
                        t0Pos = touchControl.position.ReadValue();
                        t0Delta = touchControl.delta.ReadValue();
                        tCount++;
                    }
                    else if (tCount == 1)
                    {
                        t1Pos = touchControl.position.ReadValue();
                        t1Delta = touchControl.delta.ReadValue();
                        tCount++;
                        break;
                    }
                }
            }

            if (tCount == 1 && t0Pos.y > screenH * 0.18f)
            {
                currentCamCenter.x -= t0Delta.x * worldPerPixel;
                currentCamCenter.z -= t0Delta.y * worldPerPixel;
            }
            else if (tCount == 2)
            {
                Vector2 prevP0 = t0Pos - t0Delta;
                Vector2 prevP1 = t1Pos - t1Delta;
                float prevDist = (prevP0 - prevP1).magnitude;
                float currDist = (t0Pos - t1Pos).magnitude;
                float delta = currDist - prevDist;

                targetOrthoSize = Mathf.Clamp(targetOrthoSize - delta * (targetOrthoSize * 0.0035f), minOrthoSize, maxOrthoSize);
            }
        }
#endif
        try
        {
            if (Input.touchCount == 1)
            {
                Touch t = Input.GetTouch(0);
                if (t.position.y > screenH * 0.18f && t.phase == UnityEngine.TouchPhase.Moved)
                {
                    currentCamCenter.x -= t.deltaPosition.x * worldPerPixel;
                    currentCamCenter.z -= t.deltaPosition.y * worldPerPixel;
                }
            }
            else if (Input.touchCount == 2)
            {
                Touch t0 = Input.GetTouch(0);
                Touch t1 = Input.GetTouch(1);
                Vector2 prevP0 = t0.position - t0.deltaPosition;
                Vector2 prevP1 = t1.position - t1.deltaPosition;
                float prevDist = (prevP0 - prevP1).magnitude;
                float currDist = (t0.position - t1.position).magnitude;
                float delta = currDist - prevDist;

                targetOrthoSize = Mathf.Clamp(targetOrthoSize - delta * (targetOrthoSize * 0.0035f), minOrthoSize, maxOrthoSize);
            }
        }
        catch { }

        // ─────────────────────────────────────────────────────────────
        // 2. 💻 PC 마우스 처리 (New Input System & Legacy 지원)
        // ─────────────────────────────────────────────────────────────
        float scrollVal = 0f;
        Vector2 mousePos = Vector2.zero;
        bool isMouseDown = false;

#if ENABLE_INPUT_SYSTEM
        if (Mouse.current != null)
        {
            Vector2 s = Mouse.current.scroll.ReadValue();
            if (Mathf.Abs(s.y) > 0.01f)
            {
                scrollVal = Mathf.Sign(s.y);
            }

            mousePos = Mouse.current.position.ReadValue();
            isMouseDown = Mouse.current.leftButton.isPressed || Mouse.current.rightButton.isPressed || Mouse.current.middleButton.isPressed;
        }
#endif
        try
        {
            if (Mathf.Abs(scrollVal) < 0.01f)
            {
                float legScroll = Input.mouseScrollDelta.y;
                if (Mathf.Abs(legScroll) > 0.01f) scrollVal = Mathf.Sign(legScroll);
                else
                {
                    float axisScroll = Input.GetAxis("Mouse ScrollWheel");
                    if (Mathf.Abs(axisScroll) > 0.01f) scrollVal = Mathf.Sign(axisScroll);
                }
            }
            if (!isMouseDown)
            {
                isMouseDown = Input.GetMouseButton(0) || Input.GetMouseButton(1) || Input.GetMouseButton(2);
                mousePos = Input.mousePosition;
            }
        }
        catch { }

        // 휠 줌 적용 (1클릭당 12% 줌인/줌아웃)
        if (Mathf.Abs(scrollVal) > 0.01f)
        {
            targetOrthoSize = Mathf.Clamp(targetOrthoSize - scrollVal * (targetOrthoSize * 0.12f), minOrthoSize, maxOrthoSize);
        }

        // 드래그 팬 적용
        if (isMouseDown && mousePos.y > screenH * 0.18f)
        {
            if (!isMouseDragging)
            {
                isMouseDragging = true;
                lastMousePos = mousePos;
            }
            else
            {
                Vector2 delta = mousePos - lastMousePos;
                if (delta.sqrMagnitude > 0.001f)
                {
                    currentCamCenter.x -= delta.x * worldPerPixel;
                    currentCamCenter.z -= delta.y * worldPerPixel;
                    lastMousePos = mousePos;
                }
            }
        }
        else
        {
            isMouseDragging = false;
        }

        // ─────────────────────────────────────────────────────────────
        // 3. 산맥 이탈 방지 엄격 클램핑 및 카메라 동기화
        // ─────────────────────────────────────────────────────────────
        currentCamCenter.x = Mathf.Clamp(currentCamCenter.x, boundMinX, boundMaxX);
        currentCamCenter.z = Mathf.Clamp(currentCamCenter.z, boundMinZ, boundMaxZ);

        currentOrthoSize = Mathf.Lerp(currentOrthoSize, targetOrthoSize, Time.unscaledDeltaTime * 14f);
        UpdateVisualsScale(currentOrthoSize);

        dualCam.SetReplayTopDownView(new Vector3(currentCamCenter.x, 8.0f, currentCamCenter.z), currentOrthoSize);

        // ─────────────────────────────────────────────────────────────
        // 4. 스크롤 위치에 따른 실시간 지형 청크 동기화
        // ─────────────────────────────────────────────────────────────
        SyncTerrainByZ(currentCamCenter.z);
    }

    private void SyncTerrainByZ(float centerZ)
    {
        int camPage = Mathf.Clamp(Mathf.FloorToInt(centerZ / PAGE_DISTANCE) + 1, 1, totalPages);
        if (camPage != lastLoadedTerrainPage)
        {
            lastLoadedTerrainPage = camPage;
            if (LakeEnvironmentManager.Instance != null)
            {
                LakeEnvironmentManager.Instance.PlaceTerrainAtPage(camPage);
            }
            var water = FindAnyObjectByType<WaterSurface>();
            if (water != null)
            {
                water.PlaceWaterAtPage(camPage);
            }
        }

        // 🌟 실시간 카메라 Z 위치에 따른 4단계 동적 환경/수면 라이팅 연동
        if (LakeEnvironmentManager.Instance != null)
        {
            LakeEnvironmentManager.Instance.UpdateEnvironmentByDistance(centerZ);
        }
    }

    public void StartDrawingAnimation()
    {
        if (drawCoroutine != null) StopCoroutine(drawCoroutine);
        ClearVisualMarkers();

        drawCoroutine = StartCoroutine(DrawTrajectoryRoutine());
    }

    private IEnumerator DrawTrajectoryRoutine()
    {
        isDrawing = true;
        isReplayFinished = false;

        CreateTrajectoryLineRenderers();
        trajectoryLine.enabled = true;
        trajectoryLine.positionCount = 0;
        skimLine.enabled = false;
        skimLine.positionCount = 0;

        CreateReplayStoneAvatar();
        if (replayStoneAvatar != null) replayStoneAvatar.SetActive(true);

        DualCameraSetup dualCam = (gameController != null && gameController.dualCamera != null) 
                                  ? gameController.dualCamera 
                                  : FindAnyObjectByType<DualCameraSetup>();

        if (currentHistory.Count < 2)
        {
            isDrawing = false;
            isReplayFinished = true;
            if (replayStoneAvatar != null) replayStoneAvatar.SetActive(false);
            yield break;
        }

        // 1. 시작점 마커 생성 (나무 발판/지형 위 Y = 1.02m)
        SpawnBounceMarker(currentHistory[0], 0);

        List<Vector3> flightPoints = new List<Vector3>();
        flightPoints.Add(new Vector3(currentHistory[0].position.x, 1.05f, currentHistory[0].position.z));
        trajectoryLine.positionCount = 1;
        trajectoryLine.SetPosition(0, flightPoints[0]);

        float totalDrawDuration = Mathf.Clamp(currentHistory.Count * 0.28f, 1.8f, 3.2f);
        float timePerSegment = totalDrawDuration / (currentHistory.Count - 1);

        for (int i = 0; i < currentHistory.Count - 1; i++)
        {
            Vector3 startP = new Vector3(currentHistory[i].position.x, 1.05f, currentHistory[i].position.z);
            Vector3 endP = new Vector3(currentHistory[i + 1].position.x, 1.05f, currentHistory[i + 1].position.z);

            Vector3 segDirection = (endP - startP).normalized;
            if (segDirection.sqrMagnitude < 0.001f) segDirection = Vector3.forward;
            Quaternion baseYawRot = Quaternion.LookRotation(segDirection, Vector3.up);

            bool isSkimmingSegment = (currentHistory[i].grade == "SKIM_START");
            float segElapsed = 0f;

            if (!isSkimmingSegment)
            {
                int currentSegmentIdx = flightPoints.Count;
                flightPoints.Add(startP);
                trajectoryLine.positionCount = flightPoints.Count;

                while (segElapsed < timePerSegment)
                {
                    segElapsed += Time.deltaTime;
                    float rawT = Mathf.Clamp01(segElapsed / timePerSegment);

                    // 🌟 중력 포물선 물리 이징 (수평 전진):
                    // 수면을 탕! 치고 빠른 초기 속도로 튀어나가며(Ease-Out), 정점을 지나 수면으로 가속 낙하(Ease-In)
                    float forwardT = Mathf.SmoothStep(0f, 1f, rawT);
                    Vector3 currentLeadPos = Vector3.Lerp(startP, endP, forwardT);

                    // 🌟 카툰 3D 도약 포물선 높이 곡선: 4 * t * (1 - t)
                    float heightFactor = 4f * rawT * (1f - rawT);

                    // 공중 정점 시 선두 궤적 폭 2.5배 퐁퐁퐁 도약 스케일링 (완료 후 굵기 0.8배와 1:1 통일)
                    float baseWidth = Mathf.Clamp(currentOrthoSize * 0.0128f, 0.32f, 6.0f);
                    float dynamicJumpWidth = baseWidth * (1f + heightFactor * 1.5f);
                    trajectoryLine.startWidth = baseWidth;
                    trajectoryLine.endWidth = dynamicJumpWidth;

                    // 🌟 3단 피칭 틸트 액팅 (+38도 ~ -38도 시원하게 확대):
                    // 수직 속도 v_y = 1 - 2*rawT
                    // 상승(rawT=0~0.5): 앞머리를 +38도 번쩍 쳐들고 솟구침
                    // 최고점(rawT=0.5): 0도 수평 체공
                    // 하강(rawT=0.5~1.0): 앞머리를 -38도 숙이며 슬라이스 다이빙
                    float vyFactor = (1f - 2f * rawT);
                    float pitchAngle = vyFactor * 38f; // +38도 ~ -38도

                    if (replayStoneAvatar != null)
                    {
                        float avatarBaseScale = Mathf.Clamp(currentOrthoSize * 0.28f, 3.5f, 65f);
                        float avatarCurrentScale = avatarBaseScale * (1f + heightFactor * 1.5f);

                        // 🌟 돌 아바타(Y = 1.35m+)를 푸른 리본 궤적(Y = 1.05m)보다 위에 띄워 돌 가림 완벽 방지!
                        replayStoneAvatar.transform.position = new Vector3(currentLeadPos.x, 1.35f + heightFactor * 6f, currentLeadPos.z);
                        replayStoneAvatar.transform.localScale = new Vector3(avatarCurrentScale, avatarCurrentScale * 0.35f, avatarCurrentScale);

                        // 🌟 넓고 납작한 윗면이 상공 카메라를 정면으로 바라보며 -pitchAngle 피칭 틸팅
                        Quaternion pitchRot = Quaternion.Euler(-pitchAngle, 0f, 0f);
                        replayStoneAvatar.transform.rotation = baseYawRot * pitchRot;
                    }

                    flightPoints[currentSegmentIdx] = currentLeadPos;
                    trajectoryLine.SetPosition(currentSegmentIdx, currentLeadPos);

                    // 🌟 실시간 비행 궤적 X/Z 동시 중심 추적 (화면 쏠림 완전 방지)
                    if (dualCam != null)
                    {
                        currentCamCenter.x = Mathf.Clamp(currentLeadPos.x, boundMinX, boundMaxX);
                        currentCamCenter.z = CalculateCameraZForLeadPosition(currentLeadPos.z);
                        dualCam.SetReplayTopDownView(currentCamCenter, currentOrthoSize);
                        SyncTerrainByZ(currentCamCenter.z);
                    }

                    yield return null;
                }

                flightPoints[currentSegmentIdx] = endP;
                trajectoryLine.SetPosition(currentSegmentIdx, endP);
            }
            else
            {
                skimLine.enabled = true;
                skimLine.positionCount = 2;
                skimLine.SetPosition(0, startP);
                skimLine.SetPosition(1, startP);

                while (segElapsed < timePerSegment)
                {
                    segElapsed += Time.deltaTime;
                    float rawT = Mathf.Clamp01(segElapsed / timePerSegment);
                    Vector3 currentLeadPos = Vector3.Lerp(startP, endP, rawT);

                    skimLine.SetPosition(1, currentLeadPos);

                    if (replayStoneAvatar != null)
                    {
                        float avatarBaseScale = Mathf.Clamp(currentOrthoSize * 0.28f, 3.5f, 65f);
                        replayStoneAvatar.transform.position = new Vector3(currentLeadPos.x, 1.35f, currentLeadPos.z);
                        replayStoneAvatar.transform.localScale = new Vector3(avatarBaseScale, avatarBaseScale * 0.35f, avatarBaseScale);
                        replayStoneAvatar.transform.rotation = baseYawRot; // 수면 수평 활주
                    }

                    if (dualCam != null)
                    {
                        currentCamCenter.x = Mathf.Clamp(currentLeadPos.x, boundMinX, boundMaxX);
                        currentCamCenter.z = CalculateCameraZForLeadPosition(currentLeadPos.z);
                        dualCam.SetReplayTopDownView(currentCamCenter, currentOrthoSize);
                        SyncTerrainByZ(currentCamCenter.z);
                    }

                    yield return null;
                }

                skimLine.SetPosition(1, endP);
            }

            // 착수점 도착! 마커 생성 및 0.15초 물결 파문 팝업
            SpawnBounceMarker(currentHistory[i + 1], i + 1);
        }

        // 최종 완주 위치로 카메라 정확히 안착 및 자유 네비게이션 모드 전환
        if (dualCam != null)
        {
            float finalX = (currentHistory.Count > 0) ? currentHistory[currentHistory.Count - 1].position.x : 0f;
            currentCamCenter.x = Mathf.Clamp(finalX, boundMinX, boundMaxX);
            currentCamCenter.z = CalculateCameraZForLeadPosition(cachedFinalDist);
            dualCam.SetReplayTopDownView(currentCamCenter, currentOrthoSize);
            SyncTerrainByZ(currentCamCenter.z);
        }

        // 🌟 드로잉 완료 후에도 조약돌 아바타를 끄지 않고 최종 착수 위치에 안착 유지
        if (replayStoneAvatar != null)
        {
            float finalX = (currentHistory.Count > 0) ? currentHistory[currentHistory.Count - 1].position.x : 0f;
            float finalZ = (currentHistory.Count > 0) ? currentHistory[currentHistory.Count - 1].position.z : cachedFinalDist;
            float avatarBaseScale = Mathf.Clamp(currentOrthoSize * 0.28f, 3.5f, 65f);
            replayStoneAvatar.transform.position = new Vector3(finalX, 1.35f, finalZ);
            replayStoneAvatar.transform.localScale = new Vector3(avatarBaseScale, avatarBaseScale * 0.35f, avatarBaseScale);
            replayStoneAvatar.transform.rotation = Quaternion.identity; // 수면에 납작하게 안착
            replayStoneAvatar.SetActive(true);
        }

        isDrawing = false;
        isReplayFinished = true;
    }

    /// <summary>
    /// 착수점 물결 파문 링 및 마커 생성 (수면/발판 위 Y = 1.02m)
    /// </summary>
    private void SpawnBounceMarker(SkippingStone.BounceRecord record, int index)
    {
        GameObject marker = new GameObject($"ReplayMarker_{index}_{record.grade}");
        marker.transform.SetParent(transform);
        Vector3 markerPos = new Vector3(record.position.x, 1.02f, record.position.z);
        marker.transform.position = markerPos;

        LineRenderer lr = marker.AddComponent<LineRenderer>();
        Shader unlitShader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
        Material mat = new Material(unlitShader);

        Color mColor = bounceMarkerColor;

        // 기본 반지름 15m (390m 뷰포트 기준 약 30픽셀)
        float baseRadius = 15f;
        float ringWidth = Mathf.Clamp(currentOrthoSize * 0.007f, 0.15f, 3.2f);

        if (index == 0)
        {
            mColor = startMarkerColor;
            baseRadius = 18f;
        }
        else if (record.grade == "SKIM_START")
        {
            mColor = skimStartMarkerColor;
            baseRadius = 17f;
        }
        else if (record.grade == "FINISH" || index == currentHistory.Count - 1)
        {
            mColor = finishMarkerColor;
            baseRadius = 22f;
        }
        else if (record.grade.Contains("BOOST"))
        {
            mColor = boostMarkerColor;
            baseRadius = 19f;
        }
        else if (record.grade.Contains("PERFECT"))
        {
            mColor = perfectMarkerColor;
            baseRadius = 16f;
        }

        mat.color = mColor;
        lr.material = mat;
        lr.startWidth = ringWidth;
        lr.endWidth = ringWidth;
        lr.useWorldSpace = false;
        lr.positionCount = 36;
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.receiveShadows = false;

        float angleStep = 360f / 36f;
        for (int a = 0; a < 36; a++)
        {
            float rad = Mathf.Deg2Rad * (a * angleStep);
            lr.SetPosition(a, new Vector3(Mathf.Cos(rad) * baseRadius, 0f, Mathf.Sin(rad) * baseRadius));
        }

        float markerScale = Mathf.Clamp(currentOrthoSize / 390f, 0.15f, 2.5f);
        marker.transform.localScale = new Vector3(markerScale, 1f, markerScale);

        markerObjects.Add(marker);

        // 🌟 착수 순간 0.15초 물결 파문 팝업 애니메이션
        if (gameObject.activeInHierarchy)
        {
            StartCoroutine(MarkerPopRoutine(marker, markerScale, 0.15f));
        }
    }

    private IEnumerator MarkerPopRoutine(GameObject marker, float targetScale, float duration)
    {
        if (marker == null) yield break;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            if (marker == null) yield break;
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
            float currentS = Mathf.Lerp(targetScale * 0.2f, targetScale, t);
            marker.transform.localScale = new Vector3(currentS, 1f, currentS);
            yield return null;
        }

        if (marker != null)
        {
            marker.transform.localScale = new Vector3(targetScale, 1f, targetScale);
        }
    }

    public void ClearVisualMarkers()
    {
        foreach (var m in markerObjects)
        {
            if (m != null) Destroy(m);
        }
        markerObjects.Clear();

        if (trajectoryLine != null)
        {
            trajectoryLine.positionCount = 0;
            trajectoryLine.enabled = false;
        }

        if (skimLine != null)
        {
            skimLine.positionCount = 0;
            skimLine.enabled = false;
        }

        if (replayStoneAvatar != null)
        {
            replayStoneAvatar.SetActive(false);
        }
    }

    /// <summary>
    /// 다시 보기(Replay Again) 클릭 시: 1구간부터 완주 지점까지 부드럽게 연속 드로잉 재시작
    /// </summary>
    public void ReplayAgain()
    {
        if (gameController != null) gameController.requireTouchRelease = false;
        if (drawCoroutine != null) StopCoroutine(drawCoroutine);
        if (slideCoroutine != null) StopCoroutine(slideCoroutine);

        isReplayFinished = false;
        isDrawing = true;

        SetPageCameraView(1, false);
        StartDrawingAnimation();
    }

    /// <summary>
    /// 결과 보기(Confirm) 클릭 시
    /// </summary>
    public void FinishReplayAndShowResult()
    {
        if (drawCoroutine != null) StopCoroutine(drawCoroutine);
        if (slideCoroutine != null) StopCoroutine(slideCoroutine);

        isReplayActive = false;
        isDrawing = false;
        isReplayFinished = false;

        ClearVisualMarkers();

        // 🌟 리플레이 종료 시 다음 게임 비행을 위해 3D 트레일 정상 복원
        if (stone == null) stone = FindAnyObjectByType<SkippingStone>();
        if (stone != null && stone.trail != null)
        {
            stone.trail.enabled = true;
            stone.trail.Clear();
        }

        // 1. 메인 URP 카메라 원복
        DualCameraSetup dualCam = (gameController != null && gameController.dualCamera != null) 
                                  ? gameController.dualCamera 
                                  : FindAnyObjectByType<DualCameraSetup>();
        if (dualCam != null)
        {
            dualCam.SetCameraMode(DualCameraSetup.CameraMode.TopDownPosition);
        }

        // 2. 최종 결과창 표시
        if (gameController != null)
        {
            gameController.ShowFinalResultDirect(cachedFinalDist);
        }
    }
}
