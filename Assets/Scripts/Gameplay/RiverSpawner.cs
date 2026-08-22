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
        // BG 청크 릴레이 콜백 구독: 새 1500m 구간이 앞으로 이동할 때 해당 구간 엔티티 재스폰
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

    [Header("수면 높이 연동")]
    public float waterHeight = 16.0f;

    private float GetCurrentWaterLevel()
    {
        WaterSurface ws = FindAnyObjectByType<WaterSurface>();
        if (ws != null)
        {
            Collider c = ws.GetComponent<Collider>();
            if (c != null) return c.bounds.max.y;
            return ws.transform.position.y;
        }

        GameObject water = GameObject.Find("WaterSurface") ?? GameObject.Find("Water_Surface");
        if (water != null)
        {
            Collider col = water.GetComponent<Collider>();
            if (col != null) return col.bounds.max.y;
            return water.transform.position.y;
        }
        return waterHeight;
    }

    /// <summary>
    /// 🏆 장거리 모드: 0m~4800m 전 구간에 부스트 패드, 바위 장애물, 물고기, 친구 깃발, 연잎 군락 배치
    /// 실제 굽이치는 강 중심선(RiverCenter)을 정확히 추적하여 물길 내부에만 스폰
    /// </summary>
    private void GenerateLongDistanceRiver()
    {
        ClearExistingEntities();

        startBankPos = Vector3.zero;
        spawnDirection = Vector3.forward;

        float curWaterY = GetCurrentWaterLevel();
        RiverValleyTerrainGenerator terrainGen = FindAnyObjectByType<RiverValleyTerrainGenerator>();

        // 1. 🚀 가속 부스트 패드 (강물 중심 기준 좌/중/우 균등 분산)
        for (float bDist = 45f; bDist < riverLength - 80f; bDist += Random.Range(35f, 65f))
        {
            float centerOffset = (terrainGen != null) ? (terrainGen.GetRiverCenterX(bDist) - terrainGen.sizeX * 0.5f) : 0f;
            float halfWaterW = 16f; // 안전 수면 반폭

            float leftX = centerOffset + Random.Range(-halfWaterW * 0.85f, -halfWaterW * 0.25f);
            float midX = centerOffset + Random.Range(-halfWaterW * 0.2f, halfWaterW * 0.2f);
            float rightX = centerOffset + Random.Range(halfWaterW * 0.25f, halfWaterW * 0.85f);

            TrySpawnBoostPad(new Vector3(leftX, curWaterY + 0.05f, bDist + Random.Range(-4f, 4f)));
            TrySpawnBoostPad(new Vector3(midX, curWaterY + 0.05f, bDist + Random.Range(-4f, 4f)));
            TrySpawnBoostPad(new Vector3(rightX, curWaterY + 0.05f, bDist + Random.Range(-4f, 4f)));
        }

        // 2. 🪨 강 장애물(바위) 강 중심 좌우 분산 배치
        for (float d = 50f; d < riverLength; d += Random.Range(20f, 38f))
        {
            float centerOffset = (terrainGen != null) ? (terrainGen.GetRiverCenterX(d) - terrainGen.sizeX * 0.5f) : 0f;
            float sideOffset = centerOffset + Random.Range(-15f, 15f);
            TrySpawnObstacleRock(new Vector3(sideOffset, curWaterY, d + Random.Range(-5f, 5f)));
        }

        // 3. 🐟 튀어오르는 물고기 강물 중심 기준 스폰
        for (float fDist = 40f; fDist < riverLength - 60f; fDist += Random.Range(45f, 85f))
        {
            float centerOffset = (terrainGen != null) ? (terrainGen.GetRiverCenterX(fDist) - terrainGen.sizeX * 0.5f) : 0f;
            float fX1 = centerOffset + Random.Range(-14f, -4f);
            float fX2 = centerOffset + Random.Range(-3f, 3f);
            float fX3 = centerOffset + Random.Range(4f, 14f);
            TrySpawnFish(new Vector3(fX1, curWaterY, fDist), fDist);
            TrySpawnFish(new Vector3(fX2, curWaterY, fDist + Random.Range(6f, 16f)), fDist);
            TrySpawnFish(new Vector3(fX3, curWaterY, fDist + Random.Range(12f, 24f)), fDist);
        }

        // 4. 🚩 친구 거리 깃발 (0 ~ 4800m 랭킹 이정표)
        string[] friends = { "라이언 (3위)", "어피치 (2위)", "프로도 (1위)", "콘 (국내 1위)", "무지 (마스터)", "네오 (그랜드마스터)", "튜브 (초월자)", "제이지 (레전드 4,500m)" };
        float[] friendDists = { 120f, 310f, 580f, 920f, 1500f, 2300f, 3300f, 4500f };
        for (int i = 0; i < friends.Length; i++)
        {
            float zPos = friendDists[i];
            float centerOffset = (terrainGen != null) ? (terrainGen.GetRiverCenterX(zPos) - terrainGen.sizeX * 0.5f) : 0f;
            float flagX = centerOffset + ((i % 2 == 0) ? -13f : 13f);
            Vector3 fPos = new Vector3(flagX, curWaterY, zPos);
            if (IsValidWaterPosition(fPos))
            {
                CreateFriendFlag(fPos, friends[i], $"{i + 1}위", zPos);
            }
        }

        // 5. 🪷 연잎 및 연꽃 군락 강물 중심 기준 풍성 생성
        CreateLilyPadsGridMeander(terrainGen, 30f, riverLength, curWaterY);
        CleanupOldGroundObjects();
    }

    /// <summary>
    /// 🎯 타겟 맞추기 모드: 호수 전체 수면 고른 전역 랜덤 배치
    /// </summary>
    private void GenerateTargetAccuracyRiver()
    {
        ClearExistingEntities();

        float curWaterY = GetCurrentWaterLevel();
        startBankPos = new Vector3(-20f, curWaterY, 700f);
        spawnDirection = Vector3.forward;

        // 1. 🎯 플로팅 타겟 과녁 (Floating Target Rings) 수면 전체 분산 배치
        float[] targetLanes = { -18f, -7f, 4f, 15f, 23f };
        for (float z = 50f; z < 1350f; z += 55f)
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
        for (float z = 50f; z < 1350f; z += 60f)
        {
            float x1 = Random.Range(-22f, -5f);
            float x2 = Random.Range(5f, 22f);
            CreateBoostPad(new Vector3(x1, curWaterY + 0.05f, z + Random.Range(-10f, 10f)), Quaternion.identity);
            CreateBoostPad(new Vector3(x2, curWaterY + 0.05f, z + Random.Range(-10f, 10f)), Quaternion.identity);
        }

        // 3. 🐟 튀어오르는 물고기 (Jumping Fish) 수면 전역 배치
        for (float z = 45f; z < 1350f; z += 45f)
        {
            float xPos = Random.Range(-23f, 23f);
            SpawnSingleFish(new Vector3(xPos, curWaterY, z + Random.Range(-10f, 10f)), z);
        }

        // 4. 🪨 장애물 바위 수면 전역 배치
        for (float z = 60f; z < 1350f; z += 50f)
        {
            float xPos = Random.Range(-24f, 24f);
            CreateObstacleRock(new Vector3(xPos, curWaterY, z + Random.Range(-12f, 12f)));
        }

        // 5. 🚩 친구 거리 깃발
        string[] friends = { "라이언 (3위)", "어피치 (2위)", "프로도 (1위)", "콘 (전설)" };
        float[] friendZDistances = { 120f, 310f, 580f, 920f, 1200f };
        for (int i = 0; i < friends.Length; i++)
        {
            float xSide = (i % 2 == 0) ? -18f : 18f;
            CreateFriendFlag(new Vector3(xSide, curWaterY, friendZDistances[i]), friends[i], $"{i + 1}위", friendZDistances[i]);
        }

        // 6. 🪷 풍성한 연잎 및 연꽃 군락
        CreateLilyPadsGrid(-26f, 26f, 30f, 1350f, curWaterY);
        CleanupOldGroundObjects();
    }

    private void ClearExistingEntities()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            SafeDestroy(transform.GetChild(i).gameObject);
        }
    }

    private void CreateTargetRing(Vector3 pos)
    {
        GameObject ring = new GameObject($"TargetZone_{pos.x:F0}x{pos.z:F0}");
        ring.transform.SetParent(transform);
        ring.transform.position = pos;
        ring.AddComponent<FloatingTargetZone>();
    }

    private void CreateBoostPad(Vector3 pos, Quaternion rot)
    {
        GameObject pad = new GameObject($"BoostPad_{pos.x:F0}x{pos.z:F0}");
        pad.transform.SetParent(transform);
        pad.transform.position = pos;
        pad.transform.rotation = rot;
        pad.AddComponent<BoostPad>();
    }

    private void CreateObstacleRock(Vector3 pos)
    {
        GameObject rock = new GameObject($"ObstacleRock_{pos.x:F0}x{pos.z:F0}");
        rock.transform.SetParent(transform);
        rock.transform.position = pos;
        rock.AddComponent<ObstacleRock>();
    }

    private void CreateFriendFlag(Vector3 pos, string name, string rank, float targetDist)
    {
        GameObject flag = new GameObject($"FriendFlag_{name}_{pos.z:F0}");
        flag.transform.SetParent(transform);
        flag.transform.position = pos;
        FriendFlag ff = flag.AddComponent<FriendFlag>();
        ff.friendName = name;
        ff.rankText = rank;
        ff.targetDistance = targetDist;
    }

    private void SpawnSingleFish(Vector3 pos, float dist)
    {
        GameObject fishObj = new GameObject($"JumpingFish_{pos.x:F0}x{pos.z:F0}");
        fishObj.transform.SetParent(transform);
        fishObj.transform.position = pos;
        JumpingFish jf = fishObj.AddComponent<JumpingFish>();

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

    private void CreateLilyPadsGridMeander(RiverValleyTerrainGenerator terrainGen, float minZ, float maxZ, float waterY)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        Material lilyMat = (shader != null) ? new Material(shader) { name = "LilyPadMat" } : null;
        if (lilyMat != null)
        {
            lilyMat.SetColor("_BaseColor", new Color(0.12f, 0.58f, 0.28f, 1f));
            if (lilyMat.HasProperty("_Smoothness")) lilyMat.SetFloat("_Smoothness", 0.4f);
        }

        Material flowerMat = (shader != null) ? new Material(shader) { name = "LotusFlowerMat" } : null;
        if (flowerMat != null) flowerMat.SetColor("_BaseColor", new Color(0.98f, 0.65f, 0.85f, 1f));

        for (float z = minZ; z < maxZ; z += Random.Range(20f, 35f))
        {
            float centerOffset = (terrainGen != null) ? (terrainGen.GetRiverCenterX(z) - terrainGen.sizeX * 0.5f) : 0f;
            float x1 = centerOffset + Random.Range(-13f, -5f);
            float x2 = centerOffset + Random.Range(-3f, 3f);
            float x3 = centerOffset + Random.Range(5f, 13f);

            Vector3 p1 = new Vector3(x1, waterY + 0.04f, z + Random.Range(-5f, 5f));
            Vector3 p2 = new Vector3(x2, waterY + 0.04f, z + Random.Range(-5f, 5f));
            Vector3 p3 = new Vector3(x3, waterY + 0.04f, z + Random.Range(-5f, 5f));

            if (IsValidWaterPosition(p1)) SpawnSingleLilyCluster(p1, lilyMat, flowerMat);
            if (IsValidWaterPosition(p2)) SpawnSingleLilyCluster(p2, lilyMat, flowerMat);
            if (IsValidWaterPosition(p3)) SpawnSingleLilyCluster(p3, lilyMat, flowerMat);
        }
    }

    private void CreateLilyPadsGrid(float minX, float maxX, float minZ, float maxZ, float waterY)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        Material lilyMat = (shader != null) ? new Material(shader) { name = "LilyPadMat" } : null;
        if (lilyMat != null)
        {
            lilyMat.SetColor("_BaseColor", new Color(0.12f, 0.58f, 0.28f, 1f));
            if (lilyMat.HasProperty("_Smoothness")) lilyMat.SetFloat("_Smoothness", 0.4f);
        }

        Material flowerMat = (shader != null) ? new Material(shader) { name = "LotusFlowerMat" } : null;
        if (flowerMat != null) flowerMat.SetColor("_BaseColor", new Color(0.98f, 0.65f, 0.85f, 1f));

        // 🌟 Water_Surface 전체 가로폭에 걸쳐 좌/중/우 3열로 촘촘하고 아름답게 연잎 군락 스폰
        for (float z = minZ; z < maxZ; z += Random.Range(20f, 35f))
        {
            float x1 = Random.Range(minX, minX * 0.3f);
            float x2 = Random.Range(minX * 0.3f, maxX * 0.3f);
            float x3 = Random.Range(maxX * 0.3f, maxX);

            Vector3 p1 = new Vector3(x1, waterY + 0.04f, z + Random.Range(-5f, 5f));
            Vector3 p2 = new Vector3(x2, waterY + 0.04f, z + Random.Range(-5f, 5f));
            Vector3 p3 = new Vector3(x3, waterY + 0.04f, z + Random.Range(-5f, 5f));

            if (IsValidWaterPosition(p1)) SpawnSingleLilyCluster(p1, lilyMat, flowerMat);
            if (IsValidWaterPosition(p2)) SpawnSingleLilyCluster(p2, lilyMat, flowerMat);
            if (IsValidWaterPosition(p3)) SpawnSingleLilyCluster(p3, lilyMat, flowerMat);
        }
    }

    private void SpawnSingleLilyCluster(Vector3 centerPos, Material lilyMat, Material flowerMat)
    {
        GameObject cluster = new GameObject($"LilyCluster_{centerPos.x:F0}x{centerPos.z:F0}");
        cluster.transform.SetParent(transform);
        cluster.transform.position = centerPos;

        int padCount = Random.Range(4, 7);
        for (int p = 0; p < padCount; p++)
        {
            Vector3 offset = new Vector3(Random.Range(-2.2f, 2.2f), 0f, Random.Range(-2.2f, 2.2f));
            GameObject pad = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pad.name = "LilyPad";
            pad.transform.SetParent(cluster.transform);
            pad.transform.position = centerPos + offset;
            float size = Random.Range(1.3f, 2.4f);
            pad.transform.localScale = new Vector3(size, 0.02f, size);
            pad.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            if (lilyMat != null) pad.GetComponent<Renderer>().sharedMaterial = lilyMat;
            SafeDestroy(pad.GetComponent<Collider>());

            if (p == 0 && flowerMat != null)
            {
                GameObject flower = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                flower.name = "LotusFlower";
                flower.transform.SetParent(pad.transform);
                flower.transform.localPosition = new Vector3(0f, 0.6f, 0f);
                flower.transform.localScale = new Vector3(0.4f, 0.8f, 0.4f);
                flower.GetComponent<Renderer>().sharedMaterial = flowerMat;
                SafeDestroy(flower.GetComponent<Collider>());
            }
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
    /// 새로 생성된 chunkStartZ ~ chunkStartZ+1500m 구간의 기존 엔티티 제거 후 재배치.
    /// </summary>
    public void SpawnChunkEntities(float chunkStartZ)
    {
        float chunkEndZ = chunkStartZ + 1500f;
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

        // Water_Surface로부터 실제 수면 가로폭 동적 획득
        GetWaterXBounds(out float minX, out float maxX);

        // 1. 🚀 가속 부스트 패드 (Water_Surface 전체 폭에 걸쳐 좌/중/우 균등 분산)
        for (float z = chunkStartZ + 40f; z < chunkEndZ - 50f; z += Random.Range(35f, 65f))
        {
            float leftX = Random.Range(minX, minX * 0.3f);
            float centerX = Random.Range(minX * 0.3f, maxX * 0.3f);
            float rightX = Random.Range(maxX * 0.3f, maxX);

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
            float x1 = Random.Range(minX, minX * 0.3f);
            float x2 = Random.Range(minX * 0.3f, maxX * 0.3f);
            float x3 = Random.Range(maxX * 0.3f, maxX);
            TrySpawnFish(new Vector3(x1, curWaterY, z), z);
            TrySpawnFish(new Vector3(x2, curWaterY, z + Random.Range(6f, 16f)), z);
            TrySpawnFish(new Vector3(x3, curWaterY, z + Random.Range(12f, 24f)), z);
        }

        // 4. 🪷 연잎 군락 Water_Surface 전폭 풍성 생성
        CreateLilyPadsGrid(minX, maxX, chunkStartZ + 20f, chunkEndZ - 20f, curWaterY);
    }

    /// <summary>
    /// Water_Surface 메쉬/콜라이더의 실제 가로폭을 온전히 1:1로 읽어 반환 (인위적 Clamp 완전 배제)
    /// </summary>
    private void GetWaterXBounds(out float minX, out float maxX)
    {
        GameObject wsObj = GameObject.Find("Water_Surface");
        if (wsObj != null)
        {
            Renderer rend = wsObj.GetComponent<Renderer>();
            if (rend != null && rend.bounds.extents.x > 5f)
            {
                float centerX = rend.bounds.center.x;
                float halfW = rend.bounds.extents.x * 0.95f;
                minX = centerX - halfW;
                maxX = centerX + halfW;
                return;
            }
            BoxCollider bc = wsObj.GetComponent<BoxCollider>();
            if (bc != null)
            {
                float centerX = bc.bounds.center.x;
                float halfW = (bc.bounds.extents.x > 5f ? bc.bounds.extents.x : bc.size.x * 0.5f) * 0.95f;
                minX = centerX - halfW;
                maxX = centerX + halfW;
                return;
            }
            float scaleW = wsObj.transform.lossyScale.x * 5f;
            if (scaleW > 5f)
            {
                minX = wsObj.transform.position.x - scaleW * 0.95f;
                maxX = wsObj.transform.position.x + scaleW * 0.95f;
                return;
            }
        }
        minX = -250f;
        maxX = 250f;
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