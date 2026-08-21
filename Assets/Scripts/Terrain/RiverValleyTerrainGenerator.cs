using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteInEditMode]
public class RiverValleyTerrainGenerator : MonoBehaviour
{
    [Header("1. 지형 기본 크기 (Terrain Dimensions)")]
    [Tooltip("지형의 가로 폭 (X축, 미터)")]
    public float sizeX = 1000f;

    [Tooltip("지형의 최대 높이 범위 (Y축, 미터)")]
    public float sizeY = 250f;

    [Tooltip("지형의 세로 길이이자 무한 반복 타일 주기 (Z축, 미터)")]
    public float sizeZ = 1500f;

    [Tooltip("높이맵 해상도 (513 권장)")]
    public int heightmapResolution = 513;

    [Tooltip("텍스처(알파맵) 해상도 (512 권장)")]
    public int alphamapResolution = 512;

    [Header("2. 랜덤 시드 설정 (Random Seed)")]
    [Tooltip("랜덤 노이즈 생성을 위한 고유 시드 번호")]
    public int randomSeed = 42;

    [Header("3. 강 및 수면 설정 (River & Water)")]
    [Tooltip("강폭 최소값 (미터)")]
    public float riverWidthMin = 30f;

    [Tooltip("강폭 최대값 (미터)")]
    public float riverWidthMax = 46f;

    [Tooltip("물 표면(수면)의 Y 높이 (미터)")]
    public float waterHeight = 16f;

    [Tooltip("수면 메쉬 폭 (미터) - 지형 안으로 파고들어 가장자리가 뜨지 않도록 100m 권장")]
    public float waterMeshWidth = 100f;

    [Tooltip("강바닥 중심 기준 Y 높이 (미터)")]
    public float riverBedDepth = 9.5f;

    [Tooltip("강 물길 및 평야 굽이침 1차 진폭 (미터)")]
    public float meanderPrimaryAmp = 45f;

    [Tooltip("강 물길 및 평야 굽이침 2차 세부 진폭 (미터)")]
    public float meanderSecondaryAmp = 18f;

    [Tooltip("강 물길 3차 미세 진폭 (미터)")]
    public float meanderTertiaryAmp = 8f;

    [Tooltip("강 물길(수로)에도 3차 진폭을 적용할지 여부 (체크 해제 시 평야/산맥 경계에만 3차 진폭 적용)")]
    public bool applyTertiaryToRiver = false;

    [Header("4. 산맥 및 계곡 평야 설정 (Mountains & Valley)")]
    [Tooltip("계곡 평야의 기본 바닥 Y 높이 (미터)")]
    public float valleyBaseHeight = 19.5f;

    [Tooltip("강 중심 기준 좌측 평야 반폭 최소값 (미터)")]
    public float leftValleyWidthMin = 110f;

    [Tooltip("강 중심 기준 좌측 평야 반폭 최대값 (미터)")]
    public float leftValleyWidthMax = 170f;

    [Tooltip("강 중심 기준 우측 평야 반폭 최소값 (미터)")]
    public float rightValleyWidthMin = 110f;

    [Tooltip("강 중심 기준 우측 평야 반폭 최대값 (미터)")]
    public float rightValleyWidthMax = 170f;

    [Tooltip("산맥 기슭(평야 끝) 3차 굴곡 진폭 (미터) - 산자락이 평야로 굽이치는 3차 진폭")]
    public float mountainFootTertiaryAmp = 25f;

    [Tooltip("산맥 기슭(평야 끝) 랜덤 노이즈 진폭 (미터) - 산맥 시작선의 불규칙한 돌출/만입 크기")]
    public float mountainFootNoiseAmp = 20f;

    [Tooltip("산맥 최고 높이 Y 최소값 (미터)")]
    public float mountainMaxHeightMin = 200f;

    [Tooltip("산맥 최고 높이 Y 최대값 (미터)")]
    public float mountainMaxHeightMax = 240f;

    [Tooltip("산맥 경사면 폭 최소값 (미터)")]
    public float mountainTransitionWidthMin = 260f;

    [Tooltip("산맥 경사면 폭 최대값 (미터)")]
    public float mountainTransitionWidthMax = 350f;

    [Header("5. 텍스처 및 물 머티리얼 에셋 (Terrain Layers & Material)")]
    [Tooltip("잔디 지형 레이어")]
    public TerrainLayer grassLayer;

    [Tooltip("바위/암벽 지형 레이어")]
    public TerrainLayer rockLayer;

    [Tooltip("강변 모래/자갈 지형 레이어")]
    public TerrainLayer sandLayer;

