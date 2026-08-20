# Prefab Smart Fallback & Chat Notification Rules (STRICT)

## 1. Unified 3-Step Prefab Fallback Architecture
All in-game dynamically spawned entities (`BoostPad`, `ObstacleRock`, `JumpingFish`, `FriendFlag`, `TargetZone`, `Stone`, etc.) must strictly adhere to the unified 3-step hierarchy:
1. **User Resources Prefab First**: Check if `Resources.Load<GameObject>("PrefabName")` exists. If found, instantiate user prefab with 0 temporary primitive generation.
2. **User Scene Child Mesh Second**: If `transform.childCount > 0`, respect user's pre-configured hierarchy 100%.
3. **Smart Fallback + Notification**: Only when both are absent, create temporary fallback visuals and emit a standardized warning:
   `Debug.LogWarning("💡 [프리팹 알림] 'EntityName'에 3D 프리팹이 없어 임시 더미로 자동 생성했습니다. (Assets/Resources/EntityName.prefab 등록 시 자동 대체)");`

## 2. Mandatory Proactive Chat Briefing
Whenever editor logs, compile logs, or runtime diagnostics are processed:
- Proactively summarize any `[프리팹 알림]` occurrences directly in the IDE chat response.
- Provide clear, actionable instructions for how the user can replace the temporary dummy with their own 3D model whenever they are ready.
