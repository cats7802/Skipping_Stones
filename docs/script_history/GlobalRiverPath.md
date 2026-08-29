# GlobalRiverPath 스크립트 히스토리

## [2026-08-30] 글로벌 강줄기 연속 경로 런타임 매니저 신규 제작
- **수정 목적**: 소켓 앵커로 도킹된 여러 청크의 `RiverPathChunkData`를 런타임에 단일 연속 스플라인으로 체인 연결하여 전역 경로 정보 제공.
- **핵심 구조**:
  - `RebuildPath()`: 활성화된 청크들을 Z축 좌표 순서로 정렬하여 누적 거리 세그먼트 구성.
  - `EvaluateAtDistance(totalDistance)`: 시작점부터 특정 누적 거리의 월드 좌표, 진행 접선, 강폭, 수면 Y 산출.
  - `GetClosestPointOnRiver(pos)`: 임의 월드 좌표에서 가장 가까운 물길 중심선 좌표 탐색.
  - 빌드 및 컴파일 0 Warnings, 0 Errors 완료.
