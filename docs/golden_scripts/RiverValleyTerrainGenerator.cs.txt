using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteInEditMode]
public class RiverValleyTerrainGenerator : MonoBehaviour
{
    [Header("1. 이름 및 식별자 설정 (Hierarchy & Asset Naming)")]
    [Tooltip("스크립트가 붙어있는 부모 오브젝트의 이름 (비어있을 시 'BG_01')")]
    public string parentObjectName = "BG_01";

    [Tooltip("생성될 지형(Terrain) 자식 오브젝트 및 에셋 이름 (미입력 시: [부모이름]_Ground 로 자동 생성)")]
    public string terrainObjectName = "";

    [Tooltip("생성될 수면(Water) 자식 오브젝트 및 에셋 이름 (미입력 시: [부모이름]_Water 로 자동 생성)")]
    public string waterObjectName = "";

    public string EffectiveParentName => string.IsNullOrWhiteSpace(parentObjectName) ? "BG_01" : parentObjectName.Trim();
    public string EffectiveTerrainName => string.IsNullOrWhiteSpace(terrainObjectName) ? $"{EffectiveParentName}_Ground" : terrainObjectName.Trim();
    public string EffectiveWaterName => string.IsNullOrWhiteSpace(waterObjectName) ? $"{EffectiveParentName}_Water" : waterObjectName.Trim();

    [Header("2. 지형 기본 크기 (Terrain Dimensions)")]
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
    [Tooltip("강폭 최소값 (미터, 아늑한 호수/강 느낌 권장 32m)")]
    public float riverWidthMin = 32f;

    [Tooltip("강폭 최대값 (미터, 아늑한 호수/강 느낌 권장 48m)")]
    public float riverWidthMax = 48f;

    [Tooltip("물 표면(수면)의 Y 높이 (미터)")]
    public float waterHeight = 16f;

    [Tooltip("수면 메쉬 폭 (미터) - 지형 안으로 파고들어 가장자리가 뜨지 않도록 120m 권장")]
    public float waterMeshWidth = 120f;

    [Tooltip("강바닥 중심 기준 Y 높이 (미터)")]
    public float riverBedDepth = 10f;

    [Tooltip("강 물길 및 평야 굽이침 1차 진폭 (미터)")]
    public float meanderPrimaryAmp = 35f;

    [Tooltip("강 물길 및 평야 굽이침 2차 세부 진폭 (미터)")]
    public float meanderSecondaryAmp = 14f;

    [Tooltip("강 물길 3차 미세 진폭 (미터)")]
    public float meanderTertiaryAmp = 6f;

    [Tooltip("강 물길(수로)에도 3차 진폭을 적용할지 여부")]
    public bool applyTertiaryToRiver = false;

    [Header("4. 산맥 및 계곡 평야 설정 (Mountains & Valley)")]
    [Tooltip("강변 시작점 평야의 기준 바닥 Y 높이 (미터, 수면 16m 기준 16.4m 권장)")]
    public float plainStartHeight = 16.4f;

    [Tooltip("평야 시작점 높이의 위치별 랜덤 편차 (±미터, 완만한 모래톱 연출)")]
    public float plainStartHeightVariation = 0.6f;

    [Tooltip("평야 끝(산맥 시작점) 최대 높이 Min (미터, 강변에서 완만한 구릉지 언덕으로 상승)")]
    public float valleyMaxHeightMin = 22f;

    [Tooltip("평야 끝(산맥 시작점) 최대 높이 Max (미터, 강변에서 완만한 구릉지 언덕으로 상승)")]
    public float valleyMaxHeightMax = 32f;

    [Tooltip("강 중심 기준 좌측 평야 반폭 최소값 (미터, 숲이 시야에 가깝게 들어오도록 55m 권장)")]
    public float leftValleyWidthMin = 55f;

    [Tooltip("강 중심 기준 좌측 평야 반폭 최대값 (미터, 85m 권장)")]
    public float leftValleyWidthMax = 85f;

    [Tooltip("강 중심 기준 우측 평야 반폭 최소값 (미터, 55m 권장)")]
    public float rightValleyWidthMin = 55f;

    [Tooltip("강 중심 기준 우측 평야 반폭 최대값 (미터, 85m 권장)")]
    public float rightValleyWidthMax = 85f;

    [Tooltip("산맥 기슭(평야 끝) 3차 굴곡 진폭 (미터)")]
    public float mountainFootTertiaryAmp = 15f;

