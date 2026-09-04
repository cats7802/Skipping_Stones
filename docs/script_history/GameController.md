# 📜 Script History: GameController.cs

## 🎯 1. 역할 및 책임 (Core Responsibility)
- 인게임 전체 상태 머신(`Positioning` ➜ `Angle` ➜ `Power` ➜ `Flying` ➜ `Skimming` ➜ `Replay` ➜ `ResultDirect`) 총괄 제어.
- 로비에서 전달된 `MatchSessionData`(선택 캐릭터, 조약돌, 맵, 게임 모드)의 런타임 인스턴스화 및 라이프사이클 오케스트레이션.
- 카메라(`DualCameraSetup`), 캐릭터(`StoneThrowerCharacter`), 수면 스포너(`RiverSpawner`), 리플레이(`TopDownReplayManager`) 간의 참조 자동 연결(`ResolveSceneReferences`).

## 🚫 2. 절대 금지 규칙 및 불변 표준 (Must NOT Do)
- ❌ **초기화 라이프사이클 순서 파괴 금지**:
  - 반드시 **`SetupCharacter` ➜ `SetupMapEnvironment` (지형/수면/콜라이더 배치 완료) ➜ `RiverSpawner`** 순서로 순차 실행하여 0점 쏠림 버그를 방지할 것.
- ❌ **게임 모드 전환 시 레거시 컴포넌트 잔존 금지**:
  - 장거리 모드와 타깃 모드 전환 시 발판, 타깃 경로, PIP 카메라를 정확히 활성/비활성화할 것.

### [2026-08-31] 하단 3버튼 터치 및 키보드 A/D/S 방향키 더블탭(5도/8도) 조향 조작계 개편
- **수정 목적**: 기존 모바일 스와이프 조작의 불명확성을 해소하고, PC/에디터 테스트 및 모바일 직관성을 위해 하단 3버튼(`[ ◀ ] [ ● ] [ ▶ ]`) 터치/스와이프 연계와 키보드 단일탭(`±5°`)/더블탭(`±8°`) 조향 체계 도입.
- **적용 내용**:
  - `TouchFlightController.cs`: 비행/스키밍 중 하단 3버튼 uGUI 자동 생성 및 터치 즉시 리듬 판정 + 스와이프 시 `+3°` 보너스 조향 연동.
  - `GameController.cs`: `UpdateFlying()`에서 키보드 `A/D/S`, `←/→/↓` 입력 및 0.25초 이내 더블 탭 판정 추가. 화면 전체 스와이프 시 5° / 8° 조향 각도 반영.
- **컴파일 검증**: `Assembly-CSharp.csproj`, `Editor.csproj` 경고 0개, 오류 0개 완료.

- ### [2026-09-04] 🏗️ GameController 모듈화 및 책임 분리 (GameInputHelper, MatchScoreCalculator, LaunchSequenceController)
  - **수정 목적**: 1,300줄에 달하던 `GameController.cs`의 복잡도를 낮추고 각 비즈니스 로직(입력 래퍼, 점수/보상 계산, 투구 조준 및 게이지 시퀀스)을 독립 모듈로 캡슐화.
  - **핵심 구조**:
    - `GameInputHelper`: 신구 Input System 및 모바일 터치 입력을 일원화한 정적 헬퍼.
    - `MatchScoreCalculator`: 거리/스킵/스킴/특수점수 및 코인 환산 순수 C# 도메인 계산기.
    - `LaunchSequenceController`: 발판 좌우 드래그, 타깃 모드 스와이프, 조준각/파워 왕복 게이지 연산 분리.
    - `GameController.cs`: 순수 중앙 오케스트레이터로서의 역할 유지 및 외부 API 100% 하위 호환성 보장.
  - **컴파일 검증**: 0 Errors, 0 Warnings 통과.
- ### [2026-09-01] 결과창 복귀 및 모드 전환 시 3D 텔레메트리 디버그 마커 완전 소멸 처리
- **수정 목적**: 장거리 모드에서 생성된 `[TAP_DEBUG]` 마커가 결과창 확인 후 맵 선택/로비 복귀 시 또는 타 모드 전환 시 씬에 남아있던 현상 해결.
- **적용 내용**:
  - `GameController.cs`: `FinishMatchAndReturnToMapSelect()` 및 `ResetToPositioning()`에 `SkippingStone.ClearAllTapDebugMarkers()` 호출 추가.
  - `LongDistanceModeHandler.cs`: `OnExitMode()`에 마커 일괄 제거 추가.
- **컴파일 검증**: 0 Errors, 0 Warnings.






