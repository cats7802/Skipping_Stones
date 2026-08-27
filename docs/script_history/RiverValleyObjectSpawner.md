# RiverValleyObjectSpawner

## 1. 개요 (Overview)
- **위치**: `Assets/Scripts/Terrain/RiverValleyObjectSpawner.cs`
- **역할**: Unity `Terrain` 및 일반 `Mesh / GameObject` 지형 위에 나무, 바위, 식생, 프랍 등의 오브젝트를 절차적(Noise, Slope, River Offset, Collision Check)으로 자동 배치하는 에디터 스포너.

## 2. 불변 원칙 (Immutable Principles)
- **비파괴적 생성 & Undo 지원**: 프랍 생성 시 `Undo.RegisterCreatedObjectUndo` 적용 및 `ClearAllProps` 시 부모 컨테이너 정리.
- **물길 안전 구역 준수**: `RiverValleyTerrainGenerator` 또는 지정된 강 중심선 이격 거리(`minDistanceFromRiver`)를 통해 수면/물길 영역 내 불필요한 스폰 차단.
- **다중 지형 지원**: Unity `Terrain`과 일반 `Mesh/MeshCollider` 기반의 커스텀 지형 모두 원활히 샘플링 가능해야 함.
- **중복 겹침 방지**: `SpatialGrid` 해시를 통해 카테고리 내 및 카테고리 간 오브젝트가 겹치지 않도록 이격 거리 보장.

## 3. 변경 이력 (Changelog)
- **2026-08-27**: 
  - Unity `Terrain` 전용에서 일반 메쉬(`MeshCollider`/`Renderer` 기반) 지형 오브젝트까지 지원하도록 확장 (Terrain / Mesh Collider / Auto Detect 모드).
  - Raycast 기반 높이/법선/경사도 샘플링 및 레이어 마스크 필터 지원 추가.