    [Tooltip("산 정상 설경 지형 레이어")]
    public TerrainLayer snowLayer;

    [Tooltip("강물 머티리얼")]
    public Material waterMaterial;

    // Seamless river and valley center line along Z
    public float GetRiverCenterX(float z)
    {
        float zNorm = (z % sizeZ) / sizeZ;
        if (zNorm < 0f) zNorm += 1f;
        float angle = zNorm * Mathf.PI * 2f;
        float center = (sizeX * 0.5f) + 
                       Mathf.Sin(angle) * meanderPrimaryAmp + 
                       Mathf.Sin(angle * 2f + 0.4f) * meanderSecondaryAmp;

        if (applyTertiaryToRiver)
        {
            center += Mathf.Sin(angle * 4f + 1.1f) * meanderTertiaryAmp;
        }

        return center;
    }

    // 1D Seamless Periodic Noise (Z=0 equals Z=sizeZ)
    public float SamplePeriodicNoise1D(float z, float freqZ, float seed)
    {
        float zNorm = (z % sizeZ) / sizeZ;
        if (zNorm < 0f) zNorm += 1f;
        float angle = zNorm * Mathf.PI * 2f;
        float radius = freqZ / (Mathf.PI * 2f);
        float ny = Mathf.Cos(angle) * radius;
        float nz = Mathf.Sin(angle) * radius;
        return Mathf.PerlinNoise(ny + seed, nz + seed);
    }

    // Seamless River Width along Z
    public float GetRiverWidth(float z)
    {
        float t = SamplePeriodicNoise1D(z, 4.0f, (float)randomSeed + 17.3f);
        return Mathf.Lerp(riverWidthMin, riverWidthMax, t);
    }

    // Seamless Left Valley Plain Width along Z
    public float GetLeftValleyWidth(float z)
    {
        float t = SamplePeriodicNoise1D(z, 3.5f, (float)randomSeed + 59.2f);
        return Mathf.Lerp(leftValleyWidthMin, leftValleyWidthMax, t);
    }

    // Seamless Right Valley Plain Width along Z
    public float GetRightValleyWidth(float z)
    {
        float t = SamplePeriodicNoise1D(z, 3.5f, (float)randomSeed + 93.8f);
        return Mathf.Lerp(rightValleyWidthMin, rightValleyWidthMax, t);
    }

    // Mountain Transition Width variation along Z
    public float GetMountainTransitionWidth(float z)
    {
        float t = SamplePeriodicNoise1D(z, 3.0f, (float)randomSeed + 81.1f);
        return Mathf.Lerp(mountainTransitionWidthMin, mountainTransitionWidthMax, t);
    }

