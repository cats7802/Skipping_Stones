using System;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace SkippingStones.Visuals.Replay
{
    /// <summary>
    /// 🎥 리플레이 탑다운 카메라 조작 및 내비게이션 전담 모듈
    /// - PC: 마우스 휠 부호 기반 줌, 마우스 버튼 드래그 무제한 자유 패닝
    /// - 모바일: 2터치 핀치 줌, 1터치 드래그 무제한 자유 패닝
    /// </summary>
    public class ReplayCameraController
    {
        public Vector3 CurrentCamCenter { get; set; } = Vector3.zero;
        public float CurrentOrthoSize { get; private set; } = 40f;
        public float TargetOrthoSize { get; set; } = 40f;

        public float MinOrthoSize { get; set; } = 8f;
        public float MaxOrthoSize { get; set; } = 400f;

        private Vector2 lastMousePos;
        private bool isMouseDragging = false;

        public void Initialize(Vector3 startCenter, float initialOrtho)
        {
            CurrentCamCenter = startCenter;
            TargetOrthoSize = initialOrtho;
            CurrentOrthoSize = initialOrtho;
            isMouseDragging = false;
        }

        public void UpdateNavigation(DualCameraSetup dualCam, float baseReplayLevel, Action<float> onTerrainSync, Action<float> onVisualScale)
        {
            if (dualCam == null || dualCam.mainCam == null) return;

            float screenH = Mathf.Max(Screen.height, 100f);
            float worldPerPixel = (CurrentOrthoSize * 2f) / screenH;

            // 1. 모바일 터치 처리 (핀치 줌 & 1터치 패닝)
#if ENABLE_INPUT_SYSTEM
            if (Touchscreen.current != null && Touchscreen.current.touches.Count > 0)
            {
                int tCount = 0;
                Vector2 t0Pos = Vector2.zero, t0Delta = Vector2.zero;
                Vector2 t1Pos = Vector2.zero, t1Delta = Vector2.zero;

                for (int i = 0; i < Touchscreen.current.touches.Count; i++)
                {
                    var touchControl = Touchscreen.current.touches[i];
                    if (touchControl.isInProgress)
                    {
                        if (tCount == 0)
                        {
                            t0Pos = touchControl.position.ReadValue();
                            t0Delta = touchControl.delta.ReadValue();
                            tCount++;
                        }
                        else if (tCount == 1)
                        {
                            t1Pos = touchControl.position.ReadValue();
                            t1Delta = touchControl.delta.ReadValue();
                            tCount++;
                            break;
                        }
                    }
                }

                if (tCount == 1 && t0Pos.y > screenH * 0.16f)
                {
                    Vector3 cam = CurrentCamCenter;
                    cam.x -= t0Delta.x * worldPerPixel;
                    cam.z -= t0Delta.y * worldPerPixel;
                    CurrentCamCenter = cam;
                }
                else if (tCount == 2)
                {
                    Vector2 prevP0 = t0Pos - t0Delta;
                    Vector2 prevP1 = t1Pos - t1Delta;
                    float prevDist = (prevP0 - prevP1).magnitude;
                    float currDist = (t0Pos - t1Pos).magnitude;
                    float delta = currDist - prevDist;

                    TargetOrthoSize = Mathf.Clamp(TargetOrthoSize - delta * (TargetOrthoSize * 0.0035f), MinOrthoSize, MaxOrthoSize);
                }
            }
#endif

            // 2. PC 마우스 처리 (부호 기반 휠 줌 & 전 버튼 드래그 패닝)
            float scrollVal = 0f;
            Vector2 mousePos = Vector2.zero;
            bool isMouseDown = false;

#if ENABLE_INPUT_SYSTEM
            if (Mouse.current != null)
            {
                Vector2 s = Mouse.current.scroll.ReadValue();
                if (Mathf.Abs(s.y) > 0.01f)
                {
                    scrollVal = Mathf.Sign(s.y);
                }

                mousePos = Mouse.current.position.ReadValue();
                isMouseDown = Mouse.current.leftButton.isPressed || Mouse.current.rightButton.isPressed || Mouse.current.middleButton.isPressed;
            }
#endif

            // Legacy Input 동시 감지 (백업)
            try
            {
                Vector2 legacyScroll = Input.mouseScrollDelta;
                if (Mathf.Abs(legacyScroll.y) > 0.01f && Mathf.Abs(scrollVal) <= 0.01f)
                {
                    scrollVal = Mathf.Sign(legacyScroll.y);
                }
            }
            catch { /* 무시 */ }

            // 휠 줌 적용 (1클릭당 12% 줌인/줌아웃)
            if (Mathf.Abs(scrollVal) > 0.01f)
            {
                TargetOrthoSize = Mathf.Clamp(TargetOrthoSize - scrollVal * (TargetOrthoSize * 0.12f), MinOrthoSize, MaxOrthoSize);
            }

            // 마우스 드래그 패닝
            if (isMouseDown && mousePos.y > screenH * 0.16f)
            {
                if (!isMouseDragging)
                {
                    isMouseDragging = true;
                    lastMousePos = mousePos;
                }
                else
                {
                    Vector2 delta = mousePos - lastMousePos;
                    Vector3 cam = CurrentCamCenter;
                    cam.x -= delta.x * worldPerPixel;
                    cam.z -= delta.y * worldPerPixel;
                    CurrentCamCenter = cam;
                    lastMousePos = mousePos;
                }
            }
            else
            {
                isMouseDragging = false;
            }

            // 🌟 100% 무제한 자유 패닝 및 부드러운 줌 보간
            CurrentOrthoSize = Mathf.Lerp(CurrentOrthoSize, TargetOrthoSize, Time.unscaledDeltaTime * 16f);
            onVisualScale?.Invoke(CurrentOrthoSize);

            dualCam.SetReplayTopDownView(new Vector3(CurrentCamCenter.x, baseReplayLevel + 80f, CurrentCamCenter.z), CurrentOrthoSize);
            onTerrainSync?.Invoke(CurrentCamCenter.z);
        }
    }
}
