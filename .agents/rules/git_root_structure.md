# Git Repository & Project Root Hierarchy Rules (STRICT)

## 1. True Git Root Directory
- The TRUE Git repository root is: `D:\Git_Hub\Test_AI`
- **NEVER** confuse this with the Unity Project subfolder (`D:\Git_Hub\Test_AI\Test_AI`).

## 2. GitHub Actions Workflows Location
- All GitHub Actions workflow files MUST be placed ONLY in:
  `D:\Git_Hub\Test_AI\.github\workflows\`
- **NEVER** create a `.github` folder inside `D:\Git_Hub\Test_AI\Test_AI`.

## 3. Unity Project Subfolder
- The Unity engine project files (`Assets/`, `ProjectSettings/`, `Packages/`) reside inside:
  `D:\Git_Hub\Test_AI\Test_AI\`
- In GitHub Actions workflow files, `projectPath` must be set to `Test_AI`.
- When running `git add`, `git commit`, `git push`, verify paths against `D:\Git_Hub\Test_AI`.
