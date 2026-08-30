using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using SkippingStones.Terrain;

public class EnvironmentTestHelper : MonoBehaviour
{
    private static EnvironmentTestHelper _instance;
    public static EnvironmentTestHelper Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindAnyObjectByType<EnvironmentTestHelper>();
            }
            return _instance;
        }
    }

    [Header("테스트 UI 표시 여부 (F1 키로 토글)")]
    public bool showTestUI = false;
    public bool isAutoFlying = false;

    private float simulatedDistance = 0f;

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(this);
            return;
        }
        _instance = this;
    }

    private void Update()
    {
        // 🌟 키보드 숫자키 단축키 지원 (상단 숫자키 1~4: 프리뷰 전용)
        bool press1 = false, press2 = false, press3 = false, press4 = false, pressF1 = false;

#if ENABLE_INPUT_SYSTEM
        var keyboard = Keyboard.current;
        if (keyboard != null)
        {
            if (keyboard.digit1Key.wasPressedThisFrame || keyboard.numpad1Key.wasPressedThisFrame) press1 = true;
            if (keyboard.digit2Key.wasPressedThisFrame || keyboard.numpad2Key.wasPressedThisFrame) press2 = true;
            if (keyboard.digit3Key.wasPressedThisFrame || keyboard.numpad3Key.wasPressedThisFrame) press3 = true;
            if (keyboard.digit4Key.wasPressedThisFrame || keyboard.numpad4Key.wasPressedThisFrame) press4 = true;
            if (keyboard.f1Key.wasPressedThisFrame) pressF1 = true;
        }
#endif

        try
        {
            if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1)) press1 = true;
            if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2)) press2 = true;
            if (Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Keypad3)) press3 = true;
            if (Input.GetKeyDown(KeyCode.Alpha4) || Input.GetKeyDown(KeyCode.Keypad4)) press4 = true;
            if (Input.GetKeyDown(KeyCode.F1)) pressF1 = true;
        }
        catch { }

        if (press1) SetPreviewDistance(0f);
        if (press2) SetPreviewDistance(2000f);
        if (press3) SetPreviewDistance(3600f);
        if (press4) SetPreviewDistance(4800f);
        if (pressF1) showTestUI = !showTestUI;
    }

    public void SetPreviewDistance(float dist)
    {
        simulatedDistance = dist;
        if (LakeEnvironmentManager.Instance != null)
        {
            LakeEnvironmentManager.Instance.UpdateEnvironmentByDistance(dist);
        }
        Debug.Log($"[EnvironmentTestHelper] 🌍 환경 미리보기 비거리 설정: {dist:F0}m");
    }

    public void StopAutoFly()
    {
        StopAllCoroutines();
        isAutoFlying = false;
        var gc = GameController.Instance != null ? GameController.Instance : FindAnyObjectByType<GameController>();
        if (gc != null)
        {
            gc.devGodMode = false;
            if (gc.stone != null)
            {
                gc.stone.isGodMode = false;
            }
        }
    }

    public void ToggleAutoFlyGodMode()
    {
        var gc = GameController.Instance != null ? GameController.Instance : FindAnyObjectByType<GameController>();
        if (gc == null) return;

        if (isAutoFlying || gc.devGodMode)
        {
            StopAutoFly();
        }
        else
        {
            showTestUI = false;
            StartAutoFlyNative();
        }
    }

    public void StartAutoFlyNative()
    {
        var gc = GameController.Instance != null ? GameController.Instance : FindAnyObjectByType<GameController>();
        if (gc == null) return;

        isAutoFlying = true;
        gc.devGodMode = true;
        gc.devGodModeTargetDistance = 1500f;

        if (SkippingStones.UI.MetaUIManager.Instance != null)
        {
            SkippingStones.UI.MetaUIManager.Instance.ShowScreen(SkippingStones.UI.MetaScreen.InGame);
        }

        if (gc.currentState == GameController.GameState.ModeSelect || gc.currentState == GameController.GameState.Result)
        {
            gc.StartGameSession(null, null, null, GameController.GameMode.LongDistance);
        }
        else if (gc.currentState == GameController.GameState.Positioning || gc.currentState == GameController.GameState.AimingAngle || gc.currentState == GameController.GameState.ChargingPower)
        {
            gc.LaunchStone();
        }
        else if (gc.stone != null)
        {
            gc.stone.isGodMode = true;
            gc.stone.godModeTargetDistance = 1500f;
        }
    }
}
