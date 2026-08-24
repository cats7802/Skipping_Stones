# 문서 관리 및 인수인계 프로토콜 (STRICT)

## 1. 턴별 로그 작성 금지 및 작업 종료 시점 인수인계 정리
- 매 턴마다 문서를 수정/추가하지 않습니다 (토큰 누수 및 컨텍스트 압박 원천 방지).
- **작업이 완전히 마무리되거나 인수인계가 필요한 시점**에만 전체적인 작업 과정과 변경점을 날짜별로 정리하여 인수인계 문서([docs/Work_HandoverNote.MD](file:///d:/Git_Hub/Skipping_Stones/docs/Work_HandoverNote.MD))에 반영합니다.

## 2. `docs/` 최상위 폴더 슬림화 원칙
- `docs/` 최상위에는 **현재 실시간으로 참조해야 하는 핵심 문서만 유지**합니다:
  - `Detailed_System_Specification.md` (최신 통합 시스템 명세서)
  - `Work_HandoverNote.MD` (최신 작업 인수인계서)
  - `prefab_and_script_manifest.md` (프리팹/스크립트 구조)
  - `resources_sync_manifest.md` (리소스 동기화 내역)
  - `README.md`

## 3. 과거 작업일지 및 비활성 문서 백업 관리
- 신규 일일 작업일지를 작성할 때는 **전날의 작업일지를 `docs/backup/` 폴더로 이동**시킵니다.
- 실시간으로 매일 확인하지 않는 가이드, 과거 질의 로그, 완료된 로드맵 등은 `docs/backup/`에 보관하여 `docs/` 폴더의 가독성을 최상으로 유지합니다.

## 4. 룰 듀얼 미러링 원칙 (`.agents/rules/` ⟷ `docs/rules/`)
- 외부 LLM(ChatGPT, Claude 등) 및 유니티 에디터 AI 플러그인과의 완벽한 규칙 동기화를 위해, **`.agents/rules/`의 모든 룰 파일은 `docs/rules/`에 1:1로 미러링(동기화)** 관리합니다.
- 룰이 추가/수정/삭제될 때마다 양쪽 경로에 항상 동일하게 반영합니다.

