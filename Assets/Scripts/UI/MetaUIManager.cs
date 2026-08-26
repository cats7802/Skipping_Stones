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
        private bool pointerDownConsumedThisFrame = false;

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
            }
            else if (Instance != this)
            {
                Destroy(gameObject);
                return;
            }
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

        private void HandleMatchResult(InGameResultData result)
        {
            // 인게임 uGUI (ResultModalPanel)가 결과창을 100% 전담 렌더링
        }

        private void Update()
        {
            bool isHeld = false;
#if ENABLE_INPUT_SYSTEM
            if (Touchscreen.current != null)
            {
                foreach (var touch in Touchscreen.current.touches)
                {
                    if (touch.press.isPressed) { isHeld = true; break; }
                }
            }
            if (Mouse.current != null && Mouse.current.leftButton.isPressed)
            {
                isHeld = true;
            }
#else
            if (Input.touchCount > 0 || Input.GetMouseButton(0))
            {
                isHeld = true;
            }
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
            requireTouchRelease = true;
            lastTransitionTime = Time.unscaledTime;

            UpdateLobbyShowcase(screen);
        }

        /// <summary>
        /// 화면 전환에 따른 로비 3D 디오라마 및 메인 카메라 활성화/비활성화 제어
        /// </summary>
        private void UpdateLobbyShowcase(MetaScreen screen)
        {
            if (cachedMainCamera == null)
            {
                cachedMainCamera = Camera.main;
            }

            if (screen == MetaScreen.Lobby)
            {
                // 1. 로비 3D 프리팹 스폰 (미스폰 시)
                if (spawnedLobbyInstance == null)
                {
                    GameObject prefabToUse = lobbyPrefab;
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

                // Lobby 3D 스톤 셀렉터 & 캐릭터 쇼케이스 컨트롤러 캐싱 (프리팹에 정식 부착된 컴포넌트만 참조)
                if (spawnedLobbyInstance != null)
                {
                    spawnedLobbyController = spawnedLobbyInstance.GetComponentInChildren<SkippingStones.Visuals.LobbyStoneShowcaseController>();
                    if (spawnedLobbyController == null)
                    {
                        Debug.LogWarning("[MetaUIManager] Lobby 프리팹에 'LobbyStoneShowcaseController' 컴포넌트가 누락되어 있습니다! 프리팹에 스크립트를 추가해주세요.");
                    }
                    else
                    {
                        spawnedLobbyController.OnSelectedStoneChanged += (idx, prefab) =>
                        {
                            var dm = GameDataManager.Instance;
                            if (dm != null && prefab != null)
                            {
                                dm.UserData.selectedStoneId = prefab.name;
                                dm.SaveUserData();
                            }
                        };
                    }

                    spawnedCharacterController = spawnedLobbyInstance.GetComponentInChildren<SkippingStones.Visuals.LobbyCharacterShowcaseController>();
                    if (spawnedCharacterController == null)
                    {
                        Debug.LogWarning("[MetaUIManager] Lobby 프리팹에 'LobbyCharacterShowcaseController' 컴포넌트가 누락되어 있습니다! 프리팹에 스크립트를 추가해주세요.");
                    }
                }

                // 2. 메인 카메라 비활성화 (로비 카메라 우선 구동)
                if (cachedMainCamera != null)
                {
                    cachedMainCamera.enabled = false;
                }
            }
            else
            {
                // 로비가 아닐 때: 로비 인스턴스 숨김/삭제 및 메인 카메라 복구
                if (spawnedLobbyInstance != null)
                {
                    Destroy(spawnedLobbyInstance);
                    spawnedLobbyInstance = null;
                }
                spawnedLobbyController = null;
                spawnedCharacterController = null;

                if (cachedMainCamera != null)
                {
                    cachedMainCamera.enabled = true;
                }
            }
        }

        public void OpenModal(MetaModal modal)
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

        private void InitStyles()
        {
            if (_stylesInitialized) return;

            _headerStyle = new GUIStyle(GUI.skin.box)
            {
                fontSize = 20,
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold
            };

            _cardBoxStyle = new GUIStyle(GUI.skin.box)
            {
                fontSize = 18,
                alignment = TextAnchor.UpperCenter
            };

            _glassBtnStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 20,
                fontStyle = FontStyle.Bold,
                wordWrap = true
            };

            _primaryBtnStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 24,
                fontStyle = FontStyle.Bold,
                wordWrap = true
            };

            _tabActiveStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 18,
                fontStyle = FontStyle.Bold
            };

            _tabInactiveStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 16
            };

            _labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 18,
                wordWrap = true,
                alignment = TextAnchor.MiddleLeft
            };

            _titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 32,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };

            _stylesInitialized = true;
        }

        #region 가상 720x1280 좌표계 & 통합 버튼 입력 엔진
        private bool GetPointerDownVirtualPos(out Vector2 virtualPos)
        {
            virtualPos = Vector2.zero;

#if ENABLE_INPUT_SYSTEM
            // 1. New Input System 모바일 터치
            if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame)
            {
                Vector2 p = Touchscreen.current.primaryTouch.position.ReadValue();
                virtualPos = new Vector2((p.x - currentOffsetX) / currentScale, (Screen.height - p.y - currentOffsetY) / currentScale);
                return true;
            }

            // 2. New Input System 마우스 클릭
            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            {
                Vector2 p = Mouse.current.position.ReadValue();
                virtualPos = new Vector2((p.x - currentOffsetX) / currentScale, (Screen.height - p.y - currentOffsetY) / currentScale);
                return true;
            }
