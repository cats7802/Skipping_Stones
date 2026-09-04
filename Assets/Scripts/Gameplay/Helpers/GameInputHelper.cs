using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace SkippingStones.Gameplay.Helpers
{
    /// <summary>
    /// 🎮 신구 Input System 및 모바일 터치/마우스 입력을 통합 캡슐화한 헬퍼 유틸리티
    /// </summary>
    public static class GameInputHelper
    {
        public static float GetHorizontalInput()
        {
            float h = 0f;
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null)
            {
                if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed) h -= 1f;
                if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed) h += 1f;
            }
#endif
            try
            {
                float legacyH = Input.GetAxisRaw("Horizontal");
                if (Mathf.Abs(legacyH) > 0.01f) h = legacyH;
            }
            catch { }
            return h;
        }

        public static bool IsActionTriggered(ref bool requireTouchRelease, float lastStateChangeTime, float cooldown)
        {
            if (Time.time - lastStateChangeTime < cooldown) return false;

            bool isCurrentlyHeld = false;
#if ENABLE_INPUT_SYSTEM
            if (Mouse.current != null && Mouse.current.leftButton.isPressed) isCurrentlyHeld = true;
            if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.isPressed) isCurrentlyHeld = true;
#endif
            try
            {
                if (Input.touchCount > 0 || Input.GetMouseButton(0)) isCurrentlyHeld = true;
            }
            catch { }

            if (requireTouchRelease)
            {
                if (!isCurrentlyHeld) requireTouchRelease = false;
                return false;
            }

#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null && (Keyboard.current.spaceKey.wasPressedThisFrame || Keyboard.current.enterKey.wasPressedThisFrame)) return true;
            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame) return true;
            if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame) return true;
#endif
            try
            {
                if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return) || Input.GetMouseButtonDown(0)) return true;
            }
            catch { }
            return false;
        }

        public static bool IsKeyTriggered(KeyCode key)
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null)
            {
                if (key == KeyCode.Space && Keyboard.current.spaceKey.wasPressedThisFrame) return true;
                if (key == KeyCode.Return && (Keyboard.current.enterKey.wasPressedThisFrame || Keyboard.current.numpadEnterKey.wasPressedThisFrame)) return true;
                if (key == KeyCode.R && Keyboard.current.rKey.wasPressedThisFrame) return true;
                if (key == KeyCode.Escape && Keyboard.current.escapeKey.wasPressedThisFrame) return true;
                if ((key == KeyCode.A || key == KeyCode.LeftArrow) && (Keyboard.current.aKey.wasPressedThisFrame || Keyboard.current.leftArrowKey.wasPressedThisFrame)) return true;
                if ((key == KeyCode.D || key == KeyCode.RightArrow) && (Keyboard.current.dKey.wasPressedThisFrame || Keyboard.current.rightArrowKey.wasPressedThisFrame)) return true;
                if ((key == KeyCode.S || key == KeyCode.DownArrow) && (Keyboard.current.sKey.wasPressedThisFrame || Keyboard.current.downArrowKey.wasPressedThisFrame)) return true;
            }
#endif
            try
            {
                if (Input.GetKeyDown(key)) return true;
            }
            catch { }
            return false;
        }

        public static bool GetPointerPress(out Vector2 position)
        {
            position = Vector2.zero;
#if ENABLE_INPUT_SYSTEM
            var touch = Touchscreen.current;
            var mouse = Mouse.current;
            if (touch != null && touch.primaryTouch.press.isPressed)
            {
                position = touch.primaryTouch.position.ReadValue();
                return true;
            }
            if (mouse != null && (mouse.leftButton.isPressed || mouse.rightButton.isPressed))
            {
                position = mouse.position.ReadValue();
                return true;
            }
#else
            if (Input.GetMouseButton(0) || Input.GetMouseButton(1))
            {
                position = Input.mousePosition;
                return true;
            }
#endif
            return false;
        }

        public static void GetPointerDownUp(out bool isDown, out bool isUp, out Vector2 position)
        {
            isDown = false;
            isUp = false;
            position = Vector2.zero;

#if ENABLE_INPUT_SYSTEM
            var touch = Touchscreen.current;
            var mouse = Mouse.current;
            if (touch != null && touch.primaryTouch.press.wasPressedThisFrame)
            {
                isDown = true;
                position = touch.primaryTouch.position.ReadValue();
            }
            else if (touch != null && touch.primaryTouch.press.wasReleasedThisFrame)
            {
                isUp = true;
                position = touch.primaryTouch.position.ReadValue();
            }
            else if (mouse != null && mouse.leftButton.wasPressedThisFrame)
            {
                isDown = true;
                position = mouse.position.ReadValue();
            }
            else if (mouse != null && mouse.leftButton.wasReleasedThisFrame)
            {
                isUp = true;
                position = mouse.position.ReadValue();
            }
#else
            try
            {
                if (Input.touchCount > 0)
                {
                    var t = Input.GetTouch(0);
                    if (t.phase == UnityEngine.TouchPhase.Began) { isDown = true; position = t.position; }
                    else if (t.phase == UnityEngine.TouchPhase.Ended || t.phase == UnityEngine.TouchPhase.Canceled) { isUp = true; position = t.position; }
                }
                else if (Input.GetMouseButtonDown(0)) { isDown = true; position = Input.mousePosition; }
                else if (Input.GetMouseButtonUp(0)) { isUp = true; position = Input.mousePosition; }
            }
            catch { }
#endif
        }
    }
}
