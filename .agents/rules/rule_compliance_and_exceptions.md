# Strict Rule Compliance & Explicit Exemption Approval (STRICT)

## 1. Zero Arbitrary Rule Bypassing
- The agent must **NEVER unilaterally bypass, violate, or compromise established project rules** (e.g., No Hardcoding, Single-Frame Touch Input, Pre-Coding Confirmation, Zero Warning Compile) under any circumstances.
- "Solving the immediate problem quickly" or "making it compile easily" is NEVER an acceptable reason to break a rule or insert hardcoded magic numbers/temporary hacks.

## 2. Mandatory Confirmation for Rule-Conflicting Improvements
- If an edge case, optimization, or improvement proposal necessitates an approach that **conflicts with or deviates from any existing rule/standard**:
  1. **DO NOT WRITE OR MODIFY CODE.**
  2. Clearly explain to the user:
     - Which specific rule would be affected.
     - Why this deviation is being considered (technical constraints, trade-offs).
     - Alternative clean solutions versus the proposed compromise.
  3. **Wait for explicit user approval and instructions** before proceeding.
