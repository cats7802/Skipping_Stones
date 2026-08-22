using UnityEngine;



[ExecuteAlways]
public class LakeEnvironmentManager : MonoBehaviour

{
    [Header("청크 오브젝트 참조")]
    public GameObject chunk0;
    public GameObject chunk1;
    public GameObject chunk2;
    private static bool isQuitting = false;
    private static LakeEnvironmentManager _instance;

    public static LakeEnvironmentManager Instance
    {
        get
        {
            if (isQuitting) return null;

            if (_instance == null)
            {
                _instance = FindAnyObjectByType<LakeEnvironmentManager>();
                if (_instance == null && !isQuitting)
                {
                    GameObject helperObj = new GameObject("[AutoBootstrap_LakeEnvironmentManager]");
                    _instance = helperObj.AddComponent<LakeEnvironmentManager>();
                }
            }
            return _instance;
        }
    }

    public enum MapCycleMode
    {
        Sequential, // 순차 순환 (0 -> 1 -> 2 -> 0...)
        Random      // 무작위 순환
    }

    [Header("배경 맵 목록 및 순환 방식")]
    [Tooltip("1개만 넣으면 단일 반복, 여러 개를 넣으면 설정된 모드로 순환합니다.")]
    public GameObject[] mapPrefabs;
    public MapCycleMode cycleMode = MapCycleMode.Sequential;

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
    }

    private void OnEnable()
    {
        if (_instance == null) _instance = this;
        InitReferences();
        ResetEnvironment();
        SetupBGChunks();
    }

    private void OnApplicationQuit()
    {
        isQuitting = true;
    }

    private void OnDestroy()
    {
        if (_instance == this)
        {
            _instance = null;
        }
    }

    public enum EnvironmentTheme
    {
        DynamicJourney, // 🌟 0~4500m 비거리에 따라 낮 -> 노을 -> 밤으로 실시간 자동 전환
        ClearDay,       // ☀️ 맑은 낮 고정
        Sunset,         // 🌅 황금빛 노을 고정
        Twilight,       // 🌆 짙은 석양/땅거미 고정
        MoonlitNight    // 🌙 달빛 밤 호수 고정
    }

    [Header("테마 설정")]
    public EnvironmentTheme currentTheme = EnvironmentTheme.DynamicJourney;

    [Header("거리 구간 설정 (4단계)")]
    public float dayEndDistance = 1500f;       // 0m ~ 1500m: 맑은 낮
    public float sunsetEndDistance = 3000f;    // 1500m ~ 3000m: 황금빛 노을
    public float nightStartDistance = 4500f;   // 3000m ~ 4500m: 짙은 석양 -> 밤

    [Header("🎨 환경 머티리얼 에셋 직결")]
    public Material groundMaterial;
    public Material mountainMaterial;
    public Material waterMaterial;
    public Material skyboxMaterial;

    #region 테마별 환경 프리셋 정의

    [System.Serializable]
    public struct EnvironmentPreset
    {
        public string name;
        public Color skyTop;
        public Color skyEquator;
        public Color skyHorizon;
        public Color groundAmbient;
        public Color sunLightColor;
        public float sunLightIntensity;
        public Vector3 sunLightRotation;
        public Color fogColor;
        public float fogDensity;
        public Color mountainColor;
        public Color waterDeepColor;
        public Color waterShallowColor;
        public Color waterRippleColor;
    }

    [Header("1단계: 청량한 맑은 날")]
    public EnvironmentPreset dayPreset = new EnvironmentPreset
    {
        name = "ClearDay",
        skyTop = new Color(0.15f, 0.52f, 0.95f),
        skyEquator = new Color(0.48f, 0.78f, 0.98f),
        skyHorizon = new Color(0.85f, 0.95f, 1.0f),
        groundAmbient = new Color(0.25f, 0.38f, 0.28f),
        sunLightColor = new Color(1.0f, 0.98f, 0.92f),
        sunLightIntensity = 1.45f,
        sunLightRotation = new Vector3(48f, -28f, 0f),
        fogColor = new Color(0.72f, 0.88f, 0.98f),
        fogDensity = 0.0008f,
        mountainColor = new Color(0.24f, 0.36f, 0.45f),
        waterDeepColor = new Color(0.02f, 0.48f, 0.85f, 1f),
        waterShallowColor = new Color(0.08f, 0.72f, 0.92f, 1f),
        waterRippleColor = new Color(1f, 1f, 1f, 1f)
    };

    [Header("2단계: 황금빛 노을")]
    public EnvironmentPreset sunsetPreset = new EnvironmentPreset
    {
        name = "Sunset",
        skyTop = new Color(0.22f, 0.18f, 0.42f),
        skyEquator = new Color(0.92f, 0.45f, 0.20f),
        skyHorizon = new Color(1.0f, 0.78f, 0.28f),
        groundAmbient = new Color(0.22f, 0.15f, 0.16f),
        sunLightColor = new Color(1.0f, 0.65f, 0.28f),
        sunLightIntensity = 1.55f,
        sunLightRotation = new Vector3(16f, -18f, 0f),
        fogColor = new Color(0.88f, 0.48f, 0.26f),
        fogDensity = 0.0014f,
        mountainColor = new Color(0.32f, 0.20f, 0.28f),
        waterDeepColor = new Color(0.52f, 0.22f, 0.35f, 1f),
        waterShallowColor = new Color(1.0f, 0.72f, 0.35f, 1f),
        waterRippleColor = new Color(1.0f, 0.88f, 0.65f, 1f)
    };

    [Header("3단계: 짙은 석양/땅거미")]
    public EnvironmentPreset twilightPreset = new EnvironmentPreset
    {
        name = "Twilight",
        skyTop = new Color(0.10f, 0.08f, 0.25f),
        skyEquator = new Color(0.58f, 0.20f, 0.38f),
        skyHorizon = new Color(0.88f, 0.35f, 0.24f),
        groundAmbient = new Color(0.12f, 0.08f, 0.12f),
        sunLightColor = new Color(0.88f, 0.42f, 0.28f),
        sunLightIntensity = 1.15f,
        sunLightRotation = new Vector3(6f, -12f, 0f),
        fogColor = new Color(0.38f, 0.16f, 0.32f),
        fogDensity = 0.0017f,
        mountainColor = new Color(0.18f, 0.12f, 0.22f),
        waterDeepColor = new Color(0.24f, 0.10f, 0.26f, 1f),
        waterShallowColor = new Color(0.88f, 0.40f, 0.32f, 1f),
        waterRippleColor = new Color(1.0f, 0.60f, 0.50f, 1f)
    };

    [Header("4단계: 달빛/별빛 밤 호수")]
    public EnvironmentPreset nightPreset = new EnvironmentPreset
    {
        name = "MoonlitNight",
        skyTop = new Color(0.02f, 0.04f, 0.10f),
        skyEquator = new Color(0.06f, 0.12f, 0.24f),
        skyHorizon = new Color(0.12f, 0.20f, 0.38f),
        groundAmbient = new Color(0.04f, 0.06f, 0.10f),
        sunLightColor = new Color(0.45f, 0.60f, 0.85f),
        sunLightIntensity = 0.45f,
        sunLightRotation = new Vector3(-25f, 35f, 0f),
        fogColor = new Color(0.06f, 0.10f, 0.20f),
        fogDensity = 0.0020f,
        mountainColor = new Color(0.06f, 0.08f, 0.15f),
        waterDeepColor = new Color(0.02f, 0.05f, 0.15f, 1f),
        waterShallowColor = new Color(0.18f, 0.32f, 0.58f, 1f),
        waterRippleColor = new Color(0.70f, 0.85f, 1.0f, 1f)
    };

    #endregion

    private Light mainLight;

    [Header("청크 자동 측정 정보")]
    [Tooltip("자동 감지된 배경 1청크의 Z축 길이")]
    public float autoChunkSize = 1500f;

    private GameObject baseBGChunk0;
    private readonly System.Collections.Generic.List<GameObject> dynamicChunks = new System.Collections.Generic.List<GameObject>();
    private readonly System.Collections.Generic.HashSet<int> spawnedChunkIndices = new System.Collections.Generic.HashSet<int>();

    public System.Action<float> OnChunkRelayed;

    private void Start()
    {
        InitReferences();
        ResetEnvironment();
        SetupBGChunks();
    }

    private void Update()
    {
        UpdateBGStreaming();
    }

    public void InitReferences()
    {
        EnsureDirectionalLight();
        EnsureMaterials();
        CleanUpLegacyObjects();

        if (GetComponent<EnvironmentTestHelper>() == null)
        {
            gameObject.AddComponent<EnvironmentTestHelper>();
        }
    }

    private void InitializeFirstChunk()
    {
        if (chunk0 == null)
        {
            // 씬 내 Terrain이 속한 최상위 부모를 첫 번째 청크로 자동 지정 (이름 무관)
            Terrain terrain = FindAnyObjectByType<Terrain>();
            if (terrain != null)
            {
                chunk0 = terrain.transform.parent != null ? terrain.transform.parent.gameObject : terrain.gameObject;
            }
        }
    }

    private GameObject GetMapPrefabForChunk(int chunkIndex)
    {
        if (mapPrefabs != null && mapPrefabs.Length > 0)
        {
            if (cycleMode == MapCycleMode.Random && mapPrefabs.Length > 1)
            {
                int randIdx = (chunkIndex == 0) ? 0 : UnityEngine.Random.Range(0, mapPrefabs.Length);
                return mapPrefabs[randIdx];
            }
            else
            {
                int seqIdx = chunkIndex % mapPrefabs.Length;
                return mapPrefabs[seqIdx];
            }
        }
        return chunk0;
    }
    private void EnsureMaterials()
    {
        if (skyboxMaterial == null || !skyboxMaterial.HasProperty("_SkyTint"))
        {
            skyboxMaterial = Resources.Load<Material>("Skybox_Procedural_MAT");
            if (skyboxMaterial == null)
            {
                Shader skyShader = Shader.Find("Skybox/Procedural");
                if (skyShader != null)
                {
                    skyboxMaterial = new Material(skyShader);
                    skyboxMaterial.name = "Dynamic_Procedural_Skybox";
                }
            }
        }

        if (skyboxMaterial != null && RenderSettings.skybox != skyboxMaterial)
        {
            RenderSettings.skybox = skyboxMaterial;
        }

        if (mountainMaterial == null)
        {
            mountainMaterial = Resources.Load<Material>("Mountain_MAT");
            if (mountainMaterial == null)
            {
                var mountainObj = GameObject.Find("Left_Mountains") ?? GameObject.Find("Right_Mountains");
                if (mountainObj != null)
                {
                    var mr = mountainObj.GetComponent<MeshRenderer>();
                    if (mr != null) mountainMaterial = mr.sharedMaterial;
                }
            }
        }
        if (groundMaterial == null)
        {
            groundMaterial = Resources.Load<Material>("Ground_MAT");
        }
        if (waterMaterial == null)
        {
            waterMaterial = Resources.Load<Material>("Water_MAT");
            if (waterMaterial == null)
            {
                var ws = GameObject.Find("Water_Surface");
                if (ws != null)
                {
                    var mr = ws.GetComponent<MeshRenderer>();
                    if (mr != null) waterMaterial = mr.sharedMaterial;
                }
            }
        }
    }

    private void CleanUpLegacyObjects()
    {
        string[] legacyNames = {
            "Environment_SunMoonDisc", "Sunset_SunDisc", "ClearDay_SunDisc",
            "VFX_LakeSurfaceMist", "VFX_SunsetWaterDust"
        };

        foreach (var name in legacyNames)
        {
            var obj = GameObject.Find(name);
            if (obj != null) DestroyImmediate(obj);
        }
    }

    public void ResetEnvironment()
    {
        ClearDynamicChunks();

        if (currentTheme == EnvironmentTheme.DynamicJourney || currentTheme == EnvironmentTheme.ClearDay)
        {
            ApplyPresetDirect(dayPreset);
        }
        else if (currentTheme == EnvironmentTheme.Sunset)
        {
            ApplyPresetDirect(sunsetPreset);
        }
        else if (currentTheme == EnvironmentTheme.Twilight)
        {
            ApplyPresetDirect(twilightPreset);
        }
        else if (currentTheme == EnvironmentTheme.MoonlitNight)
        {
            ApplyPresetDirect(nightPreset);
        }
    }

    public void UpdateEnvironmentByDistance(float distance)
    {
        EnvironmentPreset target;

        if (distance <= dayEndDistance)
        {
            float blendT = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(dayEndDistance * 0.5f, dayEndDistance, distance));
            target = LerpPreset(dayPreset, sunsetPreset, blendT * 0.55f);
        }
        else if (distance <= sunsetEndDistance)
        {
            float t = Mathf.Clamp01((distance - dayEndDistance) / (sunsetEndDistance - dayEndDistance));
            target = LerpPreset(sunsetPreset, twilightPreset, Mathf.SmoothStep(0f, 1f, t));
        }
        else if (distance <= nightStartDistance)
        {
            float t = Mathf.Clamp01((distance - sunsetEndDistance) / (nightStartDistance - sunsetEndDistance));
            target = LerpPreset(twilightPreset, nightPreset, Mathf.SmoothStep(0f, 1f, t));
        }
        else
        {
            target = nightPreset;
        }

        ApplyPresetDirect(target);
    }

    private EnvironmentPreset LerpPreset(EnvironmentPreset a, EnvironmentPreset b, float t)
    {
        return new EnvironmentPreset
        {
            name = (t > 0.5f) ? b.name : a.name,
            skyTop = Color.Lerp(a.skyTop, b.skyTop, t),
            skyEquator = Color.Lerp(a.skyEquator, b.skyEquator, t),
            skyHorizon = Color.Lerp(a.skyHorizon, b.skyHorizon, t),
            groundAmbient = Color.Lerp(a.groundAmbient, b.groundAmbient, t),
            sunLightColor = Color.Lerp(a.sunLightColor, b.sunLightColor, t),
            sunLightIntensity = Mathf.Lerp(a.sunLightIntensity, b.sunLightIntensity, t),
            sunLightRotation = Vector3.Lerp(a.sunLightRotation, b.sunLightRotation, t),
            fogColor = Color.Lerp(a.fogColor, b.fogColor, t),
            fogDensity = Mathf.Lerp(a.fogDensity, b.fogDensity, t),
            mountainColor = Color.Lerp(a.mountainColor, b.mountainColor, t),
            waterDeepColor = Color.Lerp(a.waterDeepColor, b.waterDeepColor, t),
            waterShallowColor = Color.Lerp(a.waterShallowColor, b.waterShallowColor, t),
            waterRippleColor = Color.Lerp(a.waterRippleColor, b.waterRippleColor, t)
        };
    }

    public void ApplyPresetDirect(EnvironmentPreset p)
    {
        EnsureDirectionalLight();
        EnsureMaterials();

        if (mainLight != null)
        {
            mainLight.color = p.sunLightColor;
            mainLight.intensity = p.sunLightIntensity;
            mainLight.transform.rotation = Quaternion.Euler(p.sunLightRotation);
        }

        if (skyboxMaterial != null)
        {
            if (RenderSettings.skybox != skyboxMaterial) RenderSettings.skybox = skyboxMaterial;
            if (skyboxMaterial.HasProperty("_SkyTint")) skyboxMaterial.SetColor("_SkyTint", p.skyTop);
            if (skyboxMaterial.HasProperty("_GroundColor")) skyboxMaterial.SetColor("_GroundColor", p.groundAmbient);
            float exposure = (p.name == "MoonlitNight") ? 0.22f : (p.name == "Twilight") ? 0.65f : 1.25f;
            if (skyboxMaterial.HasProperty("_Exposure")) skyboxMaterial.SetFloat("_Exposure", exposure);
            float thick = (p.name == "Sunset") ? 1.5f : (p.name == "Twilight") ? 1.9f : 0.85f;
            if (skyboxMaterial.HasProperty("_AtmosphereThickness")) skyboxMaterial.SetFloat("_AtmosphereThickness", thick);
        }

        if (Camera.main != null)
        {
            Camera.main.clearFlags = CameraClearFlags.Skybox;
            Camera.main.backgroundColor = p.skyTop;
        }

        if (mountainMaterial != null)
        {
            if (mountainMaterial.HasProperty("_BaseColor")) mountainMaterial.SetColor("_BaseColor", p.mountainColor);
            else if (mountainMaterial.HasProperty("_Color")) mountainMaterial.SetColor("_Color", p.mountainColor);
        }

        if (waterMaterial != null)
        {
            if (waterMaterial.HasProperty("_BaseColor")) waterMaterial.SetColor("_BaseColor", p.waterDeepColor);
            if (waterMaterial.HasProperty("_ShallowColor")) waterMaterial.SetColor("_ShallowColor", p.waterShallowColor);
            if (waterMaterial.HasProperty("_RippleColor")) waterMaterial.SetColor("_RippleColor", p.waterRippleColor);
            if (waterMaterial.HasProperty("_FoamIntensity")) waterMaterial.SetFloat("_FoamIntensity", 0.65f);
            if (waterMaterial.HasProperty("_DepthThreshold")) waterMaterial.SetFloat("_DepthThreshold", 0.20f);
        }

        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = p.skyTop;
        RenderSettings.ambientEquatorColor = p.skyEquator;
        RenderSettings.ambientGroundColor = p.groundAmbient;

        DualCameraSetup dualCam = FindAnyObjectByType<DualCameraSetup>();
        bool isTopDownReplay = (dualCam != null && dualCam.currentMode == DualCameraSetup.CameraMode.TopDownReplay);

        RenderSettings.fog = !isTopDownReplay;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogColor = p.fogColor;
        RenderSettings.fogDensity = p.fogDensity;

        DynamicGI.UpdateEnvironment();
    }

    private void EnsureDirectionalLight()
    {
        if (mainLight != null) return;
        Light[] lights = FindObjectsByType<Light>(FindObjectsInactive.Include);
        foreach (var l in lights)
        {
            if (l.type == LightType.Directional)
            {
                mainLight = l;
                break;
            }
        }

        if (mainLight == null)
        {
            GameObject lightObj = new GameObject("Environment_DirectionalLight");
            lightObj.transform.SetParent(transform);
            mainLight = lightObj.AddComponent<Light>();
            mainLight.type = LightType.Directional;
            mainLight.shadows = LightShadows.Soft;
        }
    }

    #region BG 순차 인스턴스화 및 자동 크기 감지 무한 스트리밍

    /// <summary>
    /// 배경 프리팹 내부의 렌더러/콜라이더를 순회하여 Z축 길이를 자동 측정
    /// </summary>
    private void AutoDetectChunkSize()
    {
        if (baseBGChunk0 == null) return;

        Renderer[] renderers = baseBGChunk0.GetComponentsInChildren<Renderer>();
        if (renderers.Length > 0)
        {
            Bounds combined = renderers[0].bounds;
            foreach (var r in renderers)
            {
                // 파티클/트레일 등 비정상적으로 큰 바운드는 제외
                if (r is ParticleSystemRenderer || r is TrailRenderer) continue;
                combined.Encapsulate(r.bounds);
            }
            if (combined.size.z > 50f)
            {
                autoChunkSize = combined.size.z;
            }
        }
    }

    public void SetupBGChunks()
    {
        InitializeFirstChunk(); // 🌟 첫 번째 청크 자동 인식

        if (chunk0 == null && (mapPrefabs == null || mapPrefabs.Length == 0)) return;

        if (baseBGChunk0 == null)
        {
            baseBGChunk0 = GameObject.Find("BG_01");
            if (baseBGChunk0 == null)
            {
                var g = GameObject.Find("Ground");
                if (g != null)
                {
                    baseBGChunk0 = (g.transform.parent != null && g.transform.parent.name.Contains("BG_01"))
                                   ? g.transform.parent.gameObject
                                   : g;
                }
            }
        }

        AutoDetectChunkSize();
        spawnedChunkIndices.Add(0);
    }

    public GameObject EnsureChunkSpawned(int chunkIndex)
    {
        if (chunkIndex <= 0) return baseBGChunk0;
        if (spawnedChunkIndices.Contains(chunkIndex))
        {
            return dynamicChunks.Find(c => c != null && c.name.Contains($"Section_{chunkIndex}"));
        }

        SetupBGChunks();
        if (baseBGChunk0 == null) return null;

        float targetZ = chunkIndex * autoChunkSize;

        GameObject newChunk = Instantiate(baseBGChunk0, baseBGChunk0.transform.parent);
        newChunk.name = $"BG_01_Section_{chunkIndex}_{targetZ:F0}m";

        newChunk.transform.localPosition = baseBGChunk0.transform.localPosition + new Vector3(0f, 0f, targetZ);
        newChunk.transform.localRotation = baseBGChunk0.transform.localRotation;
        newChunk.transform.localScale = baseBGChunk0.transform.localScale;

        Transform duplicatePier = newChunk.transform.Find("Lakeside_WoodenPier");
        if (duplicatePier != null) Destroy(duplicatePier.gameObject);

        Transform ws = newChunk.transform.Find("Water_Surface");
        if (ws != null)
        {
            WaterSurface wsc = ws.GetComponent<WaterSurface>();
            if (wsc != null) Destroy(wsc);
        }

        dynamicChunks.Add(newChunk);
        spawnedChunkIndices.Add(chunkIndex);

        OnChunkRelayed?.Invoke(targetZ);

        return newChunk;
    }

    public void ClearDynamicChunks()
    {
        foreach (var chunk in dynamicChunks)
        {
            if (chunk != null) Destroy(chunk);
        }
        dynamicChunks.Clear();
        spawnedChunkIndices.Clear();
        spawnedChunkIndices.Add(0);
    }

    public void PlaceBGAtPage(int page)
    {
        SetupBGChunks();
        for (int p = 1; p <= page; p++)
        {
            EnsureChunkSpawned(p);
        }
    }

    public void PlaceTerrainAtPage(int page) => PlaceBGAtPage(page);
    public void SetupTerrainChunks() => SetupBGChunks();

    /// <summary>
    /// 🌟 청크 크기(autoChunkSize)에 비례하여 다음 청크를 사전에 무한 자동 스폰
    /// </summary>
    public void UpdateBGStreaming()
    {
        if (!Application.isPlaying) return;
        SetupBGChunks();

        float trackZ = GetTrackingZ();
        if (autoChunkSize <= 0f) autoChunkSize = 1500f;

        // 현재 위치 기준으로 다음 청크 인덱스 계산 (청크의 55% 지점 통과 시 다음 청크 사전 로드)
        int requiredChunkIndex = Mathf.FloorToInt((trackZ + (autoChunkSize * 0.45f)) / autoChunkSize);

        for (int i = 1; i <= requiredChunkIndex; i++)
        {
            if (!spawnedChunkIndices.Contains(i))
            {
                EnsureChunkSpawned(i);
            }
        }
    }

    public void UpdateTerrainStreaming() => UpdateBGStreaming();

    private float GetTrackingZ()
    {
        var gc = FindAnyObjectByType<GameController>();
        if (gc != null && gc.currentState == GameController.GameState.Replay)
            return Camera.main != null ? Camera.main.transform.position.z : 0f;
        var stone = FindAnyObjectByType<SkippingStone>();
        if (stone != null) return stone.transform.position.z;
        return Camera.main != null ? Camera.main.transform.position.z : 0f;
    }

    #endregion
}