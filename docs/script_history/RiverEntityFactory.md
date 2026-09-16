# RiverEntityFactory

## 1. 개요 (Overview)
- **위치**: `Assets/Scripts/Gameplay/Spawners/RiverEntityFactory.cs`
- **역할**: 수면 엔티티(10종 물고기, 부스트 패드, 랜덤 링, 5종 바위, 과녁, 깃발, 연잎) 프리팹 인스턴스화 팩토리

## 2. 불변 원칙 (Immutable Principles)
- **프리팹 우선 로드**: `EnsurePrefabsLoaded()`에서 Editor 모드 시 `AssetDatabase`, Runtime 시 `Resources.Load`로 이중 로드 보장.
- **Null Safety**: 프리팹 배열 내 유효한 항목만 필터링하여 랜덤 선택.

## 3. 변경 이력 (Changelog)
- ### [2026-09-16] 장애물 바위 5종 프리팹 교체 및 Y축 랜덤 회전
  - **수정 목적**: 기존 단일 ObstacleRock 프리팹 대신 P_BoulderClassic1~5 Variant 5종 프리팹으로 교체하여 시각적 다양성 확보 및 월드 Y축 랜덤 회전(0~360도) 적용.
  - **핵심 구조**:
    - `obstacleRockPrefabs[5]` 배열 추가, Editor/Runtime 양측 로드 경로 구축.
    - `CreateObstacleRock()`: 유효한 5종 중 무작위 선택, `Quaternion.Euler(0f, Random.Range(0f, 360f), 0f)` 회전, `ObstacleRock` 컴포넌트 부착 보장.
    - 기존 단일 `obstacleRockPrefab` fallback 유지 (하위 호환).
