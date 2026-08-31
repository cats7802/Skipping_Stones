using UnityEngine;
using System.Collections;
using SkippingStones.Data;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class GameController : MonoBehaviour
{
    public static GameController Instance { get; private set; }

    [Header("0. 세션 데이터 및 결과 DTO")]
    public MatchSessionData currentSessionData;
    public InGameResultData lastResultData;
    public event System.Action<InGameResultData> OnMatchResultGenerated;

    public enum GameMode
    {
        LongDistance,   // 장거리 물리 모드
        TargetAccuracy, // 타깃 정밀 모드
        RhythmArcade    // 🎵 리듬 아케이드 모드
    }

    public enum GameState
    {
        ModeSelect,
        Positioning,
        AimingAngle,
        ChargingPower,
        ThrowingAnimation,
        Flying,
        Replay,
        Result
    }

    [Header("1. 게임 모드 및 상태")]
    public GameMode currentMode = GameMode.LongDistance;
    public GameState currentState = GameState.ModeSelect;

    [Header("🛠️ 개발자 테스트 설정")]
    [Tooltip("체크 시 로비/맵선택창을 건너뛰고 시작하자마자 곧바로 인게임 투척 대기(Positioning) 상태로 직행합니다.")]
    public bool autoStartInGame = false;

    [Tooltip("체크 시 물수제비 탭 없이도 착수 시 자동으로 PERFECT 바운스되어 스트리밍 맵을 끝까지 날아갑니다.")]
    public bool devGodMode = false;

    [Tooltip("갓모드 비행 시 도달하고자 하는 최대 테스트 거리 (m). 이 거리에 도달하면 자동으로 바운스를 멈추고 자연스럽게 착수/피니시합니다. (0이면 무제한)")]
    public float devGodModeTargetDistance = 1500f;

    [Header("2. 인게임 씬 참조 (인스펙터 명시 연결)")]
    [SerializeField] private StoneThrowerCharacter _character;
    public StoneThrowerCharacter character
    {
        get => _character;
        set => _character = value;
    }

    [SerializeField] private SkippingStone _stone;
    public SkippingStone stone
    {
        get => _stone;
        set => _stone = value;
    }

    [SerializeField] private DualCameraSetup _dualCamera;
    public DualCameraSetup dualCamera
    {
        get => _dualCamera;
        set => _dualCamera = value;
    }

    [SerializeField] private TopDownReplayManager _topDownReplay;
    public TopDownReplayManager topDownReplay
    {
        get => _topDownReplay;
        set => _topDownReplay = value;
    }

    [SerializeField] private Transform _currentLaunchPlatform;
    public Transform currentLaunchPlatform
    {
        get => _currentLaunchPlatform;
        set => _currentLaunchPlatform = value;
    }

    // 🌟 레거시 호환 프로퍼티
    public Transform currentLaunchPier
    {
        get => _currentLaunchPlatform;
        set => _currentLaunchPlatform = value;
    }

    [SerializeField] private Transform _playerPositionRoot;
    public Transform playerPositionRoot
    {
        get => _playerPositionRoot;
        set => _playerPositionRoot = value;
    }

    [Header("3. 기본 프리팹")]
    public GameObject defaultCharacterPrefab;
    public GameObject defaultStonePrefab;
    public GameObject defaultMapPrefab;

    [Header("4. 게이지 및 실시간 파라미터")]
    public float startPosX = 0f;
    public float aimGaugeValue = 0f;
    public float powerGaugeValue = 0f;
    public string lastTimingText = "";
    public string bannerNotificationText = "";

    [Header("5. 위치 선정 파라미터")]
    public float minPositionX = -12f;
    public float maxPositionX = 12f;
    public int targetPlatformIndex = 0;

    [Header("6. 모달 상태")]
    public bool showAquariumModal = false;
    public bool showStoneSelectorModal = false;
    public bool requireTouchRelease = false;

    [Header("7. 결과 점수 데이터")]
    public int distanceScore = 0;
    public int skipScore = 0;
    public int specialScore = 0;
    public int totalScore = 0;
    public int earnedCoins = 0;
    public int perfectTimingCount = 0;
    public int fishSnipeCount = 0;
    public int friendOvertakeCount = 0;
    public int boostPadCount = 0;
    public float lastSkimBonusDist = 0f;
    private bool hasCalculatedResult = false;

    private float aimSpeed = 2.4f;
    private float powerSpeed = 3.0f;
    private float aimDirection = 1f;
    private float powerDirection = 1f;
    private float lastStateChangeTime = 0f;
    private const float STATE_COOLDOWN = 0.35f;

    private bool isDraggingMap = false;
    private Vector2 prevDragPos;
    private bool isSwipingTarget = false;
    private Vector2 swipeStartPos;
    private float swipeThreshold = 35f;

    private Vector2 flightTouchStartPos;
    private float flightTouchStartTime = 0f;
    private bool isTrackingFlightTouch = false;

    // 🎮 모드별 독립 객체 핸들러 (Strategy Pattern)
    private SkippingStones.Gameplay.Modes.IGameModeHandler currentModeHandler;
    private readonly System.Collections.Generic.Dictionary<GameMode, SkippingStones.Gameplay.Modes.IGameModeHandler> modeHandlers = 
        new System.Collections.Generic.Dictionary<GameMode, SkippingStones.Gameplay.Modes.IGameModeHandler>();

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else if (Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        // 인스펙터 연결 누락 시에만 계층 탐색으로 보완
        ResolveSceneReferences();

        // 모드 핸들러 등록
        modeHandlers[GameMode.LongDistance] = new SkippingStones.Gameplay.Modes.LongDistanceModeHandler();
        modeHandlers[GameMode.TargetAccuracy] = new SkippingStones.Gameplay.Modes.TargetAccuracyModeHandler();
        modeHandlers[GameMode.RhythmArcade] = new SkippingStones.Gameplay.Modes.RhythmArcadeModeHandler();
    }

    private void Start()
    {
        if (autoStartInGame)
        {
            if (SkippingStones.UI.MetaUIManager.Instance != null)
            {
                SkippingStones.UI.MetaUIManager.Instance.ShowScreen(SkippingStones.UI.MetaScreen.InGame);
            }
            SelectGameMode(currentMode);
        }
        else
        {
            currentState = GameState.ModeSelect;
        }
    }

    private void ResolveSceneReferences()
    {
        if (_dualCamera == null) _dualCamera = FindAnyObjectByType<DualCameraSetup>();
        if (_topDownReplay == null)
        {
            _topDownReplay = FindAnyObjectByType<TopDownReplayManager>() ?? GetComponent<TopDownReplayManager>() ?? gameObject.AddComponent<TopDownReplayManager>();
        }

        // 🌟 맵 재스폰 시 항상 최신 유효 발판으로 재바인딩
        _currentLaunchPlatform = FindPlatformInScene();
        _playerPositionRoot = FindPlayerPositionRootInScene();
    }

    /// <summary>
    /// 🌟 씬 루트 및 배경 프리팹/청크 하위에서 투척 발판(Platform)을 다중 표준으로 탐색
    /// </summary>
    public static Transform FindPlatformInScene()
    {
        string[] candidateKeywords = { "woodenpier_platform", "lakeside_woodenpier", "lakeside_platform", "platform", "pier" };

        // 1순위: LakeEnvironmentManager가 스폰한 0번 청크 하위에서 직속 탐색
        var lem = LakeEnvironmentManager.Instance != null ? LakeEnvironmentManager.Instance : FindAnyObjectByType<LakeEnvironmentManager>();
        if (lem != null)
        {
            var chunkTransforms = lem.GetComponentsInChildren<Transform>(true);
            foreach (var t in chunkTransforms)
            {
                if (t == null) continue;
                string lName = t.name.ToLower();
                if (lName.Contains("camera") || lName.Contains("canvas") || lName.Contains("ui") || lName.Contains("guide") || lName.Contains("showcase")) continue;
                foreach (var kw in candidateKeywords)
                {
                    if (lName.Contains(kw))
                    {
                        return t;
                    }
                }
            }
        }

        // 2순위: 씬 전체 활성/비활성 오브젝트 중 탐색 (쇼케이스 더미 제외)
        var allObjs = FindObjectsByType<GameObject>(FindObjectsInactive.Include);
        foreach (var obj in allObjs)
        {
            if (obj == null) continue;
            string lowerName = obj.name.ToLower();
            if (lowerName.Contains("camera") || lowerName.Contains("canvas") || lowerName.Contains("ui") || lowerName.Contains("guide") || lowerName.Contains("showcase")) continue;

            foreach (var kw in candidateKeywords)
            {
                if (lowerName.Contains(kw))
                {
                    return obj.transform;
                }
            }
        }
        return null;
    }

    /// <summary>
    /// 🌟 씬 루트 및 배경 프리팹 하위에서 타깃 모드 위치 그룹(Player_Position) 탐색
    /// </summary>
    public static Transform FindPlayerPositionRootInScene()
    {
        string[] candidateNames = { "Player_Position", "PlayerPosition", "Player_Positions" };

        foreach (var name in candidateNames)
        {
            GameObject obj = GameObject.Find(name);
            if (obj != null) return obj.transform;
        }

        var allObjs = FindObjectsByType<GameObject>(FindObjectsInactive.Include);
        foreach (var obj in allObjs)
        {
            foreach (var name in candidateNames)
            {
                if (obj.name.Equals(name, System.StringComparison.OrdinalIgnoreCase))
                {
                    return obj.transform;
                }
            }
        }
        return null;
    }

    public void SelectGameMode(GameMode mode)
    {
        StartGameSession(defaultCharacterPrefab, defaultStonePrefab, defaultMapPrefab, mode);
    }

    public void SelectModeAndStart(GameMode mode)
    {
        SelectGameMode(mode);
    }

    public void StartGameSession(MatchSessionData session)
    {
        currentSessionData = session ?? new MatchSessionData();
        currentMode = currentSessionData.gameMode;

        GameObject stonePrefab = currentSessionData.stonePrefabOverride;
        if (stonePrefab == null && !string.IsNullOrEmpty(currentSessionData.stoneId))
        {
            var dm = GameDataManager.Instance;
            if (dm != null && dm.stoneCatalog != null)
            {
                var info = dm.stoneCatalog.Find(s => s.id == currentSessionData.stoneId || (s.prefabPath != null && s.prefabPath.Contains(currentSessionData.stoneId)));
                if (info != null && !string.IsNullOrEmpty(info.prefabPath))
                {
#if UNITY_EDITOR
                    stonePrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(info.prefabPath);
#else
                    string rPath = info.prefabPath;
                    if (rPath.StartsWith("Assets/prefab/")) rPath = rPath.Substring("Assets/prefab/".Length);
                    if (rPath.EndsWith(".prefab")) rPath = rPath.Substring(0, rPath.Length - ".prefab".Length);
                    stonePrefab = Resources.Load<GameObject>(rPath);
#endif
                }
            }
        }
        if (stonePrefab == null) stonePrefab = defaultStonePrefab;

        GameObject charPrefab = currentSessionData.characterPrefabOverride;
        if (charPrefab == null && !string.IsNullOrEmpty(currentSessionData.characterId))
        {
            var dm = GameDataManager.Instance;
            if (dm != null && dm.characterCatalog != null)
            {
                var charInfo = dm.characterCatalog.Find(c => c.id == currentSessionData.characterId || (c.prefabPath != null && c.prefabPath.Contains(currentSessionData.characterId)));
                if (charInfo != null && !string.IsNullOrEmpty(charInfo.prefabPath))
                {
#if UNITY_EDITOR
                    charPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(charInfo.prefabPath);
#else
                    string rPath = charInfo.prefabPath;
                    if (rPath.StartsWith("Assets/prefab/")) rPath = rPath.Substring("Assets/prefab/".Length);
                    if (rPath.EndsWith(".prefab")) rPath = rPath.Substring(0, rPath.Length - ".prefab".Length);
                    charPrefab = Resources.Load<GameObject>(rPath);
#endif
                }
            }

            // 폴백: 에디터에서 Assets 전체 내 StoneThrowerCharacter 컴포넌트를 가진 프리팹 중 이름 매칭
#if UNITY_EDITOR
            if (charPrefab == null)
            {
                string[] guids = UnityEditor.AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" });
                foreach (string guid in guids)
                {
                    string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                    GameObject p = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    if (p != null && p.name.Equals(currentSessionData.characterId, System.StringComparison.OrdinalIgnoreCase) && p.GetComponentInChildren<StoneThrowerCharacter>(true) != null)
                    {
                        charPrefab = p;
                        break;
                    }
                }
            }
#endif
        }
        if (charPrefab == null) charPrefab = defaultCharacterPrefab;

        GameObject mapPrefab = currentSessionData.mapPrefabOverride != null ? currentSessionData.mapPrefabOverride : defaultMapPrefab;

        StartGameSession(charPrefab, stonePrefab, mapPrefab, currentMode);
    }

    public void StartGameSession(GameObject charPrefab, GameObject stonePrefab, GameObject mapPrefab, GameMode mode)
    {
        currentMode = mode;
        if (stonePrefab != null) defaultStonePrefab = stonePrefab;

        if (currentModeHandler != null) currentModeHandler.OnExitMode(this);
        if (modeHandlers.TryGetValue(mode, out var handler))
        {
            currentModeHandler = handler;
        }
        else
        {
            currentModeHandler = new SkippingStones.Gameplay.Modes.LongDistanceModeHandler();
        }

        SetupMapEnvironment(mapPrefab);
        SetupCharacter(charPrefab);
        ResetToPositioning();

        currentModeHandler.OnEnterMode(this);
    }

    private void SetupMapEnvironment(GameObject mapPrefab)
    {
        LakeEnvironmentManager existingMgr = LakeEnvironmentManager.Instance != null ? LakeEnvironmentManager.Instance : FindAnyObjectByType<LakeEnvironmentManager>();

        // 🌟 mapPrefab이 LakeEnvironmentManager가 아니거나 null일 때 기본 환경 매니저(New_TestEnvMgr) 자동 폴백
        if (mapPrefab == null || mapPrefab.GetComponent<LakeEnvironmentManager>() == null)
        {
            if (existingMgr == null)
            {
                GameObject envPrefab = Resources.Load<GameObject>("New_TestEnvMgr");
#if UNITY_EDITOR
                if (envPrefab == null)
                {
                    envPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/prefab/BG_Env/New_TestEnvMgr.prefab");
                }
#endif
                if (envPrefab != null)
                {
                    mapPrefab = envPrefab;
                }
            }
        }

        // 🌟 씬에 이미 환경 매니저가 존재하고 동일한 맵이면 파괴 없이 보존 및 청크 세팅
        if (existingMgr != null)
        {
            if (mapPrefab == null || existingMgr.gameObject.name.StartsWith(mapPrefab.name, System.StringComparison.OrdinalIgnoreCase))
            {
                existingMgr.SetupBGChunks();
            }
            else
            {
                // 다른 맵으로 교체 요청된 경우에만 이전 매니저 교체
                LakeEnvironmentManager.Instance = null;
                if (Application.isPlaying) Destroy(existingMgr.gameObject);
                else DestroyImmediate(existingMgr.gameObject);
                existingMgr = null;
            }
        }

        // 씬에 환경 매니저가 없으면 새로 생성
        if (existingMgr == null && mapPrefab != null)
        {
            GameObject newMgrObj = Instantiate(mapPrefab, Vector3.zero, Quaternion.identity);
            newMgrObj.name = mapPrefab.name;
            var lem = newMgrObj.GetComponent<LakeEnvironmentManager>();
            if (lem != null)
            {
                LakeEnvironmentManager.Instance = lem;
                lem.SetupBGChunks();
            }
        }

        ResolveSceneReferences();

        if (currentLaunchPier != null)
        {
            currentLaunchPier.gameObject.SetActive(true);
        }

        if (_playerPositionRoot != null)
        {
            _playerPositionRoot.gameObject.SetActive(currentMode == GameMode.TargetAccuracy);
        }

        if (MapPIPManager.Instance != null)
        {
            MapPIPManager.Instance.UpdatePIPState(currentMode == GameMode.TargetAccuracy);
        }

        RiverSpawner spawner = FindAnyObjectByType<RiverSpawner>();
        if (spawner == null)
        {
            GameObject spawnerObj = new GameObject("[Auto_RiverSpawner]");
            spawner = spawnerObj.AddComponent<RiverSpawner>();
        }

        if (spawner != null)
        {
            if (character != null)
            {
                spawner.startBankPos = character.basePosition;
                spawner.spawnDirection = character.baseRotation * Vector3.forward;
            }
            spawner.GenerateRiverEntitiesForMode(currentMode);
        }
    }


    private void SetupCharacter(GameObject charPrefab)
    {
        if (charPrefab == null) charPrefab = defaultCharacterPrefab;
        if (charPrefab == null) return;

        // 1. 씬 내의 모든 기존 인게임 캐릭터 수집 (쇼케이스 더미 제외)
        StoneThrowerCharacter[] allCharacters = FindObjectsByType<StoneThrowerCharacter>(FindObjectsInactive.Include);
        System.Collections.Generic.List<StoneThrowerCharacter> inGameCharacters = new System.Collections.Generic.List<StoneThrowerCharacter>();

        foreach (var c in allCharacters)
        {
            if (c == null) continue;
            // 쇼케이스/로비 전용 더미 캐릭터는 제외
            if (c.gameObject.name.Contains("[Showcase") || c.gameObject.name.Contains("Showcase_Ctrl") ||
                c.transform.root.name.Contains("[Lobby") || c.transform.root.name.Contains("[Showcase")) continue;
            inGameCharacters.Add(c);
        }

        StoneThrowerCharacter matchedCharacter = null;

        // 2. 이미 요청된 프리팹과 일치하는 유효한 인게임 캐릭터가 씬에 있는지 루트(Root) 이름 기준으로 정밀 검사
        foreach (var c in inGameCharacters)
        {
            GameObject rootObj = c.transform.root.gameObject;
            string rootName = rootObj.name;
            string childName = c.gameObject.name;

            bool isMatch = rootName.StartsWith(charPrefab.name, System.StringComparison.OrdinalIgnoreCase) ||
                           childName.StartsWith(charPrefab.name, System.StringComparison.OrdinalIgnoreCase);

            if (matchedCharacter == null && isMatch)
            {
                matchedCharacter = c; // 일치하는 기존 캐릭터는 그대로 재사용!
            }
            else
            {
                // 불일치하거나 중복 생성된 찌꺼기 캐릭터는 루트 오브젝트 전체를 완전히 파괴하여 0점 누적 방지!
                if (Application.isPlaying) Destroy(rootObj);
                else DestroyImmediate(rootObj);
            }
        }

        // 3. 일치하는 캐릭터가 없으면 딱 1개 새로 인스턴스화
        if (matchedCharacter == null)
        {
            GameObject spawnedObj = Instantiate(charPrefab);
            spawnedObj.name = charPrefab.name;
            matchedCharacter = spawnedObj.GetComponentInChildren<StoneThrowerCharacter>(true);
            if (matchedCharacter == null)
            {
                matchedCharacter = spawnedObj.AddComponent<StoneThrowerCharacter>();
            }
        }

        character = matchedCharacter;

        if (character != null)
        {
            character.gameObject.SetActive(true);
            character.RestoreVisibility();
            PositionCharacterForMode();
            character.InitializeCharacter();
            character.SetHandStonePrefab(defaultStonePrefab);
            PositionCharacterForMode(); // 초기화 후 한 번 더 정확히 발판 위치에 고정
        }

        if (dualCamera != null && character != null)
        {
            dualCamera.targetCharacter = character.transform;
            dualCamera.SetCameraMode(DualCameraSetup.CameraMode.TopDownPosition);
            dualCamera.SnapCameraImmediate();
        }
    }

    private void PositionCharacterForMode()
    {
        if (character == null) return;
        ResolveSceneReferences();

        if (currentMode == GameMode.TargetAccuracy && _playerPositionRoot != null)
        {
            character.RefreshPlayerPositionGuide();
            int total = character.GetTotalWaypointsCount();
            int safeIdx = (total > 1) ? Mathf.Clamp(targetPlatformIndex, 0, total - 1) : 0;
            character.SetWaypointIndex(safeIdx);

            Vector3 spawnPos = character.GetWaypointWorldPos(safeIdx);
            character.basePosition = spawnPos;
            character.currentPosition = spawnPos;
            character.baseRotation = Quaternion.Euler(0f, 90f, 0f);
            character.transform.position = spawnPos;
            character.transform.rotation = character.baseRotation;
            return;
        }

        // 장거리 모드: 발판 콜라이더(루트 또는 자식)의 실제 월드 중심 상단에 정확히 배치
        if (currentLaunchPier != null)
        {
            BoxCollider pierCol = currentLaunchPier.GetComponent<BoxCollider>() ?? currentLaunchPier.GetComponentInChildren<BoxCollider>();
            Vector3 spawnPos;

            if (pierCol != null)
            {
                spawnPos = new Vector3(pierCol.bounds.center.x, pierCol.bounds.max.y, pierCol.bounds.center.z);
            }
            else
            {
                spawnPos = currentLaunchPier.position + Vector3.up * 0.5f;
            }

            character.basePosition = spawnPos;
            character.currentPosition = spawnPos;
            character.baseRotation = currentLaunchPier.rotation; // 발판의 회전 방향과 일치
            character.transform.position = spawnPos;
            character.transform.rotation = currentLaunchPier.rotation;
        }
        else
        {
            // 발판이 아직 없을 시 수면 높이를 반영하여 안전 배치
            WaterSurface ws = FindAnyObjectByType<WaterSurface>();
            float waterY = (ws != null && ws.GetComponent<BoxCollider>() != null) ? ws.GetComponent<BoxCollider>().bounds.max.y : 16.0f;
            Vector3 defaultSpawnPos = new Vector3(0f, waterY + 0.5f, -10f);
            character.basePosition = defaultSpawnPos;
            character.currentPosition = defaultSpawnPos;
            character.baseRotation = Quaternion.Euler(0f, 0f, 0f);
            character.transform.position = defaultSpawnPos;
            character.transform.rotation = Quaternion.Euler(0f, 0f, 0f);
        }
    }

    public void ReturnToModeSelect()
    {
        currentState = GameState.ModeSelect;
        lastStateChangeTime = Time.time;
        requireTouchRelease = true;
        if (MapPIPManager.Instance != null) MapPIPManager.Instance.UpdatePIPState(false);

        if (SkippingStones.UI.MetaUIManager.Instance != null)
        {
            SkippingStones.UI.MetaUIManager.Instance.ShowScreen(SkippingStones.UI.MetaScreen.Lobby);
        }
    }

    private void Update()
    {
        if (showAquariumModal || showStoneSelectorModal) return;

        switch (currentState)
        {
            case GameState.Positioning:
                UpdatePositioning();
                break;
            case GameState.AimingAngle:
                UpdateAimingAngle();
                break;
            case GameState.ChargingPower:
                UpdateChargingPower();
                break;
            case GameState.ThrowingAnimation:
                break;
            case GameState.Flying:
                UpdateFlying();
                break;
            case GameState.Result:
                // 🌟 침몰 직후 결과 화면으로 넘어가는 1.5초 지연 구간에서도 늦게 누른 스페이스바 마커 포착!
                if (IsKeyTriggered(KeyCode.Space) && stone != null)
                {
                    EvaluateRhythmTiming(0f);
                }

                if (Time.time - lastStateChangeTime > 0.7f && (IsKeyTriggered(KeyCode.R) || IsKeyTriggered(KeyCode.Space)))
                {
                    RestartGame();
                }
                break;
        }
    }

    private void UpdatePositioning()
    {
        if (currentMode == GameMode.LongDistance || currentMode == GameMode.RhythmArcade)
        {
            if (currentLaunchPier != null)
            {
                BoxCollider bc = currentLaunchPier.GetComponent<BoxCollider>();
                float halfW = (bc != null && bc.bounds.extents.x > 1f) ? (bc.bounds.extents.x * 0.85f) : 12f;
                minPositionX = -halfW;
                maxPositionX = halfW;
            }

            float hInput = GetHorizontalInput();
            if (Mathf.Abs(hInput) > 0.001f)
            {
                startPosX = Mathf.Clamp(startPosX + hInput * Time.deltaTime * 7.5f, minPositionX, maxPositionX);
            }

            HandlePierDragSlide();

            if (character != null)
            {
                // 🌟 발판의 기준 위치(basePosition)를 유지하면서 좌우 슬라이드(startPosX) 적용
                character.UpdatePositioning(startPosX, 0f);
                if (stone != null) stone.transform.position = character.GetHandPosition();
            }
        }
        else
        {
            if (IsKeyTriggered(KeyCode.LeftArrow) || IsKeyTriggered(KeyCode.A))
            {
                if (character != null) character.MoveToPreviousWaypoint();
            }
            if (IsKeyTriggered(KeyCode.RightArrow) || IsKeyTriggered(KeyCode.D))
            {
                if (character != null) character.MoveToNextWaypoint();
            }

            HandleTargetSwipeStep();

            if (character != null)
            {
                character.UpdatePositioning(startPosX, 0f);
                if (stone != null) stone.transform.position = character.GetHandPosition();
            }
        }

        if (Time.time - lastStateChangeTime > STATE_COOLDOWN)
        {
            if (IsKeyTriggered(KeyCode.Space) || IsKeyTriggered(KeyCode.Return))
            {
                ConfirmPosition();
            }
        }
    }

    private void HandlePierDragSlide()
    {
        Vector2 curPos = Vector2.zero;
        bool isPressed = false;

#if ENABLE_INPUT_SYSTEM
        var touch = Touchscreen.current;
        var mouse = Mouse.current;
        if (touch != null && touch.primaryTouch.press.isPressed)
        {
            isPressed = true;
            curPos = touch.primaryTouch.position.ReadValue();
        }
        else if (mouse != null && (mouse.leftButton.isPressed || mouse.rightButton.isPressed))
        {
            isPressed = true;
            curPos = mouse.position.ReadValue();
        }
#else
        if (Input.GetMouseButton(0) || Input.GetMouseButton(1))
        {
            isPressed = true;
            curPos = Input.mousePosition;
        }
#endif

        if (isPressed)
        {
            if (!isDraggingMap)
            {
                isDraggingMap = true;
                prevDragPos = curPos;
            }
            else
            {
                float deltaX = (curPos.x - prevDragPos.x) * 0.016f;
                startPosX = Mathf.Clamp(startPosX + deltaX, minPositionX, maxPositionX);
                prevDragPos = curPos;
            }
        }
        else
        {
            isDraggingMap = false;
        }
    }

    private void HandleTargetSwipeStep()
    {
        Vector2 curPos = Vector2.zero;
        bool isPressed = false;

#if ENABLE_INPUT_SYSTEM
        var touch = Touchscreen.current;
        var mouse = Mouse.current;
        if (touch != null && touch.primaryTouch.press.isPressed)
        {
            isPressed = true;
            curPos = touch.primaryTouch.position.ReadValue();
        }
        else if (mouse != null && (mouse.leftButton.isPressed || mouse.rightButton.isPressed))
        {
            isPressed = true;
            curPos = mouse.position.ReadValue();
        }
#else
        if (Input.GetMouseButton(0) || Input.GetMouseButton(1))
        {
            isPressed = true;
            curPos = Input.mousePosition;
        }
#endif

        if (isPressed)
        {
            if (!isSwipingTarget)
            {
                isSwipingTarget = true;
                swipeStartPos = curPos;
            }
            else
            {
                float dx = curPos.x - swipeStartPos.x;
                if (dx > swipeThreshold)
                {
                    if (character != null) character.MoveToNextWaypoint();
                    swipeStartPos = curPos;
                }
                else if (dx < -swipeThreshold)
                {
                    if (character != null) character.MoveToPreviousWaypoint();
                    swipeStartPos = curPos;
                }
            }
        }
        else
        {
            isSwipingTarget = false;
        }
    }

    public void ConfirmPosition()
    {
        currentState = GameState.AimingAngle;
        lastStateChangeTime = Time.time;
        requireTouchRelease = true;
        aimGaugeValue = 0f;
        aimDirection = 1f;

        if (MapPIPManager.Instance != null)
        {
            MapPIPManager.Instance.UpdatePIPState(false);
        }

        if (character != null) character.UpdateAiming(0f);
        if (dualCamera != null) dualCamera.SetCameraMode(DualCameraSetup.CameraMode.ShoulderAim);
    }

    private void UpdateAimingAngle()
    {
        aimGaugeValue += aimDirection * aimSpeed * Time.deltaTime;
        if (aimGaugeValue > 1f) { aimGaugeValue = 1f; aimDirection = -1f; }
        else if (aimGaugeValue < -1f) { aimGaugeValue = -1f; aimDirection = 1f; }

        if (character != null)
        {
            character.UpdateAiming(aimGaugeValue);
            if (stone != null) stone.transform.position = character.GetHandPosition();
        }

        if (Time.time - lastStateChangeTime > STATE_COOLDOWN && IsActionTriggered())
        {
            ConfirmAngle();
        }
    }

    public void ConfirmAngle()
    {
        currentState = GameState.ChargingPower;
        lastStateChangeTime = Time.time;
        requireTouchRelease = true;
        powerGaugeValue = 0.1f;
        powerDirection = 1f;
    }

    private void UpdateChargingPower()
    {
        powerGaugeValue += powerDirection * powerSpeed * Time.deltaTime;
        if (powerGaugeValue > 1f) { powerGaugeValue = 1f; powerDirection = -1f; }
        else if (powerGaugeValue < 0f) { powerGaugeValue = 0f; powerDirection = 1f; }

        if (character != null)
        {
            character.UpdateWindup(powerGaugeValue);
            if (stone != null) stone.transform.position = character.GetHandPosition();
        }

        if (Time.time - lastStateChangeTime > STATE_COOLDOWN && IsActionTriggered())
        {
            LaunchStone();
        }
    }

    public void LaunchStone()
    {
        currentState = GameState.ThrowingAnimation;
        lastStateChangeTime = Time.time;
        requireTouchRelease = true;

        if (character != null)
        {
            character.PlayThrowAnimation(
                onCameraLeadInCallback: (anchorPos, forwardDir) =>
                {
                    if (dualCamera != null) dualCamera.StartLaunchLeadIn(anchorPos, forwardDir);
                },
                onReleaseCallback: () =>
                {
                    ExecuteLaunchPhysics();
                }
            );
        }
        else
        {
            if (dualCamera != null) dualCamera.SetCameraMode(DualCameraSetup.CameraMode.DynamicFlight);
            ExecuteLaunchPhysics();
        }
    }

    private void ExecuteLaunchPhysics()
    {
        currentState = GameState.Flying;
        lastStateChangeTime = Time.time;
        requireTouchRelease = true;

        float angleDegrees = aimGaugeValue * 25f;
        Vector3 baseForward = (character != null) ? (character.baseRotation * Vector3.forward) : Vector3.forward;
        Vector3 direction = Quaternion.Euler(0f, angleDegrees, 0f) * baseForward;

        if (character != null)
        {
            character.currentAimRotation = Quaternion.Euler(0f, angleDegrees, 0f) * character.baseRotation;
            character.transform.rotation = character.currentAimRotation;
            direction = character.currentAimRotation * Vector3.forward;
        }

        float stoneMultiplier = (StoneInventory.Instance != null) ? StoneInventory.Instance.GetCurrentStone().forwardPowerMultiplier : 1.0f;
        float finalPowerMultiplier = Mathf.Lerp(0.6f, 1.4f, powerGaugeValue) * stoneMultiplier;

        Vector3 spawnPos = (character != null) ? character.GetHandPosition() : transform.position + new Vector3(0.35f, 1.2f, 0.8f);
        Quaternion spawnRot = Quaternion.LookRotation(direction, Vector3.up);

        if (currentModeHandler != null)
        {
            currentModeHandler.OnLaunchStone(this, direction, finalPowerMultiplier);
        }
        else
        {
            if (modeHandlers.TryGetValue(currentMode, out var handler))
            {
                currentModeHandler = handler;
                currentModeHandler.OnLaunchStone(this, direction, finalPowerMultiplier);
            }
        }
    }

    private float lastLeftKeyTime = -10f;
    private float lastRightKeyTime = -10f;
    private const float DOUBLE_TAP_THRESHOLD = 0.25f;

    private void UpdateFlying()
    {
        if (IsKeyTriggered(KeyCode.Escape))
        {
            Application.Quit();
        }

        // ⌨️ 키보드 리듬 탭 및 조향 (A/D/S 및 방향키)
        bool leftTriggered = IsKeyTriggered(KeyCode.A) || IsKeyTriggered(KeyCode.LeftArrow);
        bool rightTriggered = IsKeyTriggered(KeyCode.D) || IsKeyTriggered(KeyCode.RightArrow);
        bool centerTriggered = IsKeyTriggered(KeyCode.Space) || IsKeyTriggered(KeyCode.Return) || 
                              IsKeyTriggered(KeyCode.S) || IsKeyTriggered(KeyCode.DownArrow);

        if (leftTriggered)
        {
            float now = Time.unscaledTime;
            bool isDoubleTap = (now - lastLeftKeyTime <= DOUBLE_TAP_THRESHOLD);
            lastLeftKeyTime = now;

            float steerAngle = isDoubleTap ? -8.0f : -5.0f;
            TriggerButtonFeedback(steerAngle);
            EvaluateRhythmTiming(steerAngle);
            return;
        }
        else if (rightTriggered)
        {
            float now = Time.unscaledTime;
            bool isDoubleTap = (now - lastRightKeyTime <= DOUBLE_TAP_THRESHOLD);
            lastRightKeyTime = now;

            float steerAngle = isDoubleTap ? 8.0f : 5.0f;
            TriggerButtonFeedback(steerAngle);
            EvaluateRhythmTiming(steerAngle);
            return;
        }
        else if (centerTriggered)
        {
            TriggerButtonFeedback(0f);
            EvaluateRhythmTiming(0f);
            return;
        }

        if (LakeEnvironmentManager.Instance != null && stone != null)
        {
            LakeEnvironmentManager.Instance.UpdateEnvironmentByDistance(stone.totalDistance);
        }

        HandleFlightFlickSteering();
    }

    private void HandleFlightFlickSteering()
    {
        Vector2 curPos = Vector2.zero;
        bool isDown = false;
        bool isUp = false;

#if ENABLE_INPUT_SYSTEM
        var touch = Touchscreen.current;
        var mouse = Mouse.current;
        if (touch != null && touch.primaryTouch.press.wasPressedThisFrame)
        {
            isDown = true;
            curPos = touch.primaryTouch.position.ReadValue();
        }
        else if (touch != null && touch.primaryTouch.press.wasReleasedThisFrame)
        {
            isUp = true;
            curPos = touch.primaryTouch.position.ReadValue();
        }
        else if (mouse != null && mouse.leftButton.wasPressedThisFrame)
        {
            isDown = true;
            curPos = mouse.position.ReadValue();
        }
        else if (mouse != null && mouse.leftButton.wasReleasedThisFrame)
        {
            isUp = true;
            curPos = mouse.position.ReadValue();
        }
#else
        try
        {
            if (Input.touchCount > 0)
            {
                var t = Input.GetTouch(0);
                if (t.phase == UnityEngine.TouchPhase.Began) { isDown = true; curPos = t.position; }
                else if (t.phase == UnityEngine.TouchPhase.Ended || t.phase == UnityEngine.TouchPhase.Canceled) { isUp = true; curPos = t.position; }
            }
            else if (Input.GetMouseButtonDown(0)) { isDown = true; curPos = Input.mousePosition; }
            else if (Input.GetMouseButtonUp(0)) { isUp = true; curPos = Input.mousePosition; }
        }
        catch { }
#endif

        if (isDown)
        {
            // UI 버튼 영역 탭 시 화면 전체 제스처 중복 방지 (EventSystem 포인터 검사)
            if (UnityEngine.EventSystems.EventSystem.current != null && 
                UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
            {
                return;
            }

            flightTouchStartPos = curPos;
            flightTouchStartTime = Time.unscaledTime;
            isTrackingFlightTouch = true;
            EvaluateRhythmTiming(0f);
        }
        else if (isUp && isTrackingFlightTouch)
        {
            isTrackingFlightTouch = false;
            float deltaX = curPos.x - flightTouchStartPos.x;
            float duration = Time.unscaledTime - flightTouchStartTime;

            if (duration < 0.35f && Mathf.Abs(deltaX) > 30f)
            {
                // 화면 전체 스와이프: 기본 5° / 빠른 스와이프(거리 > 80px) 8°
                float baseAngle = (Mathf.Abs(deltaX) > 80f) ? 8.0f : 5.0f;
                float steerAngle = (deltaX > 0f) ? baseAngle : -baseAngle;
                if (stone != null && !stone.isSunk && !stone.isCrashed)
                {
                    stone.ApplySteerAngle(steerAngle);
                    lastTimingText += (steerAngle > 0f) ? $" \n👉 [RIGHT {steerAngle:F0}° 조향]" : $" \n👈 [LEFT {Mathf.Abs(steerAngle):F0}° 조향]";
                }
            }
        }
    }

    public void EvaluateRhythmTiming(float steerAngleDegrees = 0f)
    {
        if (currentModeHandler != null)
        {
            currentModeHandler.OnEvaluateTiming(this, steerAngleDegrees);
        }
        else if (stone != null && !stone.isCrashed)
        {
            stone.TryRhythmBounce(steerAngleDegrees, out string timingGrade);
            lastTimingText = timingGrade;
        }

        StopCoroutine(nameof(ClearTimingTextAfterDelay));
        StartCoroutine(ClearTimingTextAfterDelay(1.2f));
    }

    public void TriggerFishSnipeEffect(string speciesName)
    {
        fishSnipeCount++;
        bannerNotificationText = $"🎯 FISH SNIPE! [{speciesName}] 저격 성공! (+1,000점 & 코인)";
        lastTimingText = "🔥 FISH SNIPE! 🔥";
        StartCoroutine(HitStopSlowMo(0.25f));
        StopCoroutine(nameof(ClearBannerAfterDelay));
        StartCoroutine(ClearBannerAfterDelay(2.5f));
    }

    public void TriggerFriendOvertake(string friendName, string rank)
    {
        friendOvertakeCount++;
        bannerNotificationText = $"🚩 [{friendName}] 추월 달성! ({rank} 랭킹 진입! +800점)";
        StopCoroutine(nameof(ClearBannerAfterDelay));
        StartCoroutine(ClearBannerAfterDelay(2.5f));
    }

    public void TriggerBoostPadEffect() => boostPadCount++;

    private IEnumerator HitStopSlowMo(float duration)
    {
        Time.timeScale = 0.4f;
        yield return new WaitForSecondsRealtime(duration);
        Time.timeScale = 1.0f;
    }

    private IEnumerator ClearTimingTextAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        lastTimingText = "";
    }

    private IEnumerator ClearBannerAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        bannerNotificationText = "";
    }

    public void HandleSkipBounced(int count, string text)
    {
        lastTimingText = text;
        if (text.Contains("PERFECT")) perfectTimingCount++;
        StopCoroutine(nameof(ClearTimingTextAfterDelay));
        StartCoroutine(ClearTimingTextAfterDelay(0.8f));
    }

    public void HandleStoneSunk(float dist)
    {
        StartCoroutine(DelayedShowResultRoutine(dist, 1.5f));
    }

    private IEnumerator DelayedShowResultRoutine(float dist, float delay)
    {
        yield return new WaitForSeconds(delay);
        if (currentState == GameState.Result) yield break;

        ShowFinalResultDirect(dist);
    }

    public void ShowFinalResultDirect(float dist)
    {
        if (EnvironmentTestHelper.Instance != null)
        {
            EnvironmentTestHelper.Instance.StopAutoFly();
            EnvironmentTestHelper.Instance.showTestUI = false;
        }

        currentState = GameState.Result;
        bannerNotificationText = string.Empty;
        lastTimingText = string.Empty;
        lastStateChangeTime = Time.time;
        requireTouchRelease = true;
        CalculateFinalScores(dist);
    }

    public void CalculateFinalScores(float dist)
    {
        if (hasCalculatedResult) return;
        hasCalculatedResult = true;

        distanceScore = Mathf.RoundToInt(dist * 10f);
        int skips = (stone != null) ? stone.skipCount : 0;
        skipScore = skips * 500;
        lastSkimBonusDist = (stone != null) ? stone.skimDistance : 0f;
        int skimScore = Mathf.RoundToInt(lastSkimBonusDist * 15f);

        specialScore = (perfectTimingCount * 300) + (fishSnipeCount * 1000) + (friendOvertakeCount * 800) + (boostPadCount * 500) + skimScore;
        totalScore = distanceScore + skipScore + specialScore;
        earnedCoins = Mathf.Max(5, Mathf.RoundToInt(totalScore / 25f));

        if (AquariumManager.Instance != null)
        {
            AquariumManager.Instance.AddCoins(earnedCoins);
        }

        lastResultData = new InGameResultData
        {
            finalDistance = dist,
            skipCount = skips,
            perfectTimingCount = perfectTimingCount,
            fishSnipeCount = fishSnipeCount,
            friendOvertakeCount = friendOvertakeCount,
            boostPadCount = boostPadCount,
            earnedCoins = earnedCoins,
            totalScore = totalScore
        };

        if (GameDataManager.Instance != null)
        {
            GameDataManager.Instance.ProcessMatchResult(lastResultData);
        }

        OnMatchResultGenerated?.Invoke(lastResultData);
    }

    public void RestartGame()
    {
        if (LakeEnvironmentManager.Instance != null)
        {
            LakeEnvironmentManager.Instance.ClearDynamicChunks();
        }
        ResetToPositioning();
    }

    public void FinishMatchAndReturnToMapSelect()
    {
        StopAllCoroutines();
        if (topDownReplay != null)
        {
            topDownReplay.isReplayActive = false;
            topDownReplay.isDrawing = false;
        }
        if (LakeEnvironmentManager.Instance != null)
        {
            var mgrObj = LakeEnvironmentManager.Instance.gameObject;
            LakeEnvironmentManager.Instance = null;
            if (Application.isPlaying) Destroy(mgrObj);
            else DestroyImmediate(mgrObj);
        }
        currentState = GameState.ModeSelect;

        if (SkippingStones.UI.MetaUIManager.Instance != null)
        {
            SkippingStones.UI.MetaUIManager.Instance.ShowScreen(SkippingStones.UI.MetaScreen.MapSelect);
        }
    }

    public void ReturnToLobby()
    {
        FinishMatchAndReturnToMapSelect();
    }

    public void ResetToPositioning()
    {
        StopAllCoroutines();
        if (topDownReplay != null) topDownReplay.isReplayActive = false;

        currentState = GameState.Positioning;
        lastStateChangeTime = Time.time + 0.35f;
        startPosX = 0f;
        aimGaugeValue = 0f;
        powerGaugeValue = 0f;
        lastTimingText = "";
        bannerNotificationText = "";

        perfectTimingCount = 0;
        fishSnipeCount = 0;
        friendOvertakeCount = 0;
        boostPadCount = 0;
        distanceScore = 0;
        skipScore = 0;
        specialScore = 0;
        totalScore = 0;
        earnedCoins = 0;
        hasCalculatedResult = false;

        if (dualCamera != null)
        {
            dualCamera.SetCameraMode(DualCameraSetup.CameraMode.TopDownPosition);
        }

        requireTouchRelease = true;
        isTrackingFlightTouch = false;

        if (stone != null)
        {
            stone.OnSkipBounced -= HandleSkipBounced;
            stone.OnStoneSunk -= HandleStoneSunk;
            Destroy(stone.gameObject);
            stone = null;
        }

        if (character == null)
        {
            SetupCharacter(defaultCharacterPrefab);
        }
        else
        {
            PositionCharacterForMode();
            character.RestoreVisibility();
            character.SetHandStonePrefab(defaultStonePrefab);
        }
    }

    public void ApplyCurrentStoneVisuals()
    {
        if (stone == null) return;

        Color targetTrailColor = Color.white;
        if (StoneInventory.Instance != null)
        {
            StoneItem item = StoneInventory.Instance.GetCurrentStone();
            if (item != null)
            {
                Renderer r = stone.GetComponentInChildren<Renderer>();
                if (r != null && r.sharedMaterial != null)
                {
                    r.sharedMaterial.SetColor("_BaseColor", item.color);
                }
                targetTrailColor = item.trailColor;
            }
        }

        if (stone.trail != null)
        {
            stone.trailStartColor = targetTrailColor;
            stone.trail.startWidth = 0.075f;
            stone.trail.endWidth = 0.005f;

            if (stone.trail.material != null)
            {
                stone.trail.material.color = Color.white;
            }

            Gradient g = new Gradient();
            g.SetKeys(
                new GradientColorKey[] { new GradientColorKey(targetTrailColor, 0.0f), new GradientColorKey(targetTrailColor, 1.0f) },
                new GradientAlphaKey[] { new GradientAlphaKey(0.70f, 0.0f), new GradientAlphaKey(0.0f, 1.0f) }
            );
            stone.trail.colorGradient = g;
        }
    }

    #region Input Handling

    public float GetHorizontalInput()
    {
        float h = 0f;
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null)
        {
            if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed) h -= 1f;
            if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed) h += 1f;
        }