#else
            try
            {
                if (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began)
                {
                    Vector2 p = Input.GetTouch(0).position;
                    virtualPos = new Vector2((p.x - currentOffsetX) / currentScale, (Screen.height - p.y - currentOffsetY) / currentScale);
                    return true;
                }

                if (Input.GetMouseButtonDown(0))
                {
                    Vector3 m = Input.mousePosition;
                    virtualPos = new Vector2((m.x - currentOffsetX) / currentScale, (Screen.height - m.y - currentOffsetY) / currentScale);
                    return true;
                }
            }
            catch { }
#endif

            // 3. IMGUI 내부 MouseDown 이벤트
            if (Event.current != null && Event.current.type == EventType.MouseDown && Event.current.button == 0)
            {
                virtualPos = Event.current.mousePosition;
                return true;
            }

            return false;
        }

        private bool DrawResponsiveButton(Rect rect, string text, GUIStyle style, Color? bgColor = null)
        {
            if (Time.unscaledTime - lastTransitionTime < DEBOUNCE_COOLDOWN)
            {
                Color prev = GUI.backgroundColor;
                if (bgColor.HasValue) GUI.backgroundColor = bgColor.Value;
                GUI.Button(rect, text, style);
                if (bgColor.HasValue) GUI.backgroundColor = prev;
                return false;
            }

            if (requireTouchRelease)
            {
                Color prev = GUI.backgroundColor;
                if (bgColor.HasValue) GUI.backgroundColor = bgColor.Value;
                GUI.Button(rect, text, style);
                if (bgColor.HasValue) GUI.backgroundColor = prev;
                return false;
            }

            Color prevBg = GUI.backgroundColor;
            if (bgColor.HasValue) GUI.backgroundColor = bgColor.Value;
            bool clicked = GUI.Button(rect, text, style);
            if (bgColor.HasValue) GUI.backgroundColor = prevBg;

            if (Event.current != null && Event.current.type == EventType.Repaint)
            {
                return false;
            }

            if (pointerDownConsumedThisFrame)
            {
                return false;
            }

            if (clicked)
            {
                pointerDownConsumedThisFrame = true;
                lastTransitionTime = Time.unscaledTime;
                requireTouchRelease = true;
                if (Event.current != null && Event.current.type != EventType.Layout && Event.current.type != EventType.Repaint)
                {
                    Event.current.Use();
                }
                if (AudioManager.Instance != null) AudioManager.Instance.PlaySound(SoundType.ButtonClick, 0.9f);
                HapticFeedbackHelper.TriggerLightTap();
                return true;
            }

            if (GetPointerDownVirtualPos(out Vector2 pointerPos))
            {
                if (rect.Contains(pointerPos))
                {
                    pointerDownConsumedThisFrame = true;
                    lastTransitionTime = Time.unscaledTime;
                    requireTouchRelease = true;
                    if (Event.current != null && Event.current.type != EventType.Layout && Event.current.type != EventType.Repaint)
                    {
                        Event.current.Use();
                    }
                    if (AudioManager.Instance != null) AudioManager.Instance.PlaySound(SoundType.ButtonClick, 0.9f);
                    HapticFeedbackHelper.TriggerLightTap();
                    return true;
                }
            }

            return false;
        }
        #endregion

        private void OnGUI()
        {
            if (currentScreen == MetaScreen.InGame && currentModal == MetaModal.None) return;

            InitStyles();

            float actualW = Screen.width;
            float actualH = Screen.height;
            if (actualW <= 0 || actualH <= 0) return;

            const float targetW = 720f;
            const float targetH = 1280f;
            float targetAspect = targetW / targetH; // 0.5625 (9:16)
            float currentAspect = actualW / actualH;

            if (currentAspect > targetAspect) // 가로가 더 넓은 화면 (PC 모니터 / 가로 뷰)
            {
                currentScale = actualH / targetH;
                currentOffsetX = (actualW - targetW * currentScale) * 0.5f;
                currentOffsetY = 0f;
            }
            else // 세로 화면
            {
                currentScale = actualW / targetW;
                currentOffsetX = 0f;
                currentOffsetY = (actualH - targetH * currentScale) * 0.5f;
            }

            Matrix4x4 origMatrix = GUI.matrix;
            GUI.matrix = Matrix4x4.TRS(new Vector3(currentOffsetX, currentOffsetY, 0f), Quaternion.identity, new Vector3(currentScale, currentScale, 1f));

            pointerDownConsumedThisFrame = false;

            switch (currentScreen)
            {
                case MetaScreen.Title:
                    DrawTitleScreen();
                    break;
                case MetaScreen.Lobby:
                    DrawLobbyScreen();
                    break;
                case MetaScreen.MapSelect:
                    DrawMapSelectScreen();
                    break;
            }

            if (currentModal != MetaModal.None)
            {
                DrawModalOverlay();
            }

            GUI.matrix = origMatrix;
        }

        #region 1. 타이틀 화면
        private void DrawTitleScreen()
        {
            // 로고
            GUI.Label(new Rect(60, 220, 600, 80), "🌊 물수제비 마스터 3D", _titleStyle);
            GUI.Label(new Rect(60, 310, 600, 40), "✨ Stone Skipping 3D ✨", _headerStyle);

            var dm = GameDataManager.Instance;
            bool hasKakao = dm != null && dm.UserData != null && dm.UserData.hasKakaoAccount;

            if (hasKakao)
            {
                // 카카오 토큰 보유 시: 부드럽게 깜빡이는 펄스 안내 문구 및 전면 터치 진입
                float alpha = 0.4f + Mathf.PingPong(Time.unscaledTime * 1.5f, 0.6f);
                Color prevColor = GUI.color;
                GUI.color = new Color(1f, 1f, 1f, alpha);
                GUI.Label(new Rect(60, 840, 600, 50), "• 화면을 터치하여 시작 (Touch to Start) •", _headerStyle);
                GUI.color = prevColor;

                // 화면 어디를 터치해도 즉시 로비 진입
                if (DrawResponsiveButton(new Rect(0, 0, 720, 1280), string.Empty, GUIStyle.none))
                {
                    LoginAndEnterLobby(AuthProviderType.Kakao);
                }
            }
            else
            {
                // 미로그인 상태: 1차 로그인 선택 2종 버튼
                if (DrawResponsiveButton(new Rect(80, 820, 560, 80), "🟡 카카오 계정으로 로그인", _primaryBtnStyle))
                {
                    LoginAndEnterLobby(AuthProviderType.Kakao);
                }

                if (DrawResponsiveButton(new Rect(80, 920, 560, 75), "👤 게스트로 시작", _glassBtnStyle))
                {
                    LoginAndEnterLobby(AuthProviderType.Guest);
                }
            }
        }

        private void LoginAndEnterLobby(AuthProviderType provider)
        {
            var dm = GameDataManager.Instance;
            if (dm != null)
            {
                if (provider == AuthProviderType.Kakao)
                {
                    if (string.IsNullOrEmpty(dm.UserData.kakaoNickname))
                    {
                        dm.UserData.kakaoNickname = "카카오 달인";
                    }
                    dm.UserData.nickname = dm.UserData.kakaoNickname;
                    dm.UserData.authProvider = "Kakao";
                    dm.UserData.hasKakaoAccount = true;
                }
                else
                {
                    dm.UserData.nickname = "조약돌 마스터";
                    dm.UserData.authProvider = "Guest";
                }
                dm.SaveUserData();
            }

            ShowScreen(MetaScreen.Lobby);
        }
        #endregion

        #region 2. 로비 화면
        private void DrawLobbyScreen()
        {
            DrawTopHeaderBar();

            var dm = GameDataManager.Instance;

            // 1. 캐릭터 스위처 (좌/우 화살표 클릭 시 3D 쇼케이스 트랜지션 연동)
            if (DrawResponsiveButton(new Rect(60, 320, 80, 80), "◀", _glassBtnStyle))
            {
                if (spawnedCharacterController != null)
                {
                    spawnedCharacterController.PreviousCharacter();
                }
                else if (dm != null && dm.characterCatalog.Count > 0)
                {
                    selectedCharIndex = (selectedCharIndex - 1 + dm.characterCatalog.Count) % dm.characterCatalog.Count;
                    dm.UserData.selectedCharacterId = dm.characterCatalog[selectedCharIndex].id;
                }
            }
            if (DrawResponsiveButton(new Rect(580, 320, 80, 80), "▶", _glassBtnStyle))
            {
                if (spawnedCharacterController != null)
                {
                    spawnedCharacterController.NextCharacter();
                }
                else if (dm != null && dm.characterCatalog.Count > 0)
                {
                    selectedCharIndex = (selectedCharIndex + 1) % dm.characterCatalog.Count;
                    dm.UserData.selectedCharacterId = dm.characterCatalog[selectedCharIndex].id;
                }
            }



            // 3. 하단 독 바 & GO 버튼 (최하단 Y=1150으로 바짝 밀착)
            if (DrawResponsiveButton(new Rect(30, 1150, 130, 95), "🛒\n상점", _glassBtnStyle))
            {
                OpenModal(MetaModal.Shop);
            }
            if (DrawResponsiveButton(new Rect(175, 1150, 130, 95), "📖\n도감", _glassBtnStyle))
            {
                OpenModal(MetaModal.Collection);
            }
            if (DrawResponsiveButton(new Rect(320, 1150, 130, 95), "🏆\n랭킹", _glassBtnStyle))
            {
                OpenModal(MetaModal.Rank);
            }

            if (DrawResponsiveButton(new Rect(465, 1140, 225, 110), "🚀 GO!\n(맵 선택)", _primaryBtnStyle))
            {
                ShowScreen(MetaScreen.MapSelect);
            }
        }
        #endregion

        #region 3. 맵 & 모드 선택 화면
        private readonly List<GameObject> envMgrPrefabs = new List<GameObject>();
        private bool envMgrScanned = false;

        private void ScanEnvManagers()
        {
            if (envMgrScanned) return;
            envMgrPrefabs.Clear();

#if UNITY_EDITOR
            string[] guids = UnityEditor.AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/prefab/BG_Env" });
            foreach (var guid in guids)
            {
                string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                GameObject prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab != null && prefab.GetComponent<LakeEnvironmentManager>() != null)
                {
                    envMgrPrefabs.Add(prefab);
                }
            }
