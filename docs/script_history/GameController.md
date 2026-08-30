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

### [2026-08-30] 0번 청크(Brook_Start) 직속 발판(WoodenPier_Platform) 우선 바인딩 및 캐릭터 자동 스냅
- **수정 목적**: 새 맵 스폰 시 이전 캐시나 씬의 다른 비활성 더미를 발판으로 오인하여 발판이 꺼지고 캐릭터가 엉뚱한 위치로 밀려나던 문제를 완전 해결.
- **핵심 구조**:
  - `FindPlatformInScene()`: `LakeEnvironmentManager`의 0번 청크 하위에서 `WoodenPier_Platform` / `Lakeside_WoodenPier`를 1순위로 직속 탐색.
  - `ResolveSceneReferences()`: 맵 스폰 시마다 항상 최신 발판을 강제 재바인딩.
  - `SetupMapEnvironment()`: 발판 100% `SetActive(true)` 보장.
  - `PositionCharacterForMode()`: 발판 루트 및 자식 `BoxCollider`의 바운드를 정밀 측정하여 캐릭터를 발판 월드 상단 정중앙에 정확히 스냅.
- **컴파일 검증**: 0 Warnings, 0 Errors.



