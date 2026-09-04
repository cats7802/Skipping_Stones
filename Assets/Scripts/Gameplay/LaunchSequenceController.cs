using UnityEngine;
using SkippingStones.Gameplay.Helpers;

namespace SkippingStones.Gameplay
{
    /// <summary>
    /// 🎯 투구 전 단계(위치 슬라이드, 조준 각도 게이지, 파워 충전 게이지) 시퀀스를 전담하는 컨트롤러
    /// </summary>
    public class LaunchSequenceController
    {
        private float aimSpeed = 2.4f;
        private float powerSpeed = 3.0f;
        private float aimDirection = 1f;
        private float powerDirection = 1f;

        private bool isDraggingMap = false;
        private Vector2 prevDragPos;
        private bool isSwipingTarget = false;
        private Vector2 swipeStartPos;
        private float swipeThreshold = 35f;

        public void ResetGauges(out float aimGauge, out float powerGauge)
        {
            aimGauge = 0f;
            powerGauge = 0f;
            aimDirection = 1f;
            powerDirection = 1f;
            isDraggingMap = false;
            isSwipingTarget = false;
        }

        public float UpdatePositionSlide(float currentPosX, float minX, float maxX)
        {
            float hInput = GameInputHelper.GetHorizontalInput();
            if (Mathf.Abs(hInput) > 0.001f)
            {
                currentPosX = Mathf.Clamp(currentPosX + hInput * Time.deltaTime * 7.5f, minX, maxX);
            }

            if (GameInputHelper.GetPointerPress(out Vector2 curPos))
            {
                if (!isDraggingMap)
                {
                    isDraggingMap = true;
                    prevDragPos = curPos;
                }
                else
                {
                    float deltaX = (curPos.x - prevDragPos.x) * 0.016f;
                    currentPosX = Mathf.Clamp(currentPosX + deltaX, minX, maxX);
                    prevDragPos = curPos;
                }
            }
            else
            {
                isDraggingMap = false;
            }

            return currentPosX;
        }

        public void UpdateTargetSwipe(StoneThrowerCharacter character)
        {
            if (character == null) return;

            if (GameInputHelper.IsKeyTriggered(KeyCode.LeftArrow) || GameInputHelper.IsKeyTriggered(KeyCode.A))
            {
                character.MoveToPreviousWaypoint();
            }
            if (GameInputHelper.IsKeyTriggered(KeyCode.RightArrow) || GameInputHelper.IsKeyTriggered(KeyCode.D))
            {
                character.MoveToNextWaypoint();
            }

            if (GameInputHelper.GetPointerPress(out Vector2 curPos))
            {
                if (!isSwipingTarget)
                {
                    isSwipingTarget = true;
                    swipeStartPos = curPos;
                }
                else
                {
                    float dx = curPos.x - swipeStartPos.x;
                    if (dx > swipeThreshold)
                    {
                        character.MoveToNextWaypoint();
                        swipeStartPos = curPos;
                    }
                    else if (dx < -swipeThreshold)
                    {
                        character.MoveToPreviousWaypoint();
                        swipeStartPos = curPos;
                    }
                }
            }
            else
            {
                isSwipingTarget = false;
            }
        }

        public float UpdateAimingGauge(float currentVal)
        {
            currentVal += aimDirection * aimSpeed * Time.deltaTime;
            if (currentVal > 1f) { currentVal = 1f; aimDirection = -1f; }
            else if (currentVal < -1f) { currentVal = -1f; aimDirection = 1f; }
            return currentVal;
        }

        public float UpdatePowerGauge(float currentVal)
        {
            currentVal += powerDirection * powerSpeed * Time.deltaTime;
            if (currentVal > 1f) { currentVal = 1f; powerDirection = -1f; }
            else if (currentVal < 0f) { currentVal = 0f; powerDirection = 1f; }
            return currentVal;
        }
    }
}
