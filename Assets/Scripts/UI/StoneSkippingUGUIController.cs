using System;
using UnityEngine;
using UnityEngine.UI;

public class StoneSkippingUGUIController : MonoBehaviour
{
    public static StoneSkippingUGUIController Instance { get; private set; }

    [Header("핵심 컨트롤러")]
    public GameController gameController;

    [Header("1. 상단바 (TopBar)")]
    [SerializeField] private GameObject topBarObj;
    [SerializeField] private Text coinText;
    [SerializeField] private Text progressText;
    [SerializeField] private Button coinBoxBtn;
    [SerializeField] private Button bookBtn;
    [SerializeField] private Button skinBtn;

    [Header("2. 모드 선택창 (ModeSelect)")]
    [SerializeField] private GameObject modeSelectObj;
    [SerializeField] private Button longDistanceBtn;
    [SerializeField] private Button targetAccuracyBtn;

    [Header("3. 위치 선정 (Positioning)")]
    [SerializeField] private GameObject positioningObj;
    [SerializeField] private Text positioningGuideText;
    [SerializeField] private Text waypointBadgeText;
    [SerializeField] private Button confirmPositionBtn;

    [Header("4. 1단계 각도 조준 (AimingAngle)")]
    [SerializeField] private GameObject aimingObj;
    [SerializeField] private RectTransform aimNeedleRect;
    [SerializeField] private float aimBarWidth = 560f;
    [SerializeField] private Button confirmAngleBtn;

    [Header("5. 2단계 파워 충전 (ChargingPower)")]
    [SerializeField] private GameObject chargingObj;
    [SerializeField] private Image powerFillImage;
    [SerializeField] private Text powerPercentText;
    [SerializeField] private Button launchBtn;

    [Header("6. 비행 HUD (FlightHUD)")]
    [SerializeField] private GameObject flightHudObj;
    [SerializeField] private Text flightDistanceText;
    [SerializeField] private Text flightSkipText;
    [SerializeField] private Text flightTimingText;
    [SerializeField] private GameObject flightTouchButtonsContainer;
    [SerializeField] private Image flightTouchBtnLeft;
    [SerializeField] private Image flightTouchBtnCenter;
    [SerializeField] private Image flightTouchBtnRight;

    public void TriggerButtonVisualFeedback(float steerAngle)
    {
        if (steerAngle < 0f && flightTouchBtnLeft != null)
        {
            var h = flightTouchBtnLeft.GetComponent<SkippingStones.UI.FlightTouchButtonHandler>();
            if (h != null) h.TriggerVisualFeedback();
        }
        else if (steerAngle > 0f && flightTouchBtnRight != null)
        {
            var h = flightTouchBtnRight.GetComponent<SkippingStones.UI.FlightTouchButtonHandler>();
            if (h != null) h.TriggerVisualFeedback();
        }
        else if (Mathf.Approximately(steerAngle, 0f) && flightTouchBtnCenter != null)
        {
            var h = flightTouchBtnCenter.GetComponent<SkippingStones.UI.FlightTouchButtonHandler>();
            if (h != null) h.TriggerVisualFeedback();
        }
    }

    [Header("7. 리플레이 (Replay)")]
    [SerializeField] private GameObject replayObj;
    [SerializeField] private Text replayTitleText;
    [SerializeField] private Text replaySummaryText;
    [UnityEngine.Serialization.FormerlySerializedAs("replayRetryBtn")]
    [SerializeField] private Button replayReDrawBtn;
    [SerializeField] private Button replayResultBtn;

    [Header("8. 결과 모달 (Result)")]
    [SerializeField] private GameObject resultObj;
    [SerializeField] private Text resultReasonText;
    [SerializeField] private Text resultDistanceScoreText;
    [SerializeField] private Text resultSkipScoreText;
    [SerializeField] private Text resultSpecialScoreText;
    [SerializeField] private Text resultTotalScoreText;
    [SerializeField] private Text resultCoinText;
    [SerializeField] private Button resultReplayBtn;
    [SerializeField] private Button resultRetryBtn;
    [UnityEngine.Serialization.FormerlySerializedAs("resultLobbyBtn")]
    [SerializeField] private Button resultDoneBtn;

