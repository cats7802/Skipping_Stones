# Token Awareness & Quota Efficiency Rules (STRICT)

## 1. Token Quota Monitoring Awareness
- **User Preference**: The user wants to be notified whenever their Weekly Token drops below 1% or 5-Hour rolling token drops below 5%.
- **System Nature**: The AI model cannot directly query the IDE account's external billing/quota meter API. However, whenever quota indicators, token status, or rate-limit warnings are reported by the user or detected:
  - Immediately alert the user to check their IDE quota meter.
  - Advise switching to a fresh session or saving context to `docs/user_inquiry_log.md`.

## 2. Token Preservation Guidelines
- Always keep responses, analysis, and diffs compact and token-efficient.
- Strictly avoid reprinting entire files or redundant explanations.
- Prioritize updating `docs/user_inquiry_log.md` so work can be seamlessly continued across sessions without token loss.
