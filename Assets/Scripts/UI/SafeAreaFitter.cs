using UnityEngine;

namespace SkippingStones.UI
{
    /// <summary>
    /// 모바일 기기별 노치(Notch), Dynamic Island, 홈 바(Home Bar)를 자동 감지하여
    /// Safe Area 내부로 RectTransform 앵커를 완벽 자동 피팅하는 표준 컴포넌트
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    [DisallowMultipleComponent]
    [ExecuteAlways]
    public class SafeAreaFitter : MonoBehaviour
    {
        [Header("적용 설정")]
        [SerializeField] private bool conformX = true;
        [SerializeField] private bool conformY = true;

        private RectTransform _rectTransform;
        private Rect _lastSafeArea = Rect.zero;
        private Vector2Int _lastScreenSize = Vector2Int.zero;
        private ScreenOrientation _lastOrientation = ScreenOrientation.AutoRotation;

        private void Awake()
        {
            _rectTransform = GetComponent<RectTransform>();
            ApplySafeArea();
        }

        private void OnEnable()
        {
            ApplySafeArea();
        }

        private void Update()
        {
            RefreshIfNeeded();
        }

        public void RefreshIfNeeded()
        {
            Rect currentSafeArea = Screen.safeArea;
            Vector2Int currentScreenSize = new Vector2Int(Screen.width, Screen.height);
            ScreenOrientation currentOrientation = Screen.orientation;

            if (currentSafeArea != _lastSafeArea || currentScreenSize != _lastScreenSize || currentOrientation != _lastOrientation)
            {
                ApplySafeArea();
            }
        }

        public void ApplySafeArea()
        {
            if (_rectTransform == null) _rectTransform = GetComponent<RectTransform>();
            if (_rectTransform == null) return;

            Rect safeArea = Screen.safeArea;
            float screenWidth = Screen.width;
            float screenHeight = Screen.height;

            if (screenWidth <= 0 || screenHeight <= 0) return;

            _lastSafeArea = safeArea;
            _lastScreenSize = new Vector2Int((int)screenWidth, (int)screenHeight);
            _lastOrientation = Screen.orientation;

            Vector2 anchorMin = safeArea.position;
            Vector2 anchorMax = safeArea.position + safeArea.size;

            if (conformX)
            {
                anchorMin.x /= screenWidth;
                anchorMax.x /= screenWidth;
            }
            else
            {
                anchorMin.x = 0f;
                anchorMax.x = 1f;
            }

            if (conformY)
            {
                anchorMin.y /= screenHeight;
                anchorMax.y /= screenHeight;
            }
            else
            {
                anchorMin.y = 0f;
                anchorMax.y = 1f;
            }

            _rectTransform.anchorMin = anchorMin;
            _rectTransform.anchorMax = anchorMax;
            _rectTransform.offsetMin = Vector2.zero;
            _rectTransform.offsetMax = Vector2.zero;
        }
    }
}
