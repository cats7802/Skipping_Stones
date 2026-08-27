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

    [Header("기준 높이 (발판 상단 우선)")]
    public float baseReplayLevel = 0f;

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

    public bool isFromFlightTest = false;
    private LineRenderer trajectoryLine;
    private LineRenderer skimLine;
    private GameObject replayStoneAvatar;
    private List<GameObject> markerObjects = new List<GameObject>();
    private List<SkippingStone.BounceRecord> currentHistory = new List<SkippingStone.BounceRecord>();
    private Coroutine drawCoroutine;
    private Coroutine slideCoroutine;
    private float cachedFinalDist = 0f;

    private Vector3 currentCamCenter = Vector3.zero;
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

        UpdateBaseReplayLevel();
        CreateTrajectoryLineRenderers();
        CreateReplayStoneAvatar();
    }

    /// <summary>
    /// 발판(Lakeside_WoodenPier) 상단 표면(bounds.max.y)을 최우선 기준 높이로 취득
    /// </summary>
    public void UpdateBaseReplayLevel()
    {
        Transform platform = GameController.FindPlatformInScene();
        if (platform != null)
        {
            Collider pierCol = platform.GetComponent<Collider>();
            if (pierCol != null)
            {
                baseReplayLevel = pierCol.bounds.max.y;
                return;
            }
            baseReplayLevel = platform.position.y + 0.2f;
            return;
        }

        if (stone != null && stone.waterLevel > 0.1f)
        {
            baseReplayLevel = stone.waterLevel;
            return;
        }

        GameObject water = GameObject.Find("WaterSurface") ?? GameObject.Find("Water_Surface");
        if (water != null)
        {
            Collider col = water.GetComponent<Collider>();
            baseReplayLevel = (col != null) ? col.bounds.max.y : water.transform.position.y;
        }
    }

    private void Update()
    {
        if (!isReplayActive) return;

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

    private void CreateReplayStoneAvatar()
    {
        if (replayStoneAvatar != null)
        {
            if (Application.isPlaying) Destroy(replayStoneAvatar);
            else DestroyImmediate(replayStoneAvatar);
            replayStoneAvatar = null;
        }

        GameObject stonePrefab = null;

        // 1. 현재 GameController 또는 GameDataManager의 선택된 돌 프리팹 취득
        if (gameController != null && gameController.defaultStonePrefab != null)
        {
            stonePrefab = gameController.defaultStonePrefab;
        }
        else if (SkippingStones.Data.GameDataManager.Instance != null)
        {
            var dm = SkippingStones.Data.GameDataManager.Instance;
            string selectedId = dm.UserData != null ? dm.UserData.selectedStoneId : "default";
            var stoneInfo = dm.stoneCatalog.Find(s => s.id == selectedId || (s.prefabPath != null && s.prefabPath.Contains(selectedId)));
            if (stoneInfo != null && !string.IsNullOrEmpty(stoneInfo.prefabPath))
            {
#if UNITY_EDITOR
                stonePrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(stoneInfo.prefabPath);
#else
                string rPath = stoneInfo.prefabPath.Replace("Assets/prefab/", "").Replace(".prefab", "");
                stonePrefab = Resources.Load<GameObject>(rPath);
#endif
            }
        }

        if (stonePrefab == null)
        {
#if UNITY_EDITOR
            stonePrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/prefab/Stone/Stone.prefab");
#else
            stonePrefab = Resources.Load<GameObject>("Stone/Stone");
#endif
        }

        if (stonePrefab != null)
        {
            replayStoneAvatar = Instantiate(stonePrefab, transform);
            replayStoneAvatar.name = "TopDownReplay_StoneAvatar";

            // 불필요한 물리/인게임 스크립트 비활성화
            var ss = replayStoneAvatar.GetComponent<SkippingStone>();
            if (ss != null) Destroy(ss);
            var rb = replayStoneAvatar.GetComponent<Rigidbody>();
            if (rb != null) Destroy(rb);
            var tr = replayStoneAvatar.GetComponent<TrailRenderer>();
            if (tr != null) Destroy(tr);

            foreach (var col in replayStoneAvatar.GetComponentsInChildren<Collider>(true))
            {
                if (Application.isPlaying) Destroy(col);
                else DestroyImmediate(col);
            }

            foreach (var rend in replayStoneAvatar.GetComponentsInChildren<Renderer>(true))
            {
                rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }

            replayStoneAvatar.SetActive(false);
        }
    }

    private void UpdateVisualsScale(float orthoSize)
    {
        float dynamicW = Mathf.Clamp(orthoSize * 0.0128f, 0.32f, 6.0f);
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

        if (replayStoneAvatar != null && replayStoneAvatar.activeSelf)
        {
            // 상공 80m 탑다운 뷰에서도 조약돌이 확실하게 눈에 띄도록 시인성 강화 배율 적용
            float avatarScale = Mathf.Clamp(orthoSize * 0.18f, 2.5f, 25f);
            replayStoneAvatar.transform.localScale = new Vector3(avatarScale, avatarScale, avatarScale);
        }
    }

    public void StartReplay(float finalDist)
    {
        UpdateBaseReplayLevel();
        cachedFinalDist = finalDist;
        if (stone == null) stone = FindAnyObjectByType<SkippingStone>();
        if (gameController == null) gameController = FindAnyObjectByType<GameController>();
        if (gameController != null) gameController.currentState = GameController.GameState.Replay;

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
            Vector3 startP = (stone != null) ? stone.transform.position : new Vector3(0f, baseReplayLevel, 0f);
            currentHistory.Add(new SkippingStone.BounceRecord { position = startP, skipIndex = 0, grade = "START", distance = 0f });
            currentHistory.Add(new SkippingStone.BounceRecord { position = startP + Vector3.forward * finalDist, skipIndex = 1, grade = "FINISH", distance = finalDist });
        }

        CalculateSmartBounds();

        totalPages = Mathf.Max(1, Mathf.CeilToInt(finalDist / PAGE_DISTANCE));
        currentPage = 1;
        isReplayActive = true;
        isReplayFinished = false;

        if (EnvironmentTestHelper.Instance != null)
        {
            EnvironmentTestHelper.Instance.StopAutoFly();
            EnvironmentTestHelper.Instance.showTestUI = false;
        }

        StoneThrowerCharacter thrower = FindAnyObjectByType<StoneThrowerCharacter>();
        if (thrower != null)
        {
            thrower.RestoreVisibility();
        }

        Transform platform = GameController.FindPlatformInScene();
        if (platform != null)
        {
            platform.gameObject.SetActive(true);
            var pr = platform.GetComponent<Renderer>();
            if (pr != null) pr.enabled = true;
        }

        SetPageCameraView(1, false);
        StartDrawingAnimation();
    }

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

        boundMinX = Mathf.Max(minX - 35f, -120f);
        boundMaxX = Mathf.Min(maxX + 35f, 120f);
        boundMinZ = -15f;
        boundMaxZ = maxZ + 25f;

        minOrthoSize = 18f;

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

        UpdateBaseReplayLevel();
        Vector3 targetCenter;
        float targetOrtho;

        if (cachedFinalDist <= PAGE_DISTANCE)
        {
            float spanZ = Mathf.Max(35f, boundMaxZ - boundMinZ);
            float spanX = Mathf.Max(25f, boundMaxX - boundMinX);
            float midX = (boundMinX + boundMaxX) * 0.5f;
            targetCenter = new Vector3(midX, baseReplayLevel + 80f, (boundMinZ + boundMaxZ) * 0.5f);
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

            targetCenter = new Vector3(pageCenterX, baseReplayLevel + 80f, pageCenterZ);
            targetOrtho = Mathf.Max(pageSpanZ * 0.52f, pageSpanX * 0.98f, 25f);
        }

        currentCamCenter = targetCenter;
        targetOrthoSize = targetOrtho;
        currentOrthoSize = targetOrtho;

        UpdateVisualsScale(targetOrtho);

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

            Vector3 currentPos = Vector3.Lerp(startPos, new Vector3(targetCenter.x, baseReplayLevel + 80f, targetCenter.z), t);
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

    private float CalculateCameraZForLeadPosition(float leadZ)
    {
        if (cachedFinalDist <= PAGE_DISTANCE)
        {
            return (boundMinZ + boundMaxZ) * 0.5f;
        }

        if (leadZ <= 800f)
        {
            return 750f;
        }

        return Mathf.Clamp(leadZ - 50f, 750f, cachedFinalDist);
    }

    private void HandleFreeNavigation()
    {
        DualCameraSetup dualCam = (gameController != null && gameController.dualCamera != null)
                                  ? gameController.dualCamera
                                  : FindAnyObjectByType<DualCameraSetup>();
        if (dualCam == null || dualCam.mainCam == null) return;

        float screenH = Mathf.Max(Screen.height, 100f);
        float worldPerPixel = (currentOrthoSize * 2f) / screenH;

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

        if (Mathf.Abs(scrollVal) > 0.01f)
        {
            targetOrthoSize = Mathf.Clamp(targetOrthoSize - scrollVal * (targetOrthoSize * 0.12f), minOrthoSize, maxOrthoSize);
        }

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

        currentCamCenter.x = Mathf.Clamp(currentCamCenter.x, boundMinX, boundMaxX);
        currentCamCenter.z = Mathf.Clamp(currentCamCenter.z, boundMinZ, boundMaxZ);

        currentOrthoSize = Mathf.Lerp(currentOrthoSize, targetOrthoSize, Time.unscaledDeltaTime * 14f);
        UpdateVisualsScale(currentOrthoSize);

        dualCam.SetReplayTopDownView(new Vector3(currentCamCenter.x, baseReplayLevel + 80f, currentCamCenter.z), currentOrthoSize);

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
        UpdateBaseReplayLevel();

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

        float drawY = baseReplayLevel + 0.15f;

        SpawnBounceMarker(currentHistory[0], 0);

        List<Vector3> flightPoints = new List<Vector3>();
        flightPoints.Add(new Vector3(currentHistory[0].position.x, drawY, currentHistory[0].position.z));
        trajectoryLine.positionCount = 1;
        trajectoryLine.SetPosition(0, flightPoints[0]);

        float totalDrawDuration = Mathf.Clamp(currentHistory.Count * 0.28f, 1.8f, 3.2f);
        float timePerSegment = totalDrawDuration / (currentHistory.Count - 1);

        for (int i = 0; i < currentHistory.Count - 1; i++)
        {
            Vector3 startP = new Vector3(currentHistory[i].position.x, drawY, currentHistory[i].position.z);
            Vector3 endP = new Vector3(currentHistory[i + 1].position.x, drawY, currentHistory[i + 1].position.z);

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
                    float forwardT = Mathf.SmoothStep(0f, 1f, rawT);
                    Vector3 currentLeadPos = Vector3.Lerp(startP, endP, forwardT);

                    float heightFactor = 4f * rawT * (1f - rawT);

                    float baseWidth = Mathf.Clamp(currentOrthoSize * 0.0128f, 0.32f, 6.0f);
                    float dynamicJumpWidth = baseWidth * (1f + heightFactor * 1.5f);
                    trajectoryLine.startWidth = baseWidth;
                    trajectoryLine.endWidth = dynamicJumpWidth;

                    float vyFactor = (1f - 2f * rawT);
                    float pitchAngle = vyFactor * 38f;

                    if (replayStoneAvatar != null)
                    {
                        float avatarBaseScale = Mathf.Clamp(currentOrthoSize * 0.18f, 2.5f, 25f);
                        float avatarCurrentScale = avatarBaseScale * (1f + heightFactor * 1.5f);

                        replayStoneAvatar.transform.position = new Vector3(currentLeadPos.x, (baseReplayLevel + 0.45f) + heightFactor * 6f, currentLeadPos.z);
                        replayStoneAvatar.transform.localScale = new Vector3(avatarCurrentScale, avatarCurrentScale, avatarCurrentScale);

                        Quaternion pitchRot = Quaternion.Euler(-pitchAngle, 0f, 0f);
                        replayStoneAvatar.transform.rotation = baseYawRot * pitchRot;
                    }

                    flightPoints[currentSegmentIdx] = currentLeadPos;
                    trajectoryLine.SetPosition(currentSegmentIdx, currentLeadPos);

                    if (dualCam != null)
                    {
                        currentCamCenter.x = Mathf.Clamp(currentLeadPos.x, boundMinX, boundMaxX);
                        currentCamCenter.z = CalculateCameraZForLeadPosition(currentLeadPos.z);
                        dualCam.SetReplayTopDownView(new Vector3(currentCamCenter.x, baseReplayLevel + 80f, currentCamCenter.z), currentOrthoSize);
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
                        float avatarBaseScale = Mathf.Clamp(currentOrthoSize * 0.18f, 2.5f, 25f);
                        replayStoneAvatar.transform.position = new Vector3(currentLeadPos.x, baseReplayLevel + 0.45f, currentLeadPos.z);
                        replayStoneAvatar.transform.localScale = new Vector3(avatarBaseScale, avatarBaseScale, avatarBaseScale);
                        replayStoneAvatar.transform.rotation = baseYawRot;
                    }

                    if (dualCam != null)
                    {
                        currentCamCenter.x = Mathf.Clamp(currentLeadPos.x, boundMinX, boundMaxX);
                        currentCamCenter.z = CalculateCameraZForLeadPosition(currentLeadPos.z);
                        dualCam.SetReplayTopDownView(new Vector3(currentCamCenter.x, baseReplayLevel + 80f, currentCamCenter.z), currentOrthoSize);
                        SyncTerrainByZ(currentCamCenter.z);
                    }

                    yield return null;
                }

                skimLine.SetPosition(1, endP);
            }

            SpawnBounceMarker(currentHistory[i + 1], i + 1);
        }

        if (dualCam != null)
        {
            float finalX = (currentHistory.Count > 0) ? currentHistory[currentHistory.Count - 1].position.x : 0f;
            currentCamCenter.x = Mathf.Clamp(finalX, boundMinX, boundMaxX);
            currentCamCenter.z = CalculateCameraZForLeadPosition(cachedFinalDist);
            dualCam.SetReplayTopDownView(new Vector3(currentCamCenter.x, baseReplayLevel + 80f, currentCamCenter.z), currentOrthoSize);
            SyncTerrainByZ(currentCamCenter.z);
        }

        if (replayStoneAvatar != null)
        {
            float finalX = (currentHistory.Count > 0) ? currentHistory[currentHistory.Count - 1].position.x : 0f;
            float finalZ = (currentHistory.Count > 0) ? currentHistory[currentHistory.Count - 1].position.z : cachedFinalDist;
            float avatarBaseScale = Mathf.Clamp(currentOrthoSize * 0.18f, 2.5f, 25f);
            replayStoneAvatar.transform.position = new Vector3(finalX, baseReplayLevel + 0.45f, finalZ);
            replayStoneAvatar.transform.localScale = new Vector3(avatarBaseScale, avatarBaseScale, avatarBaseScale);
            replayStoneAvatar.transform.rotation = Quaternion.identity;
            replayStoneAvatar.SetActive(true);
        }

        isDrawing = false;
        isReplayFinished = true;
    }

    private void SpawnBounceMarker(SkippingStone.BounceRecord record, int index)
    {
        GameObject marker = new GameObject($"ReplayMarker_{index}_{record.grade}");
        marker.transform.SetParent(transform);
        Vector3 markerPos = new Vector3(record.position.x, baseReplayLevel + 0.12f, record.position.z);
        marker.transform.position = markerPos;

        LineRenderer lr = marker.AddComponent<LineRenderer>();
        Shader unlitShader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
        Material mat = new Material(unlitShader);

        Color mColor = bounceMarkerColor;

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

    public void FinishReplayAndShowResult()
    {
        if (drawCoroutine != null) StopCoroutine(drawCoroutine);
        if (slideCoroutine != null) StopCoroutine(slideCoroutine);

        isReplayActive = false;
        isDrawing = false;
        isReplayFinished = false;

        ClearVisualMarkers();

        if (stone == null) stone = FindAnyObjectByType<SkippingStone>();
        if (stone != null && stone.trail != null)
        {
            stone.trail.enabled = true;
            stone.trail.Clear();
        }

        DualCameraSetup dualCam = (gameController != null && gameController.dualCamera != null)
                                  ? gameController.dualCamera
                                  : FindAnyObjectByType<DualCameraSetup>();
        if (dualCam != null)
        {
            dualCam.SetCameraMode(DualCameraSetup.CameraMode.TopDownPosition);
        }

        if (gameController != null)
        {
            if (isFromFlightTest)
            {
                gameController.ReturnToModeSelect();
                if (EnvironmentTestHelper.Instance != null)
                {
                    EnvironmentTestHelper.Instance.showTestUI = true;
                }
            }
            else
            {
                gameController.ShowFinalResultDirect(cachedFinalDist);
            }
        }
    }
}