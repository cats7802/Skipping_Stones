# 04. Documentation, Asset Sync, Directory Standards & Handover Protocol (STRICT)

## 1. Project Directory Standards & Progressive Refactoring (`_Project/`)
- **Target Standard Structure**:
  ```text
  Assets/
  ├── _Project/                  # 자체 개발 에셋 (외부 패키지와 완전 격리)
  │   ├── Animations/
  │   ├── Audio/
  │   ├── Materials/
  │   ├── Prefabs/ (Characters, Environment, UI)
  │   ├── Scenes/
  │   ├── ScriptableObjects/
  │   └── Scripts/ (Core, Gameplay, UI, Utils)
  ├── ThirdParty/                # 외부 에셋 및 플러그인
  └── Settings/                  # Input Actions, URP 설정 에셋
  ```
- **Progressive Migration Policy**:
  - 신규 생성되는 파일/폴더는 `_Project/` 표준 구조를 우선 적용.
  - 기존 레거시 폴더는 메타(`GUID`) 손상 및 Missing Reference 방지를 위해 절대 한 번에 대량 이동하지 말고, **사용자와 사전 승인 하에 1개 폴더 단위로 차근차근 점진적 이동 및 검증**할 것.

## 2. Handover Note Management
- Do not create per-turn logs. Update `docs/Work_HandoverNote.MD` only at the conclusion of a session or milestone.
- When creating a new daily note, move previous notes into `docs/backup/Work_HandoverNote_YYYYMMDD.MD`.

## 3. Dual Rule Mirroring (`.agents/rules/` ⟷ `docs/rules/`)
- All rules in `.agents/rules/` must be 100% mirrored in `docs/rules/` to ensure full compatibility with external LLMs and Unity Editor AI plugins.

## 4. Resources Folder Mirroring & Sync
- Whenever any asset (3D model, prefab, material, audio) in `docs/resources_sync_manifest.md` is edited, keep `Assets/Resources/` clones immediately in sync.

## 5. Wrap-Up Self-Audit & Git Confirmation Gate (MANDATORY)
- When the user signals wrap-up ("작업 마무리하자", "여기까지 하자", "대화창 갱신하자"):
  1. **Self-Audit History**: Check if any modified core scripts are missing updates in `docs/script_history/` or golden snapshots. If missing, update them immediately.
  2. **Finalize Handover Note**: Ensure `docs/Work_HandoverNote.MD` is fully up-to-date.
  3. **Mandatory Confirmation Inquiry**:
     - Explicitly ask the user: *"오늘 수정한 스크립트의 히스토리와 인수인계서 정리를 완료했습니다. 지금까지의 작업 내용을 Git에 커밋 & 푸시할까요?"*
