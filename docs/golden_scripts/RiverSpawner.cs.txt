using UnityEngine;
using System.Collections.Generic;
using SkippingStones.Gameplay.Spawners;

public class RiverSpawner : MonoBehaviour
{
    [Header("스폰 설정")]
    public float riverLength = 4800f;
    public float riverWidth = 50f;
    public float riverWaterMinX = -28f;
    public float riverWaterMaxX = 28f;
    public Vector3 startBankPos = Vector3.zero;
    public Vector3 spawnDirection = Vector3.forward;

    [Header("레이캐스트 검증")]
    [Tooltip("땅속 스폰 방지용 레이캐스트 대상 레이어 (Default 체크)")]
    public LayerMask groundLayerMask = 1;
    public float raycastHeight = 20f;

    public float startBankX { get => startBankPos.x; set => startBankPos.x = value; }
    public float bankZ { get => startBankPos.z; set => startBankPos.z = value; }

    [Header("스폰 프리팹 슬롯 (인스펙터 드래그&드롭)")]
    [Tooltip("가속 부스트 패드 프리팹")]
    public GameObject boostPadPrefab;
    [Tooltip("🌀 리듬 아케이드 전용 랜덤 링 프리팹")]
    public GameObject randomRingPrefab;
    [Tooltip("장애물 바위 프리팹")]
    public GameObject obstacleRockPrefab;
    [Tooltip("타겟 과녁 프리팹")]
    public GameObject targetZonePrefab;
    [Tooltip("친구 랭킹 깃발 프리팹")]
    public GameObject friendFlagPrefab;
    [Tooltip("토종 강물고기 10종 프리팹 배열")]
    public GameObject[] fishPrefabs = new GameObject[10];
    [Tooltip("연잎/연꽃 군락 프리팹")]
    public GameObject lilyPadClusterPrefab;

    private readonly RiverEntityFactory entityFactory = new RiverEntityFactory();
    private GameController.GameMode currentSpawningMode = GameController.GameMode.LongDistance;

    private void Awake()
    {
        SyncFactoryPrefabs();
    }

    private void SyncFactoryPrefabs()
    {
        entityFactory.boostPadPrefab = boostPadPrefab;
        entityFactory.randomRingPrefab = randomRingPrefab;
        entityFactory.obstacleRockPrefab = obstacleRockPrefab;
        entityFactory.targetZonePrefab = targetZonePrefab;
        entityFactory.friendFlagPrefab = friendFlagPrefab;
        entityFactory.fishPrefabs = fishPrefabs;
        entityFactory.lilyPadClusterPrefab = lilyPadClusterPrefab;
    }

    private void Start()
    {
        if (LakeEnvironmentManager.Instance != null)
        {
            LakeEnvironmentManager.Instance.OnChunkSpawned += HandleChunkSpawned;
            LakeEnvironmentManager.Instance.OnChunkRelayed += SpawnChunkEntities;
        }
    }

    private void OnDestroy()
    {
        if (LakeEnvironmentManager.Instance != null)
        {
            LakeEnvironmentManager.Instance.OnChunkSpawned -= HandleChunkSpawned;
            LakeEnvironmentManager.Instance.OnChunkRelayed -= SpawnChunkEntities;
        }
    }

    private void HandleChunkSpawned(int chunkIndex, GameObject chunkObj, float spawnZ)
    {
        SpawnChunkEntities(chunkIndex, chunkObj, spawnZ);
    }

    public void GenerateRiverEntitiesForMode(GameController.GameMode mode)
    {
        currentSpawningMode = mode;
        if (mode == GameController.GameMode.TargetAccuracy)
        {
            GenerateTargetAccuracyRiver();
        }
        else
        {
            GenerateLongDistanceRiver();
        }
    }

    public void GenerateRiverEntities()
    {
        var gc = FindAnyObjectByType<GameController>();
        if (gc != null && gc.currentMode == GameController.GameMode.TargetAccuracy)
        {
            GenerateTargetAccuracyRiver();
        }
        else
        {
            GenerateLongDistanceRiver();
        }
    }

    public bool GetWaterColliderBounds(out float minX, out float maxX, out float minZ, out float maxZ, out float waterY)
    {
        BoxCollider bc = null;
        WaterSurface ws = FindAnyObjectByType<WaterSurface>();
        if (ws != null)
        {
            bc = ws.GetComponent<BoxCollider>();
            waterY = (bc != null) ? bc.bounds.max.y : ws.transform.position.y;
        }
        else
        {
            GameObject waterObj = GameObject.Find("WaterSurface") ?? GameObject.Find("Water_Surface") ?? GameObject.Find("RS_Surface");
            if (waterObj != null)
            {
                bc = waterObj.GetComponent<BoxCollider>();
                waterY = (bc != null) ? bc.bounds.max.y : waterObj.transform.position.y;
            }
            else
            {
                waterY = 0f;
            }
        }

        if (bc != null)
        {
            Bounds b = bc.bounds;
            minX = b.min.x;
            maxX = b.max.x;
            minZ = b.min.z;
            maxZ = b.max.z;
            return true;
        }

        minX = -100f;
        maxX = 100f;
        minZ = 0f;
        maxZ = riverLength;
        return false;
    }

