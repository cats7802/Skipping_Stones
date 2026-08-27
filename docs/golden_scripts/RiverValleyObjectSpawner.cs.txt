using System;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteInEditMode]
public class RiverValleyObjectSpawner : MonoBehaviour
{
    [System.Serializable]
    public class PropSpawnRule
    {
        public string categoryName = "Trees";
        public bool enabled = true;
        [Tooltip("배치할 프리팹 목록 (무작위 선택)")]
        public GameObject[] prefabs;

        [Header("밀도 및 군락 (Density & Clustering)")]
        [Range(10, 50000)]
        [Tooltip("배치 시도할 최대 총 인스턴스 수 (최대 50,000)")]
        public int spawnCount = 200;

        [Range(0.01f, 1.0f)]
        [Tooltip("스폰 확률 및 군락 밀도 필터")]
        public float densityThreshold = 0.45f;

        [Tooltip("군락(클러스터) 노이즈 주파수 (낮을수록 큼직한 숲 군락 형성)")]
        public float clusterFrequency = 3.5f;

        [Header("지형 조건 필터 (Terrain Filters)")]
        [Tooltip("스폰 가능한 최소 월드 Y 높이")]
        public float minHeight = 17.5f;

        [Tooltip("스폰 가능한 최대 월드 Y 높이")]
        public float maxHeight = 100f;

        [Tooltip("스폰 가능한 최소 경사도 (0~90도)")]
        [Range(0f, 90f)]
        public float minSlope = 0f;

        [Tooltip("스폰 가능한 최대 경사도 (0~90도)")]
        [Range(0f, 90f)]
        public float maxSlope = 30f;

        [Tooltip("강 중심으로부터 최소 거리 (미터)")]
        public float minDistanceFromRiver = 20f;

        [Header("변형 및 이격 거리 (Variation & Spacing)")]
        [Tooltip("오브젝트 간 최소 이격 거리 (미터, 오브젝트끼리 겹치는 현상 방지)")]
        public float minSpacing = 4.0f;

        [Tooltip("최소 스케일 배율")]
        public float minScale = 0.8f;

        [Tooltip("최대 스케일 배율")]
        public float maxScale = 1.25f;

        [Tooltip("지형 경사면 법선 방향으로 정렬할 비율 (0: 완전 수직 Y, 1: 경사면과 일치)")]
        [Range(0f, 1f)]
        public float alignToNormalRatio = 0.2f;

        [Tooltip("Y 오프셋 (지형 바닥 파고듦/뜸 보정)")]
        public float yOffset = 0f;
    }

    public enum TerrainTargetMode
    {
        AutoDetect,     // Terrain 우선, 없으면 MeshCollider 자동 탐색
        UnityTerrain,   // Unity Terrain 컴포넌트 사용
        MeshCollider    // 일반 Mesh / MeshCollider 오브젝트 사용
    }

    [Header("1. 대상 지형 및 연동 (Target Terrain)")]
    [Tooltip("지형 타겟 모드 (Unity Terrain 또는 일반 Mesh Collider)")]
    public TerrainTargetMode targetMode = TerrainTargetMode.AutoDetect;

    [Tooltip("배치 대상 Terrain (Unity Terrain 모드 시 사용)")]
    public Terrain targetTerrain;

    [Tooltip("배치 대상 Mesh GameObject / Collider (MeshCollider 모드 시 사용. 비어있을 시 자동 탐색)")]
    public GameObject targetMeshObject;

    [Tooltip("메쉬 Raycast 감지용 레이어 마스크 (기본: Everything)")]
    public LayerMask meshRaycastMask = ~0;

    [Header("1-1. 수면(Water) 감지 및 물 위 스폰 방지")]
    [Tooltip("수면 콜라이더 자동 배제 (Water, RS_Surface, River_Surface 등의 콜라이더를 바닥 지형에서 제외)")]
    public bool excludeWaterColliders = true;

    [Tooltip("수면 오브젝트 (비어있을 시 씬/부모의 WaterSurface 또는 Water 태그 자동 탐색)")]
    public GameObject targetWaterObject;

