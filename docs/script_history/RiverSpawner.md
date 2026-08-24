# 📜 Script History: RiverSpawner.cs

## 🎯 1. 역할 및 책임 (Core Responsibility)
- S자로 굽이치는 강 지형에 맞춰 부스트 패드, 바위 장애물, 점핑 물고기, 친구 깃발, 연잎 군락 등 수면 엔티티를 동적 스폰 및 관리.
- 게임 모드(`LongDistance` / `TargetAccuracy`)별 스폰 분기 및 청크 릴레이(`SpawnChunkEntities`) 지원.

## 🚫 2. 절대 금지 규칙 및 불변 표준 (Must NOT Do)
- ❌ **강폭/수면 높이 상수를 하드코딩하지 말 것** (`halfWaterW = 16f`, `curWaterY = 16f` 등 절대 금지 ➜ `RiverValleyTerrainGenerator` 및 수면 오브젝트에서 동적 취득).
- ❌ **단순 직선 좌표계(-16m~+16m)로 가두지 말 것** ➜ 강은 S자로 흐르므로 전체 수면 영역에 걸쳐 분산 시도 후 수직 레이캐스트(`IsValidWaterPosition`)를 통한 지형 회피 필터링 방식을 유지할 것.
- ❌ **지형 생성 완료 전 0,0,0 스폰 금지** ➜ `RiverValleyTerrainGenerator` 및 수면 메쉬의 준비 상태를 검증한 후 스폰할 것.

## 🕒 3. 수정 및 진화 히스토리 (Change Log)

### [2026-08-25] 프리팹 에셋 기반 배치 및 라이프사이클 순서 보장 (Golden v2.0)
- **수정 목적**: 런타임 `CreatePrimitive` 코드 생성 및 콘솔 프리팹 부재 경고를 완전히 제거하고, 정식 프리팹 기반 인스턴스화 및 `BG(지형/수면) -> Character -> RiverSpawner` 초기화 순서 확립.
- **핵심 구조**:
  - `WaterEntityPrefabGenerator.cs` 에디터 툴을 통해 수면 엔티티 6종(`BoostPad`, `ObstacleRock`, `TargetZone`, `FriendFlag`, `JumpingFish`, `LilyPadCluster`) 프리팹을 `Assets/Resources/`에 영구 저장.
  - `RiverSpawner.cs`에서 `Resources.Load` 프리팹을 캐싱하여 깨끗하게 `Instantiate` 배치.
  - `GameController.cs`에서 `SetupCharacter` 및 지형/수면 바인딩 후 `RiverSpawner`가 실행되도록 라이프사이클 순서 보장.


