using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SkippingStones.Visuals.Replay;

/// <summary>
/// 🌟 탑다운 리플레이 메인 총괄 파사드 매니저
/// - 하위 모듈(DataSampler, CameraController, TrajectoryRenderer)을 조율
/// </summary>
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

    [Header("기준 높이")]
    public float baseReplayLevel = 0f;
    public Color pathColor = new Color(0.1f, 0.95f, 1.0f, 0.95f);

    // 🌟 독립 서브 모듈 인스턴스
    private readonly ReplayDataSampler sampler = new ReplayDataSampler();
    private readonly ReplayCameraController cameraController = new ReplayCameraController();
    private readonly ReplayTrajectoryRenderer trajectoryRenderer = new ReplayTrajectoryRenderer();

    private List<SkippingStone.BounceRecord> markerRecords = new List<SkippingStone.BounceRecord>();
    private List<Vector3> trajectoryPathPoints = new List<Vector3>();
    private Coroutine drawCoroutine;
    private float cachedFinalDist = 0f;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else if (Instance != this) { Destroy(gameObject); return; }

        if (gameController == null) gameController = FindAnyObjectByType<GameController>();
        if (stone == null) stone = FindAnyObjectByType<SkippingStone>();

        UpdateBaseReplayLevel();
        trajectoryRenderer.Initialize(transform, pathColor);
    }

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

    public void ResetRealtimeTrajectory()
    {
        UpdateBaseReplayLevel();
        Vector3 startOrigin = GetExactStartPlatformPosition();
        sampler.Reset(startOrigin);
    }

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

    public void SampleStonePosition(Vector3 pos, bool isRingBoost = false)
    {
        sampler.SamplePosition(pos, isRingBoost);
    }

    private void Update()
    {
        if (gameController == null) gameController = FindAnyObjectByType<GameController>();

        // 비행 중 실시간 위치 자동 샘플링
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

        // 카메라 자유 조작 갱신
        DualCameraSetup dualCam = (gameController != null && gameController.dualCamera != null)
                                  ? gameController.dualCamera
                                  : FindAnyObjectByType<DualCameraSetup>();

        cameraController.UpdateNavigation(dualCam, baseReplayLevel, SyncTerrainByZ, trajectoryRenderer.UpdateVisualsScale);
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

        // 1. 바운스 기록 취득
        List<SkippingStone.BounceRecord> rawBounces = null;
        if (stone != null && stone.bounceHistory != null && stone.bounceHistory.Count > 0)
        {
            rawBounces = stone.bounceHistory;
        }
        else
        {
            var arcadeStone = FindAnyObjectByType<SkippingStones.Arcade.ArcadeSkippingStone>();
            if (arcadeStone != null && arcadeStone.bounceHistory != null && arcadeStone.bounceHistory.Count > 0)
            {
                rawBounces = arcadeStone.bounceHistory;
            }
        }

        Vector3 startOrigin = GetExactStartPlatformPosition();
        markerRecords = sampler.BuildMarkerRecords(rawBounces, startOrigin, finalDist);
        trajectoryPathPoints = sampler.BuildTrajectoryPathPoints(markerRecords, baseReplayLevel);

        // 🌟 맵 및 물 전체 사전 100% 스폰
        float pageDist = (LakeEnvironmentManager.Instance != null && LakeEnvironmentManager.Instance.autoChunkSize > 50f) ? LakeEnvironmentManager.Instance.autoChunkSize : 500f;
        int totalPages = Mathf.Max(1, Mathf.CeilToInt(finalDist / pageDist));

        if (LakeEnvironmentManager.Instance != null)
        {
            LakeEnvironmentManager.Instance.PlaceTerrainAtPage(totalPages + 1);
        }
        var waterSurface = FindAnyObjectByType<WaterSurface>();
        if (waterSurface != null)
        {
            waterSurface.PlaceWaterAtPage(totalPages + 1);
        }

        StoneThrowerCharacter thrower = FindAnyObjectByType<StoneThrowerCharacter>();
        if (thrower != null) thrower.RestoreVisibility();

        Transform platform = GameController.FindPlatformInScene();
        if (platform != null)
        {
            platform.gameObject.SetActive(true);
            var pr = platform.GetComponent<Renderer>();
            if (pr != null) pr.enabled = true;
        }

        // 돌 아바타 모델 로드
        GameObject stonePrefab = GetStonePrefab();
        trajectoryRenderer.CreateReplayStoneAvatar(transform, stonePrefab);

        // 카메라 초기 위치 설정
        cameraController.Initialize(new Vector3(startOrigin.x, baseReplayLevel + 80f, startOrigin.z + 15f), 32f);

        isReplayActive = true;
        isReplayFinished = false;

        StartDrawingAnimation();
    }

    private GameObject GetStonePrefab()
    {
        if (gameController != null && gameController.defaultStonePrefab != null) return gameController.defaultStonePrefab;
        if (SkippingStones.Data.GameDataManager.Instance != null)
        {
            var dm = SkippingStones.Data.GameDataManager.Instance;
            string selectedId = dm.UserData != null ? dm.UserData.selectedStoneId : "default";
            var stoneInfo = dm.stoneCatalog.Find(s => s.id == selectedId || (s.prefabPath != null && s.prefabPath.Contains(selectedId)));
            if (stoneInfo != null && !string.IsNullOrEmpty(stoneInfo.prefabPath))
            {
#if UNITY_EDITOR
                return UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(stoneInfo.prefabPath);
#else
                string rPath = stoneInfo.prefabPath.Replace("Assets/prefab/", "").Replace(".prefab", "");
                return Resources.Load<GameObject>(rPath);
#endif
            }
        }
#if UNITY_EDITOR
        return UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/prefab/Stone/Stone.prefab");
#else
        return Resources.Load<GameObject>("Stone/Stone");
#endif
    }

    public void StartDrawingAnimation()
    {
        if (drawCoroutine != null) StopCoroutine(drawCoroutine);
        trajectoryRenderer.ClearMarkers();

        drawCoroutine = StartCoroutine(DrawTrajectoryRoutineInternal());
    }

    private IEnumerator DrawTrajectoryRoutineInternal()
    {
        isDrawing = true;
        isReplayFinished = false;

        DualCameraSetup dualCam = (gameController != null && gameController.dualCamera != null)
                                  ? gameController.dualCamera
                                  : FindAnyObjectByType<DualCameraSetup>();

        yield return trajectoryRenderer.DrawTrajectoryRoutine(
            trajectoryPathPoints, 
            markerRecords, 
            baseReplayLevel, 
            cachedFinalDist, 
            transform, 
            dualCam, 
            cameraController, 
            SyncTerrainByZ,
            pathColor);

        isDrawing = false;
        isReplayFinished = true;
    }

    public void ReplayAgain()
    {
        StartDrawingAnimation();
    }

    public void FinishReplayAndShowResult()
    {
        if (drawCoroutine != null) StopCoroutine(drawCoroutine);
        isDrawing = false;
        isReplayFinished = true;
        isReplayActive = false;

        if (gameController != null)
        {
            gameController.ShowFinalResultDirect(cachedFinalDist);
        }
    }

    public void ZoomIn(float ratio = 0.82f)
    {
        cameraController.TargetOrthoSize = Mathf.Clamp(cameraController.TargetOrthoSize * ratio, cameraController.MinOrthoSize, cameraController.MaxOrthoSize);
    }

    public void ZoomOut(float ratio = 1.22f)
    {
        cameraController.TargetOrthoSize = Mathf.Clamp(cameraController.TargetOrthoSize * ratio, cameraController.MinOrthoSize, cameraController.MaxOrthoSize);
    }

    private void SyncTerrainByZ(float centerZ)
    {
        if (LakeEnvironmentManager.Instance != null)
        {
            LakeEnvironmentManager.Instance.UpdateEnvironmentByDistance(centerZ);
        }
    }
}
