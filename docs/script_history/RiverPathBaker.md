# RiverPathBaker 및 RiverPathBakerWindow 스크립트 히스토리

## [2026-08-30] 환경 매니저(LakeEnvironmentManager) 프리팹 기반 일괄 베이킹 시스템 구축
- **수정 목적**: `TestEnvMgr` 등 빈 오브젝트에 환경 매니저를 붙여 저장해 둔 프리팹을 등록받아, 그 안에 세팅된 모든 맵(`Start`, `Loop`, `Variation`, `Ending`)의 강줄기 스플라인을 원클릭으로 일괄 베이킹.
- **핵심 구조**:
  - `RiverPathBakerWindow.BakeAllFromEnvManager`: 환경 매니저 프리팹 내 고유 맵 프리팹을 전수 수집하여 5m 간격 단면 스캔 및 `RiverPathChunkData` 자동 주입/저장.
  - `LakeEnvironmentManagerEditor`: 인스펙터 하단에 **`🌊 등록된 모든 맵 강줄기 곡선 일괄 베이킹`** 버튼 추가.
  - 빌드 및 컴파일 0 Warnings, 0 Errors 완료.
