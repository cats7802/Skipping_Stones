# Resources Folder Asset Mirroring & Real-Time Sync Rule (STRICT)

## 1. Mandatory Manifest Logging
- Whenever any asset (3D Model, Prefab, Material, Shader, Texture, Animation, Audio) is cloned, copied, or mirrored into `Assets/Resources/` for runtime loading or build purposes:
  - **ALWAYS** immediately register it in `docs/resources_sync_manifest.md` with its original path and resources path.

## 2. Immediate 1:1 Synchronous Mirroring on External Changes
- Whenever any external original asset listed in `docs/resources_sync_manifest.md` is modified, re-exported, or edited (e.g. shaders, materials, textures, prefabs):
  - **ALWAYS** copy and overwrite the corresponding clone in `Assets/Resources/` immediately in the same turn.
  - Never allow stale cache or outdated assets in `Assets/Resources/` to be loaded at runtime.
