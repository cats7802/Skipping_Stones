## [2026-08-30] 청크 경계 거리 초과 시 전방 외삽(Forward Extrapolation) 및 유턴 방지
- **수정 목적**: 돌이 청크 경계(500m)를 넘어가는 순간 `Mathf.Clamp`로 인해 목표점이 뒤쪽(500m)으로 고정되어 앞뒤로 와리가리 갇히던 유턴 버그 해결.
- **핵심 구조**: `EvaluateAtDistance`에서 요청 거리가 `totalRiverLength`를 초과할 경우 마지막 청크의 끝점 접선 벡터 방향으로 직진 외삽(`worldEndPos + worldTangent * overDist`)하여 방향 연속성 보장.
- **컴파일 검증**: 0 Errors.

## [2026-08-30] 글로벌 강줄기 연속 경로 런타임 매니저 신규 제작
- **수정 목적**: 소켓 앵커로 도킹된 여러 청크의 `RiverPathChunkData`를 런타임에 단일 연속 스플라인으로 체인 연결하여 전역 경로 정보 제공.
- **핵심 구조**:
  - `RebuildPath()`: 활성화된 청크들을 Z축 좌표 순서로 정렬하여 누적 거리 세그먼트 구성.
  - `EvaluateAtDistance(totalDistance)`: 시작점부터 특정 누적 거리의 월드 좌표, 진행 접선, 강폭, 수면 Y 산출.
  - `GetClosestPointOnRiver(pos)`: 임의 월드 좌표에서 가장 가까운 물길 중심선 좌표 탐색.
  - 빌드 및 컴파일 0 Warnings, 0 Errors 완료.
