using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class StoneSkippingUI : MonoBehaviour
{
    public GameController gameController;

    private const float V_WIDTH = 720f;

    private GUIStyle titleStyle;
    private GUIStyle meterStyle;
    private GUIStyle subStyle;
    private GUIStyle subLeftStyle;
    private GUIStyle judgeStyle;
    private GUIStyle buttonStyle;
    private GUIStyle smallBtnStyle;
    private GUIStyle timingPromptStyle;
    private GUIStyle bannerStyle;
    private GUIStyle scoreLeftStyle;
    private GUIStyle scoreRightStyle;
    private GUIStyle scoreDetailStyle;
    private GUIStyle totalScoreStyle;
    private GUIStyle gameOverReasonStyle;

    private Texture2D barBgTex;
    private Texture2D barFillTex;
    private Texture2D pipFrameTex;
    private Texture2D bannerBgTex;
    private Texture2D modalBgTex;
    private Texture2D perfectZoneTex;
    private Texture2D yellowCircleTex;
    private Texture2D greenDotTex;

    private bool pointerDownConsumedThisFrame = false;
    private float lastTransitionTime = 0f;
    private GameController.GameState lastObservedState = GameController.GameState.ModeSelect;
    private bool lastAquaState = false;
    private bool lastSkinState = false;
    private bool lastTestUIState = false;

    // 🌟 상단 골드칸 5회 연속 탭 감지용
    private int goldTapCount = 0;
    private float lastGoldTapTime = 0f;

    // 🌟 에디터 보조 스크립트 없이도 9:16 완벽 유지하는 가상 캔버스 좌표계
    private float currentScale = 1f;
    private float currentOffsetX = 0f;
    private float currentOffsetY = 0f;


    private void Awake()
    {
        if (gameController == null) gameController = FindAnyObjectByType<GameController>();
        InitTextures();
    }

    private void InitTextures()
    {
        barBgTex = MakeTex(2, 2, new Color(0.08f, 0.12f, 0.18f, 0.88f));
        barFillTex = MakeTex(2, 2, new Color(0.15f, 0.82f, 0.95f, 0.95f));
        pipFrameTex = MakeTex(2, 2, new Color(0.05f, 0.08f, 0.14f, 0.95f));
        bannerBgTex = MakeTex(2, 2, new Color(0.95f, 0.75f, 0.1f, 0.92f));
        modalBgTex = MakeTex(2, 2, new Color(0.06f, 0.10f, 0.16f, 0.98f));
        perfectZoneTex = MakeTex(2, 2, new Color(0.1f, 0.9f, 0.4f, 0.65f));
        if (yellowCircleTex == null) yellowCircleTex = MakeCircleTex(48, new Color(1f, 0.92f, 0.1f, 1f), new Color(1f, 0.85f, 0.1f, 0.35f), 6);
        if (greenDotTex == null) greenDotTex = MakeCircleTex(24, new Color(0.2f, 1f, 0.4f, 1f), new Color(0.2f, 1f, 0.4f, 0.6f), 3);
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

    private void InitStyles()
    {
        if (titleStyle == null)
        {
            titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 24,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true
            };
            titleStyle.normal.textColor = Color.white;

            meterStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 32,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft
            };
            meterStyle.normal.textColor = new Color(1f, 0.88f, 0.2f);

            subStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 17,
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true
            };
            subStyle.normal.textColor = new Color(0.85f, 0.92f, 1f);

            subLeftStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 17,
                alignment = TextAnchor.MiddleLeft,
                wordWrap = true
            };
            subLeftStyle.normal.textColor = new Color(0.85f, 0.92f, 1f);

            judgeStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 36,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true
            };
            judgeStyle.normal.textColor = new Color(0.2f, 1f, 0.75f);

            buttonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 22,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true
            };
            buttonStyle.normal.textColor = Color.white;

            smallBtnStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 18,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true
            };
            smallBtnStyle.normal.textColor = Color.white;

            timingPromptStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 32,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true
            };
            timingPromptStyle.normal.textColor = new Color(0.2f, 1f, 0.4f);

            bannerStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 18,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true
            };
            bannerStyle.normal.textColor = new Color(0.1f, 0.1f, 0.15f);

            scoreLeftStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 20,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                wordWrap = true
            };
            scoreLeftStyle.normal.textColor = new Color(0.92f, 0.96f, 1f);

            scoreRightStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 22,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleRight
            };
            scoreRightStyle.normal.textColor = new Color(0.2f, 1f, 0.65f);

            scoreDetailStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 16,
                alignment = TextAnchor.MiddleLeft,
                wordWrap = true
            };
            scoreDetailStyle.normal.textColor = new Color(0.75f, 0.85f, 0.95f);

            totalScoreStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 28,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true
            };
            totalScoreStyle.normal.textColor = new Color(1f, 0.88f, 0.2f);

            gameOverReasonStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 18,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true
            };
            gameOverReasonStyle.normal.textColor = new Color(1f, 0.5f, 0.5f);
        }
    }

    #region 동적 반응형 버튼 및 입력 안전 처리 (4중 안전장치 적용)

    private bool GetPointerDownVirtualPos(out Vector2 virtualPos, float scale)
    {
        virtualPos = Vector2.zero;

#if ENABLE_INPUT_SYSTEM
        // 1. 디바이스 시뮬레이터 및 모바일 실제 터치 (단일 탭 순간만 감지)
        if (Touchscreen.current != null)
        {
            var touch = Touchscreen.current.primaryTouch;
            if (touch.press.wasPressedThisFrame)
            {
                Vector2 p = touch.position.ReadValue();
                virtualPos = new Vector2((p.x - currentOffsetX) / currentScale, (Screen.height - p.y - currentOffsetY) / currentScale);
                return true;
            }
        }

        // 2. PC 마우스 클릭 (단일 다운 순간만 감지)
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            Vector2 p = Mouse.current.position.ReadValue();
            virtualPos = new Vector2((p.x - currentOffsetX) / currentScale, (Screen.height - p.y - currentOffsetY) / currentScale);
            return true;
        }
#else
        // 3. 레거시 모바일 터치
        try
        {
            if (Input.touchCount > 0)
            {
                UnityEngine.Touch t = Input.GetTouch(0);
                if (t.phase == UnityEngine.TouchPhase.Began)
                {
                    virtualPos = new Vector2((t.position.x - currentOffsetX) / currentScale, (Screen.height - t.position.y - currentOffsetY) / currentScale);
                    return true;
                }
            }

            // 4. 레거시 마우스 클릭
            if (Input.GetMouseButtonDown(0))
            {
                Vector3 m = Input.mousePosition;
                virtualPos = new Vector2((m.x - currentOffsetX) / currentScale, (Screen.height - m.y - currentOffsetY) / currentScale);
                return true;
            }
        }
        catch { }