    [Tooltip("산맥 기슭(평야 끝) 랜덤 노이즈 진폭 (미터)")]
    public float mountainFootNoiseAmp = 12f;

    [Tooltip("산맥 최고 높이 Y 최소값 (미터, 아늑한 배경 산맥)")]
    public float mountainMaxHeightMin = 85f;

    [Tooltip("산맥 최고 높이 Y 최대값 (미터, 아늑한 배경 산맥)")]
    public float mountainMaxHeightMax = 130f;

    [Tooltip("산맥 경사면 폭 최소값 (미터, 완만한 능선)")]
    public float mountainTransitionWidthMin = 140f;

    [Tooltip("산맥 경사면 폭 최대값 (미터, 완만한 능선)")]
    public float mountainTransitionWidthMax = 220f;

    [Header("5. 텍스처 및 물 머티리얼 에셋")]
    public TerrainLayer grassLayer;
    public TerrainLayer rockLayer;
    public TerrainLayer sandLayer;
    public TerrainLayer snowLayer;
    public Material waterMaterial;

    [Header("6. 커스텀 프리셋 슬롯")]
    public RiverValleyTerrainPreset activePreset;

    public void ApplyPresetCozyStream()
    {
        riverWidthMin = 32f;
        riverWidthMax = 48f;
        waterHeight = 16f;
        waterMeshWidth = 120f;
        riverBedDepth = 10f;
        meanderPrimaryAmp = 35f;
        meanderSecondaryAmp = 14f;
        meanderTertiaryAmp = 6f;
        applyTertiaryToRiver = false;

        plainStartHeight = 16.4f;
        plainStartHeightVariation = 0.6f;
        valleyMaxHeightMin = 22f;
        valleyMaxHeightMax = 32f;
        leftValleyWidthMin = 55f;
        leftValleyWidthMax = 85f;
        rightValleyWidthMin = 55f;
        rightValleyWidthMax = 85f;
        mountainFootTertiaryAmp = 15f;
        mountainFootNoiseAmp = 12f;
        mountainMaxHeightMin = 85f;
        mountainMaxHeightMax = 130f;
        mountainTransitionWidthMin = 140f;
        mountainTransitionWidthMax = 220f;
    }

    public void ApplyPresetRuralRiver()
    {
        riverWidthMin = 60f;
        riverWidthMax = 90f;
        waterHeight = 16f;
        waterMeshWidth = 160f;
        riverBedDepth = 9f;
        meanderPrimaryAmp = 45f;
        meanderSecondaryAmp = 18f;
        meanderTertiaryAmp = 8f;
        applyTertiaryToRiver = false;

        plainStartHeight = 16.5f;
        plainStartHeightVariation = 0.8f;
        valleyMaxHeightMin = 35f;
        valleyMaxHeightMax = 50f;
        leftValleyWidthMin = 90f;
        leftValleyWidthMax = 140f;
        rightValleyWidthMin = 90f;
        rightValleyWidthMax = 140f;
        mountainFootTertiaryAmp = 20f;
        mountainFootNoiseAmp = 15f;
        mountainMaxHeightMin = 130f;
        mountainMaxHeightMax = 170f;
        mountainTransitionWidthMin = 160f;
        mountainTransitionWidthMax = 250f;
    }

    public void ApplyPresetGrandRiver()
    {
        riverWidthMin = 120f;
        riverWidthMax = 180f;
        waterHeight = 16f;
        waterMeshWidth = 240f;
        riverBedDepth = 8f;
        meanderPrimaryAmp = 65f;
        meanderSecondaryAmp = 25f;
        meanderTertiaryAmp = 10f;
        applyTertiaryToRiver = false;

        plainStartHeight = 16.5f;
        plainStartHeightVariation = 1.0f;
        valleyMaxHeightMin = 60f;
        valleyMaxHeightMax = 85f;
        leftValleyWidthMin = 180f;
        leftValleyWidthMax = 260f;
        rightValleyWidthMin = 180f;
        rightValleyWidthMax = 260f;
        mountainFootTertiaryAmp = 28f;
        mountainFootNoiseAmp = 20f;
        mountainMaxHeightMin = 180f;
        mountainMaxHeightMax = 240f;
        mountainTransitionWidthMin = 200f;
        mountainTransitionWidthMax = 300f;
    }

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