    // Periodic 3D cylinder noise (guarantees seamless Z=0 to Z=sizeZ)
    public float SamplePeriodicNoise(float worldX, float worldZ, float freqX, float freqZ, float seed)
    {
        float zNorm = (worldZ % sizeZ) / sizeZ;
        if (zNorm < 0f) zNorm += 1f;
        float angle = zNorm * Mathf.PI * 2f;
        float radius = freqZ / (Mathf.PI * 2f);

        float nx = (worldX / sizeX) * freqX;
        float ny = Mathf.Cos(angle) * radius;
        float nz = Mathf.Sin(angle) * radius;

        float n1 = Mathf.PerlinNoise(nx + seed, ny + seed);
        float n2 = Mathf.PerlinNoise(ny + seed + 53.1f, nz + seed + 53.1f);
        float n3 = Mathf.PerlinNoise(nx + seed + 97.7f, nz + seed + 97.7f);
        return (n1 + n2 + n3) / 3f;
    }

#if UNITY_EDITOR
    [ContextMenu("Generate Terrain")]
    public void Generate()
    {
        if (!AssetDatabase.IsValidFolder("Assets/TerrainData"))
        {
            AssetDatabase.CreateFolder("Assets", "TerrainData");
        }

        // 1. Auto-assign layers if null
        if (grassLayer == null) grassLayer = AssetDatabase.LoadAssetAtPath<TerrainLayer>("Assets/Design_sources/3D/Environments/SoStylized/Environment/Landscape/Layer/TL_Grass.terrainlayer");
        if (rockLayer == null) rockLayer = AssetDatabase.LoadAssetAtPath<TerrainLayer>("Assets/Design_sources/3D/Environments/SoStylized/Environment/Landscape/Layer/TL_Rock.terrainlayer");
        if (sandLayer == null) sandLayer = AssetDatabase.LoadAssetAtPath<TerrainLayer>("Assets/Design_sources/3D/Environments/SoStylized/Environment/Landscape/Layer/TL_Sand.terrainlayer");
        if (snowLayer == null) snowLayer = AssetDatabase.LoadAssetAtPath<TerrainLayer>("Assets/Design_sources/3D/Environments/SoStylized/Environment/Landscape/Layer/TL_Snow.terrainlayer");
        if (waterMaterial == null) waterMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Design_sources/3D/Environments/SoStylized/Environment/Water/Materials/M_StylizedWater.mat");

        // 2. TerrainData
        string terrainDataPath = "Assets/TerrainData/RiverValley_1000x1500.asset";
        TerrainData terrainData = AssetDatabase.LoadAssetAtPath<TerrainData>(terrainDataPath);
        if (terrainData == null)
        {
            terrainData = new TerrainData();
            AssetDatabase.CreateAsset(terrainData, terrainDataPath);
        }

        terrainData.heightmapResolution = heightmapResolution;
        terrainData.size = new Vector3(sizeX, sizeY, sizeZ);
        terrainData.alphamapResolution = alphamapResolution;

        terrainData.terrainLayers = new TerrainLayer[] { grassLayer, rockLayer, sandLayer, snowLayer };

        // 3. Heights
        float[,] heights = new float[heightmapResolution, heightmapResolution];

        for (int zi = 0; zi < heightmapResolution; zi++)
        {
            float zNorm = (zi / (float)(heightmapResolution - 1));
            float worldZ = zNorm * sizeZ;
            float angle = zNorm * Mathf.PI * 2f;
            float riverCenterX = GetRiverCenterX(worldZ);
            float currentRiverWidth = GetRiverWidth(worldZ);
            float halfRiverWidth = currentRiverWidth * 0.5f;
            float currentLeftValley = GetLeftValleyWidth(worldZ);
            float currentRightValley = GetRightValleyWidth(worldZ);
            float currentTransitionWidth = GetMountainTransitionWidth(worldZ);

            for (int xi = 0; xi < heightmapResolution; xi++)
            {
                float worldX = (xi / (float)(heightmapResolution - 1)) * sizeX;
                
                // Distance from meandering river center
                float distFromRiver = Mathf.Abs(worldX - riverCenterX);
                bool isLeft = (worldX < riverCenterX);
                float currentValleyWidth = isLeft ? currentLeftValley : currentRightValley;

                // --- 산맥 기슭(평야 끝) 3차 진폭 + 랜덤 노이즈 굴곡 ---
                float footTertiary = Mathf.Sin(angle * 3f + (isLeft ? 0.8f : 2.4f)) * mountainFootTertiaryAmp;
                float footNoise = (SamplePeriodicNoise(worldX, worldZ, 6.0f, 6.0f, (float)randomSeed + (isLeft ? 511f : 723f)) - 0.5f) * 2f * mountainFootNoiseAmp;
                float effectiveValleyWidth = Mathf.Max(25f, currentValleyWidth + footTertiary + footNoise);

                // --- A. Mountain Elevation: Valley and mountains curve along with the river meander ---
                float mountainRamp = Mathf.Clamp01((distFromRiver - effectiveValleyWidth) / currentTransitionWidth);
                float mountainShape = Mathf.SmoothStep(0f, 1f, mountainRamp);

                float oct1 = SamplePeriodicNoise(worldX, worldZ, 3.5f, 3.5f, (float)randomSeed + 12f);
                float oct2 = SamplePeriodicNoise(worldX, worldZ, 7.0f, 7.0f, (float)randomSeed + 24f) * 0.5f;
                float oct3 = SamplePeriodicNoise(worldX, worldZ, 14.0f, 14.0f, (float)randomSeed + 48f) * 0.25f;
                float oct4 = SamplePeriodicNoise(worldX, worldZ, 28.0f, 28.0f, (float)randomSeed + 96f) * 0.125f;
                float totalNoise = (oct1 + oct2 + oct3 + oct4) / 1.875f;

                float ridged = 1f - Mathf.Abs(totalNoise * 2f - 1f);
                ridged = Mathf.Pow(ridged, 1.8f);

                // Mountain max height randomized smoothly along space between Min and Max
                float heightRandT = SamplePeriodicNoise(worldX, worldZ, 2.5f, 2.5f, (float)randomSeed + 155f);
                float localMountainMaxHeight = Mathf.Lerp(mountainMaxHeightMin, mountainMaxHeightMax, heightRandT);

                float mountainElevation = mountainShape * (localMountainMaxHeight - valleyBaseHeight) * (0.25f + 0.75f * ridged);

                // --- B. Valley Lowlands with Natural Undulation (Curves with the river) ---
                float valleyNoise1 = SamplePeriodicNoise(worldX, worldZ, 3.0f, 3.0f, (float)randomSeed + 77f);
                float valleyNoise2 = SamplePeriodicNoise(worldX, worldZ, 8.0f, 8.0f, (float)randomSeed + 133f) * 0.4f;
                float valleyNoiseTotal = (valleyNoise1 + valleyNoise2) / 1.4f;
                
                float lowlandElevation = valleyBaseHeight + (valleyNoiseTotal - 0.5f) * 4.0f;

                float rawHeightY = lowlandElevation + mountainElevation;

                // --- C. Riverbed & Shoreline Carving ---
                float bedNoise = SamplePeriodicNoise(worldX, worldZ, 6.0f, 6.0f, (float)randomSeed + 219f);
                float localRiverBed = riverBedDepth + (bedNoise - 0.5f) * 3.0f;

                float shoreNoise = SamplePeriodicNoise(worldX, worldZ, 10.0f, 10.0f, (float)randomSeed + 315f);
                float localHalfWidth = halfRiverWidth + (shoreNoise - 0.5f) * 3.0f;
                localHalfWidth = Mathf.Max(9f, localHalfWidth);

                float bankWidth = 14f + (shoreNoise * 6f);

                if (distFromRiver <= localHalfWidth)
                {
                    float t = distFromRiver / localHalfWidth;
                    float channelDepth = Mathf.Lerp(localRiverBed, waterHeight - 1.5f, Mathf.Pow(t, 1.7f));
                    rawHeightY = Mathf.Min(channelDepth, waterHeight - 1.0f);
                }
                else if (distFromRiver < (localHalfWidth + bankWidth))
                {
                    float t = (distFromRiver - localHalfWidth) / bankWidth;
                    float smoothT = Mathf.SmoothStep(0f, 1f, t);
                    float underwaterLip = waterHeight - 1.5f;
                    rawHeightY = Mathf.Lerp(underwaterLip, rawHeightY, smoothT);
                }

                heights[zi, xi] = Mathf.Clamp01(rawHeightY / sizeY);
            }
        }

        terrainData.SetHeights(0, 0, heights);

        // 4. Splatmaps (Alphamaps)
        float[,,] splatmaps = new float[alphamapResolution, alphamapResolution, 4];
        for (int zi = 0; zi < alphamapResolution; zi++)
        {
            float worldZ = (zi / (float)(alphamapResolution - 1)) * sizeZ;
            float riverCenterX = GetRiverCenterX(worldZ);

            for (int xi = 0; xi < alphamapResolution; xi++)
            {
                float worldX = (xi / (float)(alphamapResolution - 1)) * sizeX;
                float normX = xi / (float)(alphamapResolution - 1);
                float normZ = zi / (float)(alphamapResolution - 1);

                float currentHeight = terrainData.GetInterpolatedHeight(normX, normZ);
                float steepness = terrainData.GetSteepness(normX, normZ);
                float distFromRiver = Mathf.Abs(worldX - riverCenterX);

                float wGrass = 0f;
                float wRock = 0f;
                float wSand = 0f;
                float wSnow = 0f;

                if (currentHeight <= waterHeight + 1.2f)
                {
                    wSand = 1f;
                }
                else if (currentHeight <= waterHeight + 3.5f)
                {
                    float sandFactor = 1f - (currentHeight - (waterHeight + 1.2f)) / 2.3f;
                    wSand = Mathf.Clamp01(sandFactor);
                }

                float slopeRockFactor = Mathf.Clamp01((steepness - 18f) / 18f);
                wRock = slopeRockFactor;

                if (currentHeight > 130f)
                {
                    float altRock = Mathf.Clamp01((currentHeight - 130f) / 40f) * 0.7f;
                    wRock = Mathf.Max(wRock, altRock);

                    if (currentHeight > 155f)
                    {
                        float snowFactor = Mathf.Clamp01((currentHeight - 155f) / 50f);
                        float flatFactor = 1f - Mathf.Clamp01((steepness - 15f) / 35f);
                        wSnow = snowFactor * (0.35f + 0.65f * flatFactor);
                    }
                }

                float nonGrass = Mathf.Clamp01(wSand + wRock + wSnow);
                wGrass = Mathf.Clamp01(1f - nonGrass);

                float total = wGrass + wRock + wSand + wSnow;
                if (total > 0.0001f)
                {
                    splatmaps[zi, xi, 0] = wGrass / total;
                    splatmaps[zi, xi, 1] = wRock / total;
                    splatmaps[zi, xi, 2] = wSand / total;
                    splatmaps[zi, xi, 3] = wSnow / total;
                }
                else
                {
                    splatmaps[zi, xi, 0] = 1f;
                }
            }
        }

        terrainData.SetAlphamaps(0, 0, splatmaps);
        EditorUtility.SetDirty(terrainData);

        // 5. Setup Terrain Component
        Terrain terrain = GetComponent<Terrain>();
        TerrainCollider col = GetComponent<TerrainCollider>();
        if (terrain == null) terrain = gameObject.AddComponent<Terrain>();
        if (col == null) col = gameObject.AddComponent<TerrainCollider>();

        terrain.terrainData = terrainData;
        col.terrainData = terrainData;

        // 6. Water Object
        Transform waterTrans = transform.Find("River_Water");
        GameObject riverWaterGO = waterTrans != null ? waterTrans.gameObject : null;
        if (riverWaterGO == null)
        {
            riverWaterGO = new GameObject("River_Water");
            riverWaterGO.transform.SetParent(transform);
            riverWaterGO.AddComponent<MeshFilter>();
            riverWaterGO.AddComponent<MeshRenderer>();
        }

        riverWaterGO.transform.localPosition = Vector3.zero;

        int zSegments = 400;
        int xSegments = 14;
        int vertCount = (zSegments + 1) * (xSegments + 1);
        Vector3[] vertices = new Vector3[vertCount];
        Vector2[] uvs = new Vector2[vertCount];
        Vector3[] normals = new Vector3[vertCount];
        int[] triangles = new int[zSegments * xSegments * 6];

        float waterHalfWidth = waterMeshWidth * 0.5f;

        for (int zi = 0; zi <= zSegments; zi++)
        {
            float zNorm = zi / (float)zSegments;
            float worldZ = zNorm * sizeZ;
            float riverCenterX = GetRiverCenterX(worldZ);

            for (int xi = 0; xi <= xSegments; xi++)
            {
                float xNorm = xi / (float)xSegments;
                float worldX = riverCenterX - waterHalfWidth + xNorm * waterMeshWidth;
                int vertIdx = zi * (xSegments + 1) + xi;

                vertices[vertIdx] = new Vector3(worldX, waterHeight, worldZ);
                uvs[vertIdx] = new Vector2(xNorm * 3f, (worldZ / 50f));
                normals[vertIdx] = Vector3.up;
            }
        }

        int triIdx = 0;
        for (int zi = 0; zi < zSegments; zi++)
        {
            for (int xi = 0; xi < xSegments; xi++)
            {
                int bl = zi * (xSegments + 1) + xi;
                int br = bl + 1;
                int tl = (zi + 1) * (xSegments + 1) + xi;
                int tr = tl + 1;

                triangles[triIdx++] = bl;
                triangles[triIdx++] = tl;
                triangles[triIdx++] = br;

                triangles[triIdx++] = br;
                triangles[triIdx++] = tl;
                triangles[triIdx++] = tr;
            }
        }

        string waterMeshPath = "Assets/TerrainData/RiverWaterMesh.asset";
        UnityEngine.Mesh waterMesh = AssetDatabase.LoadAssetAtPath<UnityEngine.Mesh>(waterMeshPath);
        if (waterMesh == null)
        {
            waterMesh = new UnityEngine.Mesh();
            waterMesh.name = "RiverWaterMesh";
            AssetDatabase.CreateAsset(waterMesh, waterMeshPath);
        }
        else
        {
            waterMesh.Clear();
        }

        waterMesh.vertices = vertices;
        waterMesh.uv = uvs;
        waterMesh.normals = normals;
        waterMesh.triangles = triangles;
        waterMesh.RecalculateBounds();

        EditorUtility.SetDirty(waterMesh);

        var mf = riverWaterGO.GetComponent<MeshFilter>();
        var mr = riverWaterGO.GetComponent<MeshRenderer>();
        mf.sharedMesh = waterMesh;
        if (waterMaterial != null) mr.sharedMaterial = waterMaterial;

        AssetDatabase.SaveAssets();
        Debug.Log($"[RiverValley] 지형 생성 완료 (시드: {randomSeed}): 평야 동적 굴곡 연동, 좌우 비대칭 강폭(Min/Max), 산맥 높이/경사면 랜덤화 및 Z축 1500m 심리스 타일링 적용");
    }
#endif
}