    public bool GetWaterColliderBounds(out float minX, out float maxX, out float waterY)
    {
        return GetWaterColliderBounds(out minX, out maxX, out _, out _, out waterY);
    }

    private float GetCurrentWaterLevel()
    {
        WaterSurface ws = FindAnyObjectByType<WaterSurface>();
        if (ws != null)
        {
            BoxCollider bc = ws.GetComponent<BoxCollider>();
            return (bc != null) ? bc.bounds.max.y : ws.transform.position.y;
        }
        GetWaterColliderBounds(out _, out _, out float waterY);
        return waterY;
    }

    private void GenerateLongDistanceRiver()
    {
        ClearExistingEntities();
        Physics.SyncTransforms();

        if (SkippingStones.Terrain.GlobalRiverPath.Instance != null)
        {
            SkippingStones.Terrain.GlobalRiverPath.Instance.RebuildPath();
        }

        if (LakeEnvironmentManager.Instance != null && LakeEnvironmentManager.Instance.DynamicChunks != null && LakeEnvironmentManager.Instance.DynamicChunks.Count > 0)
        {
            for (int i = 0; i < LakeEnvironmentManager.Instance.DynamicChunks.Count; i++)
            {
                var chunk = LakeEnvironmentManager.Instance.DynamicChunks[i];
                if (chunk != null)
                {
                    float z = chunk.transform.position.z;
                    SpawnChunkEntities(i, chunk, z);
                }
            }
            return;
        }

        SpawnChunkEntities(0, null, 0f);
    }

    private void GenerateTargetAccuracyRiver()
    {
        ClearExistingEntities();
        SyncFactoryPrefabs();

        if (SkippingStones.Terrain.GlobalRiverPath.Instance != null)
        {
            SkippingStones.Terrain.GlobalRiverPath.Instance.RebuildPath();
        }

        GetWaterColliderBounds(out float minX, out float maxX, out float minZ, out float maxZ, out float curWaterY);
        float endZ = maxZ;
        startBankPos = new Vector3((minX + maxX) * 0.5f, curWaterY, minZ);
        spawnDirection = Vector3.forward;

        float[] targetLanes = { minX * 0.7f, minX * 0.3f, 0f, maxX * 0.3f, maxX * 0.7f };
        for (float z = minZ + 50f; z < endZ - 50f; z += 55f)
        {
            for (int col = 0; col < targetLanes.Length; col++)
            {
                if (Random.value < 0.55f)
                {
                    float xPos = targetLanes[col] + Random.Range(-3f, 3f);
                    float zPos = z + Random.Range(-12f, 12f);
                    entityFactory.CreateTargetRing(transform, new Vector3(xPos, curWaterY + 0.04f, zPos));
                }
            }
        }

        for (float z = minZ + 50f; z < endZ - 60f; z += 60f)
        {
            float x1 = Random.Range(minX * 0.8f, minX * 0.2f);
            float x2 = Random.Range(maxX * 0.2f, maxX * 0.8f);
            entityFactory.CreateBoostPad(transform, new Vector3(x1, curWaterY + 0.05f, z + Random.Range(-10f, 10f)), Quaternion.identity);
            entityFactory.CreateBoostPad(transform, new Vector3(x2, curWaterY + 0.05f, z + Random.Range(-10f, 10f)), Quaternion.identity);
        }

        for (float z = minZ + 45f; z < endZ - 50f; z += 45f)
        {
            float xPos = Random.Range(minX * 0.85f, maxX * 0.85f);
            entityFactory.SpawnSingleFish(transform, new Vector3(xPos, curWaterY, z + Random.Range(-10f, 10f)), z);
        }

        for (float z = minZ + 60f; z < endZ - 50f; z += 50f)
        {
            float xPos = Random.Range(minX * 0.9f, maxX * 0.9f);
            entityFactory.CreateObstacleRock(transform, new Vector3(xPos, curWaterY, z + Random.Range(-12f, 12f)));
        }

        string[] friends = { "라이언 (3위)", "어피치 (2위)", "프로도 (1위)", "콘 (전설)" };
        float[] friendZDistances = { 120f, 310f, 580f, 920f, 1200f };
        for (int i = 0; i < friends.Length; i++)
        {
            float zPos = friendZDistances[i];
            if (zPos < minZ + 40f || zPos > endZ - 40f) continue;
            float xSide = (i % 2 == 0) ? minX * 0.6f : maxX * 0.6f;
            entityFactory.CreateFriendFlag(transform, new Vector3(xSide, curWaterY, zPos), friends[i], $"{i + 1}위", zPos);
        }

        CreateLilyPadsGrid(minX, maxX, minZ + 20f, endZ - 20f, curWaterY);
    }

