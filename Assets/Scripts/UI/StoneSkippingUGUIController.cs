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

    [Header("7. 리플레이 (Replay)")]
    [SerializeField] private GameObject replayObj;
    [SerializeField] private Text replayTitleText;
    [SerializeField] private Text replaySummaryText;
    [SerializeField] private Button replayRetryBtn;
    [SerializeField] private Button replayResultBtn;

    [Header("8. 결과 모달 (Result)")]
    [SerializeField] private GameObject resultObj;
    [SerializeField] private Text resultReasonText;
    [SerializeField] private Text resultDistanceScoreText;
    [SerializeField] private Text resultSkipScoreText;
    [SerializeField] private Text resultSpecialScoreText;
    [SerializeField] private Text resultTotalScoreText;
    [SerializeField] private Text resultCoinText;
    [SerializeField] private Button resultRetryBtn;
    [SerializeField] private Button resultLobbyBtn;

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

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (gameController == null)
            gameController = FindFirstObjectByType<GameController>();

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

        // 리플레이
        if (replayRetryBtn != null) replayRetryBtn.onClick.AddListener(() =>
        {
            if (CanInteract() && gameController != null)
            {
                if (gameController.topDownReplay != null)
                    gameController.topDownReplay.ReplayAgain();
                else
                    gameController.RestartGame();
            }
        });
        if (replayResultBtn != null) replayResultBtn.onClick.AddListener(() =>
        {
            if (CanInteract() && gameController != null)
            {
                if (gameController.topDownReplay != null)
                    gameController.topDownReplay.FinishReplayAndShowResult();
                else
                    gameController.ShowFinalResultDirect(gameController.stone != null ? gameController.stone.totalDistance : 0f);
            }
        });

        // 결과
        if (resultRetryBtn != null) resultRetryBtn.onClick.AddListener(() =>
        {
            if (CanInteract() && gameController != null) gameController.RestartGame();
        });
        if (resultLobbyBtn != null) resultLobbyBtn.onClick.AddListener(() =>
        {
            if (CanInteract() && gameController != null) gameController.ReturnToModeSelect();
        });

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
        if (devCloseBtn != null) devCloseBtn.onClick.AddListener(() =>
        {
            if (EnvironmentTestHelper.Instance != null) EnvironmentTestHelper.Instance.showTestUI = false;
        });
    }

    private void InitializePanelStates()
    {
        if (positioningObj != null) positioningObj.SetActive(false);
        if (aimingObj != null) aimingObj.SetActive(false);
        if (chargingObj != null) chargingObj.SetActive(false);
        if (flightHudObj != null) flightHudObj.SetActive(false);
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
            gameController = FindFirstObjectByType<GameController>();
        if (gameController == null) return;

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
        var state = gameController.currentState;

        if (topBarObj != null)
            topBarObj.SetActive(state != GameController.GameState.Replay && state != GameController.GameState.Result);
        if (modeSelectObj != null)
            modeSelectObj.SetActive(state == GameController.GameState.ModeSelect);
        if (positioningObj != null)
            positioningObj.SetActive(state == GameController.GameState.Positioning);
        if (aimingObj != null)
            aimingObj.SetActive(state == GameController.GameState.AimingAngle);
        if (chargingObj != null)
            chargingObj.SetActive(state == GameController.GameState.ChargingPower);
        if (flightHudObj != null)
            flightHudObj.SetActive(state == GameController.GameState.Flying);
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

        // 상단 알림 배너
        bool showBanner = !string.IsNullOrEmpty(gameController.bannerNotificationText);
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

        if (gameController.stone != null && flightHudObj != null && flightHudObj.activeSelf)
        {
            if (flightDistanceText != null) flightDistanceText.text = $"[거리] {gameController.stone.totalDistance:F1} m";
            if (flightSkipText != null) flightSkipText.text = $"[바운스] {gameController.stone.skipCount}회";
            if (flightTimingText != null) flightTimingText.text = gameController.lastTimingText;
        }

        if (resultObj != null && resultObj.activeSelf)
        {
            if (resultReasonText != null) resultReasonText.text = gameController.lastTimingText;
            if (resultDistanceScoreText != null && gameController.stone != null)
                resultDistanceScoreText.text = $"1. 도달 거리 ({gameController.stone.totalDistance:F1} m)  +{gameController.distanceScore:N0} 점";
            if (resultSkipScoreText != null && gameController.stone != null)
                resultSkipScoreText.text = $"2. 튕긴 횟수 ({gameController.stone.skipCount} 회)  +{gameController.skipScore:N0} 점";
            if (resultSpecialScoreText != null)
                resultSpecialScoreText.text = $"3. 보너스 [P:{gameController.perfectTimingCount} 저격:{gameController.fishSnipeCount}]  +{gameController.specialScore:N0} 점";
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

    private bool CanInteract()
    {
        if (Time.unscaledTime - lastTransitionTime < DEBOUNCE_COOLDOWN) return false;
        lastTransitionTime = Time.unscaledTime;
        return true;
    }
}