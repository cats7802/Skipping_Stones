# 📜 Script History: TouchFlightController.cs

## 🎯 1. 역할 및 책임 (Core Responsibility)
- 인게임 비행(`Flying`, `Skimming`) 단계에서 화면 하단에 `[ ◀ ] [ ● ] [ ▶ ]` 버튼 UI를 렌더링하고 사용자 입력을 처리.
- 중앙 `[ ● ]`: 정면 0° 리듬 스킵 판정.
- 좌/우 `[ ◀ ]`, `[ ▶ ]`: 단일 탭 시 `±5°` 기본 조향, 버튼 다운 후 외측 스와이프 시 `+3°` 추가 보너스 각도(`±8°`) 적용.
- `EventSystem.IsPointerOverGameObject()`와 연동하여 버튼 영역 터치와 화면 전체 제스처의 중복 입력을 분리 방지.

## 🚫 2. 절대 금지 규칙 및 불변 표준 (Must NOT Do)
- ❌ **터치 판정 딜레이 금지**:
  - 버튼 누름(`OnPointerDown`) 즉시 0ms 지연으로 `EvaluateRhythmTiming`을 호출해야 함 (손을 뗄 때 호출하지 말 것).
- ❌ **터치 관통 방지**:
  - `GraphicRaycaster`를 탑재하여 UI 터치 시 배경 제스처가 동시 트리거되지 않도록 차단.

## 🕒 3. 수정 및 진화 히스토리 (Change Log)

### [2026-08-31] 신규 컨트롤러 컴포넌트 생성
- 하단 3버튼 uGUI 동적 인스턴스화(`[RuntimeInitializeOnLoadMethod]`), 캔버스 및 레이아웃 설정.
- 단일 탭 / 스와이프 분기 핸들러(`FlightTouchButtonHandler`) 구현.
- `dotnet build` 0 경고 0 오류 검증 완료.