#endif

        // 5. IMGUI 내부 MouseDown 이벤트
        if (Event.current != null && Event.current.type == EventType.MouseDown && Event.current.button == 0)
        {
            virtualPos = Event.current.mousePosition;
            return true;
        }

        return false;
    }

    private bool DrawResponsiveButton(Rect rect, string text, GUIStyle style, Color? bgColor = null, float scale = 1f)
    {
        // 🌟 1. 상태 전환 디바운스 쿨다운 (0.20초) 보호
        if (Time.unscaledTime - lastTransitionTime < 0.20f)
        {
            Color prev = GUI.backgroundColor;
            if (bgColor.HasValue) GUI.backgroundColor = bgColor.Value;
            GUI.Button(rect, text, style);
            if (bgColor.HasValue) GUI.backgroundColor = prev;
            return false;
        }

        // 🌟 2. 터치 릴리즈 락 보호 (손가락을 떼기 전까지 모든 버튼 클릭 차단)
        if (gameController != null && gameController.requireTouchRelease)
        {
            Color prev = GUI.backgroundColor;
            if (bgColor.HasValue) GUI.backgroundColor = bgColor.Value;
            GUI.Button(rect, text, style);
            if (bgColor.HasValue) GUI.backgroundColor = prev;
            return false;
        }

        // 🌟 3. 버튼 렌더링
        Color prevBg = GUI.backgroundColor;
        if (bgColor.HasValue) GUI.backgroundColor = bgColor.Value;
        bool clicked = GUI.Button(rect, text, style);
        if (bgColor.HasValue) GUI.backgroundColor = prevBg;

        // Repaint 패스 중 상태 변이로 인한 레이아웃 불일치/깜빡임 방지
        if (Event.current != null && Event.current.type == EventType.Repaint)
        {
            return false;
        }

        // 🌟 4. 이번 프레임에 이미 다른 버튼/포인터가 소모되었으면 중복 클릭 차단
        if (pointerDownConsumedThisFrame)
        {
            return false;
        }

        if (clicked)
        {
            pointerDownConsumedThisFrame = true;
            lastTransitionTime = Time.unscaledTime;
            if (gameController != null) gameController.requireTouchRelease = true;
            if (AudioManager.Instance != null) AudioManager.Instance.PlaySound(SoundType.ButtonClick, 0.9f);
            HapticFeedbackHelper.TriggerLightTap();
            return true;
        }

        if (GetPointerDownVirtualPos(out Vector2 pointerPos, scale))
        {
            if (rect.Contains(pointerPos))
            {
                pointerDownConsumedThisFrame = true;
                lastTransitionTime = Time.unscaledTime;
                if (gameController != null) gameController.requireTouchRelease = true;
                if (Event.current != null) Event.current.Use();
                if (AudioManager.Instance != null) AudioManager.Instance.PlaySound(SoundType.ButtonClick, 0.9f);
                HapticFeedbackHelper.TriggerLightTap();
                return true;
            }
        }

        return false;
    }

    #endregion

    [Header("uGUI 활성화 시 레거시 OnGUI 렌더링 끄기")]
    public bool enableLegacyIMGUI = false;

    private void OnGUI()
    {
        if (!enableLegacyIMGUI)
        {
            if (EnvironmentTestHelper.Instance != null && EnvironmentTestHelper.Instance.showTestUI)
            {
                float devW = Screen.width;
                float devH = Screen.height;
                if (devW > 0 && devH > 0)
                {
                    float devScale = devH / 1280f;
                    DrawDeveloperTestMenu(720f, 1280f, devScale);
                }
            }
            return;
        }

        InitStyles();
        if (barBgTex == null || barFillTex == null || modalBgTex == null || perfectZoneTex == null) InitTextures();
        if (gameController == null) return;

        // 🌟 1. 손가락 뗌 감지: 화면에 물리적 터치/마우스가 없으면 requireTouchRelease 해제
        bool isHeld = false;
#if ENABLE_INPUT_SYSTEM
        if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.isPressed) isHeld = true;
        if (Mouse.current != null && Mouse.current.leftButton.isPressed) isHeld = true;