#endif
            envMgrScanned = true;
        }

        private void DrawMapSelectScreen()
        {
            DrawTopHeaderBar();
            ScanEnvManagers();

            if (DrawResponsiveButton(new Rect(40, 140, 90, 60), "⬅️ 뒤로", _glassBtnStyle))
            {
                ShowScreen(MetaScreen.Lobby);
            }

            var dm = GameDataManager.Instance;
            bool isLong = (dm != null && dm.UserData.selectedGameMode == GameController.GameMode.LongDistance);

            if (DrawResponsiveButton(new Rect(150, 140, 250, 60), isLong ? "🔘 1500m 원거리" : "⚪ 1500m 원거리", isLong ? _tabActiveStyle : _tabInactiveStyle))
            {
                if (dm != null)
                {
                    dm.UserData.selectedGameMode = GameController.GameMode.LongDistance;
                    dm.SaveUserData();
                }
            }
            if (DrawResponsiveButton(new Rect(410, 140, 270, 60), !isLong ? "🔘 🎯 강 건너기(타깃)" : "⚪ 🎯 강 건너기(타깃)", !isLong ? _tabActiveStyle : _tabInactiveStyle))
            {
                if (dm != null)
                {
                    dm.UserData.selectedGameMode = GameController.GameMode.TargetAccuracy;
                    dm.SaveUserData();
                }
            }

            // 맵 메타 정보 및 썸네일 결정
            int totalMaps = Mathf.Max(1, envMgrPrefabs.Count);
            if (selectedMapIndex >= totalMaps) selectedMapIndex = 0;

            string mapName = "에메랄드 호수";
            Sprite mapThumb = null;

            if (envMgrPrefabs.Count > selectedMapIndex && envMgrPrefabs[selectedMapIndex] != null)
            {
                var lem = envMgrPrefabs[selectedMapIndex].GetComponent<LakeEnvironmentManager>();
                if (lem != null)
                {
                    if (!string.IsNullOrEmpty(lem.mapTitle)) mapName = lem.mapTitle;
                    mapThumb = lem.mapThumbnail;
                }
            }
            else if (dm != null && dm.mapCatalog.Count > selectedMapIndex)
            {
                mapName = dm.mapCatalog[selectedMapIndex].name;
            }

            // 맵 카드 뷰
            GUI.Box(new Rect(40, 220, 640, 480), string.Empty, _cardBoxStyle);
            GUI.Label(new Rect(60, 235, 600, 40), $"<b>🗺️ {mapName}</b>", _titleStyle);

            // 썸네일 이미지 드로잉
            Rect thumbRect = new Rect(80, 285, 560, 395);
            if (mapThumb != null && mapThumb.texture != null)
            {
                GUI.DrawTexture(thumbRect, mapThumb.texture, ScaleMode.ScaleAndCrop);
            }
            else
            {
                GUI.Box(thumbRect, "\n\n\n\n🖼️ 맵 썸네일 이미지 미등록\n(LakeEnvironmentManager.mapThumbnail)", _cardBoxStyle);
            }

            // 좌우 맵 넘김 버튼
            if (DrawResponsiveButton(new Rect(60, 450, 70, 60), "◀", _glassBtnStyle))
            {
                selectedMapIndex = (selectedMapIndex - 1 + totalMaps) % totalMaps;
            }
            if (DrawResponsiveButton(new Rect(590, 450, 70, 60), "▶", _glassBtnStyle))
            {
                selectedMapIndex = (selectedMapIndex + 1) % totalMaps;
            }

            // 코스 미니맵 바
            GUI.Box(new Rect(40, 720, 640, 160), "🚩 Start ────────────────────🌊──────────────────── 🏁 1500m Finish", _cardBoxStyle);

            // GAME START 버튼
            if (DrawResponsiveButton(new Rect(80, 920, 560, 90), "⚡-1   GAME START !", _primaryBtnStyle))
            {
                StartMatchSession();
            }
        }

        private void StartMatchSession()
        {
            var dm = GameDataManager.Instance;
            if (dm != null)
            {
                if (!dm.ConsumeStamina(1))
                {
                    OpenModal(MetaModal.StaminaRefill);
                    return;
                }

                MatchSessionData session = dm.CreateCurrentMatchSession();
                if (envMgrPrefabs.Count > selectedMapIndex)
                {
                    session.mapPrefabOverride = envMgrPrefabs[selectedMapIndex];
                }

                ShowScreen(MetaScreen.InGame);

                if (GameController.Instance != null)
                {
                    GameController.Instance.StartGameSession(session);
                }
            }
        }
        #endregion



        #region 5. 상단 헤더 바 & 모달
        private void DrawTopHeaderBar()
        {
            GUI.Box(new Rect(20, 20, 680, 95), string.Empty, _headerStyle);

            var dm = GameDataManager.Instance;
            string nick = dm != null ? dm.UserData.nickname : "플레이어";
            int gold = dm != null ? dm.UserData.gold : 0;
            int dia = dm != null ? dm.UserData.diamonds : 0;
            int stam = dm != null ? dm.UserData.stamina : 10;
            int maxStam = dm != null ? dm.UserData.maxStamina : 10;

            GUI.Label(new Rect(40, 30, 220, 40), $"👤 {nick}", _labelStyle);
            GUI.Label(new Rect(40, 65, 220, 40), $"🪙 {gold:N0}  💎 {dia}", _labelStyle);
            GUI.Label(new Rect(450, 35, 160, 40), $"⚡ {stam}/{maxStam}", _labelStyle);

            if (DrawResponsiveButton(new Rect(615, 35, 65, 65), "⚙️", _glassBtnStyle))
            {
                OpenModal(MetaModal.Settings);
            }
        }

        private void DrawModalOverlay()
        {
            GUI.Box(new Rect(30, 120, 660, 1040), string.Empty, _cardBoxStyle);

            if (DrawResponsiveButton(new Rect(620, 135, 55, 55), "✕", _glassBtnStyle))
            {
                CloseModal();
                return;
            }

            switch (currentModal)
            {
                case MetaModal.Collection:
                    DrawCollectionModal();
                    break;
                case MetaModal.Shop:
                    DrawShopModal();
                    break;
                case MetaModal.Rank:
                    DrawRankModal();
                    break;
                case MetaModal.Settings:
                    DrawSettingsModal();
                    break;
                case MetaModal.StaminaRefill:
                    DrawStaminaRefillModal();
                    break;
            }
        }

        private void DrawCollectionModal()
        {
            GUI.Label(new Rect(60, 140, 500, 50), "📖 도감 (Collection)", _titleStyle);

            if (DrawResponsiveButton(new Rect(50, 200, 190, 55), "👤 캐릭터", collectionTabIndex == 0 ? _tabActiveStyle : _tabInactiveStyle))
            {
                collectionTabIndex = 0;
            }
            if (DrawResponsiveButton(new Rect(250, 200, 190, 55), "🪨 조약돌", collectionTabIndex == 1 ? _tabActiveStyle : _tabInactiveStyle))
            {
                collectionTabIndex = 1;
            }
            if (DrawResponsiveButton(new Rect(450, 200, 220, 55), "🐠 수족관(어항)", collectionTabIndex == 2 ? _tabActiveStyle : _tabInactiveStyle))
            {
                collectionTabIndex = 2;
            }

            if (collectionTabIndex == 0)
            {
                GUI.Label(new Rect(60, 280, 600, 40), "보유 캐릭터 목록:", _labelStyle);
                var dm = GameDataManager.Instance;
                if (dm != null)
                {
                    int y = 330;
                    foreach (var c in dm.characterCatalog)
                    {
                        GUI.Box(new Rect(60, y, 600, 110), $"<b>{c.name} ({c.title})</b>\n{c.description}\n보너스: 파워 +{c.powerBonus:P0} / 각도 +{c.angleAssist:F1}°", _cardBoxStyle);
                        y += 125;
                    }
                }
            }
            else if (collectionTabIndex == 1)
            {
                GUI.Label(new Rect(60, 280, 600, 40), "보유 조약돌 목록:", _labelStyle);
                if (StoneInventory.Instance != null)
                {
                    int y = 330;
                    foreach (var s in StoneInventory.Instance.stones)
                    {
                        GUI.Box(new Rect(60, y, 600, 110), $"<b>{s.name}</b>\n{s.description}\n탄성 계수: x{s.bounceMultiplier:F2} | 전진력: x{s.forwardPowerMultiplier:F2}", _cardBoxStyle);
                        y += 125;
                    }
                }
            }
            else if (collectionTabIndex == 2)
            {
                GUI.Label(new Rect(60, 280, 600, 40), "🐠 수족관 어종 디오라마 & 방치 보상", _labelStyle);
                if (AquariumManager.Instance != null)
                {
                    int y = 330;
                    foreach (var f in AquariumManager.Instance.fishSpeciesList)
                    {
                        GUI.Box(new Rect(60, y, 600, 100), $"{f.icon} <b>{f.name}</b> (포획 수: {f.caughtCount}회)\n{f.description}", _cardBoxStyle);
                        y += 115;
                    }
                }

                if (DrawResponsiveButton(new Rect(100, 980, 520, 70), "🪙 수족관 방치 코인 모두 받기 (+350 G)", _primaryBtnStyle))
                {
                    if (GameDataManager.Instance != null)
                    {
                        GameDataManager.Instance.AddCurrency(350, 0);
                        CloseModal();
                    }
                }
            }
        }

        private void DrawShopModal()
        {
            GUI.Label(new Rect(60, 150, 500, 50), "🛒 상점 (Shop)", _titleStyle);

            GUI.Box(new Rect(60, 240, 600, 180), "✨ <b>신비한 조약돌 가챠 (1회 뽑기)</b>\n골드 500 소모하여 랜덤 희귀/전설 돌 획득!", _cardBoxStyle);
            if (DrawResponsiveButton(new Rect(420, 340, 220, 60), "🪙 500 뽑기", _primaryBtnStyle))
            {
                if (GameDataManager.Instance != null && GameDataManager.Instance.UserData.gold >= 500)
                {
                    GameDataManager.Instance.UserData.gold -= 500;
                    GameDataManager.Instance.SaveUserData();
                }
            }

            GUI.Box(new Rect(60, 450, 600, 180), "⚡ <b>스태미나 풀 충전 (+10 ⚡)</b>\n다이아 20개로 즉시 완전 충전", _cardBoxStyle);
            if (DrawResponsiveButton(new Rect(420, 550, 220, 60), "💎 20 충전", _primaryBtnStyle))
            {
                if (GameDataManager.Instance != null && GameDataManager.Instance.UserData.diamonds >= 20)
                {
                    GameDataManager.Instance.UserData.diamonds -= 20;
                    GameDataManager.Instance.AddStamina(10);
                }
            }
        }

        private void DrawRankModal()
        {
            GUI.Label(new Rect(60, 150, 500, 50), "🏆 카카오 주간 랭킹", _titleStyle);
            GUI.Box(new Rect(60, 230, 600, 700), "1위 🥇 카카오 달인 (1,480.2m)\n2위 🥈 강변 물수제비 (1,230.5m)\n3위 🥉 조약돌 챔프 (980.0m)\n\n내 순위: 14위 (428.5m)", _cardBoxStyle);
        }

        private void DrawSettingsModal()
        {
            GUI.Label(new Rect(60, 150, 500, 50), "⚙️ 게임 설정", _titleStyle);
            var dm = GameDataManager.Instance;
            string accStatus = (dm != null && dm.UserData.hasKakaoAccount) ? $"카카오 연동됨 ({dm.UserData.kakaoNickname})" : "게스트 계정";
            GUI.Box(new Rect(60, 230, 600, 550), $"BGM 사운드: ON\nSFX 효과음: ON\n햅틱 진동: ON\n\n현재 계정: {accStatus}", _cardBoxStyle);

            if (dm != null && dm.UserData.hasKakaoAccount)
            {
                if (DrawResponsiveButton(new Rect(100, 800, 520, 70), "🚪 카카오 로그아웃", _glassBtnStyle))
                {
                    dm.UserData.hasKakaoAccount = false;
                    dm.UserData.authProvider = "Guest";
                    dm.SaveUserData();
                    CloseModal();
                    ShowScreen(MetaScreen.Title);
                }
            }
        }

        private void DrawStaminaRefillModal()
        {
            GUI.Label(new Rect(60, 150, 500, 50), "⚡ 스태미나 부족", _titleStyle);
            GUI.Label(new Rect(80, 260, 560, 80), "게임 시작에 필요한 번개(⚡)가 부족합니다!\n충전 방법을 선택해 주세요.", _labelStyle);

            if (DrawResponsiveButton(new Rect(80, 400, 560, 80), "📺 광고 보고 번개 2개 충전 (+2 ⚡)", _primaryBtnStyle))
            {
                if (GameDataManager.Instance != null)
                {
                    GameDataManager.Instance.AddStamina(2);
                    CloseModal();
                }
            }

            if (DrawResponsiveButton(new Rect(80, 510, 560, 80), "💎 다이아 20개로 풀 충전 (+10 ⚡)", _glassBtnStyle))
            {
                if (GameDataManager.Instance != null && GameDataManager.Instance.UserData.diamonds >= 20)
                {
                    GameDataManager.Instance.UserData.diamonds -= 20;
                    GameDataManager.Instance.AddStamina(10);
                    CloseModal();
                }
            }
        }
        #endregion
    }
}
