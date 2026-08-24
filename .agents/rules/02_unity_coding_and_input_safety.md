# 02. Unity C# Standards & Mobile Input Safety (STRICT)

## 1. Zero-Warning & Zero-Error Compilation Verification
- Immediately after writing or modifying C# code, **ALWAYS run compilation verification** (`dotnet build Assembly-CSharp.csproj` & `Assembly-CSharp-Editor.csproj`).
- Ensure 0 errors and 0 warnings (CS0618, etc.) before completing the turn.

## 2. No Hardcoding & Architecture Conventions
- Avoid hardcoding magic numbers (e.g. water height, river width, fixed offsets). Derive values dynamically from single sources of truth (`WaterSurface`, `RiverValleyTerrainGenerator`, `Presets`).
- Use modern Unity APIs (`FindAnyObjectByType`, `FindFirstObjectByType` instead of deprecated `FindObjectOfType`).

## 3. Mobile Touch & Button Safety Rules
- **No `isPressed` on UI Buttons**: Use exclusively single-frame down events (`wasPressedThisFrame`, `TouchPhase.Began`, `EventType.MouseDown`).
- **Touch-Release Lock & Debounce**: When switching screens/modals, enforce `requireTouchRelease = true` and a minimum 0.20s~0.25s cooldown to prevent touch bleed-through.
- **Single-Frame Event Consumption**: UI clicks must consume the event (`Event.current.Use()`) and mark pointer consumed for the frame.

## 4. Smart Prefab Fallback Architecture
- Components expecting prefabs must check inspector slots first, fallback to `Resources.Load<GameObject>()`, and provide non-intrusive fallbacks without polluting logs once prefabs exist.
