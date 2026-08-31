using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SkippingStones.UI
{
    /// <summary>
    /// 🎮 개별 터치 버튼의 다운(즉시 판정), 드래그(스와이프 감지), 업 라이프사이클 핸들러
    /// - TouchFlightController 또는 GameController 직접 연동
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
