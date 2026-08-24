# Script History & Golden Baseline Protocol (STRICT)

## 1. Directory Structure & 1:1 Naming Conventions
- **Script History Directory**: `docs/script_history/`
  - **Naming Rule**: Must EXACTLY match the C# script file basename with `.md` extension.
  - *Example*: `Assets/Scripts/Gameplay/RiverSpawner.cs` $\rightarrow$ `docs/script_history/RiverSpawner.md`
- **Golden Baseline Directory**: `docs/golden_scripts/`
  - **Naming Rule**: Must EXACTLY match the C# script filename appended with `.txt` extension (`<ScriptName>.cs.txt`).
  - *Example*: `Assets/Scripts/Gameplay/RiverSpawner.cs` $\rightarrow$ `docs/golden_scripts/RiverSpawner.cs.txt`

## 2. Pre-Modification Protocol (MANDATORY)
- **Before creating, refactoring, or modifying ANY C# script in `Assets/Scripts/`**:
  1. The agent **MUST check and read** `docs/script_history/<ScriptName>.md`.
  2. If the history file exists, review the script's core responsibility, previous bug-fix history, and strictly prohibited practices (e.g. hardcoding magic numbers, linear assumptions).
  3. If the history file does not exist, initialize it before writing code.
  4. Never re-introduce previously solved bugs or discard proven architectures.

## 3. Post-Modification Protocol (MANDATORY)
- **After modifying and successfully verifying a C# script**:
  1. **Update `docs/script_history/<ScriptName>.md`**:
     - Date & Version / Milestone tag.
     - Exact purpose and architectural direction of the change.
     - Strict invariant rules and prohibited methods established during this iteration.
  2. **Update Golden Baseline (`docs/golden_scripts/<ScriptName>.cs.txt`)**:
     - When a milestone/feature is finalized and verified working without errors ("검증 완료" / "여기까지 구현하자"), copy the clean C# script to `docs/golden_scripts/<ScriptName>.cs.txt`.