    public float GetRiverWidth(float z)
    {
        float t = SamplePeriodicNoise1D(z, 4.0f, (float)randomSeed + 17.3f);
        return Mathf.Lerp(riverWidthMin, riverWidthMax, t);
    }

    public float GetLeftValleyWidth(float z)
    {
        float t = SamplePeriodicNoise1D(z, 3.5f, (float)randomSeed + 59.2f);
        return Mathf.Lerp(leftValleyWidthMin, leftValleyWidthMax, t);
    }

    public float GetRightValleyWidth(float z)
    {
        float t = SamplePeriodicNoise1D(z, 3.5f, (float)randomSeed + 93.8f);
        return Mathf.Lerp(rightValleyWidthMin, rightValleyWidthMax, t);
    }

    public float GetMountainTransitionWidth(float z)
    {
        float t = SamplePeriodicNoise1D(z, 3.0f, (float)randomSeed + 81.1f);
        return Mathf.Lerp(mountainTransitionWidthMin, mountainTransitionWidthMax, t);
    }

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
        string pName = EffectiveParentName;
        string tName = EffectiveTerrainName;
        string wName = EffectiveWaterName;

        gameObject.name = pName;

        if (!AssetDatabase.IsValidFolder("Assets/TerrainData"))
        {
            AssetDatabase.CreateFolder("Assets", "TerrainData");
        }

        // 1. 머티리얼 및 레이어 자동 할당
        if (grassLayer == null) grassLayer = AssetDatabase.LoadAssetAtPath<TerrainLayer>("Assets/Design_sources/3D/Environments/SoStylized/Environment/Landscape/Layer/TL_Grass.terrainlayer");
        if (rockLayer == null) rockLayer = AssetDatabase.LoadAssetAtPath<TerrainLayer>("Assets/Design_sources/3D/Environments/SoStylized/Environment/Landscape/Layer/TL_Rock.terrainlayer");
        if (sandLayer == null) sandLayer = AssetDatabase.LoadAssetAtPath<TerrainLayer>("Assets/Design_sources/3D/Environments/SoStylized/Environment/Landscape/Layer/TL_Sand.terrainlayer");
        if (snowLayer == null) snowLayer = AssetDatabase.LoadAssetAtPath<TerrainLayer>("Assets/Design_sources/3D/Environments/SoStylized/Environment/Landscape/Layer/TL_Snow.terrainlayer");
        if (waterMaterial == null) waterMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Design_sources/3D/Environments/SoStylized/Environment/Water/Materials/M_StylizedWater.mat");

        // 2. 지형 에셋 생성 및 할당
        string terrainDataPath = $"Assets/TerrainData/{tName}_TerrainData.asset";
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

        // 3. 지형 높이 계산 (Heights)[cite: 7]
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
                float distFromRiver = Mathf.Abs(worldX - riverCenterX);
                bool isLeft = (worldX < riverCenterX);
                float currentValleyWidth = isLeft ? currentLeftValley : currentRightValley;

                float footTertiary = Mathf.Sin(angle * 3f + (isLeft ? 0.8f : 2.4f)) * mountainFootTertiaryAmp;
                float footNoise = (SamplePeriodicNoise(worldX, worldZ, 6.0f, 6.0f, (float)randomSeed + (isLeft ? 511f : 723f)) - 0.5f) * 2f * mountainFootNoiseAmp;
                float effectiveValleyWidth = Mathf.Max(25f, currentValleyWidth + footTertiary + footNoise);

                float mountainRamp = Mathf.Clamp01((distFromRiver - effectiveValleyWidth) / currentTransitionWidth);
                float mountainShape = Mathf.SmoothStep(0f, 1f, mountainRamp);

                float oct1 = SamplePeriodicNoise(worldX, worldZ, 3.5f, 3.5f, (float)randomSeed + 12f);
                float oct2 = SamplePeriodicNoise(worldX, worldZ, 7.0f, 7.0f, (float)randomSeed + 24f) * 0.5f;
                float oct3 = SamplePeriodicNoise(worldX, worldZ, 14.0f, 14.0f, (float)randomSeed + 48f) * 0.25f;
                float oct4 = SamplePeriodicNoise(worldX, worldZ, 28.0f, 28.0f, (float)randomSeed + 96f) * 0.125f;
                float totalNoise = (oct1 + oct2 + oct3 + oct4) / 1.875f;

