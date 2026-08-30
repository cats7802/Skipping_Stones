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

### [2026-08-30] 캐릭터 루트(Root) 오브젝트 기반 완전 파괴 및 0점 누적 버그 원천 해결
- **수정 목적**: 맵 선택 후 재진입 시 `StoneThrowerCharacter`가 자식에 붙어있어 프리팹 이름 매칭에 실패하고, 이전 캐릭터가 원점(0,0,0)에 방치되어 +1씩 무한 누적되던 버그 완전 해결.
- **핵심 구조**:
  - `SetupCharacter`: 캐릭터 비교 및 파괴 단위를 `c.transform.root.gameObject` 기준으로 정밀화.
  - 일치하지 않거나 중복 생성된 찌꺼기 캐릭터의 최상위 루트 오브젝트를 100% 파괴(`Destroy(rootObj)`)하여 0점에 잔존하는 캐릭터를 완벽히 제거.
- **컴파일 검증**: 0 Errors.




