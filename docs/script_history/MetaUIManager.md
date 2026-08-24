# 📜 Script History: MetaUIManager.cs

## 🎯 1. 역할 및 책임 (Core Responsibility)
- 타이틀 ➜ 로비 ➜ 맵선택 ➜ 인게임 ➜ 결과창 전체 메타 UI 상태 머신 및 모달(도감, 상점, 랭킹, 설정) 제어.
- 9:16 모바일 가상 720p 레터박스 반응형 버튼 및 New Input System 터치 처리.

## 🚫 2. 절대 금지 규칙 및 불변 표준 (Must NOT Do)
- ❌ **버튼에 `isPressed` 또는 연속 터치 상태 사용 금지** ➜ `wasPressedThisFrame`, `TouchPhase.Began` 단일 프레임 다운 신호만 사용.
- ❌ **화면/모달 전환 시 터치 블리드스루 방지** ➜ 반드시 `requireTouchRelease = true` 및 0.2초 디바운스 쿨다운 강제.
- ❌ **한글 텍스트 클리핑 방지** ➜ 720p 기준 충분한 라벨 높이와 `wordWrap = true` 유지.

## 🕒 3. 수정 및 진화 히스토리 (Change Log)
### [2026-08-22] 카카오 인증/게스트 로그인 및 반응형 버튼 엔진 이식 완료
- **수정 목적**: 메타 UI 전체 상태 전이 완성 및 즉각적인 터치 반응성 보장.