                float ridged = 1f - Mathf.Abs(totalNoise * 2f - 1f);
                ridged = Mathf.Pow(ridged, 1.8f);

                float heightRandT = SamplePeriodicNoise(worldX, worldZ, 2.5f, 2.5f, (float)randomSeed + 155f);
                float localMountainMaxHeight = Mathf.Lerp(mountainMaxHeightMin, mountainMaxHeightMax, heightRandT);

                // --- B. 강변 ➜ 산맥 시작부 점진적 상승 구릉지(Valley Slope) ---
                float valleySlopeRand = SamplePeriodicNoise(worldX, worldZ, 2.2f, 2.2f, (float)randomSeed + 333f);
                float currentValleyEndHeight = Mathf.Lerp(valleyMaxHeightMin, valleyMaxHeightMax, valleySlopeRand);

                // 위치별 강변 평야 시작 높이 변동 (±plainStartHeightVariation) -> 자연스러운 모래톱 형성
                float startHeightNoise = (SamplePeriodicNoise(worldX, worldZ, 4.0f, 4.0f, (float)randomSeed + 617f) - 0.5f) * 2f;
                float localPlainStartHeight = plainStartHeight + startHeightNoise * plainStartHeightVariation;

                float shoreNoise = SamplePeriodicNoise(worldX, worldZ, 10.0f, 10.0f, (float)randomSeed + 315f);
                float localHalfWidth = halfRiverWidth + (shoreNoise - 0.5f) * 3.0f;
                localHalfWidth = Mathf.Max(9f, localHalfWidth);
                float bankWidth = 14f + (shoreNoise * 6f);
                float riverShoreEdge = localHalfWidth + bankWidth;

                float valleyProgress = Mathf.Clamp01((distFromRiver - riverShoreEdge) / Mathf.Max(10f, effectiveValleyWidth - riverShoreEdge));
                // 📈 그려주신 지수형 상승 곡선: 초반(강변)은 완만한 평지 유지 -> 산맥 근처에서 점진적 급상승
                float valleySlopeCurve = Mathf.Pow(valleyProgress, 3.2f);

                float valleyNoise1 = SamplePeriodicNoise(worldX, worldZ, 3.0f, 3.0f, (float)randomSeed + 77f);
                float valleyNoise2 = SamplePeriodicNoise(worldX, worldZ, 8.0f, 8.0f, (float)randomSeed + 133f) * 0.4f;
                float valleyNoiseTotal = (valleyNoise1 + valleyNoise2) / 1.4f;

                float targetValleyElevation = Mathf.Lerp(localPlainStartHeight, currentValleyEndHeight, valleySlopeCurve);
                float lowlandElevation = targetValleyElevation + (valleyNoiseTotal - 0.5f) * 3.5f;

                float mountainElevation = mountainShape * (localMountainMaxHeight - currentValleyEndHeight) * (0.25f + 0.75f * ridged);
                float rawHeightY = lowlandElevation + mountainElevation;

                float bedNoise = SamplePeriodicNoise(worldX, worldZ, 6.0f, 6.0f, (float)randomSeed + 219f);
                float localRiverBed = riverBedDepth + (bedNoise - 0.5f) * 3.0f;

                if (distFromRiver <= localHalfWidth)
                {
                    float t = distFromRiver / localHalfWidth;
                    float channelDepth = Mathf.Lerp(localRiverBed, waterHeight - 1.5f, Mathf.Pow(t, 1.7f));
                    rawHeightY = Mathf.Min(channelDepth, waterHeight - 1.0f);
                }
                else if (distFromRiver < riverShoreEdge)
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

        // 4. 스플랫맵 계산 (Alphamaps) - 모래 0.5m 및 들쭉날쭉 해변선 적용
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

                float wGrass = 0f;
                float wRock = 0f;
                float wSand = 0f;
                float wSnow = 0f;

                // 🏖️ 수면 위 0.5m 기준 + 위치별 들쭉날쭉 노이즈 (0.3m ~ 0.8m)
                float beachNoise = SamplePeriodicNoise(worldX, worldZ, 12.0f, 12.0f, (float)randomSeed + 441f);
                float sandReachHeight = 0.55f + (beachNoise - 0.5f) * 0.45f;
                float sandBaseCap = waterHeight + 0.15f;
                float sandTopCap = waterHeight + sandReachHeight;

