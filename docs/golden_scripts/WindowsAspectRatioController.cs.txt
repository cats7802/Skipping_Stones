using UnityEngine;

/// <summary>
/// 🌟 Windows PC 스탠드얼론 전용: 9:16 모바일 세로 종횡비 실시간 스마트 스냅 컨트롤러
/// ⚠️ 모바일(Android/iOS) 및 유니티 에디터에서는 코드가 컴파일에서 완전 제외되어 부작용 0% 보장!
/// </summary>
public class WindowsAspectRatioController : MonoBehaviour
{
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
    private const float TARGET_ASPECT = 9f / 16f; // 0.5625 (9:16 세로)
    private int lastWidth = 0;
    private int lastHeight = 0;
    private float resizeDebounceTimer = 0f;

    private void Awake()
    {
        // 시작 시 540x960 9:16 창모드로 깔끔하게 실행
        Screen.fullScreenMode = FullScreenMode.Windowed;
        Screen.SetResolution(540, 960, FullScreenMode.Windowed);
        lastWidth = 540;
        lastHeight = 960;
    }

    private void Update()
    {
        // 1. 전체화면 전환 단축키(Alt+Enter 등) 차단 ➔ 항상 창모드 유지
        if (Screen.fullScreen)
        {
            Screen.fullScreen = false;
            Screen.fullScreenMode = FullScreenMode.Windowed;
        }

        // 2. 사용자가 마우스로 창 크기를 조절했을 때 실시간 감지
        if (Screen.width != lastWidth || Screen.height != lastHeight)
        {
            resizeDebounceTimer += Time.unscaledDeltaTime;
            if (resizeDebounceTimer > 0.15f) // 0.15초 드래그 정지 시 스냅
            {
                ApplyAspectSnap();
                resizeDebounceTimer = 0f;
            }
        }
        else
        {
            resizeDebounceTimer = 0f;
        }
    }

    private void ApplyAspectSnap()
    {
        int curW = Screen.width;
        int curH = Screen.height;

        // 세로 높이(Height) 기준으로 가로 너비를 9:16 비율로 자동 맞춤
        int targetW = Mathf.RoundToInt(curH * TARGET_ASPECT);
        int targetH = curH;

        // 최소/최대 안전 크기 제한 (최소 360x640, 최대 1080x1920)
        targetW = Mathf.Clamp(targetW, 360, 1080);
        targetH = Mathf.Clamp(targetH, 640, 1920);

        if (Mathf.Abs(curW - targetW) > 3 || Mathf.Abs(curH - targetH) > 3)
        {
            Screen.SetResolution(targetW, targetH, FullScreenMode.Windowed);
            lastWidth = targetW;
            lastHeight = targetH;
        }
        else
        {
            lastWidth = curW;
            lastHeight = curH;
        }
    }
#endif
}