    [Header("9. 서브 모달 (수족관 & 스킨)")]
    [SerializeField] private GameObject aquariumModalObj;
    [SerializeField] private Button aquariumCloseBtn;
    [SerializeField] private GameObject stoneSelectorModalObj;
    [SerializeField] private Button stoneSelectorCloseBtn;

    [Header("10. 🛠️ 개발자 테스트 메뉴 (Developer Menu)")]
    [SerializeField] private GameObject devTestMenuObj;
    [SerializeField] private Button devDayBtn;       // 낮 (0m)
    [SerializeField] private Button devSunset1Btn;   // 노을 (2,000m)
    [SerializeField] private Button devSunset2Btn;   // 석양 (3,600m)
    [SerializeField] private Button devNightBtn;     // 밤 (4,800m)
    [SerializeField] private Button devGodModeBtn;   // 갓모드 자동 비행
    [SerializeField] private Text devGodModeBtnText; // 갓모드 버튼 텍스트
    [SerializeField] private Button devCloseBtn;     // 닫기

    [Header("11. 알림 배너 (Notification)")]
    [SerializeField] private GameObject notificationBannerObj;
    [SerializeField] private Text notificationBannerText;

    private float lastTransitionTime = 0f;
    private const float DEBOUNCE_COOLDOWN = 0.10f;
    private int goldTapCount = 0;
    private float lastGoldTapTime = 0f;

    // 실시간 레이아웃 및 Safe Area 동기화 필드
    private Rect lastSafeArea;
    private int lastScreenWidth;
    private int lastScreenHeight;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

#if ENABLE_INPUT_SYSTEM
        // New Input System UI Input Module 자동 복구 및 마우스/터치 기본 액션 바인딩
        var inputModule = FindAnyObjectByType<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
        if (inputModule != null)
        {
            if (inputModule.actionsAsset == null)
            {
                inputModule.AssignDefaultActions();
            }
        }
        else
        {
            var eventSystem = FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>();
            if (eventSystem != null)
            {
                var mod = eventSystem.gameObject.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
                mod.AssignDefaultActions();
            }
        }
#endif

        if (gameController == null)
            gameController = FindAnyObjectByType<GameController>();

