# Diff Against Working Baseline Rule (STRICT)

## 1. Mandatory Comparison with Last Known Working State
- Whenever a process, build, or feature that **previously worked** fails in a subsequent step:
  - **NEVER** guess blindly or attempt unverified ad-hoc fixes.
  - **ALWAYS** compare the failed state directly against the last known working state (`git diff <working_commit> HEAD` or commit logs).

## 2. Check Structural & Environmental Deltas
- Systematically inspect what changed between the successful step and the failing step:
  1. File paths and directory hierarchies (e.g. root vs subfolder).
  2. Configuration files, flags, and environment variables.
  3. Method calls, event subscriptions, and lifecycle state changes.

## 3. Verify Before Concluding
- Identify the exact delta that caused the failure, explain the root cause clearly to the user, and verify the fix against the working baseline before claiming completion.
