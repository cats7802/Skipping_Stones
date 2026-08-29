# PrefabHealthCheckTool

## 1. 개요 (Overview)
- **위치**: `Assets/Scripts/Editor/PrefabHealthCheckTool.cs`
- **역할**: 물수제비 프로젝트 내 프리팹 무결성 검증, 누락 필수 컴포넌트 자동 수리, 유실된 스크립트(Missing Script) 일괄 청소, 소나무 LOD 단일화 최적화 등 에디터 유지보수 도구.

## 2. 불변 원칙 (Immutable Principles)
- **프리팹 수정 안전성**: `PrefabUtility.LoadPrefabContents` 및 `PrefabUtility.SaveAsPrefabAsset`을 사용해 안전하게 로드 및 언로드/저장 처리.
- **프로그레스 바 정리**: 에러 발생 시에도 `finally` 블록에서 `EditorUtility.ClearProgressBar()`를 호출하여 에디터 먹통 방지.
- **비파괴 검증**: 원본 데이터를 파괴하지 않고 필수 컴포넌트 및 계층 구조 보존.

## 3. 변경 이력 (Changelog)
- **2026-08-28**: 
  - 전체 프리팹 Missing Script 일괄 삭제 도구(`RemoveAllMissingScripts`) 추가.
  - 소나무 프리팹의 가짜 `LODGroup` 제거 및 중복 `LOD1/LOD2` 정리, `LOD0` 단일화 최적화 도구(`OptimizePineLODPrefabs`) 추가.
- **2026-08-30**:
  - 맵 지형 프리팹의 소켓 앵커(`Anchor_S`, `Anchor_E`) 검증(`CheckTerrainAnchors`) 및 누락 시 지형 바운드 기반 원클릭 자동 생성/부착 도구(`FixTerrainAnchors`, `RunMapAnchorCheckAndFix`) 추가.
  - 상단 메뉴(`Tools ➔ Skipping Stones ➔ ⚓ 맵 프리팹 앵커 검증 및 자동 부착 (Anchor Auto-Fix)`) 등록.
