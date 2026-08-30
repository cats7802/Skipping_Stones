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

## 🕒 3. 수정 및 진화 히스토리 (Change Log)

### [2026-08-30] 로비/맵선택 패스 인게임 즉시 시작(autoStartInGame) 개발자 모드 추가
- **수정 목적**: 핵심 조작/물리 메카닉 집중 테스트(샌드박스 씬 등) 시 로비와 맵선택 과정을 건너뛰고 실행 즉시 투척 대기(`Positioning`) 상태로 진입할 수 있는 개발자 토글 옵션 추가.
- **적용 내용**:
  - `GameController.cs` 인스펙터에 `autoStartInGame` 옵션 추가.
  - `Start()`에서 `autoStartInGame == true`일 때 `MetaUIManager` 비활성화 및 기본 세션으로 `SelectGameMode(currentMode)`를 자동 트리거하여 즉시 인게임 시작.
- **컴파일 검증**: 0 Errors.





