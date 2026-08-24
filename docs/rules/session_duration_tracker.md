# Session Duration Tracking & 2-Hour Refresh Alert Rule (STRICT)

## 1. Context Health & Duration Tracking
- Prolonged conversations lead to context saturation, performance degradation, and increased regression risk.
- The agent must track elapsed time from the first turn of the current conversation using the incoming metadata local timestamps.

## 2. Mandatory Footer Notice after 2 Hours (120 Minutes)
- Whenever the elapsed time of the current session **exceeds 2 hours (120 minutes)**:
  - The agent **MUST append a compact duration notice at the very bottom (footer) of EVERY response**.
  - **Footer Format**:
    > ⏱️ **현재 세션 진행 시간**: [X시간 Y분] 경과 (컨텍스트 최적화 및 오류 방지를 위해 작업 정리 후 새 대화창 갱신을 권장합니다)
