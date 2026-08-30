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
    /// 🏆 장거리 모드: 활성화된 1개 청크(지형 실측 길이)의 범위 내에만 엔티티 스폰 (이후 구간은 청크 릴레이 시 동적 확장)
    /// </summary>
    private void GenerateLongDistanceRiver()
    {
        ClearExistingEntities();

        GetWaterColliderBounds(out float minX, out float maxX, out float minZ, out float maxZ, out float curWaterY);
        float activeChunkSize = (LakeEnvironmentManager.Instance != null && LakeEnvironmentManager.Instance.autoChunkSize > 10f) 
            ? LakeEnvironmentManager.Instance.autoChunkSize 
            : Mathf.Max(100f, maxZ - minZ);

        float startZ = minZ;
        float endZ = startZ + activeChunkSize;
        startBankPos = Vector3.zero;
        spawnDirection = Vector3.forward;

        // 1. 🚀 가속 부스트 패드 (강폭 비례 적응형 밀도 스폰)
        for (float bDist = startZ + 40f; bDist < endZ - 40f; bDist += Random.Range(35f, 65f))
        {
            if (SkippingStones.Terrain.GlobalRiverPath.Instance != null && SkippingStones.Terrain.GlobalRiverPath.Instance.EvaluateAtDistance(bDist, out Vector3 cPos, out Vector3 tan, out float rWidth, out float wY))
            {
                Vector3 normal = Vector3.Cross(Vector3.up, tan).normalized;
                float halfW = Mathf.Clamp(rWidth * 0.45f, 4f, 35f);
                float effectiveWidth = halfW * 2f;

                if (effectiveWidth < 15f)
                {
                    // 좁은 협곡 (< 15m): 중앙 1개만 안전 스폰 (겹침 방지)
                    Vector3 midPos = cPos + normal * Random.Range(-1.5f, 1.5f);
                    midPos.y = wY + 0.05f;
                    TrySpawnBoostPad(midPos, startZ, endZ);
                }
                else if (effectiveWidth < 25f)
                {
                    // 보통 강 (15m ~ 25m): 1~2개 분산 스폰
                    Vector3 p1 = cPos - normal * (halfW * 0.5f);
                    Vector3 p2 = cPos + normal * (halfW * 0.5f);
                    p1.y = wY + 0.05f;
                    p2.y = wY + 0.05f;
                    TrySpawnBoostPad(p1, startZ, endZ);
                    if (Random.value < 0.7f) TrySpawnBoostPad(p2, startZ, endZ);
                }
                else
                {
                    // 넓은 강 (>= 25m): 좌/중/우 3개 분산 스폰
                    Vector3 leftPos = cPos - normal * (halfW * 0.65f);
                    Vector3 midPos = cPos + normal * Random.Range(-halfW * 0.2f, halfW * 0.2f);
                    Vector3 rightPos = cPos + normal * (halfW * 0.65f);
                    leftPos.y = wY + 0.05f;
                    midPos.y = wY + 0.05f;
                    rightPos.y = wY + 0.05f;

                    TrySpawnBoostPad(leftPos, startZ, endZ);
                    TrySpawnBoostPad(midPos, startZ, endZ);
                    TrySpawnBoostPad(rightPos, startZ, endZ);
                }
            }
            else
            {
                float leftX = Random.Range(minX, Mathf.Lerp(minX, maxX, 0.35f));
                float midX = Random.Range(Mathf.Lerp(minX, maxX, 0.35f), Mathf.Lerp(minX, maxX, 0.65f));
                float rightX = Random.Range(Mathf.Lerp(minX, maxX, 0.65f), maxX);

                TrySpawnBoostPad(new Vector3(leftX, curWaterY + 0.05f, bDist + Random.Range(-3f, 3f)), startZ, endZ);
                TrySpawnBoostPad(new Vector3(midX, curWaterY + 0.05f, bDist + Random.Range(-3f, 3f)), startZ, endZ);
                TrySpawnBoostPad(new Vector3(rightX, curWaterY + 0.05f, bDist + Random.Range(-3f, 3f)), startZ, endZ);
            }
        }

        // 2. 🪨 강 장애물(바위) (강폭 비례 통로 확보 스폰)
        for (float d = startZ + 45f; d < endZ - 30f; d += Random.Range(25f, 42f))
        {
            if (SkippingStones.Terrain.GlobalRiverPath.Instance != null && SkippingStones.Terrain.GlobalRiverPath.Instance.EvaluateAtDistance(d, out Vector3 cPos, out Vector3 tan, out float rWidth, out float wY))
            {
                Vector3 normal = Vector3.Cross(Vector3.up, tan).normalized;
                float halfW = Mathf.Clamp(rWidth * 0.45f, 4f, 35f);
                float effectiveWidth = halfW * 2f;

                if (effectiveWidth < 15f)
                {
                    // 좁은 협곡: 길을 막지 않도록 가장자리에만 50% 확률로 1개 배치
                    if (Random.value < 0.5f)
                    {
                        float side = (Random.value < 0.5f) ? -halfW * 0.75f : halfW * 0.75f;
                        Vector3 rockPos = cPos + normal * side;
                        rockPos.y = wY;
                        TrySpawnObstacleRock(rockPos, startZ, endZ);
                    }
                }
                else
                {
                    float offset = Random.Range(-halfW * 0.75f, halfW * 0.75f);
                    Vector3 rockPos = cPos + normal * offset;
                    rockPos.y = wY;
                    TrySpawnObstacleRock(rockPos, startZ, endZ);
                }
            }
            else
            {
                float rockX = Random.Range(minX, maxX);
                TrySpawnObstacleRock(new Vector3(rockX, curWaterY, d + Random.Range(-4f, 4f)), startZ, endZ);
            }
        }

        // 3. 🐟 튀어오르는 물고기 (강폭 적응형 스폰)
        for (float fDist = startZ + 40f; fDist < endZ - 40f; fDist += Random.Range(45f, 85f))
        {
            if (SkippingStones.Terrain.GlobalRiverPath.Instance != null && SkippingStones.Terrain.GlobalRiverPath.Instance.EvaluateAtDistance(fDist, out Vector3 cPos, out Vector3 tan, out float rWidth, out float wY))
            {
                Vector3 normal = Vector3.Cross(Vector3.up, tan).normalized;
                float halfW = Mathf.Clamp(rWidth * 0.45f, 4f, 35f);
                float effectiveWidth = halfW * 2f;

                if (effectiveWidth < 15f)
                {
                    Vector3 fPos = cPos + normal * Random.Range(-halfW * 0.4f, halfW * 0.4f);
                    fPos.y = wY;
                    TrySpawnFish(fPos, fDist, startZ, endZ);
                }
                else
                {
                    Vector3 fPos1 = cPos - normal * (halfW * 0.6f);
                    Vector3 fPos2 = cPos + normal * Random.Range(-halfW * 0.2f, halfW * 0.2f);
                    Vector3 fPos3 = cPos + normal * (halfW * 0.6f);
                    fPos1.y = wY;
                    fPos2.y = wY;
                    fPos3.y = wY;

                    TrySpawnFish(fPos1, fDist, startZ, endZ);
                    TrySpawnFish(fPos2, fDist + Random.Range(6f, 16f), startZ, endZ);
                    TrySpawnFish(fPos3, fDist + Random.Range(12f, 24f), startZ, endZ);
                }
            }
            else
            {
                float fX1 = Random.Range(minX, Mathf.Lerp(minX, maxX, 0.35f));
                float fX2 = Random.Range(Mathf.Lerp(minX, maxX, 0.35f), Mathf.Lerp(minX, maxX, 0.65f));
                float fX3 = Random.Range(Mathf.Lerp(minX, maxX, 0.65f), maxX);
                TrySpawnFish(new Vector3(fX1, curWaterY, fDist), fDist, startZ, endZ);
                TrySpawnFish(new Vector3(fX2, curWaterY, fDist + Random.Range(6f, 16f)), fDist, startZ, endZ);
                TrySpawnFish(new Vector3(fX3, curWaterY, fDist + Random.Range(12f, 24f)), fDist, startZ, endZ);
            }
        }

        // 4. 🚩 친구 거리 깃발 (현재 청크 지형 범위 내에 존재하는 깃발만 스폰)
        string[] friends = { "라이언 (3위)", "어피치 (2위)", "프로도 (1위)", "콘 (국내 1위)", "무지 (마스터)", "네오 (그랜드마스터)", "튜브 (초월자)", "제이지 (레전드)" };
        float[] friendDists = { 120f, 310f, 450f, 750f, 1200f, 1800f, 2500f, 3500f };
        for (int i = 0; i < friends.Length; i++)
        {
            float zPos = friendDists[i];
            if (zPos < startZ + 30f || zPos > endZ - 30f) continue;
            
            Vector3 fPos;
            if (SkippingStones.Terrain.GlobalRiverPath.Instance != null && SkippingStones.Terrain.GlobalRiverPath.Instance.EvaluateAtDistance(zPos, out Vector3 cPos, out Vector3 tan, out float rWidth, out float wY))
            {
                Vector3 normal = Vector3.Cross(Vector3.up, tan).normalized;
                float halfW = Mathf.Clamp(rWidth * 0.4f, 5f, 35f);
                float sideOffset = (i % 2 == 0) ? -halfW * 0.6f : halfW * 0.6f;
                fPos = cPos + normal * sideOffset;
                fPos.y = wY;
            }
            else
            {
                float flagX = (i % 2 == 0) ? Random.Range(minX, Mathf.Lerp(minX, maxX, 0.4f)) : Random.Range(Mathf.Lerp(minX, maxX, 0.6f), maxX);
                fPos = new Vector3(flagX, curWaterY, zPos);
            }

            if (IsValidWaterPosition(fPos, startZ, endZ))
            {
                CreateFriendFlag(fPos, friends[i], $"{i + 1}위", zPos);
            }
        }

        // 5. 🪷 연잎 및 연꽃 군락 (청크 지형 경계 엄격 준수)
        CreateLilyPadsGrid(minX, maxX, startZ + 20f, endZ - 20f, curWaterY);
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
    /// 🌟 새 맵 청크가 생성(스트리밍)될 때 호출됨.
    /// 새로 생성된 chunkStartZ ~ chunkStartZ+chunkSize 구간에만 정확히 엔티티 스폰.
    /// </summary>
    public void SpawnChunkEntities(float chunkStartZ)
    {
        float chunkSize = (LakeEnvironmentManager.Instance != null && LakeEnvironmentManager.Instance.autoChunkSize > 10f)
            ? LakeEnvironmentManager.Instance.autoChunkSize
            : 500f;

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

        // 1. 🚀 가속 부스트 패드 (새로 생성된 청크 지형 경계 및 강폭 비례 적응형 스폰)
        for (float z = chunkStartZ + 35f; z < chunkEndZ - 35f; z += Random.Range(35f, 65f))
        {
            if (SkippingStones.Terrain.GlobalRiverPath.Instance != null && SkippingStones.Terrain.GlobalRiverPath.Instance.EvaluateAtDistance(z, out Vector3 cPos, out Vector3 tan, out float rWidth, out float wY))
            {
                Vector3 normal = Vector3.Cross(Vector3.up, tan).normalized;
                float halfW = Mathf.Clamp(rWidth * 0.45f, 4f, 35f);
                float effectiveWidth = halfW * 2f;

                if (effectiveWidth < 15f)
                {
                    Vector3 midPos = cPos + normal * Random.Range(-1.5f, 1.5f);
                    midPos.y = wY + 0.05f;
                    TrySpawnBoostPad(midPos, chunkStartZ, chunkEndZ);
                }
                else if (effectiveWidth < 25f)
                {
                    Vector3 p1 = cPos - normal * (halfW * 0.5f);
                    Vector3 p2 = cPos + normal * (halfW * 0.5f);
                    p1.y = wY + 0.05f;
                    p2.y = wY + 0.05f;
                    TrySpawnBoostPad(p1, chunkStartZ, chunkEndZ);
                    if (Random.value < 0.7f) TrySpawnBoostPad(p2, chunkStartZ, chunkEndZ);
                }
                else
                {
                    Vector3 leftPos = cPos - normal * (halfW * 0.65f);
                    Vector3 midPos = cPos + normal * Random.Range(-halfW * 0.2f, halfW * 0.2f);
                    Vector3 rightPos = cPos + normal * (halfW * 0.65f);
                    leftPos.y = wY + 0.05f;
                    midPos.y = wY + 0.05f;
                    rightPos.y = wY + 0.05f;

                    TrySpawnBoostPad(leftPos, chunkStartZ, chunkEndZ);
                    TrySpawnBoostPad(midPos, chunkStartZ, chunkEndZ);
                    TrySpawnBoostPad(rightPos, chunkStartZ, chunkEndZ);
                }
            }
            else
            {
                float leftX = Random.Range(minX, Mathf.Lerp(minX, maxX, 0.35f));
                float centerX = Random.Range(Mathf.Lerp(minX, maxX, 0.35f), Mathf.Lerp(minX, maxX, 0.65f));
                float rightX = Random.Range(Mathf.Lerp(minX, maxX, 0.65f), maxX);

                TrySpawnBoostPad(new Vector3(leftX, curWaterY + 0.05f, z + Random.Range(-3f, 3f)), chunkStartZ, chunkEndZ);
                TrySpawnBoostPad(new Vector3(centerX, curWaterY + 0.05f, z + Random.Range(-3f, 3f)), chunkStartZ, chunkEndZ);
                TrySpawnBoostPad(new Vector3(rightX, curWaterY + 0.05f, z + Random.Range(-3f, 3f)), chunkStartZ, chunkEndZ);
            }
        }

        // 2. 🪨 장애물 바위 (강폭 비례 통로 확보 스폰)
        for (float z = chunkStartZ + 40f; z < chunkEndZ - 30f; z += Random.Range(25f, 42f))
        {
            if (SkippingStones.Terrain.GlobalRiverPath.Instance != null && SkippingStones.Terrain.GlobalRiverPath.Instance.EvaluateAtDistance(z, out Vector3 cPos, out Vector3 tan, out float rWidth, out float wY))
            {
                Vector3 normal = Vector3.Cross(Vector3.up, tan).normalized;
                float halfW = Mathf.Clamp(rWidth * 0.45f, 4f, 35f);
                float effectiveWidth = halfW * 2f;

                if (effectiveWidth < 15f)
                {
                    if (Random.value < 0.5f)
                    {
                        float side = (Random.value < 0.5f) ? -halfW * 0.75f : halfW * 0.75f;
                        Vector3 rockPos = cPos + normal * side;
                        rockPos.y = wY;
                        TrySpawnObstacleRock(rockPos, chunkStartZ, chunkEndZ);
                    }
                }
                else
                {
                    float offset = Random.Range(-halfW * 0.75f, halfW * 0.75f);
                    Vector3 rockPos = cPos + normal * offset;
                    rockPos.y = wY;
                    TrySpawnObstacleRock(rockPos, chunkStartZ, chunkEndZ);
                }
            }
            else
            {
                float x = Random.Range(minX, maxX);
                TrySpawnObstacleRock(new Vector3(x, curWaterY, z + Random.Range(-4f, 4f)), chunkStartZ, chunkEndZ);
            }
        }

        // 3. 🐟 물고기 (강폭 적응형 스폰)
        for (float z = chunkStartZ + 35f; z < chunkEndZ - 35f; z += Random.Range(45f, 85f))
        {
            if (SkippingStones.Terrain.GlobalRiverPath.Instance != null && SkippingStones.Terrain.GlobalRiverPath.Instance.EvaluateAtDistance(z, out Vector3 cPos, out Vector3 tan, out float rWidth, out float wY))
            {
                Vector3 normal = Vector3.Cross(Vector3.up, tan).normalized;
                float halfW = Mathf.Clamp(rWidth * 0.45f, 4f, 35f);
                float effectiveWidth = halfW * 2f;

                if (effectiveWidth < 15f)
                {
                    Vector3 fPos = cPos + normal * Random.Range(-halfW * 0.4f, halfW * 0.4f);
                    fPos.y = wY;
                    TrySpawnFish(fPos, z, chunkStartZ, chunkEndZ);
                }
                else
                {
                    Vector3 fPos1 = cPos - normal * (halfW * 0.6f);
                    Vector3 fPos2 = cPos + normal * Random.Range(-halfW * 0.2f, halfW * 0.2f);
                    Vector3 fPos3 = cPos + normal * (halfW * 0.6f);
                    fPos1.y = wY;
                    fPos2.y = wY;
                    fPos3.y = wY;

                    TrySpawnFish(fPos1, z, chunkStartZ, chunkEndZ);
                    TrySpawnFish(fPos2, z + Random.Range(6f, 16f), chunkStartZ, chunkEndZ);
                    TrySpawnFish(fPos3, z + Random.Range(12f, 24f), chunkStartZ, chunkEndZ);
                }
            }
            else
            {
                float x1 = Random.Range(minX, Mathf.Lerp(minX, maxX, 0.35f));
                float x2 = Random.Range(Mathf.Lerp(minX, maxX, 0.35f), Mathf.Lerp(minX, maxX, 0.65f));
                float x3 = Random.Range(Mathf.Lerp(minX, maxX, 0.65f), maxX);
                TrySpawnFish(new Vector3(x1, curWaterY, z), z, chunkStartZ, chunkEndZ);
                TrySpawnFish(new Vector3(x2, curWaterY, z + Random.Range(6f, 16f)), z, chunkStartZ, chunkEndZ);
                TrySpawnFish(new Vector3(x3, curWaterY, z + Random.Range(12f, 24f)), z, chunkStartZ, chunkEndZ);
            }
        }

        // 4. 🪷 연잎 군락 (새 청크 지형 경계 내부 엄격 준수)
        CreateLilyPadsGrid(minX, maxX, chunkStartZ + 20f, chunkEndZ - 20f, curWaterY);
    }


    /// <summary>
    /// 상공에서 수직 레이캐스트: MeshCollider 및 TerrainCollider를 모두 완벽 검사
    /// 1) 지형(MeshCollider/TerrainCollider)이 수면 위로 솟아 있는 육지인 경우 -> False
    /// 2) 수심이 너무 얕아(수면과 지형 사이 < 0.35m) 바닥에 파묻히는 경우 -> False
    /// 3) 바닥에 지형/수면이 없는 허공인 경우 -> False
    /// 4) 충분한 수심(waterDepth >= 0.35m)이 확보된 유효한 수면 영역인 경우만 -> True
    /// </summary>
    private bool IsValidWaterPosition(Vector3 pos, float chunkStartZ = 0f, float chunkEndZ = float.MaxValue)
    {
        // 1. 청크 Z 범위 경계 초과 방지
        if (chunkEndZ < float.MaxValue)
        {
            if (pos.z < chunkStartZ + 15f || pos.z > chunkEndZ - 15f) return false;
        }

        float curWater = GetCurrentWaterLevel();
        
        // 2. 초고도 상공(Y = curWater + 250m)에서 아래로 수직 레이캐스트
        float rayStart = Mathf.Max(pos.y + 250f, curWater + 250f);
        Vector3 rayOrigin = new Vector3(pos.x, rayStart, pos.z);

        // RaycastAll로 수면과 바닥 지형(MeshCollider, TerrainCollider 등)을 모두 수집
        RaycastHit[] hits = Physics.RaycastAll(rayOrigin, Vector3.down, 400f, ~0, QueryTriggerInteraction.Collide);
        if (hits == null || hits.Length == 0)
        {
            // 허공 (아무런 콜라이더도 없음)
            return false;
        }

        bool hasWaterSurface = false;
        float groundY = float.MinValue;
        bool hasGround = false;

        foreach (var hit in hits)
        {
            if (hit.collider == null) continue;

            // 수면 콜라이더인지 확인
            if (hit.collider.GetComponent<WaterSurface>() != null || hit.collider.name.Contains("Water"))
            {
                hasWaterSurface = true;
                curWater = hit.point.y; // 실제 레이에 닿은 수면 높이로 보정
            }
            else
            {
                // 지형(MeshCollider, TerrainCollider, 기타 바닥 콜라이더)
                if (hit.point.y > groundY)
                {
                    groundY = hit.point.y;
                    hasGround = true;
                }
            }
        }

        // 수면 콜라이더 또는 지형이 전혀 없는 허공 영역
        if (!hasWaterSurface && !hasGround) return false;

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
                return true; // 너무 가까운 위치에 이미 다른 오브젝트가 존재함
            }
        }
        return false;
    }

    private void TrySpawnBoostPad(Vector3 pos, float chunkStartZ = 0f, float chunkEndZ = float.MaxValue)
    {
        if (!IsValidWaterPosition(pos, chunkStartZ, chunkEndZ)) return;
        if (HasNearbySpawnedEntity(pos, 3.8f)) return;
        CreateBoostPad(pos, Quaternion.identity);
    }

    private void TrySpawnObstacleRock(Vector3 pos, float chunkStartZ = 0f, float chunkEndZ = float.MaxValue)
    {
        if (!IsValidWaterPosition(pos, chunkStartZ, chunkEndZ)) return;
        if (HasNearbySpawnedEntity(pos, 4.2f)) return;
        CreateObstacleRock(pos);
    }

    private void TrySpawnFish(Vector3 pos, float dist, float chunkStartZ = 0f, float chunkEndZ = float.MaxValue)
    {
        if (!IsValidWaterPosition(pos, chunkStartZ, chunkEndZ)) return;
        if (HasNearbySpawnedEntity(pos, 3.5f)) return;
        SpawnSingleFish(pos, dist);
    }

    private static void SafeDestroy(UnityEngine.Object obj)
    {
        if (obj == null) return;
        if (Application.isPlaying) Destroy(obj);
        else DestroyImmediate(obj);
    }
}