    [Tooltip("수면 위 안전 여유 높이 (이 높이 이하의 수면 근접/수중 지점에는 스폰 차단)")]
    public float waterHeightOffset = 0.3f;

    [Tooltip("물길 중심점 계산을 위한 지형 생성기 (선택 사항, 비어있을 시 자동 탐색)")]
    public RiverValleyTerrainGenerator terrainGenerator;

    [Header("2. 랜덤 시드 (Random Seed)")]
    public int seed = 12345;

    [Header("3. 생성될 자식 오브젝트 이름")]
    public string propsRootName = "BG_01_Props";

    [Header("4. 전역 중복/겹침 방지 (Global Overlap Prevention)")]
    [Tooltip("카테고리 간에도 서로 너무 가까이 겹치지 않도록 방지")]
    public bool preventCrossCategoryOverlap = true;

    [Tooltip("전역 최소 이격 거리 (미터)")]
    public float globalMinSpacing = 1.5f;

    [Header("5. 프랍 카테고리별 배치 규칙 (Spawn Rules)")]
    public List<PropSpawnRule> spawnRules = new List<PropSpawnRule>();

    public void ResetToDefaultRules()
    {
#if UNITY_EDITOR
        spawnRules.Clear();

        // 1. 소나무 & 침엽수 (Pines)
        var pineRule = new PropSpawnRule
        {
            categoryName = "🌲 소나무 군락 (Pine Trees)",
            enabled = true,
            spawnCount = 900,
            densityThreshold = 0.40f,
            clusterFrequency = 3.2f,
            minHeight = 16.8f,
            maxHeight = 75f,
            minSlope = 0f,
            maxSlope = 30f,
            minDistanceFromRiver = 20f,
            minSpacing = 4.2f, // 🌲 소나무 간 4.2m 이격
            minScale = 0.8f,
            maxScale = 1.35f,
            alignToNormalRatio = 0.2f,
            yOffset = -0.1f,
            prefabs = LoadPrefabs(
                "Assets/Design_sources/3D/Environments/SoStylized/Environment/Trees/Pine/Prefabs/P_Pine01.prefab",
                "Assets/Design_sources/3D/Environments/SoStylized/Environment/Trees/Pine/Prefabs/P_Pine02.prefab",
                "Assets/Design_sources/3D/Environments/SoStylized/Environment/Trees/Pine/Prefabs/P_Pine03.prefab"
            )
        };
        spawnRules.Add(pineRule);

        // 2. 덤불 & 관목 (Bushes)
        var bushRule = new PropSpawnRule
        {
            categoryName = "🌿 덤불 및 관목 (Bushes)",
            enabled = true,
            spawnCount = 1400,
            densityThreshold = 0.35f,
            clusterFrequency = 4.0f,
            minHeight = 16.2f,
            maxHeight = 60f,
            minSlope = 0f,
            maxSlope = 28f,
            minDistanceFromRiver = 16f,
            minSpacing = 2.0f, // 🌿 덤불 간 2.0m 이격
            minScale = 0.8f,
            maxScale = 1.35f,
            alignToNormalRatio = 0.35f,
            yOffset = -0.05f,
            prefabs = LoadPrefabs(
                "Assets/Design_sources/3D/Environments/SoStylized/Environment/Foliage/Prefabs/P_Bush1.prefab",
                "Assets/Design_sources/3D/Environments/SoStylized/Environment/Foliage/Prefabs/P_Bush2.prefab",
                "Assets/Design_sources/3D/Environments/SoStylized/Environment/Foliage/Prefabs/P_Bush3.prefab",
                "Assets/Design_sources/3D/Environments/SoStylized/Environment/Foliage/Prefabs/P_BushLeafy01.prefab"
            )
        };
        spawnRules.Add(bushRule);

        // 3. 야생화 & 꽃 (Wildflowers)
        var flowerRule = new PropSpawnRule
        {
            categoryName = "🌸 야생화 군락 (Flowers)",
            enabled = true,
            spawnCount = 1000,
            densityThreshold = 0.45f,
            clusterFrequency = 5.5f,
            minHeight = 16.2f,
            maxHeight = 45f,
            minSlope = 0f,
            maxSlope = 20f,
            minDistanceFromRiver = 15f,
            minSpacing = 0.9f, // 🌸 꽃 간 0.9m 이격
            minScale = 0.85f,
            maxScale = 1.3f,
            alignToNormalRatio = 0.5f,
            yOffset = 0f,
            prefabs = LoadPrefabs(
                "Assets/Design_sources/3D/Environments/SoStylized/Environment/Foliage/Prefabs/P_FlowerBush01.prefab",
                "Assets/Design_sources/3D/Environments/SoStylized/Environment/Foliage/Prefabs/P_FlowerBush02.prefab",
                "Assets/Design_sources/3D/Environments/SoStylized/Environment/Foliage/Prefabs/P_FlowerCrocus01.prefab",
                "Assets/Design_sources/3D/Environments/SoStylized/Environment/Foliage/Prefabs/P_FlowerCrocus02.prefab"
            )
        };
        spawnRules.Add(flowerRule);

        // 4. 바위 및 조약돌 (Rocks & Boulders)
        var rockRule = new PropSpawnRule
        {
            categoryName = "🪨 바위 및 암석 (Rocks)",
            enabled = true,
            spawnCount = 700,
            densityThreshold = 0.38f,
            clusterFrequency = 3.5f,
            minHeight = 16.0f,
            maxHeight = 95f,
            minSlope = 2f,
            maxSlope = 55f,
            minDistanceFromRiver = 14f,
            minSpacing = 4.0f, // 🪨 바위 간 4.0m 이격
            minScale = 0.65f,
            maxScale = 1.5f,
            alignToNormalRatio = 0.65f,
            yOffset = -0.15f,
            prefabs = LoadPrefabs(
                "Assets/Design_sources/3D/Environments/SoStylized/Environment/Rocks/Prefabs/Classic/P_RockClassic1.prefab",
                "Assets/Design_sources/3D/Environments/SoStylized/Environment/Rocks/Prefabs/Classic/P_RockClassic2.prefab",
                "Assets/Design_sources/3D/Environments/SoStylized/Environment/Rocks/Prefabs/Classic/P_RockClassic3.prefab",
                "Assets/Design_sources/3D/Environments/SoStylized/Environment/Rocks/Prefabs/Classic/P_RockClumpClassic1.prefab",
                "Assets/Design_sources/3D/Environments/SoStylized/Environment/Rocks/Prefabs/Classic/P_RockClumpClassic2.prefab"
            )
        };
        spawnRules.Add(rockRule);
#endif
    }

#if UNITY_EDITOR
    private static GameObject[] LoadPrefabs(params string[] paths)
    {
        var list = new List<GameObject>();
        foreach (var path in paths)
        {
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (go != null) list.Add(go);
        }
        return list.ToArray();
    }

