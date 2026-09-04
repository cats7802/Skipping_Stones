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
    public float GetPageDistance() => (LakeEnvironmentManager.Instance != null && LakeEnvironmentManager.Instance.autoChunkSize > 50f) ? LakeEnvironmentManager.Instance.autoChunkSize : 500f;
    public int totalPages = 1;
    public int currentPage = 1;

    [Header("기준 높이 (발판 상단 우선)")]
    public float baseReplayLevel = 0f;

    [Header("궤적 라인 및 비주얼 색상")]
    public float lineWidth = 0.55f;
    public Color pathColor = new Color(0.1f, 0.95f, 1.0f, 0.95f);
    public Color skimLineColor = new Color(1.0f, 0.85f, 0.20f, 0.98f);
    public Color randomRingPathColor = new Color(1.0f, 0.25f, 0.85f, 0.98f); // 🌀 랜덤 링 진입/부스트 구간 형광 핑크
    public Color startMarkerColor = new Color(0.2f, 1f, 0.4f, 1f);
    public Color bounceMarkerColor = new Color(0.1f, 0.85f, 1f, 1f);
    public Color perfectMarkerColor = new Color(0.2f, 1f, 0.5f, 1f);
    public Color boostMarkerColor = new Color(0.95f, 0.20f, 1.0f, 1f);
    public Color randomRingMarkerColor = new Color(1.0f, 0.15f, 0.90f, 1f); // 🌀 링 통과 마커 색상
    public Color skimStartMarkerColor = new Color(1.0f, 0.62f, 0.12f, 1f);
    public Color finishMarkerColor = new Color(1.0f, 0.22f, 0.22f, 1f);

    public bool isFromFlightTest = false;
    private LineRenderer trajectoryLine;
    private LineRenderer skimLine;
    private GameObject replayStoneAvatar;
    private List<GameObject> markerObjects = new List<GameObject>();
    private List<SkippingStone.BounceRecord> currentHistory = new List<SkippingStone.BounceRecord>();
    private List<SkippingStone.BounceRecord> markerRecords = new List<SkippingStone.BounceRecord>();
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

    public struct FlightSample
    {
        public Vector3 position;
        public bool isRingBoost;
    }

    private readonly List<FlightSample> realTimeFlightTrajectory = new List<FlightSample>();
    private float lastSampleZ = -999f;

    /// <summary>
    /// 🌟 비행 시작 시 이전 실시간 샘플링 궤적 초기화 (발판 상단 중심을 첫 시작점으로 안전 등록)
    /// </summary>
    public void ResetRealtimeTrajectory()
    {
        realTimeFlightTrajectory.Clear();
        lastSampleZ = -999f;

        Vector3 startOrigin = GetExactStartPlatformPosition();
        realTimeFlightTrajectory.Add(new FlightSample { position = startOrigin, isRingBoost = false });
        lastSampleZ = startOrigin.z;
    }

    /// <summary>
    /// 🌟 씬 내 발판 또는 캐릭터의 실제 월드 중심 좌표 정밀 취득
    /// </summary>
    public Vector3 GetExactStartPlatformPosition()
    {
        UpdateBaseReplayLevel();
        Transform platform = GameController.FindPlatformInScene();
        if (platform != null)
        {
            Collider pierCol = platform.GetComponent<Collider>() ?? platform.GetComponentInChildren<Collider>();
            if (pierCol != null)
            {
                return new Vector3(pierCol.bounds.center.x, baseReplayLevel, pierCol.bounds.center.z);
            }
            return new Vector3(platform.position.x, baseReplayLevel, platform.position.z);
        }

        StoneThrowerCharacter thrower = FindAnyObjectByType<StoneThrowerCharacter>();
        if (thrower != null)
        {
            return new Vector3(thrower.transform.position.x, baseReplayLevel, thrower.transform.position.z);
        }

        return new Vector3(0f, baseReplayLevel, 0f);
    }

    /// <summary>
    /// 🌟 매 프레임/비행 중 돌의 실제 위치(X, Y, Z)와 링 부스트 상태를 촘촘하게 샘플링 기록
    /// </summary>
    public void SampleStonePosition(Vector3 pos, bool isRingBoost = false)
    {
        if (realTimeFlightTrajectory.Count == 0 || Mathf.Abs(pos.z - lastSampleZ) >= 1.5f || (pos - realTimeFlightTrajectory[realTimeFlightTrajectory.Count - 1].position).sqrMagnitude >= 2.5f)
        {
            realTimeFlightTrajectory.Add(new FlightSample { position = pos, isRingBoost = isRingBoost });
            lastSampleZ = pos.z;
        }
    }

    private void Update()
    {
        if (gameController == null) gameController = FindAnyObjectByType<GameController>();

        // 🌟 비행 중일 때 실시간 돌 위치 자동 샘플링 (물리 모드 & 리듬 아케이드 모드 공통 지원)
        if (gameController != null && gameController.currentState == GameController.GameState.Flying)
        {
            Transform stoneT = null;
            bool ringBoostActive = false;

            if (gameController.currentMode == GameController.GameMode.RhythmArcade)
            {
                var arcade = FindAnyObjectByType<SkippingStones.Arcade.ArcadeSkippingStone>();
                if (arcade != null && !arcade.isSunk)
                {
                    stoneT = arcade.transform;
                    ringBoostActive = arcade.isInRandomRing;
                }
            }
            else
            {
                if (stone == null || !stone.gameObject.activeInHierarchy || stone.isSunk)
                {
                    stone = gameController.stone ?? FindAnyObjectByType<SkippingStone>();
                }

                if (stone != null && !stone.isSunk)
                {
                    stoneT = stone.transform;
                }
            }

            if (stoneT != null)
            {
                SampleStonePosition(stoneT.position, ringBoostActive);
            }
        }

        if (!isReplayActive) return;

        // 🌟 리플레이 도중에도 상시 줌인/줌아웃 및 자유 내비게이션 지원!
        HandleFreeNavigation();
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
                if (!sRb.isKinematic)
                {
                    sRb.linearVelocity = Vector3.zero;
                    sRb.angularVelocity = Vector3.zero;
                }
                sRb.useGravity = false;
                sRb.isKinematic = true;
            }
        }

        currentHistory.Clear();
        markerRecords.Clear();

        // 1. 실제 물수제비 바운스 지점 마커 데이터 취득
        if (stone != null && stone.bounceHistory != null && stone.bounceHistory.Count > 0)
        {
            markerRecords.AddRange(stone.bounceHistory);
        }

        // 2. 실시간 샘플링 궤적이 있으면 유려한 비행 곡선 데이터(currentHistory)로 변환
        if (realTimeFlightTrajectory.Count >= 2)
        {
            for (int i = 0; i < realTimeFlightTrajectory.Count; i++)
            {
                FlightSample sample = realTimeFlightTrajectory[i];
                string grade = (i == 0) ? "START" : ((i == realTimeFlightTrajectory.Count - 1) ? "FINISH" : (sample.isRingBoost ? "RING_BOOST" : "TRAJECTORY"));
                currentHistory.Add(new SkippingStone.BounceRecord { position = sample.position, skipIndex = i, grade = grade, distance = sample.position.z });
            }
        }
        else if (markerRecords.Count >= 2)
        {
            currentHistory.AddRange(markerRecords);
        }
        else
        {
            Vector3 startP = GetExactStartPlatformPosition();
            Vector3 endP = startP + Vector3.forward * finalDist;
            currentHistory.Add(new SkippingStone.BounceRecord { position = startP, skipIndex = 0, grade = "START", distance = 0f });
            currentHistory.Add(new SkippingStone.BounceRecord { position = endP, skipIndex = 1, grade = "FINISH", distance = finalDist });
            
            markerRecords.Add(new SkippingStone.BounceRecord { position = startP, skipIndex = 0, grade = "START", distance = 0f });
            markerRecords.Add(new SkippingStone.BounceRecord { position = endP, skipIndex = 1, grade = "FINISH", distance = finalDist });
        }

        // 시작점/종료점 마커가 누락된 경우 보정
        if (markerRecords.Count > 0 && currentHistory.Count > 0)
        {
            if (markerRecords[0].grade != "START")
            {
                markerRecords.Insert(0, new SkippingStone.BounceRecord { position = currentHistory[0].position, skipIndex = 0, grade = "START", distance = 0f });
            }
            if (markerRecords[markerRecords.Count - 1].grade != "FINISH")
            {
                markerRecords.Add(new SkippingStone.BounceRecord { position = currentHistory[currentHistory.Count - 1].position, skipIndex = markerRecords.Count, grade = "FINISH", distance = finalDist });
            }
        }

        CalculateSmartBounds();

        float pageDist = GetPageDistance();
        totalPages = Mathf.Max(1, Mathf.CeilToInt(finalDist / pageDist));
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
        float minZ = float.MaxValue, maxZ = float.MinValue;

        foreach (var r in currentHistory)
        {
            if (r.position.x < minX) minX = r.position.x;
            if (r.position.x > maxX) maxX = r.position.x;
            if (r.position.z < minZ) minZ = r.position.z;
            if (r.position.z > maxZ) maxZ = r.position.z;
        }

        if (minX > maxX) { minX = -10f; maxX = 10f; }
        if (minZ > maxZ) { minZ = -260f; maxZ = cachedFinalDist; }

        boundMinX = Mathf.Max(minX - 35f, -120f);
        boundMaxX = Mathf.Min(maxX + 35f, 120f);
        boundMinZ = minZ - 25f;
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

    public void SetPageCameraView(int page, bool smooth = true)
    {
        DualCameraSetup dualCam = (gameController != null && gameController.dualCamera != null)
                                  ? gameController.dualCamera
                                  : FindAnyObjectByType<DualCameraSetup>();
        if (dualCam == null) return;

        UpdateBaseReplayLevel();
        Vector3 targetCenter;
        float targetOrtho;
        float pageDist = GetPageDistance();

        if (page == 1)
        {
            Vector3 startPos = GetExactStartPlatformPosition();
            if (currentHistory != null && currentHistory.Count > 0)
            {
                startPos = currentHistory[0].position;
            }
            targetCenter = new Vector3(startPos.x, baseReplayLevel + 80f, startPos.z + 15f);
            targetOrtho = 32f;
        }
        else if (cachedFinalDist <= pageDist)
        {
            float spanZ = Mathf.Max(35f, boundMaxZ - boundMinZ);
            float spanX = Mathf.Max(25f, boundMaxX - boundMinX);
            float midX = (boundMinX + boundMaxX) * 0.5f;
            targetCenter = new Vector3(midX, baseReplayLevel + 80f, (boundMinZ + boundMaxZ) * 0.5f);
            targetOrtho = Mathf.Max(spanZ * 0.55f, spanX * 0.98f, 20f);
        }
        else
        {
            float pageStartZ = (page - 1) * pageDist;
            float pageEndZ = Mathf.Min(cachedFinalDist, page * pageDist);
            float pageCenterZ = (pageStartZ + pageEndZ) * 0.5f;
            float pageSpanZ = Mathf.Max(pageDist, pageEndZ - pageStartZ);

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
        float lookLeadZ = leadZ + 15f;
        return Mathf.Clamp(lookLeadZ, boundMinZ, boundMaxZ);
    }

    private Vector2 lastRightMousePos;
    private bool isRightMouseDragging = false;

    private void HandleFreeNavigation()
    {
        DualCameraSetup dualCam = (gameController != null && gameController.dualCamera != null)
                                  ? gameController.dualCamera
                                  : FindAnyObjectByType<DualCameraSetup>();
        if (dualCam == null || dualCam.mainCam == null) return;

        // 🌟 디렉터님 원칙: 드로잉이 완전히 끝난 후에만 자유 줌 & 패닝 오픈!
        if (!isReplayFinished || isDrawing) return;

        float screenH = Mathf.Max(Screen.height, 100f);
        float worldPerPixel = (currentOrthoSize * 2f) / screenH;

        float scrollY = 0f;
#if ENABLE_INPUT_SYSTEM
        // 🌟 1. 줌인 / 줌아웃 (A. 마우스 휠)
        if (Mouse.current != null)
        {
            scrollY = Mouse.current.scroll.ReadValue().y;
        }
#endif

        try
        {
            // 🌟 신형 Input System에서 스크롤 검출이 안 될 경우를 위해 레거시 및 하이브리드 축 백업 처리!
            if (Mathf.Abs(scrollY) < 0.001f)
            {
                scrollY = Input.mouseScrollDelta.y;
                if (Mathf.Abs(scrollY) < 0.001f)
                {
                    scrollY = Input.GetAxis("Mouse ScrollWheel") * 10f; // 일반화 보정
                }
            }
        }
        catch { }

        if (Mathf.Abs(scrollY) > 0.001f)
        {
            float zoomFactor = (scrollY > 0f) ? 0.82f : 1.22f; // 휠 1틱당 18~22% 줌인/줌아웃
            targetOrthoSize = Mathf.Clamp(targetOrthoSize * zoomFactor, minOrthoSize, maxOrthoSize);
        }

#if ENABLE_INPUT_SYSTEM
        // 🌟 1-B. 안전 조건: 마우스 우클릭 드래그 상하 줌
        if (Mouse.current != null)
        {
            if (Mouse.current.rightButton.isPressed)
            {
                Vector2 rightPos = Mouse.current.position.ReadValue();
                if (!isRightMouseDragging)
                {
                    isRightMouseDragging = true;
                    lastRightMousePos = rightPos;
                }
                else
                {
                    float dy = rightPos.y - lastRightMousePos.y;
                    if (Mathf.Abs(dy) > 0.1f)
                    {
                        float zoomDelta = -dy * (targetOrthoSize * 0.008f);
                        targetOrthoSize = Mathf.Clamp(targetOrthoSize + zoomDelta, minOrthoSize, maxOrthoSize);
                        lastRightMousePos = rightPos;
                    }
                }
            }
            else
            {
                isRightMouseDragging = false;
            }
        }

        // 🌟 1-C. 모바일 2터치 핀치 줌
        if (Touchscreen.current != null && Touchscreen.current.touches.Count >= 2)
        {
            var t0 = Touchscreen.current.touches[0];
            var t1 = Touchscreen.current.touches[1];
            if (t0.isInProgress && t1.isInProgress)
            {
                Vector2 p0 = t0.position.ReadValue();
                Vector2 p1 = t1.position.ReadValue();
                Vector2 d0 = t0.delta.ReadValue();
                Vector2 d1 = t1.delta.ReadValue();

                float prevDist = ((p0 - d0) - (p1 - d1)).magnitude;
                float currDist = (p0 - p1).magnitude;
                float delta = currDist - prevDist;

                if (Mathf.Abs(delta) > 0.1f)
                {
                    targetOrthoSize = Mathf.Clamp(targetOrthoSize - delta * (targetOrthoSize * 0.004f), minOrthoSize, maxOrthoSize);
                }
            }
        }

        // 🌟 2. 화면 드래그 패닝 (마우스 좌클릭 또는 모바일 1터치)
        Vector2 mousePos = Vector2.zero;
        bool isMouseDown = false;

        if (Mouse.current != null && Mouse.current.leftButton.isPressed)
        {
            mousePos = Mouse.current.position.ReadValue();
            isMouseDown = true;
        }
        else if (Touchscreen.current != null && Touchscreen.current.primaryTouch.isInProgress && Touchscreen.current.touches.Count < 2)
        {
            mousePos = Touchscreen.current.primaryTouch.position.ReadValue();
            isMouseDown = true;
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
#endif

        currentCamCenter.x = Mathf.Clamp(currentCamCenter.x, boundMinX, boundMaxX);
        currentCamCenter.z = Mathf.Clamp(currentCamCenter.z, boundMinZ, boundMaxZ);

        currentOrthoSize = Mathf.Lerp(currentOrthoSize, targetOrthoSize, Time.unscaledDeltaTime * 16f);
        UpdateVisualsScale(currentOrthoSize);

        // 🌟 카메라 및 DualCameraSetup에 실시간 동기화 반영
        dualCam.SetReplayTopDownView(new Vector3(currentCamCenter.x, baseReplayLevel + 80f, currentCamCenter.z), currentOrthoSize);
        SyncTerrainByZ(currentCamCenter.z);
    }

    private void SyncTerrainByZ(float centerZ)
    {
        float pageDist = GetPageDistance();
        int camPage = Mathf.Clamp(Mathf.FloorToInt(centerZ / pageDist) + 1, 1, totalPages);
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

        // 시작점 단발 마커 스폰
        if (markerRecords.Count > 0 && markerRecords[0].grade == "START")
        {
            SpawnBounceMarker(markerRecords[0], 0);
        }

        List<Vector3> flightPoints = new List<Vector3>();
        flightPoints.Add(new Vector3(currentHistory[0].position.x, drawY, currentHistory[0].position.z));
        trajectoryLine.positionCount = 1;
        trajectoryLine.SetPosition(0, flightPoints[0]);

        // 🌟 궤적을 충분히 음미할 수 있도록 1/3 감속 (초속 약 45m/s 속도로 10~25초간 우아하게 재생)
        float totalDrawDuration = Mathf.Clamp(cachedFinalDist / 45f, 10.0f, 25.0f);
        float timePerSegment = totalDrawDuration / Mathf.Max(1, currentHistory.Count - 1);

        int nextMarkerIdx = 1;

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

            // 🌟 1) 실제 물수제비 바운스 지점 통과 시 원형 마커 단발 스폰
            while (nextMarkerIdx < markerRecords.Count - 1 && markerRecords[nextMarkerIdx].distance <= currentHistory[i + 1].distance)
            {
                SpawnBounceMarker(markerRecords[nextMarkerIdx], nextMarkerIdx);
                nextMarkerIdx++;
            }

            // 🌟 2) 링 진입 및 탈출 순간 1회 단발 마커 스폰
            bool prevWasRing = (i > 0 && currentHistory[i - 1].grade == "RING_BOOST");
            bool currIsRing = (currentHistory[i].grade == "RING_BOOST");
            if (currIsRing && !prevWasRing)
            {
                SpawnBounceMarker(new SkippingStone.BounceRecord { position = currentHistory[i].position, grade = "RING_BOOST", distance = currentHistory[i].distance }, 9000 + i);
            }
        }

        // 🌟 종료 지점 최종 마커 스폰
        if (markerRecords.Count > 0)
        {
            SpawnBounceMarker(markerRecords[markerRecords.Count - 1], markerRecords.Count - 1);
        }

        // 🌟 종료 시 엉뚱한 곳으로 점프하지 않고 마지막 돌 위치에 정밀하게 카메라 안착
        if (dualCam != null && currentHistory.Count > 0)
        {
            Vector3 lastPos = currentHistory[currentHistory.Count - 1].position;
            currentCamCenter.x = Mathf.Clamp(lastPos.x, boundMinX, boundMaxX);
            currentCamCenter.z = Mathf.Clamp(lastPos.z, boundMinZ, boundMaxZ);
            targetOrthoSize = Mathf.Clamp(targetOrthoSize, 25f, 60f); // 마무리 시 너무 멀거나 가깝지 않게
            dualCam.SetReplayTopDownView(new Vector3(currentCamCenter.x, baseReplayLevel + 80f, currentCamCenter.z), currentOrthoSize);
            SyncTerrainByZ(currentCamCenter.z);
        }

        if (replayStoneAvatar != null && currentHistory.Count > 0)
        {
            Vector3 lastPos = currentHistory[currentHistory.Count - 1].position;
            float avatarBaseScale = Mathf.Clamp(currentOrthoSize * 0.18f, 2.5f, 25f);
            replayStoneAvatar.transform.position = new Vector3(lastPos.x, baseReplayLevel + 0.45f, lastPos.z);
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
        else if (record.grade.Contains("RING_BOOST"))
        {
            mColor = randomRingMarkerColor;
            baseRadius = 20f;
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