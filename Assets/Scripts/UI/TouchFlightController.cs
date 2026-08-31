using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SkippingStones.UI
{
    /// <summary>
    /// 🎮 비행 중 하단 3버튼(좌 [◀], 중앙 [●], 우 [▶]) 터치 및 스와이프 컨트롤러
    /// - 중앙 [●]: 정면 0° 리듬 스킵 탭
    /// - 좌 [◀]: 단일 탭 -5° / 좌측 스와이프 시 -8° 조향
    /// - 우 [▶]: 단일 탭 +5° / 우측 스와이프 시 +8° 조향
    /// </summary>
    public class TouchFlightController : MonoBehaviour
    {
        public static TouchFlightController Instance { get; private set; }

        private Canvas controllerCanvas;
        private CanvasScaler canvasScaler;
        private GraphicRaycaster graphicRaycaster;

        private GameObject rootPanel;
        private RectTransform leftBtnRect;
        private RectTransform centerBtnRect;
        private RectTransform rightBtnRect;

        private GameController gameController;

        // 🌟 StoneSkippingUGUIController의 FlightHUDPanel 내부로 일원화되어 독립 Canvas 동적 생성 불필요
        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else if (Instance != this)
            {
                Destroy(gameObject);
            }
        }

        private void BuildUGUIElements()
        {
            controllerCanvas = gameObject.AddComponent<Canvas>();
            controllerCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            controllerCanvas.sortingOrder = 45; // InGame HUD보다 약간 아래

            canvasScaler = gameObject.AddComponent<CanvasScaler>();
            canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasScaler.referenceResolution = new Vector2(720, 1280);
            canvasScaler.matchWidthOrHeight = 0.5f;

            graphicRaycaster = gameObject.AddComponent<GraphicRaycaster>();

            // 루트 컨테이너 (화면 하단 중앙 Y=60)
            rootPanel = new GameObject("FlightTouchButtons_Container");
            rootPanel.transform.SetParent(transform, false);
            RectTransform rootRect = rootPanel.AddComponent<RectTransform>();
            rootRect.anchorMin = new Vector2(0.5f, 0f);
            rootRect.anchorMax = new Vector2(0.5f, 0f);
            rootRect.pivot = new Vector2(0.5f, 0f);
            rootRect.anchoredPosition = new Vector2(0f, 60f);
            rootRect.sizeDelta = new Vector2(560f, 120f);

            // 1. 스프라이트 리소스 로드 (Assets/Resources/ 또는 Assets/2D/UI/)
            Sprite spriteL = LoadButtonSprite("Touch_Button_L", "Assets/2D/UI/Touch_Button_L.png");
            Sprite spriteO = LoadButtonSprite("Touch_Button_O", "Assets/2D/UI/Touch_Button_O.png");
            Sprite spriteR = LoadButtonSprite("Touch_Button_R", "Assets/2D/UI/Touch_Button_R.png");

            // 2. 멀티플라이어 틴트 컬러 정의 (은은한 글래스 네온 & 누름 시 선명한 발광)
            Color normalTint = new Color(0.9f, 0.95f, 1.0f, 0.75f);
            Color pressedTint = new Color(0.4f, 0.85f, 1.0f, 1.0f);

            // 3. 좌측 버튼 [ ◀ ] (-5° / 스와이프 -8°)
            leftBtnRect = CreateDirectionButton(rootPanel.transform, "Btn_Left", new Vector2(-190f, 0f), spriteL, -5f, -8f, normalTint, pressedTint);

            // 4. 중앙 버튼 [ ● ] (0°)
            centerBtnRect = CreateDirectionButton(rootPanel.transform, "Btn_Center", new Vector2(0f, 0f), spriteO, 0f, 0f, normalTint, pressedTint);

            // 5. 우측 버튼 [ ▶ ] (+5° / 스와이프 +8°)
            rightBtnRect = CreateDirectionButton(rootPanel.transform, "Btn_Right", new Vector2(190f, 0f), spriteR, 5f, 8f, normalTint, pressedTint);

            rootPanel.SetActive(false);
        }

        private Sprite LoadButtonSprite(string resourceName, string editorPath)
        {
            Sprite sp = Resources.Load<Sprite>(resourceName);
#if UNITY_EDITOR
            if (sp == null)
            {
                sp = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(editorPath);
            }
#endif
            return sp;
        }

        private RectTransform CreateDirectionButton(Transform parent, string name, Vector2 pos, Sprite sprite, float baseAngle, float swipeAngle, Color normalColor, Color pressedColor)
        {
            GameObject btnObj = new GameObject(name);
            btnObj.transform.SetParent(parent, false);

            RectTransform rt = btnObj.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(120f, 120f);

            Image img = btnObj.AddComponent<Image>();
            if (sprite != null)
            {
                img.sprite = sprite;
                img.type = Image.Type.Simple;
                img.preserveAspect = true;
            }
            img.color = normalColor;

            // 터치/제스처 핸들러 추가
            FlightTouchButtonHandler handler = btnObj.AddComponent<FlightTouchButtonHandler>();
            handler.Init(this, baseAngle, swipeAngle, img, normalColor, pressedColor);

            return rt;
        }

        private void Update()
        {
            if (gameController == null)
            {
                gameController = FindAnyObjectByType<GameController>();
            }

            bool isFlying = (gameController != null && 
                            gameController.currentState == GameController.GameState.Flying);

            if (rootPanel != null && rootPanel.activeSelf != isFlying)
            {
                rootPanel.SetActive(isFlying);
            }
        }

        public void OnButtonActionTriggered(float angle)
        {
            if (gameController != null)
            {
                gameController.EvaluateRhythmTiming(angle);
            }
        }

        public void OnExtraSteerTriggered(float additionalAngle)
        {
            if (gameController != null && gameController.stone != null && !gameController.stone.isSunk && !gameController.stone.isCrashed)
            {
                gameController.stone.ApplySteerAngle(additionalAngle);
                gameController.lastTimingText += (additionalAngle > 0f) ? "\n👉 [SWIPE +3° 추가]" : "\n👈 [SWIPE -3° 추가]";
            }
        }
    }

    /// <summary>
    /// 개별 터치 버튼의 다운(즉시 판정), 드래그(스와이프 감지), 업 라이프사이클 핸들러
    /// </summary>
    public class FlightTouchButtonHandler : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IDragHandler
    {
        private TouchFlightController controller;
        private float baseAngle;
        private float swipeAngle;
        private Image buttonImage;
        private Color normalColor;
        private Color pressedColor;

        private Vector2 pointerDownPos;
        private float pointerDownTime;
        private bool hasTriggeredSwipeBonus = false;

        public void Init(TouchFlightController ctrl, float bAngle, float sAngle, Image img, Color nColor, Color pColor)
        {
            controller = ctrl;
            baseAngle = bAngle;
            swipeAngle = sAngle;
            buttonImage = img;
            normalColor = nColor;
            pressedColor = pColor;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            pointerDownPos = eventData.position;
            pointerDownTime = Time.unscaledTime;
            hasTriggeredSwipeBonus = false;

            if (buttonImage != null)
            {
                buttonImage.color = pressedColor;
                transform.localScale = new Vector3(0.92f, 0.92f, 1f);
            }

            // 1. 터치 즉시 기본 각도로 리듬 판정 실행 (0ms 지연)
            if (controller != null)
            {
                controller.OnButtonActionTriggered(baseAngle);
            }
            else if (GameController.Instance != null)
            {
                GameController.Instance.EvaluateRhythmTiming(baseAngle);
            }
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (hasTriggeredSwipeBonus || baseAngle == 0f) return;

            Vector2 delta = eventData.position - pointerDownPos;
            float duration = Time.unscaledTime - pointerDownTime;

            // 좌측 버튼이고 좌측으로 30px 이상 스와이프 or 우측 버튼이고 우측으로 30px 이상 스와이프
            bool isLeftSwipe = (baseAngle < 0f && delta.x < -30f);
            bool isRightSwipe = (baseAngle > 0f && delta.x > 30f);

            if (duration < 0.35f && (isLeftSwipe || isRightSwipe))
            {
                hasTriggeredSwipeBonus = true;
                float bonusSteer = (baseAngle > 0f) ? 3.0f : -3.0f;
                if (controller != null)
                {
                    controller.OnExtraSteerTriggered(bonusSteer);
                }
                else if (GameController.Instance != null && GameController.Instance.stone != null && 
                         !GameController.Instance.stone.isSunk && !GameController.Instance.stone.isCrashed)
                {
                    GameController.Instance.stone.ApplySteerAngle(bonusSteer);
                    GameController.Instance.lastTimingText += (bonusSteer > 0f) ? "\n👉 [SWIPE +3° 추가]" : "\n👈 [SWIPE -3° 추가]";
                }
            }
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (buttonImage != null)
            {
                buttonImage.color = normalColor;
            }
            transform.localScale = Vector3.one;
        }
    }
}