    // 초고속 공간 분할 해시 그리드 (Spatial Hash Grid)
    private class SpatialGrid
    {
        private readonly float cellSize;
        private readonly Dictionary<long, List<Vector2>> grid = new Dictionary<long, List<Vector2>>();

        public SpatialGrid(float cellSize)
        {
            this.cellSize = Mathf.Max(0.5f, cellSize);
        }

        private long GetKey(int cx, int cz)
        {
            return ((long)cx << 32) ^ (uint)cz;
        }

        public bool IsTooClose(Vector2 pos, float minDistance)
        {
            float sqrDist = minDistance * minDistance;
            int cellRadius = Mathf.CeilToInt(minDistance / cellSize);
            int originX = Mathf.FloorToInt(pos.x / cellSize);
            int originZ = Mathf.FloorToInt(pos.y / cellSize);

            for (int dx = -cellRadius; dx <= cellRadius; dx++)
            {
                for (int dz = -cellRadius; dz <= cellRadius; dz++)
                {
                    long key = GetKey(originX + dx, originZ + dz);
                    if (grid.TryGetValue(key, out var points))
                    {
                        for (int i = 0; i < points.Count; i++)
                        {
                            if ((points[i] - pos).sqrMagnitude < sqrDist)
                            {
                                return true; // 이격 거리 미달 (중복/겹침)
                            }
                        }
                    }
                }
            }
            return false;
        }

