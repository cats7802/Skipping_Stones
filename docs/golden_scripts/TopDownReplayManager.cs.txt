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
    public Color startMarkerColor = new Color(0.2f, 1f, 0.4f, 1f);
    public Color bounceMarkerColor = new Color(0.1f, 0.85f, 1f, 1f);
    public Color perfectMarkerColor = Color.green;
    public Color greatMarkerColor = Color.cyan;
    public Color goodMarkerColor = Color.yellow;
    public Color earlyLateMarkerColor = new Color(1.0f, 0.55f, 0.15f, 1.0f);
    public Color finishMarkerColor = new Color(1.0f, 0.22f, 0.22f, 1f);

    public bool isFromFlightTest = false;
    private LineRenderer trajectoryLine;
    private LineRenderer skimLine;
    private GameObject replayStoneAvatar;
    private readonly List<GameObject> markerObjects = new List<GameObject>();
    private readonly List<SkippingStone.BounceRecord> markerRecords = new List<SkippingStone.BounceRecord>();
    private readonly List<Vector3> trajectoryPathPoints = new List<Vector3>();
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

    public struct FlightSample
    {
        public Vector3 position;
        public bool isRingBoost;
    }

    private readonly List<FlightSample> realTimeFlightTrajectory = new List<FlightSample>();
    private float lastSampleZ = -999f;

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
    /// 🌟 매 프레임/비행 중 돌의 실제 위치(X, Y, Z)를 부드러운 라인 렌더링용으로 샘플링
    /// </summary>
    public void SampleStonePosition(Vector3 pos, bool isRingBoost = false)
    {
        if (realTimeFlightTrajectory.Count == 0 || Mathf.Abs(pos.z - lastSampleZ) >= 2.0f || (pos - realTimeFlightTrajectory[realTimeFlightTrajectory.Count - 1].position).sqrMagnitude >= 4.0f)
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

        markerRecords.Clear();
        trajectoryPathPoints.Clear();

        // 1. 실제 물수제비 바운스 지점 마커 데이터 취득 (SkippingStone 또는 ArcadeSkippingStone 공통 지원)
        if (stone != null && stone.bounceHistory != null && stone.bounceHistory.Count > 0)
        {
            markerRecords.AddRange(stone.bounceHistory);
        }
        else
        {
            var arcadeStone = FindAnyObjectByType<SkippingStones.Arcade.ArcadeSkippingStone>();
            if (arcadeStone != null && arcadeStone.bounceHistory != null && arcadeStone.bounceHistory.Count > 0)
            {
                markerRecords.AddRange(arcadeStone.bounceHistory);
            }
        }

        // 🌀 랜덤 링 진입 순간 1회 단발 마커 취득 (실시간 샘플링 기반)
        if (realTimeFlightTrajectory.Count > 0)
        {
            bool wasInRing = false;
            for (int s = 0; s < realTimeFlightTrajectory.Count; s++)
            {
                var sample = realTimeFlightTrajectory[s];
                if (sample.isRingBoost && !wasInRing)
                {
                    wasInRing = true;
                    markerRecords.Add(new SkippingStone.BounceRecord
                    {
                        position = sample.position,
                        skipIndex = 9000 + s,
                        grade = "RING_BOOST",
                        distance = sample.position.z
                    });
                }
                else if (!sample.isRingBoost)
                {
                    wasInRing = false;
                }
            }
        }

        // 마커를 Z 거리순으로 정렬
        markerRecords.Sort((a, b) => a.distance.CompareTo(b.distance));

        // 시작점/종료점 보정
        Vector3 startOrigin = GetExactStartPlatformPosition();
        if (markerRecords.Count == 0 || markerRecords[0].grade != "START")
        {
            markerRecords.Insert(0, new SkippingStone.BounceRecord { position = startOrigin, skipIndex = 0, grade = "START", distance = 0f });
        }
        if (markerRecords.Count == 1 || markerRecords[markerRecords.Count - 1].grade != "FINISH")
        {
            Vector3 finishPos = (realTimeFlightTrajectory.Count > 0) ? realTimeFlightTrajectory[realTimeFlightTrajectory.Count - 1].position : startOrigin + Vector3.forward * finalDist;
            markerRecords.Add(new SkippingStone.BounceRecord { position = finishPos, skipIndex = markerRecords.Count, grade = "FINISH", distance = finalDist });
        }

        // 2. 부드러운 궤적선 경로 포인트 생성 (실시간 샘플링 궤적 우선 활용)
        if (realTimeFlightTrajectory.Count >= 2)
        {
            for (int i = 0; i < realTimeFlightTrajectory.Count; i++)
            {
                trajectoryPathPoints.Add(new Vector3(realTimeFlightTrajectory[i].position.x, baseReplayLevel + 0.15f, realTimeFlightTrajectory[i].position.z));
            }
        }
        else
        {
            for (int i = 0; i < markerRecords.Count; i++)
            {
                trajectoryPathPoints.Add(new Vector3(markerRecords[i].position.x, baseReplayLevel + 0.15f, markerRecords[i].position.z));
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

        foreach (var r in markerRecords)
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

        minOrthoSize = 8f;
        maxOrthoSize = Mathf.Max(400f, (boundMaxZ - boundMinZ) * 0.75f);
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
            if (markerRecords != null && markerRecords.Count > 0)
            {
                startPos = markerRecords[0].position;
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
            foreach (var r in markerRecords)
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

    private void HandleFreeNavigation()
    {
        DualCameraSetup dualCam = (gameController != null && gameController.dualCamera != null)
                                  ? gameController.dualCamera
                                  : FindAnyObjectByType<DualCameraSetup>();
        if (dualCam == null || dualCam.mainCam == null) return;

        float screenH = Mathf.Max(Screen.height, 100f);
        float worldPerPixel = (currentOrthoSize * 2f) / screenH;

        // 1. 모바일 터치 처리 (핀치 줌 & 1터치 패닝)
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

            if (tCount == 1 && t0Pos.y > screenH * 0.16f)
            {
                if (slideCoroutine != null) { StopCoroutine(slideCoroutine); slideCoroutine = null; }
                currentCamCenter.x -= t0Delta.x * worldPerPixel;
                currentCamCenter.z -= t0Delta.y * worldPerPixel;
            }
            else if (tCount == 2)
            {
                if (slideCoroutine != null) { StopCoroutine(slideCoroutine); slideCoroutine = null; }
                Vector2 prevP0 = t0Pos - t0Delta;
                Vector2 prevP1 = t1Pos - t1Delta;
                float prevDist = (prevP0 - prevP1).magnitude;
                float currDist = (t0Pos - t1Pos).magnitude;
                float delta = currDist - prevDist;

                targetOrthoSize = Mathf.Clamp(targetOrthoSize - delta * (targetOrthoSize * 0.0035f), minOrthoSize, maxOrthoSize);
            }
        }
#endif

        // 2. PC 마우스 처리 (부호 기반 휠 줌 & 전 버튼 드래그 패닝)
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
                Debug.Log($"<color=#00FF66>[Replay Zoom: NewInput]</color> Scroll Y={s.y:F2} | Sign={scrollVal} | PrevTargetOrtho={targetOrthoSize:F1}");
            }

            mousePos = Mouse.current.position.ReadValue();
            isMouseDown = Mouse.current.leftButton.isPressed || Mouse.current.rightButton.isPressed || Mouse.current.middleButton.isPressed;
            if (Mouse.current.leftButton.wasPressedThisFrame || Mouse.current.rightButton.wasPressedThisFrame || Mouse.current.middleButton.wasPressedThisFrame)
            {
                Debug.Log($"<color=#00FFFF>[Replay Mouse Click]</color> Pos={mousePos} | ScreenH={screenH} | DragAllowed={(mousePos.y > screenH * 0.16f)}");
            }
        }
        else
        {
            Debug.LogWarning("[Replay Input Warning] Mouse.current is NULL!");
        }
#endif

        // 🌟 Legacy Input도 동시에 디버그 감지 (어디로 휠이 들어오는지 1:1 대조)
        try
        {
            Vector2 legacyScroll = Input.mouseScrollDelta;
            if (Mathf.Abs(legacyScroll.y) > 0.01f)
            {
                Debug.Log($"<color=#FFCC00>[Replay Zoom: LegacyInput]</color> Legacy Scroll Y={legacyScroll.y:F2}");
                if (Mathf.Abs(scrollVal) <= 0.01f)
                {
                    scrollVal = Mathf.Sign(legacyScroll.y);
                }
            }
        }
        catch { /* Legacy Input 비활성화 프로젝트 환경 예외 무시 */ }

        // 휠 줌 적용 (1클릭당 12% 줌인/줌아웃)
        if (Mathf.Abs(scrollVal) > 0.01f)
        {
            if (slideCoroutine != null) { StopCoroutine(slideCoroutine); slideCoroutine = null; }
            float oldOrtho = targetOrthoSize;
            targetOrthoSize = Mathf.Clamp(targetOrthoSize - scrollVal * (targetOrthoSize * 0.12f), minOrthoSize, maxOrthoSize);
            Debug.Log($"<color=#33FF33>[Replay Zoom Applied]</color> {oldOrtho:F1} -> {targetOrthoSize:F1} (min={minOrthoSize}, max={maxOrthoSize})");
        }

        // 마우스 드래그 패닝
        if (isMouseDown && mousePos.y > screenH * 0.16f)
        {
            if (slideCoroutine != null) { StopCoroutine(slideCoroutine); slideCoroutine = null; }
            if (!isMouseDragging)
            {
                isMouseDragging = true;
                lastMousePos = mousePos;
            }
            else
            {
                Vector2 delta = mousePos - lastMousePos;
                currentCamCenter.x -= delta.x * worldPerPixel;
                currentCamCenter.z -= delta.y * worldPerPixel;
                lastMousePos = mousePos;
            }
        }
        else
        {
            isMouseDragging = false;
        }

        currentCamCenter.x = Mathf.Clamp(currentCamCenter.x, boundMinX, boundMaxX);
        currentCamCenter.z = Mathf.Clamp(currentCamCenter.z, boundMinZ, boundMaxZ);

        currentOrthoSize = Mathf.Lerp(currentOrthoSize, targetOrthoSize, Time.unscaledDeltaTime * 16f);
        UpdateVisualsScale(currentOrthoSize);

        dualCam.SetReplayTopDownView(new Vector3(currentCamCenter.x, baseReplayLevel + 80f, currentCamCenter.z), currentOrthoSize);
        SyncTerrainByZ(currentCamCenter.z);
    }

    public void ZoomIn(float ratio = 0.82f)
    {
        if (slideCoroutine != null) { StopCoroutine(slideCoroutine); slideCoroutine = null; }
        targetOrthoSize = Mathf.Clamp(targetOrthoSize * ratio, minOrthoSize, maxOrthoSize);
    }

    public void ZoomOut(float ratio = 1.22f)
    {
        if (slideCoroutine != null) { StopCoroutine(slideCoroutine); slideCoroutine = null; }
        targetOrthoSize = Mathf.Clamp(targetOrthoSize * ratio, minOrthoSize, maxOrthoSize);
    }

    public void FitEntireTrajectory()
    {
        if (slideCoroutine != null) { StopCoroutine(slideCoroutine); slideCoroutine = null; }

        float spanZ = Mathf.Max(40f, boundMaxZ - boundMinZ);
        float spanX = Mathf.Max(30f, boundMaxX - boundMinX);
        float midX = (boundMinX + boundMaxX) * 0.5f;
        float midZ = (boundMinZ + boundMaxZ) * 0.5f;

        Vector3 targetCenter = new Vector3(midX, baseReplayLevel + 80f, midZ);
        float targetOrtho = Mathf.Clamp(Mathf.Max(spanZ * 0.55f, spanX * 0.95f), minOrthoSize, maxOrthoSize);

        targetOrthoSize = targetOrtho;
        currentCamCenter = targetCenter;
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

    /// <summary>
    /// 🌟 1/2 속도로 여유롭고 부드러운 60fps 궤적 + 카툰 3D 도약(통통 점프) 연출
    /// </summary>
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

        if (trajectoryPathPoints.Count < 2)
        {
            isDrawing = false;
            isReplayFinished = true;
            if (replayStoneAvatar != null) replayStoneAvatar.SetActive(false);
            yield break;
        }

        // 1. 시작점 마커 즉시 스폰
        if (markerRecords.Count > 0 && markerRecords[0].grade == "START")
        {
            SpawnBounceMarker(markerRecords[0], 0);
        }

        // 2. 🌟 재생 속도 1/2 감속 (초당 40m/s -> 6~16초 동안 여유롭고 우아하게 재생)
        float totalDrawDuration = Mathf.Clamp(cachedFinalDist / 40f, 6.0f, 16.0f);
        float elapsed = 0f;

        List<Vector3> drawnPoints = new List<Vector3> { trajectoryPathPoints[0] };
        trajectoryLine.positionCount = 1;
        trajectoryLine.SetPosition(0, trajectoryPathPoints[0]);

        int nextMarkerIdx = 1;

        while (elapsed < totalDrawDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(elapsed / totalDrawDuration);

            // 현재 진행도에 해당하는 궤적 좌표 보간
            float pointProgress = progress * (trajectoryPathPoints.Count - 1);
            int baseIdx = Mathf.FloorToInt(pointProgress);
            int nextIdx = Mathf.Min(baseIdx + 1, trajectoryPathPoints.Count - 1);
            float segFraction = pointProgress - baseIdx;

            Vector3 currentLeadPos = Vector3.Lerp(trajectoryPathPoints[baseIdx], trajectoryPathPoints[nextIdx], segFraction);

            // 🌟 3D 카툰 도약 포물선 높이 (4 * t * (1 - t)) 및 굵기 퐁퐁퐁 애니메이션
            float heightFactor = 4f * segFraction * (1f - segFraction);
            float dynamicLeadY = baseReplayLevel + 0.45f + (heightFactor * 4.5f);

            // 라인 렌더러 점 갱신
            while (drawnPoints.Count <= baseIdx)
            {
                drawnPoints.Add(trajectoryPathPoints[drawnPoints.Count]);
            }
            if (drawnPoints.Count == baseIdx + 1)
            {
                drawnPoints.Add(currentLeadPos);
            }
            else
            {
                drawnPoints[drawnPoints.Count - 1] = currentLeadPos;
            }

            trajectoryLine.positionCount = drawnPoints.Count;
            for (int p = 0; p < drawnPoints.Count; p++)
            {
                trajectoryLine.SetPosition(p, drawnPoints[p]);
            }

            // 돌 아바타 이동, 통통 튀는 높이, 회전
            if (replayStoneAvatar != null)
            {
                Vector3 segDir = (trajectoryPathPoints[nextIdx] - trajectoryPathPoints[baseIdx]).normalized;
                if (segDir.sqrMagnitude < 0.001f) segDir = Vector3.forward;

                float avatarBaseScale = Mathf.Clamp(currentOrthoSize * 0.18f, 2.5f, 25f);
                float jumpScale = avatarBaseScale * (1f + heightFactor * 0.8f);

                float vyFactor = (1f - 2f * segFraction);
                float pitchAngle = vyFactor * 32f;

                replayStoneAvatar.transform.position = new Vector3(currentLeadPos.x, dynamicLeadY, currentLeadPos.z);
                replayStoneAvatar.transform.localScale = new Vector3(jumpScale, jumpScale, jumpScale);
                replayStoneAvatar.transform.rotation = Quaternion.LookRotation(segDir, Vector3.up) * Quaternion.Euler(-pitchAngle, 0f, 0f);
            }

            // 카메라 리드 추적 (돌의 실제 X곡선 및 Z를 정밀 추종)
            if (dualCam != null)
            {
                currentCamCenter.x = Mathf.Clamp(currentLeadPos.x, boundMinX, boundMaxX);
                currentCamCenter.z = CalculateCameraZForLeadPosition(currentLeadPos.z);
                dualCam.SetReplayTopDownView(new Vector3(currentCamCenter.x, baseReplayLevel + 80f, currentCamCenter.z), currentOrthoSize);
                SyncTerrainByZ(currentCamCenter.z);
            }

            // 바운스 마커 스폰 체크 (돌이 바운스 거리를 통과할 때)
            while (nextMarkerIdx < markerRecords.Count - 1 && markerRecords[nextMarkerIdx].distance <= currentLeadPos.z)
            {
                SpawnBounceMarker(markerRecords[nextMarkerIdx], nextMarkerIdx);
                nextMarkerIdx++;
            }

            yield return null;
        }

        // 남은 모든 마커 및 최종 마커 스폰
        while (nextMarkerIdx < markerRecords.Count)
        {
            SpawnBounceMarker(markerRecords[nextMarkerIdx], nextMarkerIdx);
            nextMarkerIdx++;
        }

        // 전체 완성된 라인 깔끔히 설정
        trajectoryLine.positionCount = trajectoryPathPoints.Count;
        for (int p = 0; p < trajectoryPathPoints.Count; p++)
        {
            trajectoryLine.SetPosition(p, trajectoryPathPoints[p]);
        }

        // 종료 시 마지막 돌 위치에 카메라 안착
        if (dualCam != null && trajectoryPathPoints.Count > 0)
        {
            Vector3 lastPos = trajectoryPathPoints[trajectoryPathPoints.Count - 1];
            currentCamCenter.x = Mathf.Clamp(lastPos.x, boundMinX, boundMaxX);
            currentCamCenter.z = Mathf.Clamp(lastPos.z, boundMinZ, boundMaxZ);
            dualCam.SetReplayTopDownView(new Vector3(currentCamCenter.x, baseReplayLevel + 80f, currentCamCenter.z), currentOrthoSize);
            SyncTerrainByZ(currentCamCenter.z);
        }

        if (replayStoneAvatar != null && trajectoryPathPoints.Count > 0)
        {
            Vector3 lastPos = trajectoryPathPoints[trajectoryPathPoints.Count - 1];
            float avatarBaseScale = Mathf.Clamp(currentOrthoSize * 0.18f, 2.5f, 25f);
            replayStoneAvatar.transform.position = new Vector3(lastPos.x, baseReplayLevel + 0.45f, lastPos.z);
            replayStoneAvatar.transform.localScale = new Vector3(avatarBaseScale, avatarBaseScale, avatarBaseScale);
            replayStoneAvatar.transform.rotation = Quaternion.identity;
            replayStoneAvatar.SetActive(true);
        }

        isDrawing = false;
        isReplayFinished = true;
    }

    /// <summary>
    /// 🌟 판정 일치 마커 스폰 (PERFECT: 초록, GREAT: 하늘, GOOD: 노랑, EARLY/LATE: 주황/보라, RING: 빨흰 스트라이프)
    /// </summary>
    private void SpawnBounceMarker(SkippingStone.BounceRecord record, int index)
    {
        if (record.grade.Contains("RING_BOOST"))
        {
            SpawnStripedRandomRingMarker(record, index);
            return;
        }

        GameObject marker = new GameObject($"ReplayMarker_{index}_{record.grade}");
        marker.transform.SetParent(transform);
        Vector3 markerPos = new Vector3(record.position.x, baseReplayLevel + 0.12f, record.position.z);
        marker.transform.position = markerPos;

        LineRenderer lr = marker.AddComponent<LineRenderer>();
        Shader unlitShader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
        Material mat = new Material(unlitShader);

        Color mColor = GetMarkerColorByGrade(record.grade, index);

        float baseRadius = (index == 0) ? 18f : (index == markerRecords.Count - 1 || record.grade == "FINISH") ? 22f : 16f;
        float ringWidth = Mathf.Clamp(currentOrthoSize * 0.007f, 0.15f, 3.2f);

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

    /// <summary>
    /// 🌀 실제 인게임 랜덤 링처럼 빨강/하양이 교차하는 스트라이프 튜브 링 마커 스폰
    /// </summary>
    private void SpawnStripedRandomRingMarker(SkippingStone.BounceRecord record, int index)
    {
        GameObject ringRoot = new GameObject($"ReplayMarker_{index}_STRIPED_RING");
        ringRoot.transform.SetParent(transform);
        Vector3 markerPos = new Vector3(record.position.x, baseReplayLevel + 0.14f, record.position.z);
        ringRoot.transform.position = markerPos;

        Shader unlitShader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
        Material redMat = new Material(unlitShader) { color = new Color(1.0f, 0.15f, 0.25f, 1f) }; // 선명한 레드
        Material whiteMat = new Material(unlitShader) { color = Color.white };                         // 퓨어 화이트

        float baseRadius = 21f; // 일반 마커보다 큼직한 링 크기
        float ringWidth = Mathf.Clamp(currentOrthoSize * 0.009f, 0.22f, 4.0f);

        int segmentCount = 12; // 12개 호(Segment)로 분할하여 빨-흰 6회 반복 교차
        float degPerSegment = 360f / segmentCount;
        int subSteps = 6;

        for (int s = 0; s < segmentCount; s++)
        {
            GameObject arcObj = new GameObject($"Arc_{s}");
            arcObj.transform.SetParent(ringRoot.transform, false);

            LineRenderer lr = arcObj.AddComponent<LineRenderer>();
            lr.material = (s % 2 == 0) ? redMat : whiteMat;
            lr.startWidth = ringWidth;
            lr.endWidth = ringWidth;
            lr.useWorldSpace = false;
            lr.positionCount = subSteps + 1;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;

            float startDeg = s * degPerSegment;
            for (int sub = 0; sub <= subSteps; sub++)
            {
                float curDeg = startDeg + (degPerSegment * sub / subSteps);
                float rad = Mathf.Deg2Rad * curDeg;
                lr.SetPosition(sub, new Vector3(Mathf.Cos(rad) * baseRadius, 0f, Mathf.Sin(rad) * baseRadius));
            }
        }

        float markerScale = Mathf.Clamp(currentOrthoSize / 390f, 0.15f, 2.5f);
        ringRoot.transform.localScale = new Vector3(markerScale, 1f, markerScale);

        markerObjects.Add(ringRoot);

        if (gameObject.activeInHierarchy)
        {
            StartCoroutine(MarkerPopRoutine(ringRoot, markerScale, 0.20f));
        }
    }

    /// <summary>
    /// 🌟 판정 등급별 정밀 일치 색상 반환
    /// </summary>
    private Color GetMarkerColorByGrade(string grade, int index)
    {
        if (index == 0 || grade == "START") return startMarkerColor;
        if (index == markerRecords.Count - 1 || grade == "FINISH") return finishMarkerColor;

        if (grade.Contains("PERFECT")) return perfectMarkerColor;
        if (grade.Contains("GREAT")) return greatMarkerColor;
        if (grade.Contains("GOOD")) return goodMarkerColor;
        if (grade.Contains("EARLY") || grade.Contains("TOO EARLY")) return earlyLateMarkerColor;
        if (grade.Contains("LATE")) return Color.magenta;

        return bounceMarkerColor;
    }

    private IEnumerator MarkerPopRoutine(GameObject marker, float targetScale, float duration)
    {
        if (marker == null) yield break;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            if (marker == null) yield break;
            elapsed += Time.unscaledDeltaTime;
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