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

### [2026-08-30] 타이틀 화면(Start) 인게임 맵 사전 스폰 꼼수 완전 삭제 및 수명주기 정석화
- **수정 목적**: 타이틀/모드선택 화면에서 500m 인게임 맵이 강제 생성되어 리소스 및 씬 라이프사이클을 파괴하던 임시 코드를 완전 제거.
- **핵심 구조**: `GameController.Start()`에서 `SetupMapEnvironment()` 호출을 제거하고 오직 `currentState = GameState.ModeSelect;`만 수행. 맵은 오직 맵 선택 후 `StartGameSession()` 시점에만 정상 인스턴스화됨.
- **컴파일 검증**: 0 Warnings, 0 Errors.


