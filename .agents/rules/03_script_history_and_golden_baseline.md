# 03. Script History & Golden Baseline System (STRICT)

## 1. Directory Structure & 1:1 Naming Conventions
- **Script History Directory**: `docs/script_history/`
  - Naming: `<ScriptName>.md` (e.g. `RiverSpawner.cs` $\rightarrow$ `docs/script_history/RiverSpawner.md`).
  - **Scope**: Applied selectively to **core, complex architecture scripts** with previous trials/regressions (e.g. Terrain, Spawner, Physics, Character, GameController). Simple DTOs, data models, or trivial helpers are excluded to avoid doc bloat.
- **Golden Baseline Directory**: `docs/golden_scripts/`
  - Naming: `<ScriptName>.cs.txt` (e.g. `RiverSpawner.cs` $\rightarrow$ `docs/golden_scripts/RiverSpawner.cs.txt`).

## 2. Pre-Modification Protocol (MANDATORY)
- Before creating or modifying ANY core C# script in `Assets/Scripts/`:
  1. The agent **MUST check and read** `docs/script_history/<ScriptName>.md` if it exists.
  2. Review the script's core responsibility, past failed attempts, and invariant rules.
  3. Never re-introduce previously solved bugs or discard proven architectures.

## 3. Post-Modification Protocol (MANDATORY)
- After modifying and successfully verifying a core script:
  1. **Update `docs/script_history/<ScriptName>.md`**: Date, version, exact purpose, and newly established invariant rules.
  2. **Update Golden Snapshot**: When a feature is verified and completed, update `docs/golden_scripts/<ScriptName>.cs.txt`.

## 4. Regression Analysis against Golden Baseline
- If regression or unexpected behavior occurs, compare the active script directly against `docs/golden_scripts/<ScriptName>.cs.txt` to identify deviations before fixing.