#endif
        try
        {
            float legacyH = Input.GetAxisRaw("Horizontal");
            if (Mathf.Abs(legacyH) > 0.01f) h = legacyH;
        }
        catch { }
        return h;
    }

    public bool IsActionTriggered()
    {
        if (Time.time - lastStateChangeTime < STATE_COOLDOWN) return false;

        bool isCurrentlyHeld = false;
#if ENABLE_INPUT_SYSTEM
        if (Mouse.current != null && Mouse.current.leftButton.isPressed) isCurrentlyHeld = true;
        if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.isPressed) isCurrentlyHeld = true;
#endif
        try
        {
            if (Input.touchCount > 0 || Input.GetMouseButton(0)) isCurrentlyHeld = true;
        }
        catch { }

        if (requireTouchRelease)
        {
            if (!isCurrentlyHeld) requireTouchRelease = false;
            return false;
        }

#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null && (Keyboard.current.spaceKey.wasPressedThisFrame || Keyboard.current.enterKey.wasPressedThisFrame)) return true;
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame) return true;
        if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame) return true;
#endif
        try
        {
            if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return) || Input.GetMouseButtonDown(0)) return true;
        }
        catch { }
        return false;
    }

    private bool IsKeyTriggered(KeyCode key)
    {
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null)
        {
            if (key == KeyCode.Space && Keyboard.current.spaceKey.wasPressedThisFrame) return true;
            if (key == KeyCode.Return && (Keyboard.current.enterKey.wasPressedThisFrame || Keyboard.current.numpadEnterKey.wasPressedThisFrame)) return true;
            if (key == KeyCode.R && Keyboard.current.rKey.wasPressedThisFrame) return true;
            if ((key == KeyCode.A || key == KeyCode.LeftArrow) && (Keyboard.current.aKey.wasPressedThisFrame || Keyboard.current.leftArrowKey.wasPressedThisFrame)) return true;
            if ((key == KeyCode.D || key == KeyCode.RightArrow) && (Keyboard.current.dKey.wasPressedThisFrame || Keyboard.current.rightArrowKey.wasPressedThisFrame)) return true;
            if ((key == KeyCode.S || key == KeyCode.DownArrow) && (Keyboard.current.sKey.wasPressedThisFrame || Keyboard.current.downArrowKey.wasPressedThisFrame)) return true;
        }
#endif
        try
        {
            if (Input.GetKeyDown(key)) return true;
        }
        catch { }
        return false;
    }

    private void TriggerButtonFeedback(float steerAngle)
    {
        var ugui = Object.FindAnyObjectByType<StoneSkippingUGUIController>();
        if (ugui != null)
        {
            ugui.TriggerButtonVisualFeedback(steerAngle);
        }
    }

    #endregion
}