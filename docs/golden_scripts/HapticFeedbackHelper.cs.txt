using UnityEngine;

public static class HapticFeedbackHelper
{
#if UNITY_ANDROID && !UNITY_EDITOR
    private static AndroidJavaObject vibrator;
    private static AndroidJavaClass vibrationEffectClass;
    private static bool isInitialized = false;

    private static void InitializeAndroidHaptics()
    {
        if (isInitialized) return;
        try
        {
            using (AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (AndroidJavaObject currentActivity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
            {
                vibrator = currentActivity.Call<AndroidJavaObject>("getSystemService", "vibrator");
                vibrationEffectClass = new AndroidJavaClass("android.os.VibrationEffect");
            }
            isInitialized = true;
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[HapticFeedbackHelper] Android 진동 초기화 예외: {ex.Message}");
        }
    }

    private static void VibrateAndroid(long milliseconds, int amplitude)
    {
        InitializeAndroidHaptics();
        if (vibrator != null)
        {
            try
            {
                // Android 8.0 (API 26) 이상: VibrationEffect.createOneShot
                if (vibrationEffectClass != null)
                {
                    AndroidJavaObject effect = vibrationEffectClass.CallStatic<AndroidJavaObject>("createOneShot", milliseconds, amplitude);
                    vibrator.Call("vibrate", effect);
                    return;
                }
            }
            catch
            {
                // Fallback for older API or device specific issues
            }

            try
            {
                vibrator.Call("vibrate", milliseconds);
            }
            catch
            {
                Handheld.Vibrate();
            }
        }
        else
        {
            Handheld.Vibrate();
        }
    }
#endif

    /// <summary>
    /// 가벼운 탭 진동 (15ms) - 터치 입력, 조준, UI 버튼 클릭
    /// </summary>
    public static void TriggerLightTap()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        VibrateAndroid(15, 80); // 약한 강도 (80/255)
#elif UNITY_IOS && !UNITY_EDITOR
        Handheld.Vibrate();
#endif
    }

    /// <summary>
    /// 경쾌한 타격 진동 (35ms) - 일반 및 GOOD 물수제비 수면 바운스
    /// </summary>
    public static void TriggerMediumBounce()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        VibrateAndroid(35, 160); // 중간 강도 (160/255)
#elif UNITY_IOS && !UNITY_EDITOR
        Handheld.Vibrate();
#endif
    }

    /// <summary>
    /// 묵직한 쾌감 임팩트 진동 (40ms + 60ms) - PERFECT 타이밍 바운스 및 부스트 패드 적중
    /// </summary>
    public static void TriggerPerfectImpact()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        VibrateAndroid(55, 255); // 최대 강도 (255/255)
#elif UNITY_IOS && !UNITY_EDITOR
        Handheld.Vibrate();
#endif
    }

    /// <summary>
    /// 부드러운 침몰 진동 (75ms) - 돌멩이가 가라앉을 때
    /// </summary>
    public static void TriggerSink()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        VibrateAndroid(75, 100);
#elif UNITY_IOS && !UNITY_EDITOR
        Handheld.Vibrate();
#endif
    }
}
