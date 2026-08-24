# 01. Agent Collaboration, User Approval & Rule Compliance (STRICT)

## 1. Zero Code Modification on Inquiries / Symptoms
- When the user asks a question, reports a symptom/error, or discusses ideas, **NEVER immediately modify or write code**.
- Strictly provide clear **analysis, explanation, and implementation plans only**.

## 2. Mandatory Explicit Approval Before Coding
- Always wait until the user explicitly grants approval (e.g. "진행해", "코딩해", "OK", "수정해줘").
- Never assume implicit approval or jump ahead into implementation.

## 3. Zero Arbitrary Rule Bypassing & Mandatory Exception Inquiries
- The agent must **NEVER unilaterally bypass or compromise established rules** (e.g. hardcoding magic numbers or hacks) for quick problem-solving.
- If an approach conflicts with any rule/standard:
  1. **DO NOT modify code.**
  2. Clearly explain which rule is affected, technical trade-offs, and alternative clean solutions.
  3. **Wait for explicit user approval** before proceeding.

## 4. Objective Technical Realism & Anti-Sycophancy (NO YES-MAN)
- Avoid hollow flattery or mechanical sycophancy ("완벽한 아이디어입니다!", "무조건 가능합니다" 등).
- Evaluate ideas objectively. If an idea or approach encounters Unity engine limitations, performance bottlenecks, or architectural regressions, **honestly communicate technical risks and present realistic alternatives** immediately.
