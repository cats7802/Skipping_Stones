# 04. Documentation, Asset Sync & Handover Protocol (STRICT)

## 1. Handover Note Management
- Do not create per-turn logs. Update `docs/Work_HandoverNote.MD` only at the conclusion of a session or milestone.
- When creating a new daily note, move previous notes into `docs/backup/Work_HandoverNote_YYYYMMDD.MD`.

## 2. Dual Rule Mirroring (`.agents/rules/` ⟷ `docs/rules/`)
- All rules in `.agents/rules/` must be 100% mirrored in `docs/rules/` to ensure full compatibility with external LLMs and Unity Editor AI plugins.

## 3. Resources Folder Mirroring & Sync
- Whenever any asset (3D model, prefab, material, audio) in `docs/resources_sync_manifest.md` is edited, keep `Assets/Resources/` clones immediately in sync.

## 4. True Git Root & Wrap-Up Git Confirmation
- The true Git repository root is `D:\Git_Hub\Skipping_Stones`.
- When the user signals wrap-up ("작업 마무리하자", "여기까지 하자", "대화창 갱신하자"):
  1. Finalize handover notes and backups.
  2. **Mandatory Inquiry**: Always ask the user *"지금까지 작업한 내용을 Git에 커밋 & 푸시할까요?"* before wrapping up.
