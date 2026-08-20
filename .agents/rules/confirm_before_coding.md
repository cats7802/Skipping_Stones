---
trigger: always_on
---

# User Confirmation Before Coding Rule (STRICT)

1. **Never Code on Questions / Inquiries**:
   - When the user asks a question, reports a symptom/error, or inquires about a feature, **NEVER immediately modify or write code**.
   - First, strictly provide **analysis and explanation only**.

2. **Wait for Explicit Approval**:
   - Always wait until the user explicitly gives approval (e.g., "코딩해", "OK", "수정해줘", "진행해").
   - Do NOT assume approval or jump ahead.

3. **Mandatory Post-Coding Compile Verification**:
   - Once explicitly approved and code is written, ALWAYS run compilation verification (`dotnet build Assembly-CSharp.csproj` or check editor logs) before concluding the turn.
   - Verify 0 errors and 0 warnings.
