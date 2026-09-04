using UnityEngine;
using System.Collections.Generic;

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
    public LayerMask groundLayerMask = 1; // Default layer
    public float raycastHeight = 20f;      // 위에서 쏘는 레이 시작 높이

    public float startBankX { get => startBankPos.x; set => startBankPos.x = value; }
    public float bankZ { get => startBankPos.z; set => startBankPos.z = value; }

    private void Start()
    {
        // 타이틀/로비에서는 아무런 스폰도 실행하지 않고 대기.
        // BG 청크 릴레이 콜백 구독: 인게임 중 새 청크 구간이 스폰/도킹될 때 해당 구간 엔티티 재스폰
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
            // LongDistance 및 RhythmArcade 모드는 강줄기 장거리 엔티티 스폰 (타깃 부표 제외)
            GenerateLongDistanceRiver();
        }
    }

    private GameController.GameMode currentSpawningMode = GameController.GameMode.LongDistance;

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

    /// <summary>
    /// 수면 오브젝트(WaterSurface / Water_Surface)로부터 실시간 가로폭(minX, maxX), 세로길이(minZ, maxZ)와 수면 높이(waterY)를 100% 신뢰성 있게 획득
    /// </summary>
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

        // BoxCollider가 없을 경우 안전 폴백 (Transform / default 0)
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

    /// <summary>
    /// 🏆 장거리 & 리듬 아케이드 모드: 활성화된 모든 청크의 물길 구간에 엔티티 일괄 스폰
    /// </summary>
    private void GenerateLongDistanceRiver()
    {
        ClearExistingEntities();
        Physics.SyncTransforms();

        // 🌟 [핵심] 청크가 로드/재구성된 상태에서 글로벌 스플라인 체인 최신화 보장
        if (SkippingStones.Terrain.GlobalRiverPath.Instance != null)
        {
            SkippingStones.Terrain.GlobalRiverPath.Instance.RebuildPath();
        }

        // 🌟 LakeEnvironmentManager에 로드된 모든 청크(0번, 1번, 2번...)에 대해 일괄 스폰 실행
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

        // 단일 청크 폴백
        SpawnChunkEntities(0, null, 0f);
    }

    /// <summary>
    /// 🎯 타겟 맞추기 모드: 호수 전체 수면 고른 전역 랜덤 배치
    /// </summary>
    private void GenerateTargetAccuracyRiver()
    {
        ClearExistingEntities();

        if (SkippingStones.Terrain.GlobalRiverPath.Instance != null)
        {
            SkippingStones.Terrain.GlobalRiverPath.Instance.RebuildPath();
        }

        GetWaterColliderBounds(out float minX, out float maxX, out float minZ, out float maxZ, out float curWaterY);
        float endZ = maxZ;
        startBankPos = new Vector3((minX + maxX) * 0.5f, curWaterY, minZ);
        spawnDirection = Vector3.forward;

        // 1. 🎯 플로팅 타겟 과녁 (Floating Target Rings) 수면 전체 분산 배치
        float[] targetLanes = { minX * 0.7f, minX * 0.3f, 0f, maxX * 0.3f, maxX * 0.7f };
        for (float z = minZ + 50f; z < endZ - 50f; z += 55f)
        {
            for (int col = 0; col < targetLanes.Length; col++)
            {
                if (Random.value < 0.55f)
                {
                    float xPos = targetLanes[col] + Random.Range(-3f, 3f);
                    float zPos = z + Random.Range(-12f, 12f);
                    CreateTargetRing(new Vector3(xPos, curWaterY + 0.04f, zPos));
                }
            }
        }

        // 2. 🚀 가속 부스트 패드 수면 전역 배치
        for (float z = minZ + 50f; z < endZ - 60f; z += 60f)
        {
            float x1 = Random.Range(minX * 0.8f, minX * 0.2f);
            float x2 = Random.Range(maxX * 0.2f, maxX * 0.8f);
            CreateBoostPad(new Vector3(x1, curWaterY + 0.05f, z + Random.Range(-10f, 10f)), Quaternion.identity);
            CreateBoostPad(new Vector3(x2, curWaterY + 0.05f, z + Random.Range(-10f, 10f)), Quaternion.identity);
        }

        // 3. 🐟 튀어오르는 물고기 (Jumping Fish) 수면 전역 배치
        for (float z = minZ + 45f; z < endZ - 50f; z += 45f)
        {
            float xPos = Random.Range(minX * 0.85f, maxX * 0.85f);
            SpawnSingleFish(new Vector3(xPos, curWaterY, z + Random.Range(-10f, 10f)), z);
        }

        // 4. 🪨 장애물 바위 수면 전역 배치
        for (float z = minZ + 60f; z < endZ - 50f; z += 50f)
        {
            float xPos = Random.Range(minX * 0.9f, maxX * 0.9f);
            CreateObstacleRock(new Vector3(xPos, curWaterY, z + Random.Range(-12f, 12f)));
        }

        // 5. 🚩 친구 거리 깃발
        string[] friends = { "라이언 (3위)", "어피치 (2위)", "프로도 (1위)", "콘 (전설)" };
        float[] friendZDistances = { 120f, 310f, 580f, 920f, 1200f };
        for (int i = 0; i < friends.Length; i++)
        {
            float zPos = friendZDistances[i];
            if (zPos < minZ + 40f || zPos > endZ - 40f) continue;
            float xSide = (i % 2 == 0) ? minX * 0.6f : maxX * 0.6f;
            CreateFriendFlag(new Vector3(xSide, curWaterY, zPos), friends[i], $"{i + 1}위", zPos);
        }

        // 6. 🪷 풍성한 연잎 및 연꽃 군락
        CreateLilyPadsGrid(minX, maxX, minZ + 20f, endZ - 20f, curWaterY);
        CleanupOldGroundObjects();
    }

    private void ClearExistingEntities()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            SafeDestroy(transform.GetChild(i).gameObject);
        }
    }

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

    private void EnsurePrefabsLoaded()
    {
#if UNITY_EDITOR
        if (boostPadPrefab == null) boostPadPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/prefab/BG_Deco/BoostPad.prefab");
        if (randomRingPrefab == null) randomRingPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/3D/Ingame_Object/Random_Ring.fbx");
        if (obstacleRockPrefab == null) obstacleRockPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/prefab/BG_Deco/ObstacleRock.prefab");
        if (targetZonePrefab == null) targetZonePrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/prefab/BG_Deco/TargetZone.prefab");
        if (friendFlagPrefab == null) friendFlagPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/prefab/BG_Deco/FriendFlag.prefab");
        if (lilyPadClusterPrefab == null) lilyPadClusterPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/prefab/BG_Deco/LilyPadCluster.prefab");

        for (int i = 1; i <= 10; i++)
        {
            if (fishPrefabs[i - 1] == null)
            {
                string p = $"Assets/Resources/FishPrefabs/River_Fish_{i:D2}.prefab";
                fishPrefabs[i - 1] = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(p);
            }
        }
#endif
        if (boostPadPrefab == null) boostPadPrefab = Resources.Load<GameObject>("BoostPad");
        if (randomRingPrefab == null) randomRingPrefab = Resources.Load<GameObject>("Random_Ring");
        if (obstacleRockPrefab == null) obstacleRockPrefab = Resources.Load<GameObject>("ObstacleRock");
        if (targetZonePrefab == null) targetZonePrefab = Resources.Load<GameObject>("TargetZone");
        if (friendFlagPrefab == null) friendFlagPrefab = Resources.Load<GameObject>("FriendFlag");
        if (lilyPadClusterPrefab == null) lilyPadClusterPrefab = Resources.Load<GameObject>("LilyPadCluster");

        for (int i = 1; i <= 10; i++)
        {
            if (fishPrefabs[i - 1] == null)
            {
                fishPrefabs[i - 1] = Resources.Load<GameObject>($"FishPrefabs/River_Fish_{i:D2}");
            }
        }
    }

    private void CreateTargetRing(Vector3 pos)
    {
        EnsurePrefabsLoaded();
        if (targetZonePrefab != null)
        {
            GameObject obj = Instantiate(targetZonePrefab, pos, Quaternion.identity, transform);
            obj.name = $"TargetZone_{pos.x:F0}x{pos.z:F0}";
        }
        else
        {
            GameObject ring = new GameObject($"TargetZone_{pos.x:F0}x{pos.z:F0}");
            ring.transform.SetParent(transform);
            ring.transform.position = pos;
            ring.AddComponent<FloatingTargetZone>();
        }
    }

    private void CreateBoostPad(Vector3 pos, Quaternion rot)
    {
        EnsurePrefabsLoaded();
        if (boostPadPrefab != null)
        {
            GameObject obj = Instantiate(boostPadPrefab, pos, rot, transform);
            obj.name = $"BoostPad_{pos.x:F0}x{pos.z:F0}";
        }
        else
        {
            GameObject pad = new GameObject($"BoostPad_{pos.x:F0}x{pos.z:F0}");
            pad.transform.SetParent(transform);
            pad.transform.position = pos;
            pad.transform.rotation = rot;
            pad.AddComponent<BoostPad>();
        }
    }

    private void CreateRandomRing(Vector3 pos, Quaternion rot)
    {
        EnsurePrefabsLoaded();
        GameObject ringObj = new GameObject($"RandomRing_{pos.x:F0}x{pos.z:F0}");
        ringObj.transform.SetParent(transform);
        ringObj.transform.position = pos;
        ringObj.transform.rotation = rot;
        ringObj.AddComponent<SkippingStones.Arcade.RandomRing>();
    }

    private void CreateObstacleRock(Vector3 pos)
    {
        EnsurePrefabsLoaded();
        if (obstacleRockPrefab != null)
        {
            GameObject obj = Instantiate(obstacleRockPrefab, pos, Quaternion.identity, transform);
            obj.name = $"ObstacleRock_{pos.x:F0}x{pos.z:F0}";
        }
        else
        {
            GameObject rock = new GameObject($"ObstacleRock_{pos.x:F0}x{pos.z:F0}");
            rock.transform.SetParent(transform);
            rock.transform.position = pos;
            rock.AddComponent<ObstacleRock>();
        }
    }

    private void CreateFriendFlag(Vector3 pos, string name, string rank, float targetDist)
    {
        EnsurePrefabsLoaded();
        GameObject flagObj;
        if (friendFlagPrefab != null)
        {
            flagObj = Instantiate(friendFlagPrefab, pos, Quaternion.identity, transform);
            flagObj.name = $"FriendFlag_{name}_{pos.z:F0}";
        }
        else
        {
            flagObj = new GameObject($"FriendFlag_{name}_{pos.z:F0}");
            flagObj.transform.SetParent(transform);
            flagObj.transform.position = pos;
            flagObj.AddComponent<FriendFlag>();
        }

        FriendFlag ff = flagObj.GetComponent<FriendFlag>();
        if (ff != null)
        {
            ff.friendName = name;
            ff.rankText = rank;
            ff.targetDistance = targetDist;
        }
    }

    private void SpawnSingleFish(Vector3 pos, float dist)
    {
        EnsurePrefabsLoaded();

        // 10종 물고기 프리팹 중 거리와 랜덤성을 고려하여 선택
        // 앞쪽은 소/중형 어종(1~5번), 뒤쪽으로 갈수록 대형 어종(6~10번) 확률 증가
        int minFishIdx = 0;
        int maxFishIdx = 9;

        if (dist < 150f)
        {
            minFishIdx = 0; // 버들치
            maxFishIdx = 3; // 은어
        }
        else if (dist < 400f)
        {
            minFishIdx = 1; // 피라미
            maxFishIdx = 6; // 쏘가리
        }
        else if (dist < 800f)
        {
            minFishIdx = 3; // 은어
            maxFishIdx = 8; // 무지개송어
        }
        else
        {
            minFishIdx = 4; // 산천어
            maxFishIdx = 9; // 강준치
        }

        int chosenIdx = Random.Range(minFishIdx, maxFishIdx + 1);
        GameObject chosenPrefab = (fishPrefabs != null && chosenIdx < fishPrefabs.Length) 
            ? fishPrefabs[chosenIdx] 
            : null;

        GameObject fishObj;
        if (chosenPrefab != null)
        {
            fishObj = Instantiate(chosenPrefab, pos, Quaternion.identity, transform);
            fishObj.name = $"Fish_{chosenIdx + 1:D2}_{pos.x:F0}x{pos.z:F0}";
        }
        else
        {
            fishObj = new GameObject($"Fish_{chosenIdx + 1:D2}_{pos.x:F0}x{pos.z:F0}");
            fishObj.transform.SetParent(transform);
            fishObj.transform.position = pos;
            fishObj.AddComponent<JumpingFish>();
        }

        JumpingFish jf = fishObj.GetComponent<JumpingFish>();
        if (jf != null)
        {
            FishSpeciesData preset = FishPresetDatabase.GetPreset(chosenIdx + 1);
            jf.fishIndex = preset.index;
            jf.speciesId = preset.id;
            jf.speciesName = preset.nameKor;
            jf.jumpHeight = Random.Range(preset.minJumpHeight, preset.maxJumpHeight);
            jf.jumpDuration = preset.jumpDuration;
            // 🐟 개체별 1.0 ~ 2.5 범위의 시원하고 눈에 잘 띄는 스케일 적용
            float randomVariation = Random.Range(0.95f, 1.1f);
            jf.scaleFactor = Mathf.Clamp(preset.scaleFactor * randomVariation, 1.0f, 2.6f);
            jf.rewardCoins = preset.rewardCoins;
        }
    }

    private void CreateLilyPadsGrid(float minX, float maxX, float minZ, float maxZ, float waterY)
    {
        EnsurePrefabsLoaded();
        for (float z = minZ; z < maxZ; z += Random.Range(20f, 35f))
        {
            float x1 = Random.Range(minX, Mathf.Lerp(minX, maxX, 0.35f));
            float x2 = Random.Range(Mathf.Lerp(minX, maxX, 0.35f), Mathf.Lerp(minX, maxX, 0.65f));
            float x3 = Random.Range(Mathf.Lerp(minX, maxX, 0.65f), maxX);

            Vector3 p1 = new Vector3(x1, waterY + 0.04f, z + Random.Range(-5f, 5f));
            Vector3 p2 = new Vector3(x2, waterY + 0.04f, z + Random.Range(-5f, 5f));
            Vector3 p3 = new Vector3(x3, waterY + 0.04f, z + Random.Range(-5f, 5f));

            if (IsValidWaterPosition(p1)) SpawnSingleLilyCluster(p1);
            if (IsValidWaterPosition(p2)) SpawnSingleLilyCluster(p2);
            if (IsValidWaterPosition(p3)) SpawnSingleLilyCluster(p3);
        }
    }

    private void SpawnSingleLilyCluster(Vector3 centerPos)
    {
        EnsurePrefabsLoaded();
        if (lilyPadClusterPrefab != null)
        {
            GameObject cluster = Instantiate(lilyPadClusterPrefab, centerPos, Quaternion.identity, transform);
            cluster.name = $"LilyCluster_{centerPos.x:F0}x{centerPos.z:F0}";
        }
    }



    private void CleanupOldGroundObjects()
    {
        var allObjects = Resources.FindObjectsOfTypeAll<GameObject>();
        foreach (var obj in allObjects)
        {
            if (obj != null && obj.name.StartsWith("PineTree_Sunset"))
            {
                SafeDestroy(obj);
            }
        }
    }

    // ─────────────────────────────────────────────────────────────────────
    // 🌊 BG 청크 릴레이 연동: 새 1500m 수면 구간 동적 엔티티 재스폰
    // ─────────────────────────────────────────────────────────────────────

    /// <summary>
    /// 🌟 새 맵 청크가 생성(스트리밍)될 때 호출됨.
    /// 새로 생성된 chunkStartZ ~ chunkStartZ+chunkSize 구간에만 정확히 엔티티 스폰.
    /// </summary>
    /// <summary>
    /// 🌟 새 맵 청크가 생성(스트리밍)될 때 호출됨.
    /// 해당 청크 객체의 베이킹된 곡선 구간(startDist ~ endDist)에 엔티티 정밀 스폰.
    /// </summary>
    public void SpawnChunkEntities(float chunkStartZ)
    {
        SpawnChunkEntities(-1, null, chunkStartZ);
    }

    public void SpawnChunkEntities(int chunkIndex, GameObject chunkObj, float chunkStartZ)
    {
        Physics.SyncTransforms();

        float chunkSize = (LakeEnvironmentManager.Instance != null && LakeEnvironmentManager.Instance.autoChunkSize > 10f)
            ? LakeEnvironmentManager.Instance.autoChunkSize
            : 500f;

        float chunkEndZ = chunkStartZ + chunkSize;
        float curWaterY = GetCurrentWaterLevel();

        // 🌟 [핵심] 스플라인 곡선 거리 획득:
        // chunkObj 또는 chunkIndex를 통해 해당 청크의 실제 스플라인 시작~끝 거리를 100% 정밀 매칭
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

        // 해당 청크 범위의 기존 엔티티 제거
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

        // Water_Surface BoxCollider로부터 실제 수면 가로폭 및 높이 동적 획득
        GetWaterColliderBounds(out float minX, out float maxX, out curWaterY);

        // 1. 🚀 가속 부스트 패드 / 랜덤 링 (갈라진 양쪽 물길 및 곡선 강폭 비례 적응형 스폰)
        for (float z = curveStartDist + 35f; z < curveEndDist - 35f; z += Random.Range(35f, 65f))
        {
            if (hasRiverPath && SkippingStones.Terrain.GlobalRiverPath.Instance.EvaluateAtDistance(z, out Vector3 cPos, out Vector3 tan, out float rWidth, out float wY))
            {
                Vector3 normal = Vector3.Cross(Vector3.up, tan).normalized;
                float halfW = Mathf.Clamp(rWidth * 0.45f, 4f, 35f);

                List<Vector3> splitChannels = DetectSplitWaterChannels(cPos, normal, Mathf.Max(halfW * 1.6f, 30f), wY + 0.05f);

                if (splitChannels.Count > 1)
                {
                    foreach (var chPos in splitChannels)
                    {
                        TrySpawnBoostPad(chPos, tan);
                    }
                }
                else
                {
                    float effectiveWidth = halfW * 2f;
                    if (effectiveWidth < 15f)
                    {
                        Vector3 midPos = cPos + normal * Random.Range(-1.5f, 1.5f);
                        midPos.y = wY + 0.05f;
                        TrySpawnBoostPad(midPos, tan);
                    }
                    else if (effectiveWidth < 25f)
                    {
                        Vector3 p1 = cPos - normal * (halfW * 0.5f);
                        Vector3 p2 = cPos + normal * (halfW * 0.5f);
                        p1.y = wY + 0.05f;
                        p2.y = wY + 0.05f;
                        TrySpawnBoostPad(p1, tan);
                        if (Random.value < 0.7f) TrySpawnBoostPad(p2, tan);
                    }
                    else
                    {
                        Vector3 leftPos = cPos - normal * (halfW * 0.65f);
                        Vector3 midPos = cPos + normal * Random.Range(-halfW * 0.2f, halfW * 0.2f);
                        Vector3 rightPos = cPos + normal * (halfW * 0.65f);
                        leftPos.y = wY + 0.05f;
                        midPos.y = wY + 0.05f;
                        rightPos.y = wY + 0.05f;

                        TrySpawnBoostPad(leftPos, tan);
                        TrySpawnBoostPad(midPos, tan);
                        TrySpawnBoostPad(rightPos, tan);
                    }
                }
            }
            else
            {
                float leftX = Random.Range(minX, Mathf.Lerp(minX, maxX, 0.35f));
                float centerX = Random.Range(Mathf.Lerp(minX, maxX, 0.35f), Mathf.Lerp(minX, maxX, 0.65f));
                float rightX = Random.Range(Mathf.Lerp(minX, maxX, 0.65f), maxX);

                TrySpawnBoostPad(new Vector3(leftX, curWaterY + 0.05f, z + Random.Range(-3f, 3f)), Vector3.forward);
                TrySpawnBoostPad(new Vector3(centerX, curWaterY + 0.05f, z + Random.Range(-3f, 3f)), Vector3.forward);
                TrySpawnBoostPad(new Vector3(rightX, curWaterY + 0.05f, z + Random.Range(-3f, 3f)), Vector3.forward);
            }
        }

        // 2. 🪨 장애물 바위 (강폭 및 분기 물길 비례 통로 확보 스폰)
        for (float z = curveStartDist + 40f; z < curveEndDist - 30f; z += Random.Range(25f, 42f))
        {
            if (hasRiverPath && SkippingStones.Terrain.GlobalRiverPath.Instance.EvaluateAtDistance(z, out Vector3 cPos, out Vector3 tan, out float rWidth, out float wY))
            {
                Vector3 normal = Vector3.Cross(Vector3.up, tan).normalized;
                float halfW = Mathf.Clamp(rWidth * 0.45f, 4f, 35f);

                List<Vector3> splitChannels = DetectSplitWaterChannels(cPos, normal, Mathf.Max(halfW * 1.6f, 30f), wY);

                if (splitChannels.Count > 1)
                {
                    foreach (var chPos in splitChannels)
                    {
                        if (Random.value < 0.6f)
                        {
                            Vector3 rockPos = chPos + normal * (Random.value < 0.5f ? -2f : 2f);
                            rockPos.y = wY;
                            TrySpawnObstacleRock(rockPos);
                        }
                    }
                }
                else
                {
                    float effectiveWidth = halfW * 2f;
                    if (effectiveWidth < 15f)
                    {
                        if (Random.value < 0.5f)
                        {
                            float side = (Random.value < 0.5f) ? -halfW * 0.75f : halfW * 0.75f;
                            Vector3 rockPos = cPos + normal * side;
                            rockPos.y = wY;
                            TrySpawnObstacleRock(rockPos);
                        }
                    }
                    else
                    {
                        float offset = Random.Range(-halfW * 0.75f, halfW * 0.75f);
                        Vector3 rockPos = cPos + normal * offset;
                        rockPos.y = wY;
                        TrySpawnObstacleRock(rockPos);
                    }
                }
            }
            else
            {
                float x = Random.Range(minX, maxX);
                TrySpawnObstacleRock(new Vector3(x, curWaterY, z + Random.Range(-4f, 4f)));
            }
        }

        // 3. 🐟 물고기 (갈라진 양쪽 물길 및 강폭 적응형 스폰 - 개체 수 풍성 배치)
        for (float z = curveStartDist + 25f; z < curveEndDist - 25f; z += Random.Range(20f, 42f))
        {
            if (hasRiverPath && SkippingStones.Terrain.GlobalRiverPath.Instance.EvaluateAtDistance(z, out Vector3 cPos, out Vector3 tan, out float rWidth, out float wY))
            {
                Vector3 normal = Vector3.Cross(Vector3.up, tan).normalized;
                float halfW = Mathf.Clamp(rWidth * 0.45f, 4f, 35f);

                List<Vector3> splitChannels = DetectSplitWaterChannels(cPos, normal, Mathf.Max(halfW * 1.6f, 30f), wY);

                if (splitChannels.Count > 1)
                {
                    for (int chIdx = 0; chIdx < splitChannels.Count; chIdx++)
                    {
                        TrySpawnFish(splitChannels[chIdx], z + (chIdx * 6f));
                        if (Random.value < 0.55f)
                        {
                            Vector3 sidePos = splitChannels[chIdx] + normal * Random.Range(-2.5f, 2.5f);
                            TrySpawnFish(sidePos, z + (chIdx * 6f) + Random.Range(3f, 8f));
                        }
                    }
                }
                else
                {
                    float effectiveWidth = halfW * 2f;
                    if (effectiveWidth < 15f)
                    {
                        Vector3 fPos = cPos + normal * Random.Range(-halfW * 0.5f, halfW * 0.5f);
                        fPos.y = wY;
                        TrySpawnFish(fPos, z);
                    }
                    else
                    {
                        Vector3 fPos1 = cPos - normal * (halfW * 0.65f);
                        Vector3 fPos2 = cPos + normal * Random.Range(-halfW * 0.25f, halfW * 0.25f);
                        Vector3 fPos3 = cPos + normal * (halfW * 0.65f);
                        fPos1.y = wY;
                        fPos2.y = wY;
                        fPos3.y = wY;

                        TrySpawnFish(fPos1, z);
                        TrySpawnFish(fPos2, z + Random.Range(4f, 10f));
                        TrySpawnFish(fPos3, z + Random.Range(8f, 16f));
                    }
                }
            }
            else
            {
                float x1 = Random.Range(minX, Mathf.Lerp(minX, maxX, 0.35f));
                float x2 = Random.Range(Mathf.Lerp(minX, maxX, 0.35f), Mathf.Lerp(minX, maxX, 0.65f));
                float x3 = Random.Range(Mathf.Lerp(minX, maxX, 0.65f), maxX);
                TrySpawnFish(new Vector3(x1, curWaterY, z), z);
                TrySpawnFish(new Vector3(x2, curWaterY, z + Random.Range(4f, 10f)), z);
                TrySpawnFish(new Vector3(x3, curWaterY, z + Random.Range(8f, 16f)), z);
            }
        }

        // 4. 🪷 연잎 군락
        CreateLilyPadsGrid(minX, maxX, chunkStartZ + 20f, chunkEndZ - 20f, curWaterY);
    }

    /// <summary>
    /// 🏝️ 특정 단면(Distance)에서 섬으로 인해 분리된 다중 수면 채널(Water Channels) 중심점 검출
    /// </summary>
    private List<Vector3> DetectSplitWaterChannels(Vector3 centerPos, Vector3 normal, float maxScanWidth, float waterY)
    {
        List<Vector3> channels = new List<Vector3>();
        // 섬 주변 반대편 물길까지 포용할 수 있도록 스캔 반경 확장 (최소 75m)
        float scanRange = Mathf.Max(maxScanWidth, 75f);
        float step = 2.5f;

        bool inWater = false;
        float segmentStartOffset = 0f;

        for (float offset = -scanRange; offset <= scanRange; offset += step)
        {
            Vector3 testPos = centerPos + normal * offset;
            testPos.y = waterY;

            bool isWater = IsValidWaterPosition(testPos, false);

            if (isWater && !inWater)
            {
                inWater = true;
                segmentStartOffset = offset;
            }
            else if (!isWater && inWater)
            {
                inWater = false;
                float segEndOffset = offset - step;
                if (segEndOffset - segmentStartOffset >= 4.0f) // 폭 4m 이상의 유효 수로만 채널로 인정
                {
                    float midOffset = (segmentStartOffset + segEndOffset) * 0.5f;
                    Vector3 chPos = centerPos + normal * midOffset;
                    chPos.y = waterY;
                    channels.Add(chPos);
                }
            }
        }

        if (inWater)
        {
            float segEndOffset = scanRange;
            if (segEndOffset - segmentStartOffset >= 4.0f)
            {
                float midOffset = (segmentStartOffset + segEndOffset) * 0.5f;
                Vector3 chPos = centerPos + normal * midOffset;
                chPos.y = waterY;
                channels.Add(chPos);
            }
        }

        return channels;
    }

    /// <summary>
    /// 상공에서 수직 레이캐스트: MeshCollider 및 TerrainCollider를 모두 완벽 검사
    /// 1) 지형(MeshCollider/TerrainCollider)이 수면 위로 솟아 있는 육지인 경우 -> False
    /// 2) 수심이 너무 얕아(수면과 지형 사이 < 0.35m) 바닥에 파묻히는 경우 -> False
    /// 3) 충분한 수심(waterDepth >= 0.35m)이 확보된 유효한 수면 영역인 경우 -> True
    /// </summary>
    private bool IsValidWaterPosition(Vector3 pos, bool checkZBounds = false, float chunkStartZ = 0f, float chunkEndZ = float.MaxValue)
    {
        // 1. 단순 Z 경계 검사 (곡선 강줄기는 옆으로 굽이치므로 기본 비활성화)
        if (checkZBounds && chunkEndZ < float.MaxValue)
        {
            if (pos.z < chunkStartZ - 50f || pos.z > chunkEndZ + 50f) return false;
        }

        float curWater = pos.y;
        if (Mathf.Abs(curWater) < 0.001f) curWater = GetCurrentWaterLevel();

        // 2. 초고도 상공(Y = curWater + 250m)에서 아래로 수직 레이캐스트
        float rayStart = curWater + 250f;
        Vector3 rayOrigin = new Vector3(pos.x, rayStart, pos.z);

        RaycastHit[] hits = Physics.RaycastAll(rayOrigin, Vector3.down, 400f, ~0, QueryTriggerInteraction.Collide);
        if (hits == null || hits.Length == 0)
        {
            // 베이킹된 강줄기 상의 점이면 안전 수면 인정
            return true;
        }

        bool hasWaterSurface = false;
        float groundY = float.MinValue;
        bool hasGround = false;

        foreach (var hit in hits)
        {
            if (hit.collider == null) continue;

            if (hit.collider.GetComponent<WaterSurface>() != null || hit.collider.name.ToLower().Contains("water"))
            {
                hasWaterSurface = true;
                curWater = hit.point.y;
            }
            else
            {
                if (hit.point.y > groundY)
                {
                    groundY = hit.point.y;
                    hasGround = true;
                }
            }
        }

        // 지형이 수면보다 높이 솟아 있는 육지/언덕인 경우 스폰 차단
        if (hasGround && groundY >= curWater - 0.15f)
        {
            return false;
        }

        // 수심(waterDepth)이 너무 얕아 흙바닥에 박히는 경우 스폰 차단
        if (hasGround && (curWater - groundY) < 0.35f)
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// 🌟 오브젝트 간 겹침 방지 (최소 3.8m 이격 거리 검사)
    /// </summary>
    private bool HasNearbySpawnedEntity(Vector3 pos, float minRadius = 3.8f)
    {
        float minSq = minRadius * minRadius;
        for (int i = 0; i < transform.childCount; i++)
        {
            Transform child = transform.GetChild(i);
            if (child == null) continue;
            Vector3 diff = child.position - pos;
            diff.y = 0f; // 수평 거리 기준
            if (diff.sqrMagnitude < minSq)
            {
                return true;
            }
        }
        return false;
    }

    private void TrySpawnBoostPad(Vector3 pos, Vector3 tangent = default)
    {
        if (!IsValidWaterPosition(pos, false)) return;
        if (HasNearbySpawnedEntity(pos, 3.8f)) return;

        // 🌀 리듬 아케이드 모드: 지상 부스트 패드 대신 공중 RandomRing 스폰
        if (currentSpawningMode == GameController.GameMode.RhythmArcade)
        {
            Quaternion rot = (tangent.sqrMagnitude > 0.01f) ? Quaternion.LookRotation(tangent, Vector3.up) : Quaternion.identity;
            CreateRandomRing(pos, rot);
            return;
        }

        CreateBoostPad(pos, Quaternion.identity);
    }

    private void TrySpawnObstacleRock(Vector3 pos)
    {
        if (!IsValidWaterPosition(pos, false)) return;
        if (HasNearbySpawnedEntity(pos, 4.2f)) return;
        CreateObstacleRock(pos);
    }

    private void TrySpawnFish(Vector3 pos, float dist)
    {
        if (!IsValidWaterPosition(pos, false)) return;
        if (HasNearbySpawnedEntity(pos, 2.0f)) return;
        SpawnSingleFish(pos, dist);
    }

    private static void SafeDestroy(UnityEngine.Object obj)
    {
        if (obj == null) return;
        if (Application.isPlaying) Destroy(obj);
        else DestroyImmediate(obj);
    }
}