        BindButtonEvents();
    }

    private void Start()
    {
        InitializePanelStates();
    }

    private void BindButtonEvents()
    {
        // 상단바
        if (coinBoxBtn != null) coinBoxBtn.onClick.AddListener(OnCoinBoxTapped);
        if (bookBtn != null) bookBtn.onClick.AddListener(() =>
        {
            if (CanInteract() && gameController != null)
            {
                gameController.showAquariumModal = !gameController.showAquariumModal;
                gameController.showStoneSelectorModal = false;
            }
        });
        if (skinBtn != null) skinBtn.onClick.AddListener(() =>
        {
            if (CanInteract() && gameController != null)
            {
                gameController.showStoneSelectorModal = !gameController.showStoneSelectorModal;
                gameController.showAquariumModal = false;
            }
        });

        // 모달 닫기
        if (aquariumCloseBtn != null) aquariumCloseBtn.onClick.AddListener(() =>
        {
            if (CanInteract() && gameController != null) gameController.showAquariumModal = false;
        });
        if (stoneSelectorCloseBtn != null) stoneSelectorCloseBtn.onClick.AddListener(() =>
        {
            if (CanInteract() && gameController != null) gameController.showStoneSelectorModal = false;
        });

        // 모드 선택
        if (longDistanceBtn != null) longDistanceBtn.onClick.AddListener(() =>
        {
            if (CanInteract() && gameController != null)
            {
                gameController.SelectGameMode(GameController.GameMode.LongDistance);
                gameController.currentState = GameController.GameState.Positioning;
            }
        });
        if (targetAccuracyBtn != null) targetAccuracyBtn.onClick.AddListener(() =>
        {
            if (CanInteract() && gameController != null)
            {
                gameController.SelectGameMode(GameController.GameMode.TargetAccuracy);
                gameController.currentState = GameController.GameState.Positioning;
            }
        });

        // 조작 단계
        if (confirmPositionBtn != null) confirmPositionBtn.onClick.AddListener(() =>
        {
            if (CanInteract() && gameController != null) gameController.ConfirmPosition();
        });
        if (confirmAngleBtn != null) confirmAngleBtn.onClick.AddListener(() =>
        {
            if (CanInteract() && gameController != null) gameController.ConfirmAngle();
        });
        if (launchBtn != null) launchBtn.onClick.AddListener(() =>
        {
            if (CanInteract() && gameController != null) gameController.LaunchStone();
        });

        // 7. 리플레이 패널
        if (replayReDrawBtn == null && replayObj != null)
        {
            var btns = replayObj.GetComponentsInChildren<Button>(true);
            foreach (var b in btns)
            {
                if (b.name.IndexOf("ReDraw", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    b.name.IndexOf("Retry", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    b.name.IndexOf("다시", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    replayReDrawBtn = b;
                    break;
                }
            }
        }
        if (replayReDrawBtn != null)
        {
            replayReDrawBtn.onClick.RemoveAllListeners();
            replayReDrawBtn.onClick.AddListener(() =>
            {
                if (CanInteract() && gameController != null && gameController.topDownReplay != null)
                {
                    gameController.topDownReplay.ReplayAgain();
                }
            });
        }

        if (replayResultBtn == null && replayObj != null)
        {
            var btns = replayObj.GetComponentsInChildren<Button>(true);
            foreach (var b in btns)
            {
                if (b.name.IndexOf("Result", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    b.name.IndexOf("결과", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    replayResultBtn = b;
                    break;
                }
            }
        }
        if (replayResultBtn != null)
        {
            replayResultBtn.onClick.RemoveAllListeners();
            replayResultBtn.onClick.AddListener(() =>
            {
                if (CanInteract() && gameController != null)
                {
                    if (gameController.topDownReplay != null)
                        gameController.topDownReplay.FinishReplayAndShowResult();
                    else
                        gameController.ShowFinalResultDirect(gameController.stone != null ? gameController.stone.totalDistance : 0f);
                }
            });
        }

        // 8. 결과 화면
        if (resultReplayBtn == null && resultObj != null)
        {
            var btns = resultObj.GetComponentsInChildren<Button>(true);
            foreach (var b in btns)
            {
                if (b.name.IndexOf("Replay", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    b.name.IndexOf("리플레이", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    resultReplayBtn = b;
                    break;
                }
            }
        }
        if (resultReplayBtn != null)
        {
            resultReplayBtn.onClick.RemoveAllListeners();
            resultReplayBtn.onClick.AddListener(() =>
            {
                if (CanInteract() && gameController != null)
                {
                    float dist = gameController.stone != null ? gameController.stone.totalDistance : (gameController.distanceScore / 10f);
                    gameController.currentState = GameController.GameState.Replay;
                    if (gameController.topDownReplay == null)
                    {
                        gameController.topDownReplay = FindAnyObjectByType<TopDownReplayManager>() ?? gameController.GetComponent<TopDownReplayManager>() ?? gameController.gameObject.AddComponent<TopDownReplayManager>();
                    }
                    if (gameController.topDownReplay != null)
                    {
                        gameController.topDownReplay.StartReplay(dist);
                    }
                }
            });
        }
        if (resultRetryBtn != null)
        {
            resultRetryBtn.onClick.RemoveAllListeners();
            resultRetryBtn.onClick.AddListener(() =>
            {
                if (CanInteract() && gameController != null) gameController.RestartGame();
            });
        }
        if (resultDoneBtn == null && resultObj != null)
        {
            var btns = resultObj.GetComponentsInChildren<Button>(true);
            foreach (var b in btns)
            {
                if (b.name.IndexOf("Done", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    b.name.IndexOf("Finish", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    b.name.IndexOf("완료", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    b.name.IndexOf("Lobby", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    resultDoneBtn = b;
                    break;
                }
            }
        }
        if (resultDoneBtn != null)
        {
            resultDoneBtn.onClick.RemoveAllListeners();
            resultDoneBtn.onClick.AddListener(() =>
            {
                if (CanInteract())
                {
                    if (gameController != null) gameController.FinishMatchAndReturnToMapSelect();
                    if (SkippingStones.UI.MetaUIManager.Instance != null)
                    {
                        SkippingStones.UI.MetaUIManager.Instance.ShowScreen(SkippingStones.UI.MetaScreen.MapSelect);
                    }
                }
            });
        }

        // 🛠️ 개발자 메뉴 버튼 바인딩
        if (devDayBtn != null) devDayBtn.onClick.AddListener(() =>
        {
            if (EnvironmentTestHelper.Instance != null) EnvironmentTestHelper.Instance.SetPreviewDistance(0f);
        });
        if (devSunset1Btn != null) devSunset1Btn.onClick.AddListener(() =>
        {
            if (EnvironmentTestHelper.Instance != null) EnvironmentTestHelper.Instance.SetPreviewDistance(2000f);
        });
        if (devSunset2Btn != null) devSunset2Btn.onClick.AddListener(() =>
        {
            if (EnvironmentTestHelper.Instance != null) EnvironmentTestHelper.Instance.SetPreviewDistance(3600f);
        });
        if (devNightBtn != null) devNightBtn.onClick.AddListener(() =>
        {
            if (EnvironmentTestHelper.Instance != null) EnvironmentTestHelper.Instance.SetPreviewDistance(4800f);
        });
        if (devGodModeBtn != null) devGodModeBtn.onClick.AddListener(() =>
        {
            if (EnvironmentTestHelper.Instance != null)
            {
                EnvironmentTestHelper.Instance.ToggleAutoFlyGodMode();
            }
        });
        // 🎮 6. 비행 HUD 하단 3버튼 바인딩 (FlightHUD 하위 자동 탐색 및 생성 보완)
        SetupFlightTouchButtons();
    }

    private void SetupFlightTouchButtons()
    {
        if (flightHudObj == null) return;

        Transform safeContainer = transform.Find("StoneSkipping_uGUICanvas/SafeContainer") ?? transform;

        // 컨테이너 탐색/생성
        if (flightTouchButtonsContainer == null)
        {
            Transform existing = safeContainer.Find("FlightTouchButtons_Container");
            if (existing != null) flightTouchButtonsContainer = existing.gameObject;
        }

        if (flightTouchButtonsContainer == null)
        {
            flightTouchButtonsContainer = new GameObject("FlightTouchButtons_Container", typeof(RectTransform));
            flightTouchButtonsContainer.transform.SetParent(safeContainer, false);

            RectTransform rootRt = flightTouchButtonsContainer.GetComponent<RectTransform>();
            rootRt.anchorMin = new Vector2(0.5f, 0f);
            rootRt.anchorMax = new Vector2(0.5f, 0f);
            rootRt.pivot = new Vector2(0.5f, 0f);
            rootRt.anchoredPosition = new Vector2(0f, 60f);
            rootRt.sizeDelta = new Vector2(560f, 120f);
        }
        else
        {
            // 부모가 SafeContainer가 아니라면 이동
            if (flightTouchButtonsContainer.transform.parent != safeContainer)
            {
                flightTouchButtonsContainer.transform.SetParent(safeContainer, false);
            }
        }

        // 스프라이트 로드
        Sprite spL = Resources.Load<Sprite>("Touch_Button_L");
        Sprite spO = Resources.Load<Sprite>("Touch_Button_O");
        Sprite spR = Resources.Load<Sprite>("Touch_Button_R");

#if UNITY_EDITOR
        if (spL == null) spL = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/2D/UI/Touch_Button_L.png");
        if (spO == null) spO = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/2D/UI/Touch_Button_O.png");
        if (spR == null) spR = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/2D/UI/Touch_Button_R.png");
#endif

        Color normalTint = new Color(0.9f, 0.95f, 1.0f, 0.75f);
        Color pressedTint = new Color(0.4f, 0.85f, 1.0f, 1.0f);

        // 1. 좌측 버튼
        if (flightTouchBtnLeft == null)
        {
            flightTouchBtnLeft = CreateOrGetTouchButton(flightTouchButtonsContainer.transform, "Btn_Left", new Vector2(-190f, 0f), spL, -5f, -8f, normalTint, pressedTint);
        }
        // 2. 중앙 버튼
        if (flightTouchBtnCenter == null)
        {
            flightTouchBtnCenter = CreateOrGetTouchButton(flightTouchButtonsContainer.transform, "Btn_Center", new Vector2(0f, 0f), spO, 0f, 0f, normalTint, pressedTint);
        }
        // 3. 우측 버튼
        if (flightTouchBtnRight == null)
        {
            flightTouchBtnRight = CreateOrGetTouchButton(flightTouchButtonsContainer.transform, "Btn_Right", new Vector2(190f, 0f), spR, 5f, 8f, normalTint, pressedTint);
        }
    }

    private Image CreateOrGetTouchButton(Transform parent, string name, Vector2 pos, Sprite sprite, float baseAngle, float swipeAngle, Color normalColor, Color pressedColor)
    {
        Transform existing = parent.Find(name);
        GameObject btnObj = (existing != null) ? existing.gameObject : new GameObject(name);
        btnObj.transform.SetParent(parent, false);

        RectTransform rt = btnObj.GetComponent<RectTransform>() ?? btnObj.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = new Vector2(120f, 120f);

        Image img = btnObj.GetComponent<Image>() ?? btnObj.AddComponent<Image>();
        if (sprite != null)
        {
            img.sprite = sprite;
            img.type = Image.Type.Simple;
            img.preserveAspect = true;
        }

        var handler = btnObj.GetComponent<SkippingStones.UI.FlightTouchButtonHandler>() ?? btnObj.AddComponent<SkippingStones.UI.FlightTouchButtonHandler>();
        handler.Init(null, baseAngle, swipeAngle, img, normalColor, pressedColor);

        return img;
    }

    private void InitializePanelStates()
    {
        if (positioningObj != null) positioningObj.SetActive(false);
        if (aimingObj != null) aimingObj.SetActive(false);
        if (chargingObj != null) chargingObj.SetActive(false);
        if (flightHudObj != null) flightHudObj.SetActive(false);
        if (flightTouchButtonsContainer != null) flightTouchButtonsContainer.SetActive(false);
        if (replayObj != null) replayObj.SetActive(false);
        if (resultObj != null) resultObj.SetActive(false);
        if (aquariumModalObj != null) aquariumModalObj.SetActive(false);
        if (stoneSelectorModalObj != null) stoneSelectorModalObj.SetActive(false);
        if (devTestMenuObj != null) devTestMenuObj.SetActive(false);
        if (notificationBannerObj != null) notificationBannerObj.SetActive(false);

        if (topBarObj != null) topBarObj.SetActive(true);
        if (modeSelectObj != null) modeSelectObj.SetActive(true);
    }

    private void Update()
    {
        if (gameController == null)
            gameController = FindAnyObjectByType<GameController>();
        if (gameController == null) return;

        // 실시간 Safe Area 및 종횡비 감지 후 레이아웃 자동 동기화
        if (Screen.safeArea != lastSafeArea || Screen.width != lastScreenWidth || Screen.height != lastScreenHeight)
        { 
            lastSafeArea = Screen.safeArea;
            lastScreenWidth = Screen.width;
            lastScreenHeight = Screen.height;
            ApplyLayouts();
        }

        bool isHeld = false;
#if ENABLE_INPUT_SYSTEM
        if (UnityEngine.InputSystem.Touchscreen.current != null &&
            UnityEngine.InputSystem.Touchscreen.current.primaryTouch.press.isPressed)
        {
            isHeld = true;
        }
        else if (UnityEngine.InputSystem.Mouse.current != null &&
                 UnityEngine.InputSystem.Mouse.current.leftButton.isPressed)
        {
            isHeld = true;
        }
#else
        try
        {
            isHeld = Input.touchCount > 0 || Input.GetMouseButton(0);
        }
        catch { }
#endif

        if (!isHeld && gameController.requireTouchRelease)
        {
            gameController.requireTouchRelease = false;
        }

        UpdateUIVisibilityByState();
        UpdateDynamicTexts();
    }

    private void UpdateUIVisibilityByState()
    {
        if (gameController == null) return;

        // 🌟 MetaUIManager가 메타 화면(타이틀, 로비, 맵선택, 결과) 제어 중일 때의 인게임 UGUI 처리
        if (SkippingStones.UI.MetaUIManager.Instance != null &&
            SkippingStones.UI.MetaUIManager.Instance.gameObject.activeInHierarchy &&
            SkippingStones.UI.MetaUIManager.Instance.currentScreen != SkippingStones.UI.MetaScreen.InGame)
        {
            if (topBarObj != null) topBarObj.SetActive(false);
            if (modeSelectObj != null) modeSelectObj.SetActive(false);
            if (positioningObj != null) positioningObj.SetActive(false);
            if (aimingObj != null) aimingObj.SetActive(false);
            if (chargingObj != null) chargingObj.SetActive(false);
            if (flightHudObj != null) flightHudObj.SetActive(false);
            if (flightTouchButtonsContainer != null) flightTouchButtonsContainer.SetActive(false);
            if (replayObj != null) replayObj.SetActive(false);
            if (resultObj != null) resultObj.SetActive(false);
            if (aquariumModalObj != null) aquariumModalObj.SetActive(false);
            if (stoneSelectorModalObj != null) stoneSelectorModalObj.SetActive(false);

            // 🛠️ 개발자 메뉴: F1 토글 시 로비 화면에서도 안전하게 열고 닫기 지원
            bool showDevInMeta = (EnvironmentTestHelper.Instance != null && EnvironmentTestHelper.Instance.showTestUI);
            if (devTestMenuObj != null && devTestMenuObj.activeSelf != showDevInMeta)
            {
                devTestMenuObj.SetActive(showDevInMeta);
            }
            if (devGodModeBtnText != null && EnvironmentTestHelper.Instance != null)
            {
                devGodModeBtnText.text = EnvironmentTestHelper.Instance.isAutoFlying ? "⏹️ 비행 중지" : "▶️ 갓모드 자동 비행";
            }
            return;
        }

        var state = gameController.currentState;
        // 🌟 레거시 모드선택 및 구형 상단바는 MetaUIManager가 전담하므로 영구 비활성화
        if (topBarObj != null)
            topBarObj.SetActive(false);
        if (modeSelectObj != null)
            modeSelectObj.SetActive(false);
        if (positioningObj != null)
            positioningObj.SetActive(state == GameController.GameState.Positioning);
        if (aimingObj != null)
            aimingObj.SetActive(state == GameController.GameState.AimingAngle);
        if (chargingObj != null)
            chargingObj.SetActive(state == GameController.GameState.ChargingPower);
        if (flightHudObj != null)
            flightHudObj.SetActive(state == GameController.GameState.Flying);
        if (flightTouchButtonsContainer != null)
            flightTouchButtonsContainer.SetActive(state == GameController.GameState.Flying);
        if (replayObj != null)
            replayObj.SetActive(state == GameController.GameState.Replay);
        if (resultObj != null)
            resultObj.SetActive(state == GameController.GameState.Result);

        // 모달 팝업 상태
        if (aquariumModalObj != null)
            aquariumModalObj.SetActive(gameController.showAquariumModal);
        if (stoneSelectorModalObj != null)
            stoneSelectorModalObj.SetActive(gameController.showStoneSelectorModal);

        // 🛠️ 개발자 메뉴 표시 여부
        bool showDev = (EnvironmentTestHelper.Instance != null && EnvironmentTestHelper.Instance.showTestUI);
        if (devTestMenuObj != null && devTestMenuObj.activeSelf != showDev)
        {
            devTestMenuObj.SetActive(showDev);
        }
        if (devGodModeBtnText != null && EnvironmentTestHelper.Instance != null)
        {
            devGodModeBtnText.text = EnvironmentTestHelper.Instance.isAutoFlying ? "⏹️ 비행 중지" : "▶️ 갓모드 자동 비행";
        }

        // 상단 알림 배너: 비행 중(Flying)에만 표시하고 결과/리플레이/대기 시에는 즉시 완전 숨김
        bool showBanner = !string.IsNullOrEmpty(gameController.bannerNotificationText) && (state == GameController.GameState.Flying);
        if (notificationBannerObj != null && notificationBannerObj.activeSelf != showBanner)
        {
            notificationBannerObj.SetActive(showBanner);
        }
        if (showBanner && notificationBannerText != null)
        {
            notificationBannerText.text = gameController.bannerNotificationText;
        }
    }

    private void UpdateDynamicTexts()
    {
        if (AquariumManager.Instance != null)
        {
            if (coinText != null) coinText.text = $"[C] {AquariumManager.Instance.totalCoins:N0}";
            if (progressText != null) progressText.text = $"도감:{AquariumManager.Instance.GetCompletionPercentage():F0}%";
        }

        if (positioningObj != null && positioningObj.activeSelf && gameController.character != null)
        {
            if (gameController.currentMode == GameController.GameMode.TargetAccuracy && waypointBadgeText != null)
            {
                int curIdx = gameController.character.GetCurrentWaypointIndex();
                int totalCount = gameController.character.GetTotalWaypointsCount();
                waypointBadgeText.text = $"선택: PP{curIdx + 1:02d} / PP{totalCount:02d}";
            }
        }

        if (aimingObj != null && aimingObj.activeSelf && aimNeedleRect != null)
        {
            float posX = gameController.aimGaugeValue * (aimBarWidth * 0.5f);
            aimNeedleRect.anchoredPosition = new Vector2(posX, aimNeedleRect.anchoredPosition.y);
        }

        if (chargingObj != null && chargingObj.activeSelf)
        {
            if (powerFillImage != null) powerFillImage.fillAmount = gameController.powerGaugeValue;
            if (powerPercentText != null) powerPercentText.text = $"[POWER] {(int)(gameController.powerGaugeValue * 100)}%";
        }

        if (flightHudObj != null && flightHudObj.activeSelf)
        {
            float dist = 0f;
            int skips = 0;
            if (gameController.currentMode == GameController.GameMode.RhythmArcade)
            {
                var arcade = FindAnyObjectByType<SkippingStones.Arcade.ArcadeSkippingStone>();
                if (arcade != null)
                {
                    dist = arcade.totalDistance;
                    skips = arcade.skipCount;
                }
            }
            else if (gameController.stone != null)
            {
                dist = gameController.stone.totalDistance;
                skips = gameController.stone.skipCount;
            }

            if (flightDistanceText != null) flightDistanceText.text = $"[거리] {dist:F1} m";
            if (flightSkipText != null) flightSkipText.text = $"[바운스] {skips}회";
            if (flightTimingText != null) flightTimingText.text = gameController.lastTimingText;
        }

        if (resultObj != null && resultObj.activeSelf)
        {
            float finalDist = 0f;
            int finalSkips = 0;
            if (gameController.currentMode == GameController.GameMode.RhythmArcade)
            {
                var arcade = FindAnyObjectByType<SkippingStones.Arcade.ArcadeSkippingStone>();
                if (arcade != null)
                {
                    finalDist = arcade.totalDistance;
                    finalSkips = arcade.skipCount;
                }
            }
            else if (gameController.stone != null)
            {
                finalDist = gameController.stone.totalDistance;
                finalSkips = gameController.stone.skipCount;
            }

            if (resultReasonText != null) resultReasonText.text = gameController.lastTimingText;
            if (resultDistanceScoreText != null)
                resultDistanceScoreText.text = $"1. 도달 거리 ({finalDist:F1} m)  +{gameController.distanceScore:N0} 점";
            if (resultSkipScoreText != null)
                resultSkipScoreText.text = $"2. 튕긴 횟수 ({finalSkips} 회)  +{gameController.skipScore:N0} 점";
            if (resultSpecialScoreText != null)
            {
                string skimInfo = (gameController.lastSkimBonusDist > 0.1f) ? $" 도로록:+{gameController.lastSkimBonusDist:F1}m" : "";
                resultSpecialScoreText.text = $"3. 보너스 [P:{gameController.perfectTimingCount} 저격:{gameController.fishSnipeCount}{skimInfo}]  +{gameController.specialScore:N0} 점";
            }
            if (resultTotalScoreText != null)
                resultTotalScoreText.text = $"최종 점수 : {gameController.totalScore:N0} PTS";
            if (resultCoinText != null)
                resultCoinText.text = $"[보상]: +{gameController.earnedCoins:N0} COIN 획득!";
        }

        // 🛠️ 갓모드 버튼 라벨 실시간 갱신
        if (devTestMenuObj != null && devTestMenuObj.activeSelf && devGodModeBtnText != null && EnvironmentTestHelper.Instance != null)
        {
            bool isFlying = EnvironmentTestHelper.Instance.isAutoFlying;
            devGodModeBtnText.text = isFlying ? "🚀 갓모드 비행 중... [중지하기]" : "🚀 3,500m 갓모드 자동 비행 감상 (God Mode)";
        }
    }

    private void OnCoinBoxTapped()
    {
        if (Time.unscaledTime - lastGoldTapTime > 2.5f) goldTapCount = 0;
        lastGoldTapTime = Time.unscaledTime;
        goldTapCount++;

        if (goldTapCount >= 5)
        {
            goldTapCount = 0;
            if (EnvironmentTestHelper.Instance != null)
            {
                EnvironmentTestHelper.Instance.showTestUI = true;
            }
        }
    }

    private void ApplyLayouts()
    {
        // 1. 탑바 Safe Area 적용 (최상단 고정 및 노치 회피)
        if (topBarObj != null)
        {
            RectTransform topBarRt = topBarObj.GetComponent<RectTransform>();
            if (topBarRt != null)
            {
                Rect safeArea = Screen.safeArea;
                
                // 스크린 높이 대비 안전 영역 상단(yMax)의 비율 계산
                float safeTopRatio = safeArea.yMax / Screen.height;
                
                // TopBar의 anchors를 사단 노치 밑에 딱 고정
                topBarRt.anchorMin = new Vector2(topBarRt.anchorMin.x, safeTopRatio);
                topBarRt.anchorMax = new Vector2(topBarRt.anchorMax.x, safeTopRatio);
                topBarRt.pivot = new Vector2(topBarRt.pivot.x, 1.0f); // 피벗 상단 고정
                topBarRt.anchoredPosition = new Vector2(topBarRt.anchoredPosition.x, 0f); // 오프셋 0
            }
        }

        // 2. 개발자 메뉴 위치 계산 (탑바 밑에 일정한 간격 유지)
        if (topBarObj != null && devTestMenuObj != null)
        {
            RectTransform topBarRt = topBarObj.GetComponent<RectTransform>();
            RectTransform devRt = devTestMenuObj.GetComponent<RectTransform>();
            if (topBarRt != null && devRt != null)
            {
                float topBarHeight = topBarRt.rect.height;
                
                // 개발자 메뉴의 anchors를 탑바의 anchor와 일치시킴
                devRt.anchorMin = new Vector2(devRt.anchorMin.x, topBarRt.anchorMin.y);
                devRt.anchorMax = new Vector2(devRt.anchorMax.x, topBarRt.anchorMax.y);
                devRt.pivot = new Vector2(devRt.pivot.x, 1.0f); // 피벗 상단 고정
                
                // 탑바 높이 + 간격(예: 15f) 만큼 내려서 배치
                float spacing = 15f;
                devRt.anchoredPosition = new Vector2(devRt.anchoredPosition.x, -(topBarHeight + spacing));
            }
        }

        // 3. 🌟 비행 HUD (거리/바운스 정보) 배경 제거 및 수면선 아래로 하향 배치
        if (flightHudObj != null)
        {
            var hudImg = flightHudObj.GetComponent<Image>();
            if (hudImg != null) hudImg.enabled = false; // 답답한 배경 박스 제거

            RectTransform hudRt = flightHudObj.GetComponent<RectTransform>();
            if (hudRt != null)
            {
                hudRt.anchorMin = new Vector2(0.5f, 1f);
                hudRt.anchorMax = new Vector2(0.5f, 1f);
                hudRt.pivot = new Vector2(0.5f, 1f);
                hudRt.anchoredPosition = new Vector2(0f, -170f); // 수면선 밑으로 적절히 하향
            }
        }

        // 4. 🌟 추월 알림 배너 배경 제거 및 거리 HUD 하단 배치
        if (notificationBannerObj != null)
        {
            var bannerImg = notificationBannerObj.GetComponent<Image>();
            if (bannerImg != null) bannerImg.enabled = false; // 노란색 배경 박스 제거

            RectTransform bannerRt = notificationBannerObj.GetComponent<RectTransform>();
            if (bannerRt != null)
            {
                bannerRt.anchorMin = new Vector2(0.5f, 1f);
                bannerRt.anchorMax = new Vector2(0.5f, 1f);
                bannerRt.pivot = new Vector2(0.5f, 1f);
                bannerRt.anchoredPosition = new Vector2(0f, -240f); // 거리 텍스트 바로 밑
            }
        }
    }

    private bool CanInteract()
    {
        if (Time.unscaledTime - lastTransitionTime < DEBOUNCE_COOLDOWN) return false;
        lastTransitionTime = Time.unscaledTime;
        return true;
    }
}