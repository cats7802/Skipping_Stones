# 05. Chat Compactness & Session Duration Tracker (STRICT)

## 1. Compact Chat & UI Formatting
- Keep chat messages, implementation plans, and summaries concise and structured.
- Prevent excessively tall messages or modals that push action buttons off the screen.

## 2. Token Awareness & Efficiency
- Avoid reading full directories or large files repeatedly. Target specific line ranges and only open relevant files.

## 3. Mandatory 2-Hour Duration Tracker Notice
- Track session duration using incoming metadata timestamps.
- Whenever current session time **exceeds 2 hours (120 minutes)**:
  - **Append to the footer of EVERY response**:
    > ⏱️ **현재 세션 진행 시간**: [X시간 Y분] 경과 (컨텍스트 최적화 및 오류 방지를 위해 작업 정리 후 새 대화창 갱신을 권장합니다)
