---
trigger: always_on
---

# UI Button Interaction & Touch Safety Rules (STRICT)

## 1. Zero Touch Bleed-Through & No `isPressed` on Buttons
- **NEVER** use continuous touch/click state (`isPressed`, `GetMouseButton(0)`, `TouchPhase.Moved/Stationary`) to trigger UI buttons or state transitions.
- **ALWAYS** require single-frame down events (`wasPressedThisFrame`, `TouchPhase.Began`, `EventType.MouseDown`) exclusively.

## 2. Mandatory Touch-Release Lock on Screen / Modal Transitions
- Whenever the game changes state, switches UI screens, opens/closes a modal, or starts a replay/result screen:
  - **ALWAYS** set `requireTouchRelease = true` and record `lastTransitionTime = Time.unscaledTime`.
  - **NEVER** allow any button on the new screen to trigger until the user has completely lifted their finger from the screen (`!isPressed` / `Input.touchCount == 0`).

## 3. Mandatory Transition Debounce Cooldown
- All UI buttons must enforce a minimum debounce cooldown (0.20s ~ 0.25s) after any state transition before accepting new click inputs.

## 4. Single-Frame Event Consumption
- When any button is clicked, it must immediately consume the input event (`Event.current.Use()`) and mark the frame's pointer as consumed (`pointerDownConsumedThisFrame = true`) so no underlying or overlapping elements receive the touch.

## 5. Mobile Typography & Clipping Prevention
- All UI text, titles, subtitles, banners, and score labels must have sufficient label heights and line spacing (with `wordWrap = true`) to ensure no text or Korean characters are clipped at virtual 720p resolution.
