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

### [2026-08-30] 캐릭터 단일 인스턴스 보장 및 중복 인게임 찌꺼기 캐릭터 CleanUp 적용
- **수정 목적**: 로비에서 캐릭터를 변경하거나 새 매치를 시작할 때 이전 캐릭터가 파괴되지 않고 씬 원점(0,0,0)에 누적되어 +1씩 쌓이던 버그를 완전 해결.
- **핵심 구조**:
  - `SetupCharacter`: 동일한 캐릭터인 경우 기존 오브젝트를 재사용(`matchedCharacter`)하여 씬 부하 방지.
  - 다른 캐릭터로 전환 시 쇼케이스 더미를 제외한 모든 기존 인게임 캐릭터를 100% 파괴(`Destroy`) 후 단 1개만 인스턴스화.
- **컴파일 검증**: 0 Errors.




