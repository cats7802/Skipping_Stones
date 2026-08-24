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

    [Header("1. 대상 지형 및 연동 (Target Terrain)")]
    [Tooltip("배치 대상 Terrain (비어있을 시 부모/자식/씬에서 자동 탐색)")]
    public Terrain targetTerrain;

    [Tooltip("물길 중심점 계산을 위한 지형 생성기 (비어있을 시 자동 탐색)")]
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

    [ContextMenu("오브젝트 자동 배치 (Spawn Props)")]
    public void SpawnAllProps()
    {
        ClearAllProps();

        if (targetTerrain == null) targetTerrain = GetComponentInChildren<Terrain>();
        if (targetTerrain == null) targetTerrain = Terrain.activeTerrain;
        if (targetTerrain == null)
        {
            Debug.LogError("[RiverValleyObjectSpawner] 대상 Terrain을 찾을 수 없습니다!");
            return;
        }

        if (terrainGenerator == null) terrainGenerator = GetComponent<RiverValleyTerrainGenerator>();
        if (terrainGenerator == null) terrainGenerator = GetComponentInParent<RiverValleyTerrainGenerator>();

        TerrainData tData = targetTerrain.terrainData;
        Vector3 terrainSize = tData.size;
        Vector3 terrainPos = targetTerrain.transform.position;

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
                    continue; // 다른 카테고리(예: 소나무 위에 바위) 간 겹침 방지
                }

                // 2. 군락 노이즈 필터 (Clustering Noise)
                float clusterNoise = SamplePeriodicNoise(worldX, worldZ, terrainSize.x, terrainSize.z, rule.clusterFrequency, rule.clusterFrequency, ruleSeed);
                if (clusterNoise < rule.densityThreshold) continue;

                // 3. 높이 및 경사도 검사
                float normX = Mathf.Clamp01((worldX - terrainPos.x) / terrainSize.x);
                float normZ = Mathf.Clamp01((worldZ - terrainPos.z) / terrainSize.z);

                float worldY = terrainPos.y + tData.GetInterpolatedHeight(normX, normZ);
                float slope = tData.GetSteepness(normX, normZ);

                if (worldY < rule.minHeight || worldY > rule.maxHeight) continue;
                if (slope < rule.minSlope || slope > rule.maxSlope) continue;

                // 4. 강 중심점 거리 검사 (물속 및 강 중심 배제)
                if (terrainGenerator != null)
                {
                    float riverCenterX = terrainGenerator.GetRiverCenterX(worldZ);
                    float distFromRiver = Mathf.Abs(worldX - riverCenterX);
                    if (distFromRiver < rule.minDistanceFromRiver) continue;
                }

                // 5. 프리팹 무작위 선택
                var validPrefabs = new List<GameObject>();
                foreach (var p in rule.prefabs) if (p != null) validPrefabs.Add(p);
                if (validPrefabs.Count == 0) continue;

                GameObject chosenPrefab = validPrefabs[prng.Next(validPrefabs.Count)];

                // 6. 회전 및 지형 법선 정렬
                Vector3 normal = tData.GetInterpolatedNormal(normX, normZ);
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
        Debug.Log($"[RiverValleyObjectSpawner] ✅ 총 {totalSpawned}개의 프랍 및 식생 오브젝트가 지형에 성공적으로 배치되었습니다! (시드: {seed})");
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
