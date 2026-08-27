using UnityEngine;

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
        // BG 청크 릴레이 콜백 구독: 인게임 중 새 1500m 구간이 앞으로 이동할 때 해당 구간 엔티티 재스폰
        if (LakeEnvironmentManager.Instance != null)
            LakeEnvironmentManager.Instance.OnChunkRelayed += SpawnChunkEntities;
    }

    private void OnDestroy()
    {
        if (LakeEnvironmentManager.Instance != null)
            LakeEnvironmentManager.Instance.OnChunkRelayed -= SpawnChunkEntities;
    }

    public void GenerateRiverEntitiesForMode(GameController.GameMode mode)
    {
        if (mode == GameController.GameMode.LongDistance)
        {
            GenerateLongDistanceRiver();
        }
        else
        {
            GenerateTargetAccuracyRiver();
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

    [Header("수면 높이 및 영역 연동")]
    public float defaultWaterHeight = 16.0f;

    /// <summary>
    /// 수면 오브젝트(WaterSurface / Water_Surface)의 BoxCollider로부터 실시간 가로폭(minX, maxX), 세로길이(minZ, maxZ)와 수면 높이(waterY)를 100% 신뢰성 있게 획득
    /// </summary>
    public bool GetWaterColliderBounds(out float minX, out float maxX, out float minZ, out float maxZ, out float waterY)
    {
        BoxCollider bc = null;
        WaterSurface ws = FindAnyObjectByType<WaterSurface>();
        if (ws != null)
        {
            bc = ws.GetComponent<BoxCollider>();
        }

        if (bc == null)
        {
            GameObject waterObj = GameObject.Find("WaterSurface") ?? GameObject.Find("Water_Surface") ?? GameObject.Find("RS_Surface");
            if (waterObj != null)
            {
                bc = waterObj.GetComponent<BoxCollider>();
            }
        }

        if (bc != null)
        {
            Bounds b = bc.bounds;
            minX = b.min.x;
            maxX = b.max.x;
            minZ = b.min.z;
            maxZ = b.max.z;
            waterY = b.max.y;
            return true;
        }

        // BoxCollider가 없을 경우 안전 폴백 (Transform / default)
        minX = -100f;
        maxX = 100f;
        minZ = 0f;
        maxZ = riverLength;
        waterY = defaultWaterHeight;
        return false;
    }

    public bool GetWaterColliderBounds(out float minX, out float maxX, out float waterY)
    {
        return GetWaterColliderBounds(out minX, out maxX, out _, out _, out waterY);
    }

    private float GetCurrentWaterLevel()
    {
        GetWaterColliderBounds(out _, out _, out float waterY);
        return waterY;
    }

    /// <summary>
    /// 🏆 장거리 모드: 씬 내 실제 수면 BoxCollider의 1개 청크 범위 내에만 엔티티 스폰 (이후 구간은 청크 릴레이 시 동적 확장)
    /// </summary>
    private void GenerateLongDistanceRiver()
    {
        ClearExistingEntities();

        GetWaterColliderBounds(out float minX, out float maxX, out float minZ, out float maxZ, out float curWaterY);
        float endZ = maxZ;
        startBankPos = Vector3.zero;
        spawnDirection = Vector3.forward;

        // 1. 🚀 가속 부스트 패드
        for (float bDist = minZ + 45f; bDist < endZ - 80f; bDist += Random.Range(35f, 65f))
        {
            float leftX = Random.Range(minX, Mathf.Lerp(minX, maxX, 0.35f));
            float midX = Random.Range(Mathf.Lerp(minX, maxX, 0.35f), Mathf.Lerp(minX, maxX, 0.65f));
            float rightX = Random.Range(Mathf.Lerp(minX, maxX, 0.65f), maxX);

            TrySpawnBoostPad(new Vector3(leftX, curWaterY + 0.05f, bDist + Random.Range(-4f, 4f)));
            TrySpawnBoostPad(new Vector3(midX, curWaterY + 0.05f, bDist + Random.Range(-4f, 4f)));
            TrySpawnBoostPad(new Vector3(rightX, curWaterY + 0.05f, bDist + Random.Range(-4f, 4f)));
        }

        // 2. 🪨 강 장애물(바위)
        for (float d = minZ + 50f; d < endZ - 20f; d += Random.Range(20f, 38f))
        {
            float rockX = Random.Range(minX, maxX);
            TrySpawnObstacleRock(new Vector3(rockX, curWaterY, d + Random.Range(-5f, 5f)));
        }

        // 3. 🐟 튀어오르는 물고기
        for (float fDist = minZ + 40f; fDist < endZ - 60f; fDist += Random.Range(45f, 85f))
        {
            float fX1 = Random.Range(minX, Mathf.Lerp(minX, maxX, 0.35f));
            float fX2 = Random.Range(Mathf.Lerp(minX, maxX, 0.35f), Mathf.Lerp(minX, maxX, 0.65f));
            float fX3 = Random.Range(Mathf.Lerp(minX, maxX, 0.65f), maxX);
            TrySpawnFish(new Vector3(fX1, curWaterY, fDist), fDist);
            TrySpawnFish(new Vector3(fX2, curWaterY, fDist + Random.Range(6f, 16f)), fDist);
            TrySpawnFish(new Vector3(fX3, curWaterY, fDist + Random.Range(12f, 24f)), fDist);
        }

        // 4. 🚩 친구 거리 깃발 (수면 범위 내에 존재하는 깃발만 스폰)
        string[] friends = { "라이언 (3위)", "어피치 (2위)", "프로도 (1위)", "콘 (국내 1위)", "무지 (마스터)", "네오 (그랜드마스터)", "튜브 (초월자)", "제이지 (레전드)" };
        float[] friendDists = { 120f, 310f, 450f, 750f, 1200f, 1800f, 2500f, 3500f };
        for (int i = 0; i < friends.Length; i++)
        {
            float zPos = friendDists[i];
            if (zPos < minZ + 50f || zPos > endZ - 50f) continue;
            float flagX = (i % 2 == 0) ? Random.Range(minX, Mathf.Lerp(minX, maxX, 0.4f)) : Random.Range(Mathf.Lerp(minX, maxX, 0.6f), maxX);
            Vector3 fPos = new Vector3(flagX, curWaterY, zPos);
            if (IsValidWaterPosition(fPos))
            {
                CreateFriendFlag(fPos, friends[i], $"{i + 1}위", zPos);
            }
        }

        // 5. 🪷 연잎 및 연꽃 군락
        CreateLilyPadsGrid(minX, maxX, minZ + 20f, endZ - 20f, curWaterY);
        CleanupOldGroundObjects();
    }

    /// <summary>
    /// 🎯 타겟 맞추기 모드: 호수 전체 수면 고른 전역 랜덤 배치
    /// </summary>
    private void GenerateTargetAccuracyRiver()
    {
        ClearExistingEntities();

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
    [Tooltip("장애물 바위 프리팹")]
    public GameObject obstacleRockPrefab;
    [Tooltip("타겟 과녁 프리팹")]
    public GameObject targetZonePrefab;
    [Tooltip("친구 랭킹 깃발 프리팹")]
    public GameObject friendFlagPrefab;
    [Tooltip("튀어오르는 물고기 프리팹")]
    public GameObject jumpingFishPrefab;
    [Tooltip("연잎/연꽃 군락 프리팹")]
    public GameObject lilyPadClusterPrefab;

    private void EnsurePrefabsLoaded()
    {
#if UNITY_EDITOR
        if (boostPadPrefab == null) boostPadPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/prefab/BG_Deco/BoostPad.prefab");
        if (obstacleRockPrefab == null) obstacleRockPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/prefab/BG_Deco/ObstacleRock.prefab");
        if (targetZonePrefab == null) targetZonePrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/prefab/BG_Deco/TargetZone.prefab");
        if (friendFlagPrefab == null) friendFlagPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/prefab/BG_Deco/FriendFlag.prefab");
        if (jumpingFishPrefab == null) jumpingFishPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/prefab/BG_Deco/JumpingFish.prefab");
        if (lilyPadClusterPrefab == null) lilyPadClusterPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/prefab/BG_Deco/LilyPadCluster.prefab");
#endif
        if (boostPadPrefab == null) boostPadPrefab = Resources.Load<GameObject>("BoostPad");
        if (obstacleRockPrefab == null) obstacleRockPrefab = Resources.Load<GameObject>("ObstacleRock");
        if (targetZonePrefab == null) targetZonePrefab = Resources.Load<GameObject>("TargetZone");
        if (friendFlagPrefab == null) friendFlagPrefab = Resources.Load<GameObject>("FriendFlag");
        if (jumpingFishPrefab == null) jumpingFishPrefab = Resources.Load<GameObject>("JumpingFish");
        if (lilyPadClusterPrefab == null) lilyPadClusterPrefab = Resources.Load<GameObject>("LilyPadCluster");
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
        GameObject fishObj;
        if (jumpingFishPrefab != null)
        {
            fishObj = Instantiate(jumpingFishPrefab, pos, Quaternion.identity, transform);
            fishObj.name = $"JumpingFish_{pos.x:F0}x{pos.z:F0}";
        }
        else
        {
            fishObj = new GameObject($"JumpingFish_{pos.x:F0}x{pos.z:F0}");
            fishObj.transform.SetParent(transform);
            fishObj.transform.position = pos;
            fishObj.AddComponent<JumpingFish>();
        }

        JumpingFish jf = fishObj.GetComponent<JumpingFish>();
        if (jf != null)
        {
            if (dist < 300f)
            {
                jf.speciesId = "minnow";
                jf.speciesName = "피라미";
                jf.jumpHeight = 2.4f;
            }
            else if (dist < 750f)
            {
                jf.speciesId = "carp";
                jf.speciesName = "비단 잉어";
                jf.jumpHeight = 3.2f;
            }
            else
            {
                jf.speciesId = "flying_fish";
                jf.speciesName = "날치";
                jf.jumpHeight = 4.0f;
            }
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
    /// BG_01 청크가 앞으로 릴레이될 때 호출됨.
    /// 새로 생성된 chunkStartZ ~ chunkStartZ+chunkSize 구간의 기존 엔티티 제거 후 재배치.
    /// </summary>
    public void SpawnChunkEntities(float chunkStartZ)
    {
        float chunkSize = 500f;
        if (LakeEnvironmentManager.Instance != null && LakeEnvironmentManager.Instance.autoChunkSize > 50f)
        {
            chunkSize = LakeEnvironmentManager.Instance.autoChunkSize;
        }
        else if (GetWaterColliderBounds(out _, out _, out float wMinZ, out float wMaxZ, out _))
        {
            chunkSize = Mathf.Max(100f, wMaxZ - wMinZ);
        }

        float chunkEndZ = chunkStartZ + chunkSize;
        float curWaterY = GetCurrentWaterLevel();

        // 해당 구간의 기존 엔티티 제거
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Transform child = transform.GetChild(i);
            if (child != null)
            {
                float cz = child.position.z;
                if (cz >= chunkStartZ && cz < chunkEndZ)
                    SafeDestroy(child.gameObject);
            }
        }

        // Water_Surface BoxCollider로부터 실제 수면 가로폭 및 높이 동적 획득
        GetWaterColliderBounds(out float minX, out float maxX, out curWaterY);


        // 1. 🚀 가속 부스트 패드 (Water_Surface BoxCollider 전체 폭에 걸쳐 좌/중/우 균등 분산)
        for (float z = chunkStartZ + 40f; z < chunkEndZ - 50f; z += Random.Range(35f, 65f))
        {
            float leftX = Random.Range(minX, Mathf.Lerp(minX, maxX, 0.35f));
            float centerX = Random.Range(Mathf.Lerp(minX, maxX, 0.35f), Mathf.Lerp(minX, maxX, 0.65f));
            float rightX = Random.Range(Mathf.Lerp(minX, maxX, 0.65f), maxX);

            TrySpawnBoostPad(new Vector3(leftX, curWaterY + 0.05f, z + Random.Range(-4f, 4f)));
            TrySpawnBoostPad(new Vector3(centerX, curWaterY + 0.05f, z + Random.Range(-4f, 4f)));
            TrySpawnBoostPad(new Vector3(rightX, curWaterY + 0.05f, z + Random.Range(-4f, 4f)));
        }

        // 2. 🪨 장애물 바위 Water_Surface 전폭 균등 분산 배치
        for (float z = chunkStartZ + 50f; z < chunkEndZ; z += Random.Range(20f, 38f))
        {
            float x = Random.Range(minX, maxX);
            TrySpawnObstacleRock(new Vector3(x, curWaterY, z + Random.Range(-5f, 5f)));
        }

        // 3. 🐟 물고기 Water_Surface 전폭 스폰 (좌/중/우)
        for (float z = chunkStartZ + 40f; z < chunkEndZ - 60f; z += Random.Range(45f, 85f))
        {
            float x1 = Random.Range(minX, Mathf.Lerp(minX, maxX, 0.35f));
            float x2 = Random.Range(Mathf.Lerp(minX, maxX, 0.35f), Mathf.Lerp(minX, maxX, 0.65f));
            float x3 = Random.Range(Mathf.Lerp(minX, maxX, 0.65f), maxX);
            TrySpawnFish(new Vector3(x1, curWaterY, z), z);
            TrySpawnFish(new Vector3(x2, curWaterY, z + Random.Range(6f, 16f)), z);
            TrySpawnFish(new Vector3(x3, curWaterY, z + Random.Range(12f, 24f)), z);
        }

        // 4. 🪷 연잎 군락 Water_Surface 전폭 풍성 생성
        CreateLilyPadsGrid(minX, maxX, chunkStartZ + 20f, chunkEndZ - 20f, curWaterY);
    }


    /// <summary>
    /// 상공에서 수직 레이캐스트: 지형(Ground)이 수면보다 높이 솟아 있으면 False (땅속 스폰 100% 방지)
    /// </summary>
    private bool IsValidWaterPosition(Vector3 pos)
    {
        float curWater = GetCurrentWaterLevel();
        float rayStart = Mathf.Max(pos.y + 30f, curWater + 50f);
        Vector3 rayOrigin = new Vector3(pos.x, rayStart, pos.z);
        if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, 80f, groundLayerMask))
        {
            if (hit.point.y > curWater + 0.35f) return false;
        }
        return true;
    }

    private void TrySpawnBoostPad(Vector3 pos)
    {
        if (!IsValidWaterPosition(pos)) return;
        CreateBoostPad(pos, Quaternion.identity);
    }

    private void TrySpawnObstacleRock(Vector3 pos)
    {
        if (!IsValidWaterPosition(pos)) return;
        CreateObstacleRock(pos);
    }

    private void TrySpawnFish(Vector3 pos, float dist)
    {
        if (!IsValidWaterPosition(pos)) return;
        SpawnSingleFish(pos, dist);
    }

    private static void SafeDestroy(UnityEngine.Object obj)
    {
        if (obj == null) return;
        if (Application.isPlaying) Destroy(obj);
        else DestroyImmediate(obj);
    }
}