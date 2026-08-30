using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace SkippingStones.UI
{
    /// <summary>
    /// 🌊 인게임 모멘텀 (스태미나/라이프) uGUI HUD 관리자
    /// - 화면 상단 중앙에 실시간 모멘텀 바 표시
    /// - 부드러운 게이지 Lerp 및 판정별 플로팅 텍스트 애니메이션
    /// - 0점 이하 침몰 위험 시 붉은색 경고 펄스
    /// </summary>
    public class InGameMomentumHUD : MonoBehaviour
    {
        public static InGameMomentumHUD Instance { get; private set; }

        private Canvas hudCanvas;
        private CanvasScaler canvasScaler;
        private GraphicRaycaster graphicRaycaster;

        private GameObject rootPanel;
        private Image bgBar;
        private Image fillBar;
        private Text momentumText;
        private Text floatingGradeText;

        private SkippingStone currentStone;
        private float displayedFillAmount = 0.6f;
        private Coroutine floatingTextCoroutine;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoInitialize()
        {
            if (Instance == null)
            {
                GameObject go = new GameObject("[InGameMomentumHUD]");
                Instance = go.AddComponent<InGameMomentumHUD>();
                DontDestroyOnLoad(go);
            }
        }

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
                BuildUGUIElements();
            }
            else if (Instance != this)
            {
                Destroy(gameObject);
            }
        }

        private void BuildUGUIElements()
        {
            // 1. Canvas 설정
            hudCanvas = gameObject.AddComponent<Canvas>();
            hudCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            hudCanvas.sortingOrder = 50;

            canvasScaler = gameObject.AddComponent<CanvasScaler>();
            canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasScaler.referenceResolution = new Vector2(720, 1280);
            canvasScaler.matchWidthOrHeight = 0.5f;

            graphicRaycaster = gameObject.AddComponent<GraphicRaycaster>();

            // 2. 루트 컨테이너 (화면 상단 중앙 Y=1180)
            rootPanel = new GameObject("MomentumBar_Container");
            rootPanel.transform.SetParent(transform, false);
            RectTransform rootRect = rootPanel.AddComponent<RectTransform>();
            rootRect.anchorMin = new Vector2(0.5f, 1f);
            rootRect.anchorMax = new Vector2(0.5f, 1f);
            rootRect.pivot = new Vector2(0.5f, 1f);
            rootRect.anchoredPosition = new Vector2(0f, -45f);
            rootRect.sizeDelta = new Vector2(360f, 48f);

            // 3. 배경 바 (반투명 다크 글래스)
            GameObject bgObj = new GameObject("BgBar");
            bgObj.transform.SetParent(rootPanel.transform, false);
            RectTransform bgRect = bgObj.AddComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.sizeDelta = Vector2.zero;

            bgBar = bgObj.AddComponent<Image>();
            bgBar.color = new Color(0.05f, 0.08f, 0.15f, 0.75f);

            // 4. 채움 게이지 바 (에메랄드/네온 시안)
            GameObject fillObj = new GameObject("FillBar");
            fillObj.transform.SetParent(rootPanel.transform, false);
            RectTransform fillRect = fillObj.AddComponent<RectTransform>();
            fillRect.anchorMin = new Vector2(0.02f, 0.12f);
            fillRect.anchorMax = new Vector2(0.98f, 0.88f);
            fillRect.sizeDelta = Vector2.zero;

            fillBar = fillObj.AddComponent<Image>();
            fillBar.type = Image.Type.Filled;
            fillBar.fillMethod = Image.FillMethod.Horizontal;
            fillBar.fillOrigin = 0; // Left to Right
            fillBar.fillAmount = 0.6f;
            fillBar.color = new Color(0.0f, 0.88f, 0.75f, 0.95f);

            // 5. 모멘텀 수치 텍스트
            GameObject textObj = new GameObject("MomentumValueText");
            textObj.transform.SetParent(rootPanel.transform, false);
            RectTransform textRect = textObj.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.sizeDelta = Vector2.zero;

            momentumText = textObj.AddComponent<Text>();
            momentumText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf") ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
            momentumText.fontSize = 20;
            momentumText.fontStyle = FontStyle.Bold;
            momentumText.alignment = TextAnchor.MiddleCenter;
            momentumText.color = Color.white;
            momentumText.text = "⚡ MOMENTUM: 6.0 / 10.0";

            // 6. 플로팅 판정 텍스트 (바 바로 아래 Y=-30)
            GameObject floatObj = new GameObject("FloatingGradeText");
            floatObj.transform.SetParent(rootPanel.transform, false);
            RectTransform floatRect = floatObj.AddComponent<RectTransform>();
            floatRect.anchorMin = new Vector2(0.5f, 0f);
            floatRect.anchorMax = new Vector2(0.5f, 0f);
            floatRect.pivot = new Vector2(0.5f, 1f);
            floatRect.anchoredPosition = new Vector2(0f, -10f);
            floatRect.sizeDelta = new Vector2(400f, 40f);

            floatingGradeText = floatObj.AddComponent<Text>();
            floatingGradeText.font = momentumText.font;
            floatingGradeText.fontSize = 26;
            floatingGradeText.fontStyle = FontStyle.Bold;
            floatingGradeText.alignment = TextAnchor.MiddleCenter;
            floatingGradeText.color = Color.green;
            floatingGradeText.text = "";

            rootPanel.SetActive(false);
        }

        private void Update()
        {
            if (currentStone == null || !currentStone.isThrown || currentStone.isSunk || currentStone.isCrashed)
            {
                currentStone = FindAnyObjectByType<SkippingStone>();
            }

            bool inGamePlaying = (currentStone != null && currentStone.isThrown && !currentStone.isSunk && !currentStone.isCrashed);
            if (rootPanel != null && rootPanel.activeSelf != inGamePlaying)
            {
                rootPanel.SetActive(inGamePlaying);
            }

            if (!inGamePlaying || currentStone == null) return;

            // 부드러운 게이지 Lerp
            float targetFill = Mathf.Clamp01(currentStone.currentMomentum / currentStone.maxMomentum);
            displayedFillAmount = Mathf.Lerp(displayedFillAmount, targetFill, Time.deltaTime * 8f);

            if (fillBar != null)
            {
                fillBar.fillAmount = displayedFillAmount;

                // 게이지 잔량에 따른 색상 전환 (정상: 시안, 위험: 오렌지/레드 펄스)
                if (currentStone.currentMomentum <= 2.5f)
                {
                    float pulse = (Mathf.Sin(Time.time * 12f) + 1f) * 0.5f;
                    fillBar.color = Color.Lerp(new Color(1f, 0.2f, 0.2f, 0.95f), new Color(1f, 0.6f, 0.1f, 0.95f), pulse);
                }
                else if (currentStone.currentMomentum <= 5.0f)
                {
                    fillBar.color = new Color(0.95f, 0.85f, 0.2f, 0.95f);
                }
                else
                {
                    fillBar.color = new Color(0.0f, 0.88f, 0.75f, 0.95f);
                }
            }

            if (momentumText != null)
            {
                momentumText.text = $"⚡ MOMENTUM: {currentStone.currentMomentum:F1} / {currentStone.maxMomentum:F0}";
            }
        }

        public void TriggerGradePopup(string gradeText)
        {
            if (floatingGradeText == null) return;
            if (floatingTextCoroutine != null) StopCoroutine(floatingTextCoroutine);
            floatingTextCoroutine = StartCoroutine(AnimateFloatingText(gradeText));
        }

        private IEnumerator AnimateFloatingText(string gradeText)
        {
            floatingGradeText.text = gradeText;

            Color targetColor = Color.green;
            if (gradeText.Contains("PERFECT")) targetColor = new Color(0.2f, 1f, 0.3f, 1f);
            else if (gradeText.Contains("GREAT")) targetColor = new Color(0.2f, 0.9f, 1f, 1f);
            else if (gradeText.Contains("GOOD")) targetColor = new Color(1f, 0.9f, 0.2f, 1f);
            else if (gradeText.Contains("TOO EARLY")) targetColor = new Color(1f, 0.6f, 0.1f, 1f);
            else if (gradeText.Contains("LATE")) targetColor = new Color(0.9f, 0.3f, 1f, 1f);
            else targetColor = new Color(1f, 0.25f, 0.25f, 1f);

            floatingGradeText.color = targetColor;

            RectTransform rt = floatingGradeText.rectTransform;
            Vector2 startPos = new Vector2(0f, -8f);
            Vector2 endPos = new Vector2(0f, -28f);

            float elapsed = 0f;
            float duration = 0.75f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                rt.anchoredPosition = Vector2.Lerp(startPos, endPos, Mathf.SmoothStep(0f, 1f, t));
                Color c = targetColor;
                c.a = Mathf.Clamp01((1f - t) * 1.5f);
                floatingGradeText.color = c;
                yield return null;
            }

            floatingGradeText.text = "";
        }
    }
}
