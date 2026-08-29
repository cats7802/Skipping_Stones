# 📜 Script History: MapAnchorHelper.cs

## 🎯 1. 역할 및 책임 (Core Responsibility)
- 3ds Max 및 3D 모델링 툴에서 생성된 다양한 앵커 명칭(`Ancher_S`, `Ancher_S001~004`, `Anchor_S*`, `Ancher_E`, `Ancher_E001~004`, `Anchor_E*` 등)을 스마트하게 자동 탐색.
- 앵커가 누락된 맵/프리팹의 경우 지형 메쉬(`MeshFilter`, `MeshCollider`, `Terrain`)의 로컬 바운드(`X=0, minZ / maxZ`)를 실측하여 동적 가상 앵커 생성 및 반환.
- `PrefabHealthCheckTool` 및 `LakeEnvironmentManager`의 단일 진실 공급원(Single Source of Truth)으로 동작.

## 🚫 2. 절대 금지 규칙 및 불변 표준 (Must NOT Do)
- ❌ **대소문자/네이밍 하드코딩 금지**: 정규식 및 유연한 부분 매칭을 사용하여 다양한 3D 모델러 네이밍 컨벤션 수용.
- ❌ **임의 오프셋 하드코딩 금지**: 지형 바운드 측정 시 수면/발판/캐릭터 메쉬를 제외한 순수 지형 바운드만 엄격 계산.

## 🕒 3. 수정 및 진화 히스토리 (Change Log)

### [2026-08-30] 최초 신규 작성 (소켓 앵커 스마트 탐색 및 지형 바운드 자동 생성)
- **제작 목적**: 브룩 5종 맵의 불규칙한 앵커 이름(`S001`, `E001` 등)을 완벽 매칭하고 앵커 누락 프리팹을 자동 보완하기 위해 신규 제작.
- **핵심 구조**:
  - `FindStartAnchor`, `FindEndAnchor` 정규식 기반 전수 계층 탐색.
  - `GetOrCreateAnchors`를 통한 런타임/에디터 가상 앵커 동적 생성 및 Warning 로깅.
  - `TryGetTerrainLocalBounds` 지형 메쉬 바운드 실측 엔진 탑재.
  - 빌드 및 컴파일 0 Warnings, 0 Errors 완료.
