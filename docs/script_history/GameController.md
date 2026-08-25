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

### [2026-08-25] 투척 발판(`Platform`) & 타깃 위치(`Player_Position`) 프리팹 통합 탐색 표준화
- **수정 목적**: 배경 프리팹 내부 포함 구조 전환에 맞추어 `Platform` 및 `Player_Position`의 루트/자식 다중 Fallback 탐색 및 복제 청크 정리 지원.
- **핵심 구조**:
  - `FindPlatformInScene()`: `Lakeside_Platform`, `Platform`, `Lakeside_WoodenPier`, `Pier` 다중 탐색 지원.
  - `FindPlayerPositionRootInScene()`: `Player_Position`, `PlayerPosition` 탐색 지원.
  - 프로퍼티 `currentLaunchPlatform` 정식 도입 및 `currentLaunchPier` 하위 호환 유지.
  - Unity 6 `FindObjectsByType` CS0618 경고 100% 제거.