        public void Add(Vector2 pos)
        {
            int cx = Mathf.FloorToInt(pos.x / cellSize);
            int cz = Mathf.FloorToInt(pos.y / cellSize);
            long key = GetKey(cx, cz);

            if (!grid.TryGetValue(key, out var points))
            {
                points = new List<Vector2>();
                grid[key] = points;
            }
            points.Add(pos);
        }
    }

    private struct TerrainSampleResult
    {
        public bool valid;
        public Vector3 position;
        public Vector3 normal;
        public float slope;
    }

    [ContextMenu("오브젝트 자동 배치 (Spawn Props)")]
    public void SpawnAllProps()
    {
        ClearAllProps();

        // 1. 타겟 모드 판별 및 영역(Bounds) 설정
        bool useMesh = false;
        Bounds spawnBounds = new Bounds();
        Collider[] targetColliders = null;

        if (targetMode == TerrainTargetMode.UnityTerrain)
        {
            useMesh = false;
        }
        else if (targetMode == TerrainTargetMode.MeshCollider)
        {
            useMesh = true;
        }
        else // AutoDetect
        {
            if (targetTerrain != null)
            {
                useMesh = false;
            }
            else if (targetMeshObject != null)
            {
                useMesh = true;
            }
            else
            {
                // 컴포넌트 자동 탐색
                Terrain foundTerrain = GetComponentInChildren<Terrain>();
                if (foundTerrain == null) foundTerrain = GetComponentInParent<Terrain>();
                if (foundTerrain == null) foundTerrain = Terrain.activeTerrain;

                if (foundTerrain != null)
                {
                    targetTerrain = foundTerrain;
                    useMesh = false;
                }
                else
                {
                    useMesh = true;
                }
            }
        }

        TerrainData tData = null;
        Vector3 terrainPos = Vector3.zero;
        Vector3 terrainSize = Vector3.zero;

        if (!useMesh)
        {
            if (targetTerrain == null) targetTerrain = GetComponentInChildren<Terrain>();
            if (targetTerrain == null) targetTerrain = GetComponentInParent<Terrain>();
            if (targetTerrain == null) targetTerrain = Terrain.activeTerrain;

            if (targetTerrain == null)
            {
                Debug.LogWarning("[RiverValleyObjectSpawner] Unity Terrain을 찾지 못해 Mesh 모드로 자동 전환을 시도합니다.");
                useMesh = true;
            }
            else
            {
                tData = targetTerrain.terrainData;
                terrainSize = tData.size;
                terrainPos = targetTerrain.transform.position;
                spawnBounds = new Bounds(terrainPos + terrainSize * 0.5f, terrainSize);
            }
        }

        float detectedWaterHeight = float.MinValue;
        bool hasWaterDetected = false;

        // 수면 오브젝트 및 높이 자동 탐색
        if (targetWaterObject != null)
        {
            detectedWaterHeight = targetWaterObject.transform.position.y;
            hasWaterDetected = true;
            var wCol = targetWaterObject.GetComponent<Collider>();
            if (wCol != null) detectedWaterHeight = Mathf.Max(detectedWaterHeight, wCol.bounds.max.y);
        }
        else
        {
            var ws = GetComponentInChildren<WaterSurface>();
            if (ws == null) ws = GetComponentInParent<WaterSurface>();
            if (ws == null) ws = FindAnyObjectByType<WaterSurface>();
            if (ws != null)
            {
                detectedWaterHeight = ws.transform.position.y;
                hasWaterDetected = true;
                var wCol = ws.GetComponent<Collider>();
                if (wCol != null) detectedWaterHeight = Mathf.Max(detectedWaterHeight, wCol.bounds.max.y);
            }
            else if (terrainGenerator != null)
            {
                detectedWaterHeight = terrainGenerator.waterHeight;
                hasWaterDetected = true;
            }
        }

        if (useMesh)
        {
            GameObject meshRoot = targetMeshObject != null ? targetMeshObject : gameObject;
            var allCols = meshRoot.GetComponentsInChildren<Collider>();

            if (allCols == null || allCols.Length == 0)
            {
                Debug.LogError("[RiverValleyObjectSpawner] 메쉬 지형에 Collider가 없습니다! 대상 오브젝트에 MeshCollider 또는 Collider 컴포넌트를 추가해주세요.");
                return;
            }

            // 지형 바닥 콜라이더만 선별 (수면 콜라이더 제외)
            var validGroundCols = new List<Collider>();
            foreach (var col in allCols)
            {
                if (excludeWaterColliders)
                {
                    string cName = col.gameObject.name.ToUpperInvariant();
                    if (cName.Contains("WATER") || cName.Contains("SURFACE") || cName.Contains("RS_") || cName.Contains("RIVER_START"))
                    {
                        // 수면 오브젝트의 높이를 아직 못 구했다면 여기서 자동 획득
                        if (!hasWaterDetected)
                        {
                            detectedWaterHeight = col.bounds.max.y;
                            hasWaterDetected = true;
                        }
                        continue; // 바닥 지형 대상에서 제외
                    }
                }
                validGroundCols.Add(col);
            }

            if (validGroundCols.Count == 0)
            {
                validGroundCols.AddRange(allCols); // 필터링 후 아무것도 없으면 전체 사용
            }

            targetColliders = validGroundCols.ToArray();

            // 순수 지형 Collider들의 Bounds만 정확하게 병합 (섹션 실제 크기 한정)
            spawnBounds = targetColliders[0].bounds;
            for (int i = 1; i < targetColliders.Length; i++)
            {
                spawnBounds.Encapsulate(targetColliders[i].bounds);
            }

            terrainPos = spawnBounds.min;
            terrainSize = spawnBounds.size;
        }

        if (terrainGenerator == null) terrainGenerator = GetComponent<RiverValleyTerrainGenerator>();
        if (terrainGenerator == null) terrainGenerator = GetComponentInParent<RiverValleyTerrainGenerator>();

        // 부모 그룹 오브젝트 생성
        Transform rootTrans = transform.Find(propsRootName);
        GameObject rootGO = rootTrans != null ? rootTrans.gameObject : null;
        if (rootGO == null)
        {
            rootGO = new GameObject(propsRootName);
            rootGO.transform.SetParent(transform);
            rootGO.transform.localPosition = Vector3.zero;
            rootGO.transform.localRotation = Quaternion.identity;
            rootGO.transform.localScale = Vector3.one;
            Undo.RegisterCreatedObjectUndo(rootGO, "Create Props Root");
        }

        int totalSpawned = 0;
        var prng = new System.Random(seed);

        // 전역 및 카테고리별 중복 방지 공간 그리드 초기화
        SpatialGrid globalGrid = new SpatialGrid(Mathf.Max(2.0f, globalMinSpacing));

        for (int r = 0; r < spawnRules.Count; r++)
        {
            var rule = spawnRules[r];
            if (!rule.enabled || rule.prefabs == null || rule.prefabs.Length == 0) continue;

            // 카테고리별 서브 그룹
            string cleanCatName = rule.categoryName.Replace(" ", "_").Split('(')[0].Trim();
            GameObject catGO = new GameObject(cleanCatName);
            catGO.transform.SetParent(rootGO.transform);
            catGO.transform.localPosition = Vector3.zero;
            catGO.transform.localRotation = Quaternion.identity;
            catGO.transform.localScale = Vector3.one;
            Undo.RegisterCreatedObjectUndo(catGO, "Create Category Group");

            int ruleSeed = seed + r * 7919;
            int spawnedForRule = 0;
            SpatialGrid categoryGrid = new SpatialGrid(Mathf.Max(1.0f, rule.minSpacing));

            for (int i = 0; i < rule.spawnCount; i++)
            {
                // 균일 격자 + 지터 (Jittered Grid Distribution)
                float u = (float)prng.NextDouble();
                float v = (float)prng.NextDouble();

                float worldX = terrainPos.x + u * terrainSize.x;
                float worldZ = terrainPos.z + v * terrainSize.z;
                Vector2 pos2D = new Vector2(worldX, worldZ);

                // 🌟 1. 중복/겹침 방지 검사 (Spatial Hash Collision Check)
                if (rule.minSpacing > 0.01f && categoryGrid.IsTooClose(pos2D, rule.minSpacing))
                {
                    continue; // 동일 카테고리 내 오브젝트끼리 겹침 방지
                }

                if (preventCrossCategoryOverlap && globalMinSpacing > 0.01f && globalGrid.IsTooClose(pos2D, globalMinSpacing))
                {
                    continue; // 다른 카테고리 간 겹침 방지
                }

                // 2. 군락 노이즈 필터 (Clustering Noise)
                float clusterNoise = SamplePeriodicNoise(worldX, worldZ, terrainSize.x, terrainSize.z, rule.clusterFrequency, rule.clusterFrequency, ruleSeed);
                if (clusterNoise < rule.densityThreshold) continue;

                // 3. 지형 샘플링 (높이, 경사도, 노멀)
                TerrainSampleResult sample;
                if (!useMesh)
                {
                    sample = SampleUnityTerrain(tData, terrainPos, terrainSize, worldX, worldZ);
                }
                else
                {
                    sample = SampleMeshTerrain(worldX, worldZ, spawnBounds, targetColliders);
                }

                if (!sample.valid) continue;

                float worldY = sample.position.y;
                float slope = sample.slope;

                if (worldY < rule.minHeight || worldY > rule.maxHeight) continue;
                if (slope < rule.minSlope || slope > rule.maxSlope) continue;

                // 4. 수면(Water) 높이 체크 (물속 및 수면 근접 스폰 원천 차단)
                if (hasWaterDetected && worldY <= (detectedWaterHeight + waterHeightOffset))
                {
                    continue; // 수면보다 낮거나 너무 가까운 위치 제외
                }

                // 5. 강 중심점 거리 검사 (물속 및 강 중심 배제)
                if (terrainGenerator != null)
                {
                    float riverCenterX = terrainGenerator.GetRiverCenterX(worldZ);
                    float distFromRiver = Mathf.Abs(worldX - riverCenterX);
                    if (distFromRiver < rule.minDistanceFromRiver) continue;
                }

                // 6. 프리팹 무작위 선택
                var validPrefabs = new List<GameObject>();
                foreach (var p in rule.prefabs) if (p != null) validPrefabs.Add(p);
                if (validPrefabs.Count == 0) continue;

                GameObject chosenPrefab = validPrefabs[prng.Next(validPrefabs.Count)];

                // 6. 회전 및 지형 법선 정렬
                Vector3 normal = sample.normal;
                float randomYaw = (float)prng.NextDouble() * 360f;
                Quaternion baseRot = Quaternion.Euler(0f, randomYaw, 0f);
                Quaternion normalRot = Quaternion.FromToRotation(Vector3.up, normal) * baseRot;
                Quaternion finalRot = Quaternion.Slerp(baseRot, normalRot, rule.alignToNormalRatio);

                // 7. 스케일 랜덤화
                float scaleT = (float)prng.NextDouble();
                float scaleVal = Mathf.Lerp(rule.minScale, rule.maxScale, scaleT);
                Vector3 finalScale = Vector3.one * scaleVal;

                // 8. 인스턴스 생성
                Vector3 spawnPos = new Vector3(worldX, worldY + rule.yOffset, worldZ);
                GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(chosenPrefab, catGO.transform);
                if (instance == null) instance = Instantiate(chosenPrefab, catGO.transform);

                instance.transform.position = spawnPos;
                instance.transform.rotation = finalRot;
                instance.transform.localScale = finalScale;

                // 정적 배칭 및 오클루전 컬링 최적화 플래그 지정
                GameObjectUtility.SetStaticEditorFlags(instance, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic);

                // 그리드에 등록하여 다음 오브젝트와의 중복 방지
                categoryGrid.Add(pos2D);
                globalGrid.Add(pos2D);

                Undo.RegisterCreatedObjectUndo(instance, "Spawn Prop Instance");
                spawnedForRule++;
                totalSpawned++;
            }

            Debug.Log($" • [{rule.categoryName}]: {spawnedForRule}개 배치 완료");
        }

        EditorUtility.SetDirty(gameObject);
        string modeStr = useMesh ? "메쉬 지형 (Mesh)" : "Terrain 지형";
        Debug.Log($"[RiverValleyObjectSpawner] ✅ [{modeStr}] 총 {totalSpawned}개의 프랍 및 식생 오브젝트가 성공적으로 배치되었습니다! (시드: {seed})");
    }

