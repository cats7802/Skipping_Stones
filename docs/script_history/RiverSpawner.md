# RiverSpawner

## 1. 개요 (Overview)
- **위치**: `Assets/Scripts/Gameplay/RiverSpawner.cs`
- **역할**: 물수제비 인게임(장거리/타겟 모드) 및 청크 릴레이 시, 물 표면에 부스트 패드, 바위 장애물, 튀어오르는 물고기, 친구 깃발, 연잎/연꽃 군락 등을 절차적으로 동적 스폰하는 게임플레이 스포너.

## 2. 불변 원칙 (Immutable Principles)
- **실시간 수면 Bounds 참조 (하드코딩 금지)**: 수면의 `BoxCollider`로부터 가로폭(`minX`, `maxX`), 세로길이(`minZ`, `maxZ`), 수면 높이(`waterY`)를 동적으로 획득하여 실제 물 영역 내에서만 스폰.
- **땅속 스폰 방지**: `IsValidWaterPosition` 레이캐스트를 통해 지형이 수면보다 높이 솟은 땅 위 스폰 차단.
- **모드별 격리**: `LongDistance`와 `TargetAccuracy` 모드별로 적합한 엔티티 패턴 유지.

## 3. 변경 이력 (Changelog)
- **2026-08-28**: 
  - `GetWaterColliderBounds`에 `minZ`, `maxZ` 반환 추가.
  - 고정 길이 대신 슬롯 맵 청크(500m 등 가변 지형)의 `autoChunkSize`를 실측하여 해당 1개 청크 지형 경계(`startZ + 20m ~ endZ - 20m`) 내에서만 엔티티 스폰.
  - `IsValidWaterPosition`을 전면 개편: 초고도 `RaycastAll`로 메쉬 지형(`MeshCollider`), 터레인 지형(`TerrainCollider`), 수면 콜라이더를 전수 감지하고 안전 수심(`waterDepth >= 0.35m`)을 확보하여 땅속 파묻힘 및 허공 스폰 100% 원천 차단.

