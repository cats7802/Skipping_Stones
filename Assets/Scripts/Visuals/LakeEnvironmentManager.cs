using UnityEngine;



public class LakeEnvironmentManager : MonoBehaviour
{
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
            }
            return _instance;
        }
    }

    [System.Serializable]
    public class ChunkSlot
    {
        [Tooltip("이 슬롯의 기본(메인) 맵 프리팹 (필수 등록 - 비어있을 시 명시적 오류 발생)")]
        public GameObject baseMapPrefab;

        [Tooltip("체크 시 아래 변주 목록 중 랜덤하게 선택하여 스폰합니다.")]
        public bool useVariations = false;

        [Tooltip("변주(랜덤) 프리팹 목록 (useVariations 체크 시 활성화)")]
        public GameObject[] variationPrefabs;
    }

    [Header("0. 맵 메타 정보")]
    [Tooltip("3번 맵 선택 및 로비/인게임 UI에 표시될 맵 이름(타이틀)")]
    public string mapTitle = "호수 (Lake)";

    [Tooltip("3번 맵 선택 및 로비 UI에 표시될 2D 맵 썸네일 이미지")]
    public Sprite mapThumbnail;

    [Header("1. 모듈러 스토리 시퀀스 설정")]
    [Tooltip("시작 전용 맵 프리팹 (SM, 비어있을 시 슬롯 1번의 Base Map 사용)")]
    public GameObject startMapPrefab;

    [Tooltip("루프 슬롯 목록")]
    public System.Collections.Generic.List<ChunkSlot> loopSlots = new System.Collections.Generic.List<ChunkSlot>();

    [Tooltip("엔딩 맵 프리팹 (EM, 비어있거나 목표 거리 미지정 시 무한 루프)")]
    public GameObject endingMapPrefab;

    [Tooltip("엔딩 맵 진입 목표 거리 (0 이하 시 무한 루프)")]
    public float targetClearDistance = 0f;

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
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
        // 타이틀/로비에서는 인게임 배경 청크를 사전 생성하지 않고 대기
        // 인게임 세션 시작 시 GameController -> SetupEnvironmentForSession()에서 호출
    }

    public void SetupEnvironmentForSession()
    {
        ResetEnvironment();
        SetupBGChunks();
    }

    private void Update()
    {
        // 인게임 상태가 아닐 때는 배경 스트리밍 갱신 중단
        if (SkippingStones.UI.MetaUIManager.Instance != null &&
            SkippingStones.UI.MetaUIManager.Instance.currentScreen != SkippingStones.UI.MetaScreen.InGame)
        {
            return;
        }

        UpdateBGStreaming();
    }

    public void InitReferences()
    {
        EnsureDirectionalLight();

        if (GetComponent<EnvironmentTestHelper>() == null)
        {
            gameObject.AddComponent<EnvironmentTestHelper>();
        }
    }

    private void InitializeFirstChunk()
    {
        if (baseBGChunk0 == null)
        {
            // 씬 내 Terrain이 속한 최상위 부모를 첫 번째 청크로 자동 지정 (이름 무관)
            Terrain terrain = FindAnyObjectByType<Terrain>();
            if (terrain != null)
            {
                baseBGChunk0 = terrain.transform.parent != null ? terrain.transform.parent.gameObject : terrain.gameObject;
            }
        }
    }

    /// <summary>
    /// 🌟 청크 인덱스 및 위치에 따른 프리팹 결정 (엄격한 검증 및 에러 검출)
    /// </summary>
    public GameObject GetMapPrefabForChunk(int chunkIndex, float targetZ = 0f)
    {
        // 0번 시작 청크인 경우: StartMap이 있으면 최우선 반환
        if (chunkIndex == 0 && startMapPrefab != null)
        {
            ValidatePrefabPlatform(startMapPrefab, "StartMapPrefab (SM)");
            return startMapPrefab;
        }

        // 엔딩 목표 거리 도달 및 엔딩 맵 지정 시: EM 반환
        if (targetClearDistance > 0f && targetZ >= targetClearDistance && endingMapPrefab != null)
        {
            return endingMapPrefab;
        }

        // 슬롯이 설정되어 있는 경우
        if (loopSlots != null && loopSlots.Count > 0)
        {
            int slotIdx = (chunkIndex == 0) ? 0 : (chunkIndex - 1) % loopSlots.Count;
            var slot = loopSlots[slotIdx];
            if (slot == null)
            {
                Debug.LogError($"[LakeEnvironmentManager] ❌ 루프 슬롯 [{slotIdx + 1}] 데이터가 비어있습니다! (인스펙터 확인 필요)");
                return baseBGChunk0;
            }

            if (slot.baseMapPrefab == null)
            {
                Debug.LogError($"[LakeEnvironmentManager] ❌ 루프 슬롯 [{slotIdx + 1}]의 BaseMapPrefab이 등록되지 않았습니다! (인스펙터 확인 필요)");
                return baseBGChunk0;
            }

            // 0번 시작 위치로 사용될 베이스 맵인 경우 발판 존재 여부 엄격 검증
            if (chunkIndex == 0)
            {
                ValidatePrefabPlatform(slot.baseMapPrefab, $"LoopSlot [{slotIdx + 1}] BaseMap");
            }

            // 변주 체크박스가 켜져 있고 유효한 변주 프리팹이 등록되어 있는 경우
            if (slot.useVariations && slot.variationPrefabs != null && slot.variationPrefabs.Length > 0)
            {
                // BaseMap을 포함한 변주 후보 풀 구성
                System.Collections.Generic.List<GameObject> candidates = new System.Collections.Generic.List<GameObject>();
                if (slot.baseMapPrefab != null) candidates.Add(slot.baseMapPrefab);
                foreach (var v in slot.variationPrefabs)
                {
                    if (v != null) candidates.Add(v);
                }

                if (candidates.Count > 0)
                {
                    int rand = UnityEngine.Random.Range(0, candidates.Count);
                    return candidates[rand];
                }
            }

            return slot.baseMapPrefab;
        }

        // 슬롯이 아예 비어있는데 씬에 BG_01도 없는 경우 콘솔 에러 출력
        if (baseBGChunk0 == null)
        {
            Debug.LogError("[LakeEnvironmentManager] ❌ 씬에 배치된 배경(BG_01)도 없고, 루프 슬롯(loopSlots)도 비어있어 맵을 스폰할 수 없습니다!");
        }

        return baseBGChunk0;
    }

    /// <summary>
    /// 🌟 시작 맵 프리팹 내부에 투척 발판(Platform)이 존재하는지 엄격 검증
    /// </summary>
    private void ValidatePrefabPlatform(GameObject prefab, string slotDescription)
    {
        if (prefab == null) return;
        string[] platformNames = { "Lakeside_Platform", "Platform", "Lakeside_WoodenPier", "Pier" };
        bool hasPlatform = false;
        foreach (var pName in platformNames)
        {
            if (prefab.transform.Find(pName) != null)
            {
                hasPlatform = true;
                break;
            }
        }

        if (!hasPlatform)
        {
            // 하위 깊은 자식까지 전수 검색
            var allTransforms = prefab.GetComponentsInChildren<Transform>(true);
            foreach (var t in allTransforms)
            {
                foreach (var pName in platformNames)
                {
                    if (t.name.Equals(pName, System.StringComparison.OrdinalIgnoreCase))
                    {
                        hasPlatform = true;
                        break;
                    }
                }
                if (hasPlatform) break;
            }
        }

        if (!hasPlatform)
        {
            Debug.LogError($"[LakeEnvironmentManager] ❌ 시작 지점으로 사용될 [{slotDescription}] 프리팹('{prefab.name}') 내부에 발판(Platform/Lakeside_Platform) 오브젝트가 없습니다! 캐릭터가 스폰될 수 없습니다.");
        }
    }

    public void ResetEnvironment()
    {
        ClearDynamicChunks();
    }

    public void UpdateEnvironmentByDistance(float distance)
    {
        // 추후 유니티 표준 Lighting & Volume 전환 전까지 노옵(No-Op) 처리
    }

    private Light mainLight;

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

        // 🌟 씬에 미리 배치된 BG_01이 없다면, 슬롯 설정 프리팹(0m 기준)을 직접 인스턴스화하여 0번 청크로 생성
        if (baseBGChunk0 == null)
        {
            GameObject prefab0 = GetMapPrefabForChunk(0, 0f);
            if (prefab0 != null)
            {
                Transform parentTransform = transform.parent != null ? transform.parent : transform;
                baseBGChunk0 = Instantiate(prefab0, Vector3.zero, Quaternion.identity, parentTransform);
                baseBGChunk0.name = $"{prefab0.name}_Section_0_0m";
            }
        }

        if (baseBGChunk0 == null && (loopSlots == null || loopSlots.Count == 0)) return;

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
        GameObject sourcePrefab = GetMapPrefabForChunk(chunkIndex, targetZ) ?? baseBGChunk0;
        if (sourcePrefab == null) return null;

        Transform parentTransform = (baseBGChunk0 != null && baseBGChunk0.transform.parent != null) ? baseBGChunk0.transform.parent : transform;
        GameObject newChunk = Instantiate(sourcePrefab, parentTransform);
        newChunk.name = $"{sourcePrefab.name}_Section_{chunkIndex}_{targetZ:F0}m";

        Vector3 basePos = (baseBGChunk0 != null) ? baseBGChunk0.transform.localPosition : Vector3.zero;
        Quaternion baseRot = (baseBGChunk0 != null) ? baseBGChunk0.transform.localRotation : Quaternion.identity;
        Vector3 baseScale = (baseBGChunk0 != null) ? baseBGChunk0.transform.localScale : Vector3.one;

        newChunk.transform.localPosition = basePos + new Vector3(0f, 0f, targetZ);
        newChunk.transform.localRotation = baseRot;
        newChunk.transform.localScale = baseScale;

        // 🌟 복제 청크 내 발판 및 타깃 위치 그룹(PP) 자동 제거
        string[] cleanNames = { "Lakeside_Platform", "Platform", "Lakeside_WoodenPier", "Pier", "Player_Position", "PlayerPosition", "Player_Positions" };
        foreach (var cleanName in cleanNames)
        {
            Transform dupObj = newChunk.transform.Find(cleanName);
            if (dupObj != null)
            {
                if (Application.isPlaying) Destroy(dupObj.gameObject);
                else DestroyImmediate(dupObj.gameObject);
            }
        }

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