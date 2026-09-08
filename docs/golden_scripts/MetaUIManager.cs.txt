using System;
using System.Collections.Generic;
using UnityEngine;
using SkippingStones.Data;
using SkippingStones.Auth;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace SkippingStones.UI
{
    public enum MetaScreen
    {
        Title,
        Lobby,
        MapSelect,
        InGame
    }

    public enum MetaModal
    {
        None,
        Collection,
        Shop,
        Rank,
        Settings,
        StaminaRefill
    }

    /// <summary>
    /// 메타 UI (타이틀, 로비, 맵선택, 결과창, 모달) 통합 관리자
    /// - State/Strategy 패턴을 적용하여 화면별 전담 핸들러로 렌더링/로직 캡슐화
    /// - 9:16 모바일 뷰포트(720x1280) 완벽 종횡비 유지 및 레터박스 자동 보정
    /// - New Input System / Mouse / Touch 100% 통합 감지 (DrawResponsiveButton)
    /// - 터치 안전 규칙(0.20s 디바운스, 터치 릴리즈 락) 적용
    /// </summary>
    public class MetaUIManager : MonoBehaviour
    {
        public static MetaUIManager Instance { get; private set; }

        [Header("현재 화면 및 모달 상태")]
        public MetaScreen currentScreen = MetaScreen.Title;
        public MetaModal currentModal = MetaModal.None;

        [Header("터치 안전성 & 디바운스")]
        public bool requireTouchRelease = false;
        private float lastTransitionTime = 0f;
        private const float DEBOUNCE_COOLDOWN = 0.20f;

        [Header("도감 탭 인덱스 (0: 캐릭터, 1: 돌, 2: 수족관)")]
        public int collectionTabIndex = 0;

        [Header("선택 인덱스")]
        public int selectedCharIndex = 0;
        public int selectedStoneIndex = 0;
        public int selectedMapIndex = 0;

        [Header("로비 3D 쇼케이스 프리팹 & 카메라")]
        [SerializeField] private GameObject lobbyPrefab;
        private GameObject spawnedLobbyInstance;
        private SkippingStones.Visuals.LobbyStoneShowcaseController spawnedLobbyController;
        private SkippingStones.Visuals.LobbyCharacterShowcaseController spawnedCharacterController;
        private Camera cachedMainCamera;

        [Header("맵 환경 매니저 프리팹 목록 (단일 진실 공급원)")]
        [SerializeField] private List<GameObject> mapEnvironmentPrefabs = new List<GameObject>();
        private readonly List<GameObject> envMgrPrefabs = new List<GameObject>();
        private bool envMgrScanned = false;

        public IReadOnlyList<GameObject> EnvMgrPrefabs
        {
            get
            {
                ScanEnvManagers();
                return envMgrPrefabs;
            }
        }

        public void ScanEnvManagers()
        {
            if (envMgrScanned && envMgrPrefabs.Count > 0) return;
            envMgrPrefabs.Clear();

            // 1. 인스펙터에 명시된 맵 프리팹 우선 등록
            if (mapEnvironmentPrefabs != null && mapEnvironmentPrefabs.Count > 0)
            {
                foreach (var p in mapEnvironmentPrefabs)
                {
                    if (p != null && !envMgrPrefabs.Contains(p))
                    {
                        envMgrPrefabs.Add(p);
                    }
                }
            }

            // 2. 런타임 Resources 폴더 내 맵 프리팹 자동 등록 (빌드 환경 지원)
            var resMaps = Resources.LoadAll<GameObject>("BG_Env");
            if (resMaps != null && resMaps.Length > 0)
            {
                foreach (var p in resMaps)
                {
                    if (p != null && p.GetComponent<LakeEnvironmentManager>() != null && !envMgrPrefabs.Contains(p))
                    {
                        envMgrPrefabs.Add(p);
                    }
                }
            }

#if UNITY_EDITOR
            // 3. 에디터 환경: Assets/prefab/BG_Env 전체 자동 스캔 폴백
            string[] guids = UnityEditor.AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/prefab/BG_Env" });
            foreach (var guid in guids)
            {
                string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                GameObject prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab != null && prefab.GetComponent<LakeEnvironmentManager>() != null && !envMgrPrefabs.Contains(prefab))
                {
                    envMgrPrefabs.Add(prefab);
                }
            }
#endif
            // 4. 최후의 폴백: GameController의 defaultMapPrefab 사용
            if (envMgrPrefabs.Count == 0 && GameController.Instance != null && GameController.Instance.defaultMapPrefab != null)
            {
                envMgrPrefabs.Add(GameController.Instance.defaultMapPrefab);
            }

            envMgrScanned = true;
        }

        // 화면별 독립 핸들러 (State Pattern)
        private TitleScreenHandler titleHandler;
        private LobbyScreenHandler lobbyHandler;
        private MapSelectScreenHandler mapSelectHandler;
        private MetaModalHubHandler modalHubHandler;

        // 9:16 가상 좌표계 변환 필드
        private float currentScale = 1f;
        private float currentOffsetX = 0f;
        private float currentOffsetY = 0f;

        // UI 스타일 캐싱
        private GUIStyle _headerStyle;
        private GUIStyle _cardBoxStyle;
        private GUIStyle _glassBtnStyle;
        private GUIStyle _primaryBtnStyle;
        private GUIStyle _tabActiveStyle;
        private GUIStyle _tabInactiveStyle;
        private GUIStyle _labelStyle;
        private GUIStyle _titleStyle;
        private bool _stylesInitialized = false;

        public GUIStyle HeaderStyle => _headerStyle;
        public GUIStyle CardBoxStyle => _cardBoxStyle;
        public GUIStyle GlassBtnStyle => _glassBtnStyle;
        public GUIStyle PrimaryBtnStyle => _primaryBtnStyle;
        public GUIStyle TabActiveStyle => _tabActiveStyle;
        public GUIStyle TabInactiveStyle => _tabInactiveStyle;
        public GUIStyle LabelStyle => _labelStyle;
        public GUIStyle TitleStyle => _titleStyle;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void AutoInitialize()
        {
            if (Instance == null)
            {
                GameObject go = new GameObject("[MetaUIManager]");
                go.AddComponent<MetaUIManager>();
                DontDestroyOnLoad(go);
            }
        }

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
                InitializeHandlers();
            }
            else if (Instance != this)
            {
                Destroy(gameObject);
                return;
            }
        }

        private void InitializeHandlers()
        {
            titleHandler = new TitleScreenHandler(this);
            lobbyHandler = new LobbyScreenHandler(this);
            mapSelectHandler = new MapSelectScreenHandler(this);
            modalHubHandler = new MetaModalHubHandler(this);
        }

        private void Start()
        {
            ShowScreen(MetaScreen.Title);

            if (GameController.Instance != null)
            {
                GameController.Instance.OnMatchResultGenerated += HandleMatchResult;
            }
        }

        private void OnDestroy()
        {
            if (GameController.Instance != null)
            {
                GameController.Instance.OnMatchResultGenerated -= HandleMatchResult;
            }
        }

        private void Update()
        {
            bool isHeld = false;

#if ENABLE_INPUT_SYSTEM
            if (Touchscreen.current != null)
            {
                isHeld = Touchscreen.current.primaryTouch.press.isPressed;
            }
            else if (Mouse.current != null)
            {
                isHeld = Mouse.current.leftButton.isPressed;
            }
#else
            isHeld = Input.GetMouseButton(0);
#endif

            if (!isHeld && requireTouchRelease)
            {
                requireTouchRelease = false;
            }
        }

        public void ShowScreen(MetaScreen screen)
        {
            currentScreen = screen;
            currentModal = MetaModal.None;
            requireTouchRelease = false;
            lastTransitionTime = Time.unscaledTime;

            UpdateLobbyShowcase(screen);

            if (screen == MetaScreen.Title) titleHandler?.OnEnter();
            else if (screen == MetaScreen.Lobby) lobbyHandler?.OnEnter();
            else if (screen == MetaScreen.MapSelect) mapSelectHandler?.OnEnter();
        }

        public void ShowModal(MetaModal modal)
        {
            currentModal = modal;
            requireTouchRelease = true;
            lastTransitionTime = Time.unscaledTime;
        }

        public void CloseModal()
        {
            currentModal = MetaModal.None;
            requireTouchRelease = true;
            lastTransitionTime = Time.unscaledTime;
        }

        private void UpdateLobbyShowcase(MetaScreen screen)
        {
            if (cachedMainCamera == null)
            {
                cachedMainCamera = Camera.main;
            }

            if (screen == MetaScreen.Lobby)
            {
                if (GameController.Instance != null)
                {
                    GameController.Instance.ClearInGameCharacter();
                }

                if (spawnedLobbyInstance == null)
                {
                    GameObject prefabToUse = lobbyPrefab;
                    if (prefabToUse == null)
                    {
                        prefabToUse = Resources.Load<GameObject>("Lobby");
                    }
#if UNITY_EDITOR
                    if (prefabToUse == null)
                    {
                        prefabToUse = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/prefab/Lobby.prefab");
                    }
#endif
                    if (prefabToUse != null)
                    {
                        spawnedLobbyInstance = Instantiate(prefabToUse);
                        spawnedLobbyInstance.name = "[Lobby_3D_Showcase]";
                    }
                }
                else
                {
                    spawnedLobbyInstance.SetActive(true);
                }

                if (spawnedLobbyInstance != null)
                {
                    spawnedLobbyController = spawnedLobbyInstance.GetComponentInChildren<SkippingStones.Visuals.LobbyStoneShowcaseController>();
                    if (spawnedLobbyController != null)
                    {
                        spawnedLobbyController.OnSelectedStoneChanged -= HandleLobbyStoneChanged;
                        spawnedLobbyController.OnSelectedStoneChanged += HandleLobbyStoneChanged;
                        spawnedLobbyController.InitializeShowcase();
                    }

                    spawnedCharacterController = spawnedLobbyInstance.GetComponentInChildren<SkippingStones.Visuals.LobbyCharacterShowcaseController>();
                    if (spawnedCharacterController != null)
                    {
                        spawnedCharacterController.ClearAllShowcaseCharacters();
                        spawnedCharacterController.InitializeShowcase();
                    }
                }

                if (cachedMainCamera != null) cachedMainCamera.enabled = false;
                if (spawnedLobbyInstance != null)
                {
                    var lobbyCam = spawnedLobbyInstance.GetComponentInChildren<Camera>(true);
                    if (lobbyCam != null) lobbyCam.enabled = true;
                }
            }
            else
            {
                if (spawnedLobbyInstance != null)
                {
                    spawnedLobbyInstance.SetActive(false);
                }
                if (cachedMainCamera != null)
                {
                    cachedMainCamera.enabled = true;
                }
            }
        }

        public void SwitchCharacter(int direction)
        {
            var dm = GameDataManager.Instance;
            if (dm == null || dm.characterCatalog == null || dm.characterCatalog.Count == 0) return;

            int total = dm.characterCatalog.Count;
            selectedCharIndex = (selectedCharIndex + direction + total) % total;
            var chosen = dm.characterCatalog[selectedCharIndex];

            if (dm.UserData != null)
            {
                dm.UserData.selectedCharacterId = chosen.id;
                dm.SaveUserData();
            }

            if (spawnedCharacterController != null)
            {
                if (direction > 0) spawnedCharacterController.NextCharacter();
                else spawnedCharacterController.PreviousCharacter();
            }
        }

        private void HandleLobbyStoneChanged(int stoneIdx, GameObject stonePrefab)
        {
            var dm = GameDataManager.Instance;
            if (dm == null || stonePrefab == null) return;

            string stoneId = stonePrefab.name;
            var db = dm.StoneDatabase;
            if (db != null)
            {
                var stoneSO = db.GetStoneByIndex(stoneIdx);
                if (stoneSO != null) stoneId = stoneSO.id;
            }

            if (dm.UserData != null)
            {
                dm.UserData.selectedStoneId = stoneId;
                dm.SaveUserData();
            }
        }

        public async void LoginAndEnterLobby(AuthProviderType authType)
        {
            if (AuthManager.Instance != null)
            {
                var user = await AuthManager.Instance.LoginAsync(authType);
                if (user != null)
                {
                    ShowScreen(MetaScreen.Lobby);
                }
            }
            else
            {
                ShowScreen(MetaScreen.Lobby);
            }
        }

        public void StartMatchGame()
        {
            var dm = GameDataManager.Instance;
            if (dm != null && !dm.ConsumeStamina(1))
            {
                ShowModal(MetaModal.StaminaRefill);
                return;
            }

            MatchSessionData session = dm != null ? dm.CreateCurrentMatchSession() : new MatchSessionData();
            ScanEnvManagers();
            if (envMgrPrefabs.Count > selectedMapIndex && selectedMapIndex >= 0)
            {
                session.mapPrefabOverride = envMgrPrefabs[selectedMapIndex];
            }
            else if (envMgrPrefabs.Count > 0)
            {
                session.mapPrefabOverride = envMgrPrefabs[0];
            }

            ShowScreen(MetaScreen.InGame);

            if (GameController.Instance != null)
            {
                GameController.Instance.StartGameSession(session);
            }
        }

        private void HandleMatchResult(InGameResultData result)
        {
            // 인게임 결과 처리 후 로비 복귀 지원
        }

        private void OnGUI()
        {
            if (currentScreen == MetaScreen.InGame) return;

            InitStyles();

            // 9:16 모바일 뷰포트(720x1280) 스케일링 계산
            float targetAspect = 720f / 1280f;
            float currentAspect = (float)Screen.width / Screen.height;

            if (currentAspect < targetAspect)
            {
                currentScale = (float)Screen.width / 720f;
                currentOffsetX = 0f;
                currentOffsetY = (Screen.height - (1280f * currentScale)) * 0.5f;
            }
            else
            {
                currentScale = (float)Screen.height / 1280f;
                currentOffsetX = (Screen.width - (720f * currentScale)) * 0.5f;
                currentOffsetY = 0f;
            }

            Matrix4x4 origMatrix = GUI.matrix;
            GUI.matrix = Matrix4x4.TRS(new Vector3(currentOffsetX, currentOffsetY, 0f), Quaternion.identity, new Vector3(currentScale, currentScale, 1f));

            // 화면별 렌더링 라우팅 (State Pattern)
            if (currentScreen == MetaScreen.Title) titleHandler?.DrawScreen(1f, 0f, 0f);
            else if (currentScreen == MetaScreen.Lobby) lobbyHandler?.DrawScreen(1f, 0f, 0f);
            else if (currentScreen == MetaScreen.MapSelect) mapSelectHandler?.DrawScreen(1f, 0f, 0f);

            // 모달 허브 렌더링
            if (currentModal != MetaModal.None)
            {
                modalHubHandler?.DrawModal(currentModal, 1f, 0f, 0f);
            }

            GUI.matrix = origMatrix;
        }

        public Rect GetScaledRect(float x, float y, float w, float h, float scale, float offsetX, float offsetY)
        {
            return new Rect(x, y, w, h);
        }

        public bool DrawResponsiveButton(Rect rect, string text, GUIStyle style)
        {
            if (Time.unscaledTime - lastTransitionTime < DEBOUNCE_COOLDOWN)
            {
                GUI.Box(rect, text, style);
                return false;
            }

            // 1. 유니티 IMGUI 표준 GUI.Button 클릭 감지
            if (GUI.Button(rect, text, style))
            {
                lastTransitionTime = Time.unscaledTime;
                return true;
            }

            // 2. 디바이스 시뮬레이터(Simulator) 터치 및 마우스 다운 이벤트 즉시 감지 (GUI.matrix 역변환 지원)
            if (Event.current != null && (Event.current.type == EventType.MouseDown || Event.current.type == EventType.TouchDown))
            {
                Vector2 pointerPos = Event.current.mousePosition;
                if (rect.Contains(pointerPos))
                {
                    lastTransitionTime = Time.unscaledTime;
                    Event.current.Use();
                    return true;
                }
            }

            return false;
        }

        private void InitStyles()
        {
            if (_stylesInitialized) return;

            _headerStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.RoundToInt(28 * currentScale),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            _headerStyle.normal.textColor = Color.white;

            _titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.RoundToInt(44 * currentScale),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            _titleStyle.normal.textColor = new Color(0.35f, 0.85f, 1.0f);

            _labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.RoundToInt(20 * currentScale),
                fontStyle = FontStyle.Normal,
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true
            };
            _labelStyle.normal.textColor = new Color(0.85f, 0.85f, 0.85f);

            _cardBoxStyle = new GUIStyle(GUI.skin.box);
            Texture2D cardTex = MakeTex(2, 2, new Color(0.08f, 0.12f, 0.18f, 0.85f));
            _cardBoxStyle.normal.background = cardTex;

            _glassBtnStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = Mathf.RoundToInt(22 * currentScale),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true
            };
            _glassBtnStyle.normal.background = MakeTex(2, 2, new Color(0.18f, 0.26f, 0.38f, 0.80f));
            _glassBtnStyle.normal.textColor = Color.white;

            _primaryBtnStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = Mathf.RoundToInt(26 * currentScale),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true
            };
            _primaryBtnStyle.normal.background = MakeTex(2, 2, new Color(0.15f, 0.65f, 0.95f, 0.95f));
            _primaryBtnStyle.normal.textColor = Color.white;

            _tabActiveStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = Mathf.RoundToInt(22 * currentScale),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            _tabActiveStyle.normal.background = MakeTex(2, 2, new Color(0.20f, 0.70f, 1.0f, 0.95f));
            _tabActiveStyle.normal.textColor = Color.white;

            _tabInactiveStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = Mathf.RoundToInt(20 * currentScale),
                fontStyle = FontStyle.Normal,
                alignment = TextAnchor.MiddleCenter
            };
            _tabInactiveStyle.normal.background = MakeTex(2, 2, new Color(0.12f, 0.16f, 0.22f, 0.70f));
            _tabInactiveStyle.normal.textColor = new Color(0.70f, 0.70f, 0.70f);

            _stylesInitialized = true;
        }

        private Texture2D MakeTex(int width, int height, Color col)
        {
            Color[] pix = new Color[width * height];
            for (int i = 0; i < pix.Length; ++i) pix[i] = col;
            Texture2D result = new Texture2D(width, height);
            result.SetPixels(pix);
            result.Apply();
            return result;
        }
    }
}
