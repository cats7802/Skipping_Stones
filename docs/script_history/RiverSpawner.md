# RiverSpawner

## 1. 개요 (Overview)
- **위치**: `Assets/Scripts/Gameplay/RiverSpawner.cs`
- **역할**: 물수제비 인게임(장거리/타겟 모드) 및 청크 릴레이 시, 물 표면에 부스트 패드, 바위 장애물, 튀어오르는 물고기, 친구 깃발, 연잎/연꽃 군락 등을 절차적으로 동적 스폰하는 게임플레이 스포너.

## 2. 불변 원칙 (Immutable Principles)
- **실시간 수면 Bounds 참조 (하드코딩 금지)**: 수면의 `BoxCollider`로부터 가로폭(`minX`, `maxX`), 세로길이(`minZ`, `maxZ`), 수면 높이(`waterY`)를 동적으로 획득하여 실제 물 영역 내에서만 스폰.
- **땅속 스폰 방지**: `IsValidWaterPosition` 레이캐스트를 통해 지형이 수면보다 높이 솟은 땅 위 스폰 차단.
- **모드별 격리**: `LongDistance`와 `TargetAccuracy` 모드별로 적합한 엔티티 패턴 유지.

## 3. 변경 이력 (Changelog)
- **2026-08-30**: 강폭(Width) 비례 적응형 밀도 제어 및 반경 겹침 방지 시스템 구축
  - **수정 목적**: 좁은 협곡 구간에서 다수의 오브젝트가 겹치거나 통로를 가로막는 문제를 원천 차단.
  - **핵심 구조**:
    - 강폭 < 15m (협곡): 부스트 패드/물고기 1개만 중앙 스폰, 바위는 측면 1개만 확률 스폰하여 최소 50% 이상의 안전 탈출 통로 확보.
    - 강폭 15m ~ 25m: 1~2개 분산 스폰.
    - 강폭 >= 25m: 2~3개 좌/중/우 분산 스폰.
    - `HasNearbySpawnedEntity`: 반경 3.8m~4.2m 이내에 이미 다른 스폰 엔티티가 존재하면 겹침 방지(스킵).
  - **컴파일 검증**: 0 Warnings, 0 Errors.
- **2026-08-28**: 
  - `GetWaterColliderBounds`에 `minZ`, `maxZ` 반환 추가.
  - 고정 길이 대신 슬롯 맵 청크(500m 등 가변 지형)의 `autoChunkSize`를 실측하여 해당 1개 청크 지형 경계(`startZ + 20m ~ endZ - 20m`) 내에서만 엔티티 스폰.
  - `IsValidWaterPosition`을 전면 개편: 초고도 `RaycastAll`로 메쉬 지형(`MeshCollider`), 터레인 지형(`TerrainCollider`), 수면 콜라이더를 전수 감지하고 안전 수심(`waterDepth >= 0.35m`)을 확보하여 땅속 파묻힘 및 허공 스폰 100% 원천 차단.