#endif
        try
        {
            if (Input.touchCount > 0 || Input.GetMouseButton(0)) isHeld = true;
        }
        catch { }

        if (!isHeld && gameController.requireTouchRelease)
        {
            gameController.requireTouchRelease = false;
        }

        // 🌟 2. 화면/모달 상태 전환 감지 시 자동 쿨다운 & 릴리즈 락 갱신
        bool currentTestUI = (EnvironmentTestHelper.Instance != null && EnvironmentTestHelper.Instance.showTestUI);
        if (gameController.currentState != lastObservedState ||
            gameController.showAquariumModal != lastAquaState ||
            gameController.showStoneSelectorModal != lastSkinState ||
            currentTestUI != lastTestUIState)
        {
            lastObservedState = gameController.currentState;
            lastAquaState = gameController.showAquariumModal;
            lastSkinState = gameController.showStoneSelectorModal;
            lastTestUIState = currentTestUI;
            lastTransitionTime = Time.unscaledTime;
            if (isHeld) gameController.requireTouchRelease = true;
        }

        float actualW = Screen.width;
        float actualH = Screen.height;
        if (actualW <= 0 || actualH <= 0) return;

        // 🌟 에디터 스크립트 없이도 어떤 화면비에서든 9:16 모바일 뷰포트(720x1280) 완벽 구현
        const float targetW = 720f;
        const float targetH = 1280f;
        float targetAspect = targetW / targetH; // 0.5625 (9:16)
        float currentAspect = actualW / actualH;

        if (currentAspect > targetAspect) // 가로가 더 넓은 화면 (PC 창모드, 에디터 가로 뷰)
        {
            currentScale = actualH / targetH;
            currentOffsetX = (actualW - targetW * currentScale) * 0.5f;
            currentOffsetY = 0f;
        }
        else // 9:16 이거나 세로로 더 긴 화면 (Free Aspect 롱 뷰)
        {
            currentScale = actualW / targetW;
            currentOffsetX = 0f;
            currentOffsetY = (actualH - targetH * currentScale) * 0.5f;
        }

        float sw = targetW;
        float sh = targetH;
        float scale = currentScale;

        Matrix4x4 prevMatrix = GUI.matrix;
        GUI.matrix = Matrix4x4.TRS(new Vector3(currentOffsetX, currentOffsetY, 0.0f), Quaternion.identity, new Vector3(currentScale, currentScale, 1.0f));

        try
        {
            pointerDownConsumedThisFrame = false;

            // 🌟 개발자 테스트 메뉴 팝업 시 다른 게임 UI 전체 숨김 & 테스트 모달 최우선 렌더링
            if (EnvironmentTestHelper.Instance != null && EnvironmentTestHelper.Instance.showTestUI)
            {
                DrawDeveloperTestMenu(sw, sh, scale);
                return;
            }

            // Safe Area 노치/카메라 홀 여백 계산 (가상 단위)
            float safeTop = (actualH - Screen.safeArea.yMax) / scale;
            float topOffset = Mathf.Max(safeTop, 16f);

            // 상단 재화 및 도감/스킨 버튼 (리플레이 중에는 숨김)
            if (gameController.currentState != GameController.GameState.Replay)
            {
                DrawTopBar(sw, sh, topOffset, scale);
            }

            // 상단 알림 배너 (추월 / 물고기 저격 알림) - HUD 아래 단독 노출
            if (!string.IsNullOrEmpty(gameController.bannerNotificationText))
            {
                float bW = Mathf.Min(sw - 36f, 660f);
                float bH = 50f;
                float bX = (sw - bW) * 0.5f;
                float bY = topOffset + 128f;

                GUI.DrawTexture(new Rect(bX, bY, bW, bH), bannerBgTex);
                DrawRectOutline(new Rect(bX, bY, bW, bH), 2, Color.white);
                GUI.Label(new Rect(bX + 10, bY + 6, bW - 20, bH - 12), gameController.bannerNotificationText, bannerStyle);
            }

            // 모달 창 팝업 시 배경 블러/오버레이 및 모달만 최우선 렌더링
            if (gameController.showAquariumModal)
            {
                DrawAquariumModal(sw, sh, scale);
                return;
            }

            if (gameController.showStoneSelectorModal)
            {
                DrawStoneSelectorModal(sw, sh, scale);
                return;
            }

            switch (gameController.currentState)
            {
                case GameController.GameState.ModeSelect:
                    DrawModeSelectUI(sw, sh, scale);
                    break;
                case GameController.GameState.Positioning:
                    DrawPositioningUI(sw, sh, topOffset, scale);
                    break;
                case GameController.GameState.AimingAngle:
                    DrawAimingAngleUI(sw, sh, topOffset, scale);
                    break;
                case GameController.GameState.ChargingPower:
                    DrawChargingPowerUI(sw, sh, topOffset, scale);
                    break;
                case GameController.GameState.ThrowingAnimation:
                    // 투구 스윙 중: HUD 조용히 유지
                    break;
                case GameController.GameState.Flying:
                    bool inTimingWindow = (gameController.stone != null && gameController.stone.isInTimingWindow);
                    DrawFlightHUD(sw, sh, topOffset, inTimingWindow, scale);
                    break;
                case GameController.GameState.Replay:
                    DrawReplayUI(sw, sh, topOffset, scale);
                    break;
                case GameController.GameState.Result:
                    DrawResultUI(sw, sh, scale);
                    break;
            }
        }
        finally
        {
            GUI.matrix = prevMatrix;
        }
    }

    private void DrawTopBar(float sw, float sh, float topOffset, float scale)
    {
        int coins = (AquariumManager.Instance != null) ? AquariumManager.Instance.totalCoins : 0;
        float progress = (AquariumManager.Instance != null) ? AquariumManager.Instance.GetCompletionPercentage() : 0f;

        float totalW = sw - 36f;
        float infoW = totalW * 0.48f;
        float btnW = (totalW - infoW - 16f) * 0.5f;
        float barH = 50f;
        float barY = topOffset;

        // 코인 및 진행률 표시 (5회 연속 탭 시 테스트 메뉴 오픈)
        Rect coinBoxRect = new Rect(18, barY, infoW, barH);
        GUI.DrawTexture(coinBoxRect, barBgTex);
        DrawRectOutline(coinBoxRect, 2, new Color(0.2f, 0.8f, 1f));
        GUI.Label(new Rect(22, barY + 7, infoW * 0.54f, barH - 14), $"[C] {coins:N0}", titleStyle);
        GUI.Label(new Rect(18 + infoW * 0.52f, barY + 11, infoW * 0.46f, barH - 22), $"도감:{progress:F0}%", subStyle);

        // 🌟 골드칸 5회 연속 탭 감지
        if (DrawResponsiveButton(coinBoxRect, "", GUIStyle.none, null, scale))
        {
            if (Time.unscaledTime - lastGoldTapTime > 2.5f)
            {
                goldTapCount = 0;
            }
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

        // 도감 버튼
        if (DrawResponsiveButton(new Rect(18 + infoW + 8, barY, btnW, barH), "[도감]", smallBtnStyle, null, scale))
        {
            gameController.showAquariumModal = !gameController.showAquariumModal;
            gameController.showStoneSelectorModal = false;
        }

        // 돌 선택 버튼
        if (DrawResponsiveButton(new Rect(18 + infoW + btnW + 16, barY, btnW, barH), "[스킨]", smallBtnStyle, null, scale))
        {
            gameController.showStoneSelectorModal = !gameController.showStoneSelectorModal;
            gameController.showAquariumModal = false;
        }
    }

    private void DrawAquariumModal(float sw, float sh, float scale)
    {
        float mW = Mathf.Min(sw - 40f, 660f);
        float mH = Mathf.Min(sh - 120f, 900f);
        float mx = (sw - mW) * 0.5f;
        float my = (sh - mH) * 0.5f;

        GUI.DrawTexture(new Rect(mx, my, mW, mH), modalBgTex);
        DrawRectOutline(new Rect(mx, my, mW, mH), 3, new Color(0.2f, 0.8f, 1f));

        GUI.Label(new Rect(mx, my + 18, mW, 34), "[수족관 도감] (Aquarium)", titleStyle);
        GUI.Label(new Rect(mx + 15, my + 54, mW - 30, 32), "물수제비 도중 도약하는 물고기를 저격하여 수집하세요!", subStyle);

        if (AquariumManager.Instance != null)
        {
            float itemY = my + 94;
            foreach (var fish in AquariumManager.Instance.fishSpeciesList)
            {
                float itemH = 96f;
                GUI.DrawTexture(new Rect(mx + 20, itemY, mW - 40, itemH), barBgTex);
                DrawRectOutline(new Rect(mx + 20, itemY, mW - 40, itemH), 1, (fish.caughtCount > 0) ? new Color(0.2f, 1f, 0.5f) : Color.gray);

                string statusText = (fish.caughtCount > 0) ? $"포획: {fish.caughtCount}마리" : "미발견";
                GUI.Label(new Rect(mx + 30, itemY + 8, mW - 60, 32), $"[{fish.name}] ({fish.spawnStartDistance}m ~ {fish.spawnEndDistance}m)", titleStyle);
                GUI.Label(new Rect(mx + 30, itemY + 44, mW - 60, 44), $"{fish.description} | {statusText}", subStyle);

                itemY += itemH + 10;
            }

            // 100% 달성 보상 안내
            bool completed = AquariumManager.Instance.CheckAllCompleted();
            string rewardText = completed ? "[COMPLETE] 도감 100% 달성! [황금 조약돌] 해금!" : "[LOCK] 100% 수집 시 [황금 조약돌] 해금!";
            GUI.Label(new Rect(mx + 15, my + mH - 120, mW - 30, 38), rewardText, subStyle);
        }

        if (DrawResponsiveButton(new Rect(mx + (mW - 240) * 0.5f, my + mH - 72, 240, 56), "닫기 (Close)", buttonStyle, null, scale))
        {
            gameController.showAquariumModal = false;
        }
    }

    private void DrawStoneSelectorModal(float sw, float sh, float scale)
    {
        float mW = Mathf.Min(sw - 40f, 660f);
        float mH = Mathf.Min(sh - 120f, 900f);
        float mx = (sw - mW) * 0.5f;
        float my = (sh - mH) * 0.5f;

        GUI.DrawTexture(new Rect(mx, my, mW, mH), modalBgTex);
        DrawRectOutline(new Rect(mx, my, mW, mH), 3, new Color(1f, 0.85f, 0.2f));

        GUI.Label(new Rect(mx, my + 18, mW, 34), "[조약돌 스킨 도감]", titleStyle);
        GUI.Label(new Rect(mx + 15, my + 54, mW - 30, 32), "다양한 특수 성능을 가진 조약돌을 장착하세요!", subStyle);

        if (StoneInventory.Instance != null)
        {
            float itemY = my + 94;
            for (int i = 0; i < StoneInventory.Instance.stones.Count; i++)
            {
                var stone = StoneInventory.Instance.stones[i];
                float itemH = 96f;
                bool isEquipped = (StoneInventory.Instance.currentStoneIndex == i);
                bool isUnlocked = stone.isUnlocked;

                GUI.DrawTexture(new Rect(mx + 20, itemY, mW - 40, itemH), barBgTex);
                DrawRectOutline(new Rect(mx + 20, itemY, mW - 40, itemH), 2, isEquipped ? new Color(1f, 0.85f, 0.2f) : (isUnlocked ? new Color(0.2f, 0.8f, 1f) : Color.gray));

                string equipStatus = isEquipped ? "[장착중]" : (isUnlocked ? "보유중" : "[잠김] 수족관 100% 달성 시 해금");
                GUI.Label(new Rect(mx + 28, itemY + 8, mW - 210, 32), $"{stone.name} ({equipStatus})", titleStyle);
                GUI.Label(new Rect(mx + 28, itemY + 44, mW - 210, 44), $"{stone.description} | 추진력 x{stone.forwardPowerMultiplier:F1} / 반사력 x{stone.bounceMultiplier:F1}", subLeftStyle);

                float btnW = 140f;
                float btnH = 50f;
                float btnX = mx + mW - 40 - btnW - 12;
                float btnY = itemY + (itemH - btnH) * 0.5f;

                if (isEquipped)
                {
                    GUI.Label(new Rect(btnX, btnY + 10, btnW, 30), "[사용중]", titleStyle);
                }
                else if (isUnlocked)
                {
                    int stoneIdx = i;
                    if (DrawResponsiveButton(new Rect(btnX, btnY, btnW, btnH), "장착하기", smallBtnStyle, new Color(0.2f, 0.8f, 1f), scale))
                    {
                        StoneInventory.Instance.SelectStone(stoneIdx);
                        if (gameController != null) gameController.ApplyCurrentStoneVisuals();
                    }
                }
                else
                {
                    GUI.Label(new Rect(btnX, btnY + 10, btnW, 30), "[잠김]", subStyle);
                }

                itemY += itemH + 10;
            }
        }

        if (DrawResponsiveButton(new Rect(mx + (mW - 240) * 0.5f, my + mH - 72, 240, 56), "닫기 (Close)", buttonStyle, null, scale))
        {
            gameController.showStoneSelectorModal = false;
        }
    }

    private void DrawDeveloperTestMenu(float sw, float sh, float scale)
    {
        float mW = Mathf.Min(sw - 40f, 660f);
        float mH = Mathf.Min(sh - 100f, 490f);
        float mx = (sw - mW) * 0.5f;
        float my = (sh - mH) * 0.5f;

        // 배경 박스 및 테두리 (골드 & 다크 블루 테마)
        GUI.DrawTexture(new Rect(mx, my, mW, mH), modalBgTex);
        DrawRectOutline(new Rect(mx, my, mW, mH), 3, new Color(1f, 0.85f, 0.2f));

        GUI.Label(new Rect(mx, my + 18, mW, 34), "🛠️ [개발자 환경 & 비행 테스트 메뉴]", titleStyle);
        GUI.Label(new Rect(mx + 15, my + 54, mW - 30, 32), "원하는 시간대 및 4,500m 갓모드 비행을 즉시 테스트하세요!", subStyle);

        // 1. 시간대 프리뷰 버튼 (4개: 낮, 노을, 석양, 밤)
        float btnW = (mW - 50f) / 4f;
        float btnH = 74f;
        float btnY = my + 96f;

        if (DrawResponsiveButton(new Rect(mx + 16f, btnY, btnW, btnH), "☀️ 낮\n(0m)", buttonStyle, new Color(0.2f, 0.8f, 1f), scale))
        {
            if (EnvironmentTestHelper.Instance != null) EnvironmentTestHelper.Instance.SetPreviewDistance(0f);
        }
        if (DrawResponsiveButton(new Rect(mx + 16f + (btnW + 6f), btnY, btnW, btnH), "🌅 노을\n(2,000m)", buttonStyle, new Color(1f, 0.6f, 0.2f), scale))
        {
            if (EnvironmentTestHelper.Instance != null) EnvironmentTestHelper.Instance.SetPreviewDistance(2000f);
        }
        if (DrawResponsiveButton(new Rect(mx + 16f + (btnW + 6f) * 2f, btnY, btnW, btnH), "🌆 석양\n(3,600m)", buttonStyle, new Color(0.9f, 0.3f, 0.4f), scale))
        {
            if (EnvironmentTestHelper.Instance != null) EnvironmentTestHelper.Instance.SetPreviewDistance(3600f);
        }
        if (DrawResponsiveButton(new Rect(mx + 16f + (btnW + 6f) * 3f, btnY, btnW, btnH), "🌙 밤\n(4,800m)", buttonStyle, new Color(0.4f, 0.3f, 0.9f), scale))
        {
            if (EnvironmentTestHelper.Instance != null) EnvironmentTestHelper.Instance.SetPreviewDistance(4800f);
        }

        // 2. 🚀 3,500m 갓모드 자동 비행 감상 버튼
        float autoBtnY = btnY + btnH + 18f;
        float autoBtnW = mW - 32f;
        float autoBtnH = 80f;
        bool isFlying = (EnvironmentTestHelper.Instance != null && EnvironmentTestHelper.Instance.isAutoFlying);
        string autoLabel = isFlying ? "🚀 갓모드 비행 중... [중지하기]" : "🚀 3,500m 갓모드 자동 비행 감상 (God Mode)";
        Color autoColor = isFlying ? new Color(1f, 0.3f, 0.3f) : new Color(0.2f, 0.9f, 0.4f);

        if (DrawResponsiveButton(new Rect(mx + 16f, autoBtnY, autoBtnW, autoBtnH), autoLabel, buttonStyle, autoColor, scale))
        {
            if (EnvironmentTestHelper.Instance != null) EnvironmentTestHelper.Instance.ToggleAutoFlyGodMode();
        }

        // 3. 닫기 버튼
        float closeBtnY = autoBtnY + autoBtnH + 18f;
        if (DrawResponsiveButton(new Rect(mx + (mW - 240) * 0.5f, closeBtnY, 240, 58), "✖ 닫기 (Close)", buttonStyle, null, scale))
        {
            if (EnvironmentTestHelper.Instance != null) EnvironmentTestHelper.Instance.showTestUI = false;
        }
    }

    private void DrawModeSelectUI(float sw, float sh, float scale)
    {
        float panelW = Mathf.Min(sw - 40f, 660f);
        float panelH = Mathf.Min(sh * 0.80f, 720f);
        float px = (sw - panelW) * 0.5f;
        float py = (sh - panelH) * 0.5f;

        GUI.DrawTexture(new Rect(px, py, panelW, panelH), modalBgTex);
        DrawRectOutline(new Rect(px, py, panelW, panelH), 3, new Color(0.2f, 0.85f, 1f));

        // 게임 타이틀 & 서브타이틀
        GUI.Label(new Rect(px, py + 18, panelW, 40), "[물수제비 마스터 3D]", titleStyle);
        GUI.Label(new Rect(px + 15, py + 60, panelW - 30, 30), "도전할 게임 모드를 선택하세요!", subStyle);

        float cardW = panelW - 48f;
        float cardH = (panelH - 160f) * 0.48f;
        float cardX = px + 24f;
        float card1Y = py + 100f;
        float card2Y = card1Y + cardH + 16f;

        // 1. [ 장거리 도전 ] (상단 카드)
        GUI.DrawTexture(new Rect(cardX, card1Y, cardW, cardH), barBgTex);
        DrawRectOutline(new Rect(cardX, card1Y, cardW, cardH), 2, new Color(0.15f, 0.85f, 1f));
        GUI.Label(new Rect(cardX, card1Y + 12, cardW, 34), "[장거리 도전] (Long Distance)", titleStyle);
        GUI.Label(new Rect(cardX + 16, card1Y + 50, cardW - 32, 80), "물 위 나무 발판에서 출발하여 1,500m 강줄기를 따라\n최대 비거리 & 친구 랭킹 추월에 도전하세요!\n(부스트 패드, 물고기 저격 레이스)", subStyle);

        float btnW = Mathf.Min(cardW - 40f, 440f);
        float btnH = 64f;
        if (DrawResponsiveButton(new Rect(cardX + (cardW - btnW) * 0.5f, card1Y + cardH - btnH - 14, btnW, btnH), "[장거리 도전] 시작 ▶", buttonStyle, new Color(0.15f, 0.85f, 1f, 1f), scale))
        {
            gameController.SelectGameMode(GameController.GameMode.LongDistance);
        }

        // 2. [ 타겟 맞추기 ] (하단 카드)
        GUI.DrawTexture(new Rect(cardX, card2Y, cardW, cardH), barBgTex);
        DrawRectOutline(new Rect(cardX, card2Y, cardW, cardH), 2, new Color(0.1f, 0.95f, 0.6f));
        GUI.Label(new Rect(cardX, card2Y + 12, cardW, 34), "[타겟 맞추기] (Target Accuracy)", titleStyle);
        GUI.Label(new Rect(cardX + 16, card2Y + 50, cardW - 32, 80), "강변(PP)을 따라 최적의 투구 위치를 선정하고\n강 건너편 목표물을 향해 정밀하게 투구하세요!\n(상단 1/4 미니맵 & PP01~PP29 포인트 이동)", subStyle);

        if (DrawResponsiveButton(new Rect(cardX + (cardW - btnW) * 0.5f, card2Y + cardH - btnH - 14, btnW, btnH), "[타겟 맞추기] 시작 ▶", buttonStyle, new Color(0.1f, 0.95f, 0.6f, 1f), scale))
        {
            gameController.SelectGameMode(GameController.GameMode.TargetAccuracy);
        }
    }

    private void DrawPositioningUI(float sw, float sh, float topOffset, float scale)
    {
        // [모드 1: 장거리 도전 모드 0단계]
        if (gameController.currentMode == GameController.GameMode.LongDistance)
        {
            float boxW = Mathf.Min(sw - 40f, 640f);
            float boxX = (sw - boxW) * 0.5f;
            float boxY = topOffset + 76f;

            GUI.DrawTexture(new Rect(boxX, boxY, boxW, 105), barBgTex);
            DrawRectOutline(new Rect(boxX, boxY, boxW, 105), 2, new Color(0.15f, 0.85f, 1f));
            GUI.Label(new Rect(boxX, boxY + 10, boxW, 34), "0단계: 나무 발판 위치 선정", titleStyle);
            GUI.Label(new Rect(boxX + 10, boxY + 46, boxW - 20, 52), "◀ A / D 키보드(길게 누르기) 또는 화면 드래그로\n발판 위 최적의 투구 위치를 잡으세요! ▶", subStyle);

            float btnW = Mathf.Min(sw - 60f, 560f);
            float btnH = 76f;
            float btnY = sh * 0.84f;

            if (DrawResponsiveButton(new Rect((sw - btnW) * 0.5f, btnY, btnW, btnH), "발판 위치 확정 ▶ (Space / Enter)", buttonStyle, new Color(0.15f, 0.85f, 1f, 1.0f), scale))
            {
                gameController.ConfirmPosition();
            }
            return;
        }

        // [모드 2: 타겟 맞추기 모드 0단계]
        int curIdx = (gameController.character != null) ? gameController.character.GetCurrentWaypointIndex() : 0;
        int totalCount = (gameController.character != null) ? gameController.character.GetTotalWaypointsCount() : 29;

        // 1. 상단 1/4 MAP_Camera PIP 창 및 PP 포인트 / 노란색 타깃 원 오버레이
        float pipBottomY = topOffset + 76f;
        if (MapPIPManager.Instance != null && MapPIPManager.Instance.mapCamera != null)
        {
            Rect pipPixelRect = MapPIPManager.Instance.GetScreenPixelRect();
            Rect pipVirtualRect = new Rect(pipPixelRect.x / scale, pipPixelRect.y / scale, pipPixelRect.width / scale, pipPixelRect.height / scale);
            pipBottomY = pipVirtualRect.y + pipVirtualRect.height + 14f;

            DrawRectOutline(pipVirtualRect, 3, new Color(0.2f, 0.85f, 1f, 0.95f));

            Camera mapCam = MapPIPManager.Instance.mapCamera;
            if (yellowCircleTex == null) yellowCircleTex = MakeCircleTex(48, new Color(1f, 0.92f, 0.1f, 1f), new Color(1f, 0.85f, 0.1f, 0.35f), 6);
            if (greenDotTex == null) greenDotTex = MakeCircleTex(24, new Color(0.2f, 1f, 0.4f, 1f), new Color(0.2f, 1f, 0.4f, 0.6f), 3);

            if (gameController.character != null)
            {
                for (int i = 0; i < totalCount; i++)
                {
                    Vector3 wpWorldPos = gameController.character.GetWaypointWorldPos(i);
                    Vector3 sp = mapCam.WorldToScreenPoint(wpWorldPos);

                    if (sp.z > 0)
                    {
                        float guiX = sp.x / scale;
                        float guiY = (Screen.height - sp.y) / scale;

                        if (pipVirtualRect.Contains(new Vector2(guiX, guiY)))
                        {
                            if (i == curIdx)
                            {
                                float pulseSize = 40f + Mathf.Sin(Time.time * 6f) * 6f;
                                GUI.DrawTexture(new Rect(guiX - pulseSize * 0.5f, guiY - pulseSize * 0.5f, pulseSize, pulseSize), yellowCircleTex);
                                GUI.Label(new Rect(guiX - 45, guiY - pulseSize * 0.5f - 26, 90, 26), $"PP{i+1:02d}", titleStyle);
                            }
                            else
                            {
                                GUI.DrawTexture(new Rect(guiX - 8, guiY - 8, 16, 16), greenDotTex);
                            }
                        }
                    }
                }
            }
        }

        // 2. 안내 텍스트 박스
        float boxW2 = Mathf.Min(sw - 40f, 640f);
        float boxX2 = (sw - boxW2) * 0.5f;
        float boxY2 = pipBottomY;

        GUI.DrawTexture(new Rect(boxX2, boxY2, boxW2, 80), barBgTex);
        DrawRectOutline(new Rect(boxX2, boxY2, boxW2, 80), 2, new Color(0.1f, 0.95f, 0.6f));
        GUI.Label(new Rect(boxX2, boxY2 + 8, boxW2, 32), "0단계: 투구 포인트 선정 (PP01~PP29)", titleStyle);
        GUI.Label(new Rect(boxX2 + 10, boxY2 + 42, boxW2 - 20, 32), "◀ A / D 키보드 또는 화면 좌우 스와이프로 위치 이동 ▶", subStyle);

        // 3. 현재 선택된 포인트 뱃지
        float badgeW = 260f;
        float badgeH = 48f;
        float badgeX = (sw - badgeW) * 0.5f;
        float badgeY = boxY2 + 92f;

        GUI.DrawTexture(new Rect(badgeX, badgeY, badgeW, badgeH), barBgTex);
        DrawRectOutline(new Rect(badgeX, badgeY, badgeW, badgeH), 2, new Color(1f, 0.85f, 0.2f));
        GUI.Label(new Rect(badgeX, badgeY + 8, badgeW, 32), $"선택: PP{curIdx + 1:02d} / PP{totalCount:02d}", titleStyle);

        // 4. 하단 포인트 확정 버튼
        float btnW2 = Mathf.Min(sw - 60f, 560f);
        float btnH2 = 76f;
        float btnY2 = sh * 0.84f;

        if (DrawResponsiveButton(new Rect((sw - btnW2) * 0.5f, btnY2, btnW2, btnH2), $"PP{curIdx + 1:02d} 포인트 확정 ▶", buttonStyle, new Color(0.1f, 0.95f, 0.6f, 1.0f), scale))
        {
            gameController.ConfirmPosition();
        }
    }

    private void DrawAimingAngleUI(float sw, float sh, float topOffset, float scale)
    {
        float boxW = Mathf.Min(sw - 40f, 640f);
        float boxX = (sw - boxW) * 0.5f;

        GUI.DrawTexture(new Rect(boxX, topOffset + 76f, boxW, 86), barBgTex);
        DrawRectOutline(new Rect(boxX, topOffset + 76f, boxW, 86), 2, new Color(0.2f, 0.8f, 1f));
        GUI.Label(new Rect(boxX, topOffset + 84f, boxW, 34), "1단계: 방향 조준 (Aiming Angle)", titleStyle);
        GUI.Label(new Rect(boxX + 10, topOffset + 122f, boxW - 20, 32), "정중앙 완벽 각도에 맞춰 탭하세요!", subStyle);

        float barW = Mathf.Min(sw - 60f, 560f);
        float barH = 46f;
        float barX = (sw - barW) * 0.5f;
        float barY = sh * 0.72f;

        GUI.DrawTexture(new Rect(barX, barY, barW, barH), barBgTex);
        DrawRectOutline(new Rect(barX, barY, barW, barH), 2, new Color(0.2f, 0.8f, 1f));

        float perfectW = barW * 0.22f;
        GUI.DrawTexture(new Rect(barX + (barW - perfectW) * 0.5f, barY, perfectW, barH), perfectZoneTex);

        float needleX = barX + (gameController.aimGaugeValue + 1f) * 0.5f * barW - 6f;
        GUI.DrawTexture(new Rect(needleX, barY - 8f, 12f, barH + 16f), barFillTex);

        float btnW = Mathf.Min(sw - 60f, 560f);
        float btnH = 76f;
        if (DrawResponsiveButton(new Rect((sw - btnW) * 0.5f, sh * 0.84f, btnW, btnH), "각도 고정 ▶ (Tap / Space)", buttonStyle, null, scale))
        {
            gameController.ConfirmAngle();
        }
    }

    private void DrawChargingPowerUI(float sw, float sh, float topOffset, float scale)
    {
        float boxW = Mathf.Min(sw - 40f, 640f);
        float boxX = (sw - boxW) * 0.5f;

        GUI.DrawTexture(new Rect(boxX, topOffset + 76f, boxW, 86), barBgTex);
        DrawRectOutline(new Rect(boxX, topOffset + 76f, boxW, 86), 2, new Color(0.2f, 0.8f, 1f));
        GUI.Label(new Rect(boxX, topOffset + 84f, boxW, 34), "2단계: 파워 충전 (Power Wind-up)", titleStyle);
        GUI.Label(new Rect(boxX + 10, topOffset + 122f, boxW - 20, 32), "파워가 높을 때 발사하여 추진력을 얻으세요!", subStyle);

        float barW = 60f;
        float barH = Mathf.Min(sh * 0.32f, 320f);
        float barX = (sw - barW) * 0.5f;
        float barY = (sh - barH) * 0.48f;

        GUI.DrawTexture(new Rect(barX, barY, barW, barH), barBgTex);
        DrawRectOutline(new Rect(barX, barY, barW, barH), 2, new Color(0.2f, 0.8f, 1f));

        float fillH = barH * gameController.powerGaugeValue;
        GUI.DrawTexture(new Rect(barX + 2, barY + barH - fillH, barW - 4, fillH), barFillTex);

        GUI.Label(new Rect(barX - 60, barY + barH + 12, 180, 36), $"[POWER] {(int)(gameController.powerGaugeValue * 100)}%", titleStyle);

        float btnW = Mathf.Min(sw - 60f, 560f);
        float btnH = 76f;
        if (DrawResponsiveButton(new Rect((sw - btnW) * 0.5f, sh * 0.84f, btnW, btnH), "[투구하기!] (Launch)", buttonStyle, new Color(0.95f, 0.45f, 0.1f, 1f), scale))
        {
            gameController.LaunchStone();
        }
    }

    private void DrawFlightHUD(float sw, float sh, float topOffset, bool inTimingWindow, float scale)
    {
        // 1. 실시간 거리 & 바운스 카운터 카드
        float cardW = Mathf.Min(sw - 36f, 660f);
        float cardX = (sw - cardW) * 0.5f;
        float cardY = topOffset + 56f;
        float cardH = 66f;

        GUI.DrawTexture(new Rect(cardX, cardY, cardW, cardH), barBgTex);
        DrawRectOutline(new Rect(cardX, cardY, cardW, cardH), 2, new Color(0.1f, 0.8f, 0.95f));

        GUI.Label(new Rect(cardX + 16, cardY + 8, cardW * 0.52f, 50f), $"[거리] {gameController.stone.totalDistance:F1} m", meterStyle);
        GUI.Label(new Rect(cardX + cardW * 0.52f, cardY + 14, cardW * 0.45f, 36f), $"[바운스] {gameController.stone.skipCount}회", titleStyle);

        // 2. 직전 타이밍 판정 피드백 (화면 중앙 상단에 여유있는 110px 높이로 배치)
        if (!string.IsNullOrEmpty(gameController.lastTimingText))
        {
            GUI.Label(new Rect(20, sh * 0.35f, sw - 40, 110f), gameController.lastTimingText, judgeStyle);
        }
    }

    private void DrawReplayUI(float sw, float sh, float topOffset, float scale)
    {
        float vW = sw;
        float vH = sh;

        TopDownReplayManager replayMgr = (gameController.topDownReplay != null)
                                         ? gameController.topDownReplay
                                         : FindAnyObjectByType<TopDownReplayManager>();

        float cachedDist = (gameController.stone != null) ? gameController.stone.totalDistance : 0f;
        int totalSkips = (gameController.stone != null) ? gameController.stone.skipCount : 0;

        // 1. 상단 정보 헤더 (Safe Area 노치/카메라홀 회피 적용 및 660px 중앙 정렬)
        float topPanelW = Mathf.Min(vW - 40f, 660f);
        float topPanelH = 76f;
        float topX = (vW - topPanelW) * 0.5f;
        float topY = topOffset;

        GUI.DrawTexture(new Rect(topX, topY, topPanelW, topPanelH), barBgTex);
        DrawRectOutline(new Rect(topX, topY, topPanelW, topPanelH), 2, new Color(0.2f, 0.85f, 1f, 0.85f));

        GUI.Label(new Rect(topX, topY + 8, topPanelW, 32), "🗺️ [바운스 궤적 맵 리플레이]", titleStyle);
        GUI.Label(new Rect(topX, topY + 42, topPanelW, 26), $"최종 비거리: {cachedDist:F1}m  |  총 바운스: {totalSkips}회 스킵", subStyle);

        // 2. 🌟 자유 줌/스크롤 뷰어 안내 텍스트 (페이지 버튼 제거)
        float guideY = vH - 165f;
        float guideW = vW - 40f;
        float guideX = 20f;
        float guideH = 44f;

        GUI.DrawTexture(new Rect(guideX, guideY, guideW, guideH), barBgTex);
        DrawRectOutline(new Rect(guideX, guideY, guideW, guideH), 1, new Color(0.2f, 0.75f, 1f, 0.6f));
        GUI.Label(new Rect(guideX + 10f, guideY + 8f, guideW - 20f, 28f), "🔍 마우스 휠/두손가락(핀치)으로 확대·축소 & 드래그로 궤적 자유 탐색", subStyle);

        // 3. 하단 반응형 컨트롤 버튼 영역
        float btnAreaY = vH - 110f;
        float btnH = 64f;
        float bW = (vW - 70f) * 0.5f;
        float bX1 = 30f;
        float bX2 = bX1 + bW + 10f;

        bool isDrawing = (replayMgr != null && replayMgr.isDrawing);

        if (isDrawing)
        {
            GUI.Label(new Rect(bX1, btnAreaY + 16, bW, 36), "🌊 궤적 재생 중...", subStyle);

            if (DrawResponsiveButton(new Rect(bX2, btnAreaY, bW, btnH), "결과 보기 (완료) ✔", smallBtnStyle, new Color(0.18f, 0.55f, 0.35f, 0.98f), scale))
            {
                if (replayMgr != null) replayMgr.FinishReplayAndShowResult();
                else gameController.ShowFinalResultDirect(cachedDist);
            }
        }
        else
        {
            if (DrawResponsiveButton(new Rect(bX1, btnAreaY, bW, btnH), "다시 보기 ↺", smallBtnStyle, new Color(0.18f, 0.48f, 0.78f, 0.98f), scale))
            {
                if (replayMgr != null) replayMgr.ReplayAgain();
            }

            if (DrawResponsiveButton(new Rect(bX2, btnAreaY, bW, btnH), "결과 보기 (완료) ✔", smallBtnStyle, new Color(0.18f, 0.55f, 0.35f, 0.98f), scale))
            {
                if (replayMgr != null) replayMgr.FinishReplayAndShowResult();
                else gameController.ShowFinalResultDirect(cachedDist);
            }
        }
    }

    private void DrawResultUI(float sw, float sh, float scale)
    {
        float panelW = Mathf.Min(sw - 36f, 660f);
        float panelH = Mathf.Min(sh * 0.92f, 600f);
        float px = (sw - panelW) * 0.5f;
        float py = (sh - panelH) * 0.5f;

        // 결과창 카드 배경 및 테두리
        GUI.DrawTexture(new Rect(px, py, panelW, panelH), modalBgTex);
        DrawRectOutline(new Rect(px, py, panelW, panelH), 3, new Color(0.2f, 0.85f, 1f));

        // 1. 헤더 타이틀 & 종료 원인
        GUI.Label(new Rect(px, py + 14, panelW, 34), "[게임 결과] (GAME OVER)", titleStyle);
        if (!string.IsNullOrEmpty(gameController.lastTimingText))
        {
            GUI.Label(new Rect(px, py + 48, panelW, 26), gameController.lastTimingText, gameOverReasonStyle);
        }

        float innerX = px + 18;
        float innerW = panelW - 36;
        float currY = py + 78;

        // 2. 도달거리 점수 항목
        float itemH = 60f;
        GUI.DrawTexture(new Rect(innerX, currY, innerW, itemH), barBgTex);
        DrawRectOutline(new Rect(innerX, currY, innerW, itemH), 1, new Color(0.2f, 0.7f, 1f, 0.6f));
        GUI.Label(new Rect(innerX + 14, currY + 6, innerW * 0.65f, 26), $"1. 도달 거리 ({gameController.stone.totalDistance:F1} m)", scoreLeftStyle);
        GUI.Label(new Rect(innerX + innerW * 0.62f, currY + 6, innerW * 0.35f, 26), $"+{gameController.distanceScore:N0} 점", scoreRightStyle);
        GUI.Label(new Rect(innerX + 14, currY + 32, innerW - 28, 24), "   (1m당 10점 적용)", scoreDetailStyle);

        currY += itemH + 8;

        // 3. 튕긴 횟수(스킵) 점수 항목
        GUI.DrawTexture(new Rect(innerX, currY, innerW, itemH), barBgTex);
        DrawRectOutline(new Rect(innerX, currY, innerW, itemH), 1, new Color(0.2f, 0.7f, 1f, 0.6f));
        GUI.Label(new Rect(innerX + 14, currY + 6, innerW * 0.65f, 26), $"2. 튕긴 횟수 ({gameController.stone.skipCount} 회)", scoreLeftStyle);
        GUI.Label(new Rect(innerX + innerW * 0.62f, currY + 6, innerW * 0.35f, 26), $"+{gameController.skipScore:N0} 점", scoreRightStyle);
        GUI.Label(new Rect(innerX + 14, currY + 32, innerW - 28, 24), "   (1회당 500점 적용)", scoreDetailStyle);

        currY += itemH + 8;

        // 4. 특별 이벤트 점수 항목
        float specH = 82f;
        GUI.DrawTexture(new Rect(innerX, currY, innerW, specH), barBgTex);
        DrawRectOutline(new Rect(innerX, currY, innerW, specH), 1, new Color(0.2f, 0.7f, 1f, 0.6f));
        GUI.Label(new Rect(innerX + 14, currY + 6, innerW * 0.65f, 26), "3. 특별 이벤트 보너스", scoreLeftStyle);
        GUI.Label(new Rect(innerX + innerW * 0.62f, currY + 6, innerW * 0.35f, 26), $"+{gameController.specialScore:N0} 점", scoreRightStyle);

        string eventDetail = $"   [PERFECT]: {gameController.perfectTimingCount}회  |  [저격]: {gameController.fishSnipeCount}마리  |  [추월]: {gameController.friendOvertakeCount}명";
        if (gameController.lastSkimBonusDist > 0.5f)
        {
            eventDetail += $"  |  [스키밍]: +{gameController.lastSkimBonusDist:F1}m";
        }
        GUI.Label(new Rect(innerX + 14, currY + 32, innerW - 28, 46), eventDetail, scoreDetailStyle);

        currY += specH + 10;

        // 5. 종합 점수 & 코인 보상 배너
        float totalH = 76f;
        GUI.DrawTexture(new Rect(innerX, currY, innerW, totalH), barBgTex);
        DrawRectOutline(new Rect(innerX, currY, innerW, totalH), 2, new Color(1f, 0.85f, 0.2f));
        GUI.Label(new Rect(innerX, currY + 6, innerW, 36), $"최종 점수 : {gameController.totalScore:N0} PTS", totalScoreStyle);
        GUI.Label(new Rect(innerX, currY + 42, innerW, 28), $"[보상]: +{gameController.earnedCoins:N0} COIN 획득!", subStyle);

        // 6. 하단 '다시 던지기' 및 '모드 선택' 버튼 2개
        float btnW = (innerW - 14f) * 0.5f;
        float btnH = 64f;
        float btnY = py + panelH - btnH - 18f;

        if (DrawResponsiveButton(new Rect(innerX, btnY, btnW, btnH), "[다시 던지기]", buttonStyle, new Color(0.15f, 0.85f, 0.95f), scale))
        {
            gameController.RestartGame();
        }

        if (DrawResponsiveButton(new Rect(innerX + btnW + 14f, btnY, btnW, btnH), "[모드 선택]", buttonStyle, new Color(1f, 0.78f, 0.2f), scale))
        {
            gameController.ReturnToModeSelect();
        }
    }

    private Texture2D MakeCircleTex(int diameter, Color strokeCol, Color fillCol, int strokeWidth = 3)
    {
        Texture2D tex = new Texture2D(diameter, diameter, TextureFormat.RGBA32, false);
        Color[] cols = new Color[diameter * diameter];
        float r = diameter * 0.5f;
        float innerR = r - strokeWidth;

        for (int y = 0; y < diameter; y++)
        {
            for (int x = 0; x < diameter; x++)
            {
                float dx = x - r;
                float dy = y - r;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);

                if (dist > r) cols[y * diameter + x] = Color.clear;
                else if (dist > innerR) cols[y * diameter + x] = strokeCol;
                else cols[y * diameter + x] = fillCol;
            }
        }

        tex.SetPixels(cols);
        tex.Apply();
        return tex;
    }

    private void DrawRectOutline(Rect r, int thickness, Color color)
    {
        Texture2D lineTex = MakeTex(1, 1, color);
        GUI.DrawTexture(new Rect(r.x, r.y, r.width, thickness), lineTex);
        GUI.DrawTexture(new Rect(r.x, r.y + r.height - thickness, r.width, thickness), lineTex);
        GUI.DrawTexture(new Rect(r.x, r.y, thickness, r.height), lineTex);
        GUI.DrawTexture(new Rect(r.x + r.width - thickness, r.y, thickness, r.height), lineTex);
    }
}