    private void ClearExistingEntities()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            SafeDestroy(transform.GetChild(i).gameObject);
        }
    }

    public void SpawnChunkEntities(float chunkStartZ)
    {
        SpawnChunkEntities(-1, null, chunkStartZ);
    }

    public void SpawnChunkEntities(int chunkIndex, GameObject chunkObj, float chunkStartZ)
    {
        Physics.SyncTransforms();
        SyncFactoryPrefabs();

        float chunkSize = (LakeEnvironmentManager.Instance != null && LakeEnvironmentManager.Instance.autoChunkSize > 10f)
            ? LakeEnvironmentManager.Instance.autoChunkSize
            : 500f;

        float chunkEndZ = chunkStartZ + chunkSize;
        float curWaterY = GetCurrentWaterLevel();

        float curveStartDist = chunkStartZ;
        float curveEndDist = chunkEndZ;
        bool hasRiverPath = false;

        if (SkippingStones.Terrain.GlobalRiverPath.Instance != null)
        {
            if (chunkObj != null && SkippingStones.Terrain.GlobalRiverPath.Instance.GetSegmentDistanceRange(chunkObj, out float sDist, out float eDist))
            {
                curveStartDist = sDist;
                curveEndDist = eDist;
                hasRiverPath = true;
            }
            else if (chunkIndex >= 0 && SkippingStones.Terrain.GlobalRiverPath.Instance.GetSegmentDistanceRangeByIndex(chunkIndex, out float isDist, out float ieDist))
            {
                curveStartDist = isDist;
                curveEndDist = ieDist;
                hasRiverPath = true;
            }
            else if (SkippingStones.Terrain.GlobalRiverPath.Instance.GetSegmentDistanceRange(chunkStartZ, out float zsDist, out float zeDist))
            {
                curveStartDist = zsDist;
                curveEndDist = zeDist;
                hasRiverPath = true;
            }
        }

        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Transform child = transform.GetChild(i);
            if (child != null)
            {
                float cz = child.position.z;
                if (cz >= chunkStartZ - 35f && cz < chunkEndZ + 35f)
                    SafeDestroy(child.gameObject);
            }
        }

        GetWaterColliderBounds(out float minX, out float maxX, out curWaterY);

        bool isRhythmArcade = (currentSpawningMode == GameController.GameMode.RhythmArcade);
        if (!isRhythmArcade)
        {
            var gc = FindAnyObjectByType<GameController>();
            if (gc != null && gc.currentMode == GameController.GameMode.RhythmArcade)
            {
                isRhythmArcade = true;
                currentSpawningMode = GameController.GameMode.RhythmArcade;
            }
        }

        RiverChunkPlacementStrategy.PlaceChunkEntities(
            transform,
            entityFactory,
            curveStartDist,
            curveEndDist,
            hasRiverPath,
            minX,
            maxX,
            curWaterY,
            isRhythmArcade
        );

        if (!hasRiverPath)
        {
            CreateLilyPadsGrid(minX, maxX, chunkStartZ + 20f, chunkEndZ - 20f, curWaterY);
        }
    }

    private void CreateLilyPadsGrid(float minX, float maxX, float minZ, float maxZ, float waterY)
    {
        for (float z = minZ; z < maxZ; z += Random.Range(20f, 35f))
        {
            float x1 = Random.Range(minX, Mathf.Lerp(minX, maxX, 0.35f));
            float x2 = Random.Range(Mathf.Lerp(minX, maxX, 0.35f), Mathf.Lerp(minX, maxX, 0.65f));
            float x3 = Random.Range(Mathf.Lerp(minX, maxX, 0.65f), maxX);

            Vector3 p1 = new Vector3(x1, waterY + 0.04f, z + Random.Range(-5f, 5f));
            Vector3 p2 = new Vector3(x2, waterY + 0.04f, z + Random.Range(-5f, 5f));
            Vector3 p3 = new Vector3(x3, waterY + 0.04f, z + Random.Range(-5f, 5f));

            if (RiverWaterValidator.IsValidWaterPosition(p1, waterY, false)) entityFactory.SpawnSingleLilyCluster(transform, p1);
            if (RiverWaterValidator.IsValidWaterPosition(p2, waterY, false)) entityFactory.SpawnSingleLilyCluster(transform, p2);
            if (RiverWaterValidator.IsValidWaterPosition(p3, waterY, false)) entityFactory.SpawnSingleLilyCluster(transform, p3);
        }
    }

    private static void SafeDestroy(UnityEngine.Object obj)
    {
        if (obj == null) return;
        if (Application.isPlaying) Destroy(obj);
        else DestroyImmediate(obj);
    }
}