    private TerrainSampleResult SampleUnityTerrain(TerrainData tData, Vector3 terrainPos, Vector3 terrainSize, float worldX, float worldZ)
    {
        TerrainSampleResult result = new TerrainSampleResult();
        float normX = Mathf.Clamp01((worldX - terrainPos.x) / terrainSize.x);
        float normZ = Mathf.Clamp01((worldZ - terrainPos.z) / terrainSize.z);

        result.position = new Vector3(worldX, terrainPos.y + tData.GetInterpolatedHeight(normX, normZ), worldZ);
        result.slope = tData.GetSteepness(normX, normZ);
        result.normal = tData.GetInterpolatedNormal(normX, normZ);
        result.valid = true;
        return result;
    }

    private TerrainSampleResult SampleMeshTerrain(float worldX, float worldZ, Bounds bounds, Collider[] targetColliders)
    {
        TerrainSampleResult result = new TerrainSampleResult();
        float rayStartY = bounds.max.y + 10f;
        float rayDistance = bounds.size.y + 20f;
        Ray ray = new Ray(new Vector3(worldX, rayStartY, worldZ), Vector3.down);

        RaycastHit[] hits = Physics.RaycastAll(ray, rayDistance, meshRaycastMask);
        if (hits == null || hits.Length == 0)
        {
            result.valid = false;
            return result;
        }

        // targetColliders 중 가장 높은 충돌 지점 선택
        RaycastHit bestHit = default;
        float highestY = float.MinValue;
        bool found = false;

        foreach (var hit in hits)
        {
            bool isTarget = false;
            for (int i = 0; i < targetColliders.Length; i++)
            {
                if (hit.collider == targetColliders[i])
                {
                    isTarget = true;
                    break;
                }
            }

            if (isTarget && hit.point.y > highestY)
            {
                highestY = hit.point.y;
                bestHit = hit;
                found = true;
            }
        }

        if (!found)
        {
            result.valid = false;
            return result;
        }

        result.position = bestHit.point;
        result.normal = bestHit.normal;
        result.slope = Vector3.Angle(bestHit.normal, Vector3.up);
        result.valid = true;
        return result;
    }

    [ContextMenu("모든 프랍 지우기 (Clear All Props)")]
    public void ClearAllProps()
    {
        Transform rootTrans = transform.Find(propsRootName);
        if (rootTrans != null)
        {
            Undo.DestroyObjectImmediate(rootTrans.gameObject);
        }
    }

    private static float SamplePeriodicNoise(float worldX, float worldZ, float sizeX, float sizeZ, float freqX, float freqZ, float seed)
    {
        float zNorm = (worldZ % sizeZ) / sizeZ;
        if (zNorm < 0f) zNorm += 1f;
        float angle = zNorm * Mathf.PI * 2f;
        float radius = freqZ / (Mathf.PI * 2f);

        float nx = (worldX / sizeX) * freqX;
        float ny = Mathf.Cos(angle) * radius;
        float nz = Mathf.Sin(angle) * radius;

        return Mathf.PerlinNoise(nx + seed, ny + nz + seed);
    }
#endif
}