                if (currentHeight <= sandBaseCap)
                {
                    wSand = 1f;
                }
                else if (currentHeight <= sandTopCap)
                {
                    float sandFactor = 1f - (currentHeight - sandBaseCap) / Mathf.Max(0.05f, sandTopCap - sandBaseCap);
                    wSand = Mathf.SmoothStep(0f, 1f, sandFactor);
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

        // 🌟 5. 자식 오브젝트 1: [Terrain] 오브젝트 구조 보장
        Transform groundTrans = transform.Find(tName) ?? transform.Find("Ground");
        GameObject groundGO;
        if (groundTrans == null)
        {
            groundGO = new GameObject(tName);
            Undo.RegisterCreatedObjectUndo(groundGO, "Create Terrain Child");
            groundGO.transform.SetParent(transform);
        }
        else
        {
            groundGO = groundTrans.gameObject;
            groundGO.name = tName;
        }
        groundGO.transform.localPosition = new Vector3(-sizeX * 0.5f, 0f, 0f);

        Terrain terrain = groundGO.GetComponent<Terrain>();
        if (terrain == null) terrain = Undo.AddComponent<Terrain>(groundGO);
        TerrainCollider col = groundGO.GetComponent<TerrainCollider>();
        if (col == null) col = Undo.AddComponent<TerrainCollider>(groundGO);

        terrain.terrainData = terrainData;
        col.terrainData = terrainData;

        // URP Terrain MaterialTemplate 자동 보장
        if (terrain.materialTemplate == null)
        {
            Shader terrainShader = Shader.Find("Universal Render Pipeline/Terrain/Lit") ?? Shader.Find("Nature/Terrain/Standard");
            if (terrainShader != null)
            {
                terrain.materialTemplate = new Material(terrainShader) { name = "URP_Terrain_Default_MAT" };
            }
        }

        EditorUtility.SetDirty(terrain);
        EditorUtility.SetDirty(groundGO);

        // 🌟 6. 자식 오브젝트 2: [Water] 오브젝트 및 콜라이더 완벽 구축
        Transform waterTrans = transform.Find(wName) ?? transform.Find("Water_Surface") ?? transform.Find("River_Water");
        GameObject riverWaterGO;
        if (waterTrans == null)
        {
            riverWaterGO = new GameObject(wName);
            Undo.RegisterCreatedObjectUndo(riverWaterGO, "Create Water Child");
            riverWaterGO.transform.SetParent(transform);
        }
        else
        {
            riverWaterGO = waterTrans.gameObject;
            riverWaterGO.name = wName;
        }

        riverWaterGO.transform.localPosition = new Vector3(-sizeX * 0.5f, 0f, 0f);

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

        string waterMeshPath = $"Assets/TerrainData/{wName}_WaterMesh.asset";
        UnityEngine.Mesh waterMesh = AssetDatabase.LoadAssetAtPath<UnityEngine.Mesh>(waterMeshPath);
        if (waterMesh == null)
        {
            waterMesh = new UnityEngine.Mesh();
            waterMesh.name = $"{wName}_WaterMesh";
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
        if (mf == null) mf = Undo.AddComponent<MeshFilter>(riverWaterGO);

        var mr = riverWaterGO.GetComponent<MeshRenderer>();
        if (mr == null) mr = Undo.AddComponent<MeshRenderer>(riverWaterGO);

        mf.sharedMesh = waterMesh;
        if (waterMaterial != null) mr.sharedMaterial = waterMaterial;

        // 🌟 수면 물리 콜라이더 & WaterSurface 컴포넌트 자동 부착
        var waterCol = riverWaterGO.GetComponent<BoxCollider>();
        if (waterCol == null) waterCol = Undo.AddComponent<BoxCollider>(riverWaterGO);
        waterCol.isTrigger = true;
        waterCol.center = new Vector3(sizeX * 0.5f, waterHeight - 2.0f, sizeZ * 0.5f);
        waterCol.size = new Vector3(sizeX, 4.0f, sizeZ);

        if (riverWaterGO.GetComponent<WaterSurface>() == null)
        {
            Undo.AddComponent<WaterSurface>(riverWaterGO);
        }

        EditorUtility.SetDirty(riverWaterGO);
        AssetDatabase.SaveAssets();
        Debug.Log($"✅ [{pName}] 지형 생성 완료: {pName}/{tName} 및 {pName}/{wName} 계층 구조와 에셋이 독립적으로 완벽 빌드되었습니다.");
    }
#endif
}