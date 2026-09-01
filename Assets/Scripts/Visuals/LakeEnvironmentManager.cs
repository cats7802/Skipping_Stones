using UnityEngine;
using SkippingStones.Terrain;



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
        set => _instance = value;
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

    public enum EndingTriggerMode
    {
        ByLoopCount, // 루프 횟수 기준 (예: 1회 루프 완주 후 엔딩 맵)
        ByDistance,  // 목표 거리(m) 도달 기준
        Infinite     // 엔딩 없이 무한 루프
    }

    [Header("2. 루프 횟수 및 엔딩 완주 규칙")]
    [Tooltip("엔딩 맵 진입 판정 모드 (ByLoopCount: 지정 횟수 완주 후 엔딩, ByDistance: 목표 거리 도달 시 엔딩, Infinite: 무한 루프)")]
    public EndingTriggerMode endingTriggerMode = EndingTriggerMode.ByLoopCount;

    [Tooltip("중간 슬롯(1~N번) 시퀀스를 몇 회 반복할 것인가? (1이면 1바퀴 돌고 엔딩 맵 스폰, 0 이하 시 무한 반복)")]
    public int loopRepeatCount = 1;

    [Tooltip("엔딩 맵 프리팹 (지정된 루프 횟수 완주 시 스폰)")]
    public GameObject endingMapPrefab;

    [Tooltip("엔딩 맵 진입 목표 거리 (0 이하 시 무한 루프)")]
    public float targetClearDistance = 0f;

    [Tooltip("엔딩 맵 스폰 이후 더 이상 다음 청크를 스폰하지 않고 코스를 완주(종료)할 것인가?")]
    public bool stopSpawningOnEnding = true;

    private void Awake()
    {
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
        if (chunkIndex == 0)
        {
            if (startMapPrefab != null)
            {
                ValidatePrefabPlatform(startMapPrefab, "StartMapPrefab (SM)");
                ValidatePrefabWaterSurface(startMapPrefab, "StartMapPrefab (SM)");
                return startMapPrefab;
            }
            if (loopSlots != null && loopSlots.Count > 0 && loopSlots[0].baseMapPrefab != null)
            {
                ValidatePrefabPlatform(loopSlots[0].baseMapPrefab, "LoopSlot [1] BaseMap");
                ValidatePrefabWaterSurface(loopSlots[0].baseMapPrefab, "LoopSlot [1] BaseMap");
                return loopSlots[0].baseMapPrefab;
            }
            return baseBGChunk0;
        }

        // 루프 슬롯이 설정되어 있는 경우
        if (loopSlots != null && loopSlots.Count > 0)
        {
            // 1) 횟수 기준 엔딩 판정 (ByLoopCount)
            if (endingTriggerMode == EndingTriggerMode.ByLoopCount && loopRepeatCount > 0 && endingMapPrefab != null)
            {
                int totalLoopChunks = loopRepeatCount * loopSlots.Count;
                if (chunkIndex == totalLoopChunks + 1)
                {
                    ValidatePrefabWaterSurface(endingMapPrefab, "EndingMapPrefab (EM)");
                    return endingMapPrefab; // 엔딩 맵 스폰
                }
                if (chunkIndex > totalLoopChunks + 1)
                {
                    if (stopSpawningOnEnding) return null; // 완주 완료 - 스폰 정지
                    ValidatePrefabWaterSurface(endingMapPrefab, "EndingMapPrefab (EM)");
                    return endingMapPrefab;
                }
            }
            // 2) 거리 기준 엔딩 판정 (ByDistance)
            else if (endingTriggerMode == EndingTriggerMode.ByDistance && targetClearDistance > 0f && targetZ >= targetClearDistance && endingMapPrefab != null)
            {
                ValidatePrefabWaterSurface(endingMapPrefab, "EndingMapPrefab (EM)");
                return endingMapPrefab;
            }

            int slotIdx = (chunkIndex - 1) % loopSlots.Count;
            var slot = loopSlots[slotIdx];
            if (slot == null || slot.baseMapPrefab == null)
            {
                Debug.LogError($"[LakeEnvironmentManager] ❌ 루프 슬롯 [{slotIdx + 1}] BaseMap이 등록되지 않았습니다! (인스펙터 확인 필요)");
                return baseBGChunk0;
            }

            // 변주 체크박스가 켜져 있고 유효한 변주 프리팹이 등록되어 있는 경우
            if (slot.useVariations && slot.variationPrefabs != null && slot.variationPrefabs.Length > 0)
            {
                System.Collections.Generic.List<GameObject> candidates = new System.Collections.Generic.List<GameObject>();
                if (slot.baseMapPrefab != null) candidates.Add(slot.baseMapPrefab);
                foreach (var v in slot.variationPrefabs)
                {
                    if (v != null) candidates.Add(v);
                }

                if (candidates.Count > 0)
                {
                    int rand = UnityEngine.Random.Range(0, candidates.Count);
                    GameObject chosen = candidates[rand];
                    ValidatePrefabWaterSurface(chosen, $"LoopSlot [{slotIdx + 1}] Variation");
                    return chosen;
                }
            }

            ValidatePrefabWaterSurface(slot.baseMapPrefab, $"LoopSlot [{slotIdx + 1}] BaseMap");
            return slot.baseMapPrefab;
        }

        // 엔딩 목표 거리 도달 및 엔딩 맵 지정 시: EM 반환 (슬롯 없는 경우)
        if (targetClearDistance > 0f && targetZ >= targetClearDistance && endingMapPrefab != null)
        {
            ValidatePrefabWaterSurface(endingMapPrefab, "EndingMapPrefab (EM)");
            return endingMapPrefab;
        }

        // 슬롯이 아예 비어있는데 씬에 BG_01도 없는 경우 콘솔 에러 출력
        if (baseBGChunk0 == null)
        {
            Debug.LogError("[LakeEnvironmentManager] ❌ 씬에 배치된 배경(BG_01)도 없고, 루프 슬롯(loopSlots)도 비어있어 맵을 스폰할 수 없습니다!");
        }

        return baseBGChunk0;
    }

    /// <summary>
    /// 🌟 시작 맵 프리팹 내부에 투척 발판(Platform)이 존재하는지 검증하고, 없을 시 자동 생성/도킹
    /// </summary>
    public void EnsureLaunchPier(GameObject chunk0, Transform anchorS)
    {
        if (chunk0 == null) return;

        // 1. chunk0 내부 모든 자식에서 pier 또는 platform 키워드 검사
        var allTransforms = chunk0.GetComponentsInChildren<Transform>(true);
        foreach (var t in allTransforms)
        {
            if (t == null) continue;
            string lName = t.name.ToLower();
            if (lName.Contains("pier") || lName.Contains("platform"))
            {
                return; // 맵 내부에 이미 발판(WoodenPier_Platform 등)이 있으므로 중복 스폰하지 않음!
            }
        }

        // 2. 씬 전체에 이미 발판이 존재하는지 확인
        var allObjs = FindObjectsByType<GameObject>(FindObjectsInactive.Include);
        foreach (var obj in allObjs)
        {
            if (obj == null) continue;
            string lName = obj.name.ToLower();
            if (lName.Contains("camera") || lName.Contains("canvas") || lName.Contains("ui") || lName.Contains("guide")) continue;
            if (lName.Contains("pier") || lName.Contains("platform"))
            {
                return;
            }
        }

        // 🌟 시작 맵에 발판이 없다면 Lakeside_WoodenPier 자동 스폰 및 도킹
        GameObject pierPrefab = Resources.Load<GameObject>("Lakeside_WoodenPier");
#if UNITY_EDITOR
        if (pierPrefab == null)
        {
            pierPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/prefab/Lakeside_WoodenPier.prefab");
        }
#endif
        if (pierPrefab != null)
        {
            Vector3 spawnPos = (anchorS != null) ? anchorS.position : chunk0.transform.position;
            Quaternion spawnRot = (anchorS != null) ? anchorS.rotation : Quaternion.identity;

            WaterSurface ws = chunk0.GetComponentInChildren<WaterSurface>();
            float waterY = (ws != null && ws.GetComponent<BoxCollider>() != null) ? ws.GetComponent<BoxCollider>().bounds.max.y : spawnPos.y;
            spawnPos.y = waterY;

            GameObject pierObj = Instantiate(pierPrefab, spawnPos, spawnRot, chunk0.transform);
            pierObj.name = "Lakeside_WoodenPier";
        }
    }

    /// <summary>
    /// 🌟 모든 맵 프리팹(시작/루프/엔딩) 내부에 필수 수면(WaterSurface & Collider)이 존재하는지 엄격 검증
    /// </summary>
    private void ValidatePrefabWaterSurface(GameObject prefab, string slotDescription)
    {
        if (prefab == null) return;

        WaterSurface ws = prefab.GetComponentInChildren<WaterSurface>(true);
        Collider col = ws != null ? ws.GetComponent<Collider>() : null;

        if (ws == null || col == null)
        {
            Debug.LogError($"[LakeEnvironmentManager] ❌ <b>[{slotDescription}]</b> 프리팹('{prefab.name}')에 <b>WaterSurface 컴포넌트 또는 수면 Collider</b>가 누락되어 있습니다! (돌이 수면을 감지하지 못하고 추락합니다)");
        }
    }

    /// <summary>
    /// 🌟 시작 맵 프리팹(0번 청크) 전용: 투척 발판(Platform)이 존재하는지 검증
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
            Debug.Log($"[LakeEnvironmentManager] ℹ️ [{slotDescription}] 프리팹('{prefab.name}')에 고정 발판이 없어 런타임에 Lakeside_WoodenPier가 자동 도킹됩니다.");
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

    #region BG 순차 인스턴스화 및 앵커 기반 무한 스트리밍

    public void SetupBGChunks()
    {
        InitializeFirstChunk(); // 🌟 첫 번째 청크 자동 인식

        // 🌟 씬에 미리 배치된 청크가 없다면, 슬롯 설정 프리팹(0m 기준)을 직접 인스턴스화하여 0번 청크로 생성
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

        // 0번 청크 등록 및 발판 자동 도킹
        if (baseBGChunk0 != null)
        {
            MapAnchorHelper.GetOrCreateAnchors(baseBGChunk0, out Transform anchorS, out _);

            if (!dynamicChunks.Contains(baseBGChunk0))
            {
                dynamicChunks.Add(baseBGChunk0);
            }

            EnsureLaunchPier(baseBGChunk0, anchorS);
        }

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

        // 이전 청크들의 순차 스폰 보장 (이전 청크의 End Anchor 획득 목적)
        if (chunkIndex > 1)
        {
            for (int i = 1; i < chunkIndex; i++)
            {
                if (!spawnedChunkIndices.Contains(i))
                {
                    EnsureChunkSpawned(i);
                }
            }
        }

        // 이전 청크 (chunkIndex - 1) 및 해당 청크의 End Anchor 획득
        GameObject prevChunk = (chunkIndex == 1)
            ? baseBGChunk0
            : dynamicChunks.Find(c => c != null && c.name.Contains($"Section_{chunkIndex - 1}"));
        if (prevChunk == null) prevChunk = baseBGChunk0;

        MapAnchorHelper.GetOrCreateAnchors(prevChunk, out _, out Transform prevAnchorE);
        float prevEndZ = prevAnchorE != null ? prevAnchorE.position.z : (chunkIndex - 1) * autoChunkSize;

        GameObject sourcePrefab = GetMapPrefabForChunk(chunkIndex, prevEndZ);
        if (sourcePrefab == null)
        {
            if (stopSpawningOnEnding)
            {
                Debug.Log($"[LakeEnvironmentManager] 🏁 엔딩 맵({chunkIndex - 1}번 청크)에 도달하여 코스가 완주되었습니다. 추가 청크 생성을 정지합니다.");
                return null;
            }
            sourcePrefab = baseBGChunk0;
        }
        if (sourcePrefab == null) return null;

        Transform parentTransform = (baseBGChunk0 != null && baseBGChunk0.transform.parent != null) ? baseBGChunk0.transform.parent : transform;
        GameObject newChunk = Instantiate(sourcePrefab, parentTransform);

        // 새 청크의 Start & End 앵커 획득
        MapAnchorHelper.GetOrCreateAnchors(newChunk, out Transform currAnchorS, out Transform currAnchorE);

        if (prevAnchorE != null && currAnchorS != null)
        {
            // 🌟 정밀 소켓 도킹 (Socket Snapping): 이전 청크 Anchor_E와 현재 청크 Anchor_S 완벽 일치
            Quaternion localRotS = Quaternion.Inverse(newChunk.transform.rotation) * currAnchorS.rotation;
            Vector3 localPosS = newChunk.transform.InverseTransformPoint(currAnchorS.position);

            Quaternion targetRot = prevAnchorE.rotation * Quaternion.Inverse(localRotS);
            Vector3 targetPos = prevAnchorE.position - (targetRot * localPosS);

            newChunk.transform.rotation = targetRot;
            newChunk.transform.position = targetPos;
        }
        else
        {
            // 앵커 도킹 불가 시 기존 Z축 오프셋 폴백
            float targetZ = chunkIndex * autoChunkSize;
            Vector3 basePos = (baseBGChunk0 != null) ? baseBGChunk0.transform.localPosition : Vector3.zero;
            Quaternion baseRot = (baseBGChunk0 != null) ? baseBGChunk0.transform.localRotation : Quaternion.identity;
            Vector3 baseScale = (baseBGChunk0 != null) ? baseBGChunk0.transform.localScale : Vector3.one;

            newChunk.transform.localPosition = basePos + new Vector3(0f, 0f, targetZ);
            newChunk.transform.localRotation = baseRot;
            newChunk.transform.localScale = baseScale;
        }

        float spawnZ = currAnchorS != null ? currAnchorS.position.z : newChunk.transform.position.z;
        newChunk.name = $"{sourcePrefab.name}_Section_{chunkIndex}_{spawnZ:F0}m";

        // 🌟 복제 청크 내 발판 및 타깃 위치 그룹(PP) 자동 제거 (발판은 0번 시작 맵에만 존재해야 함)
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

        dynamicChunks.Add(newChunk);
        spawnedChunkIndices.Add(chunkIndex);

        // 🌟 [핵심] 새 청크가 도킹/생성되었으므로 글로벌 연속 스플라인 경로 즉시 재연결 (500m/1000m 경계 끊김 원천 차단)
        if (GlobalRiverPath.Instance != null)
        {
            GlobalRiverPath.Instance.RebuildPath();
        }

        OnChunkRelayed?.Invoke(spawnZ);

        return newChunk;
    }

    public void ClearDynamicChunks()
    {
        for (int i = dynamicChunks.Count - 1; i >= 0; i--)
        {
            var chunk = dynamicChunks[i];
            if (chunk != null && chunk != baseBGChunk0)
            {
                if (Application.isPlaying) Destroy(chunk);
                else DestroyImmediate(chunk);
            }
        }
        dynamicChunks.Clear();
        if (baseBGChunk0 != null) dynamicChunks.Add(baseBGChunk0);
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
    /// 🌟 소켓 앵커 끝단 위치에 비례하여 다음 청크를 사전에 무한 자동 스폰
    /// </summary>
    public void UpdateBGStreaming()
    {
        if (!Application.isPlaying) return;
        SetupBGChunks();

        float trackZ = GetTrackingZ();

        // 마지막으로 스폰된 청크의 End Anchor Z 위치 추적
        GameObject lastChunk = dynamicChunks.Count > 0 ? dynamicChunks[dynamicChunks.Count - 1] : baseBGChunk0;
        float lastEndZ = 0f;
        if (lastChunk != null)
        {
            MapAnchorHelper.GetOrCreateAnchors(lastChunk, out _, out Transform lastAnchorE);
            if (lastAnchorE != null) lastEndZ = lastAnchorE.position.z;
            else lastEndZ = spawnedChunkIndices.Count * 500f;
        }

        // 현재 추적 위치가 마지막 청크 끝단으로부터 350m 이내에 도달하면 다음 청크 사전 스폰
        float spawnThreshold = 350f;
        if (trackZ >= lastEndZ - spawnThreshold)
        {
            int nextIndex = dynamicChunks.Count;
            if (!spawnedChunkIndices.Contains(nextIndex))
            {
                EnsureChunkSpawned(nextIndex);
            }
        }
    }

    public void UpdateTerrainStreaming() => UpdateBGStreaming();

    private float GetTrackingZ()
    {
        var gc = FindAnyObjectByType<GameController>();
        if (gc != null && gc.currentState == GameController.GameState.Replay)
            return Camera.main != null ? Camera.main.transform.position.z : 0f;

        var arcade = FindAnyObjectByType<SkippingStones.Arcade.ArcadeSkippingStone>();
        if (arcade != null && arcade.isThrown && !arcade.isSunk) return arcade.transform.position.z;

        var stone = FindAnyObjectByType<SkippingStone>();
        if (stone != null && stone.isThrown && !stone.isSunk) return stone.transform.position.z;

        return Camera.main != null ? Camera.main.transform.position.z : 0f;
    }

    #endregion

    #region 에디터 테스트 도구 (In-Editor Test Tools)
    /// <summary>
    /// 규칙대로 설정된 전체 시퀀스 맵을 에디터 씬에 일괄 생성
    /// </summary>
    public void TestBuildFullSequence()
    {
        ClearDynamicChunks();
        SetupBGChunks();

        int totalChunks = 0;
        if (loopSlots != null && loopSlots.Count > 0)
        {
            if (endingTriggerMode == EndingTriggerMode.ByLoopCount && loopRepeatCount > 0 && endingMapPrefab != null)
            {
                totalChunks = (loopRepeatCount * loopSlots.Count) + 1; // 슬롯 N회 + 엔딩 1개
            }
            else
            {
                totalChunks = Mathf.Max(loopSlots.Count * 2, 4);
            }
        }
        else
        {
            totalChunks = 3;
        }

        for (int i = 1; i <= totalChunks; i++)
        {
            EnsureChunkSpawned(i);
        }

        Debug.Log($"[LakeEnvironmentManager] 🚀 규칙 기반 시퀀스 맵 생성 완료: 총 {dynamicChunks.Count}개 청크가 소켓 앵커로 도킹되었습니다.");
    }

    /// <summary>
    /// 다음 순서의 1개 청크만 순차적으로 스폰
    /// </summary>
    public void TestSpawnNextChunk()
    {
        SetupBGChunks();
        int nextIndex = dynamicChunks.Count;
        GameObject spawned = EnsureChunkSpawned(nextIndex);
        if (spawned != null)
        {
            Debug.Log($"[LakeEnvironmentManager] ➕ {nextIndex}번 청크 '{spawned.name}' 스폰 완료!");
        }
    }

    /// <summary>
    /// 생성된 동적 청크들을 모두 제거하고 초기화
    /// </summary>
    public void TestClearChunks()
    {
        ClearDynamicChunks();
        Debug.Log("[LakeEnvironmentManager] 🧹 생성된 테스트 청크 초기화 완료!");
    }
    #endregion
}