# 📜 Script History: SkippingStone.cs

## 🎯 1. 역할 및 책임 (Core Responsibility)
- 돌의 비행 궤적 물리(중력, 양력, 수면 바운스 반발, 스핀 감쇠), 물수제비 판정, 리듬 링 인디케이터 트리거 및 사운드/햅틱 연동.

## 🚫 2. 절대 금지 규칙 및 불변 표준 (Must NOT Do)
- ❌ **수면 높이를 고정 상수로 하드코딩 금지** ➜ `WaterSurface` 및 지형 수면 높이를 동적 감지.
- ❌ **연속 입력 시 중복 바운스 트리거 금지** ➜ 1바운스 1터치 판정 소모 구조 유지.
- ❌ **수면 물리 콜라이더 접촉 시 지형 충돌(CrashOnLand) 오판정 금지** ➜ 수면과의 물리 접촉은 완전 무시.

## 🕒 3. 수정 및 진화 히스토리 (Change Log)

### [2026-08-31] 수면 반사 그림자(Water_Reflection_Shadow) OnDestroy 라이프사이클 파괴/누수 정리
- **수정 목적**: 돌이 파괴되거나 게임 재시작 시 동적으로 씬 루트에 생성되었던 `[Water_Reflection_Shadow]` Quad 오브젝트 및 생성 텍스처/머티리얼이 하이어라키에 잔존/누적되던 문제 해결.
- **적용 내용**:
  - `SkippingStone.cs`: `OnDestroy()` 라이프사이클 메서드를 구현하여 `waterReflectionObj`, `waterReflectionMat`, `mainTexture`를 명시적으로 파괴/해제하도록 처리.
- **컴파일 검증**: 0 Errors, 0 Warnings.

### [2026-09-01] 장거리 모드 리듬 링 자동 보장 (EnsureRhythmRing 동적 생성)
- **수정 목적**: 장거리 물리 모드에서 프리팹에 `RhythmRingIndicator`가 없더라도 비행 시 동적으로 생성 및 돌 인스턴스에 자동 바인딩되도록 개선.
- **적용 내용**:
  - `SkippingStone.cs`: `EnsureRhythmRing()`에서 누락 시 자식 오브젝트로 `RhythmRingIndicator` 자동 생성 및 `ring.stone = this` 바인딩 처리.
- **컴파일 검증**: 0 Errors, 0 Warnings.

### [2026-09-04] 대형 과밀 파일 객체화 및 모듈 분리 (Calculators / Timing / Shadow)
- **수정 목적**: 1,250줄 이상의 대형 과밀 파일이었던 `SkippingStone.cs`의 물리 계산, 타이밍 평가, 수면 그림자 로직을 독립 객체로 분리하여 유지보수성 및 단위 테스트성 극대화.
- **적용 내용**:
  - `StonePhysicsCalculator.cs` 생성: 스킵별 바운스 감쇠, 3축 비행 피치/스핀 회전 연산, 좌우 조향 각도 벡터 연산 분리.
  - `StoneTimingEvaluator.cs` 생성: 수면 고도차 기준 +/- 양방향 판정(PERFECT, GREAT, GOOD, LATE, TOO LATE, BAD), 모멘텀 증감, 속도 및 바운스력 배율 산출 분리.
  - `WaterReflectionShadowController.cs` 생성: Quad 메쉬 동적 생성, 소프트 알파 텍스처 베이킹, 비행 고도별 크기/알파 트래킹 및 `SafeDestroy` 클린업 분리.
  - `SkippingStone.cs`: 분리된 3대 모듈 클래스들과 바인딩 연동, EditMode 및 런타임 Null-Safety 강화.
- **컴파일 & MCP 검증**: C# 컴파일 0 Errors, 0 Warnings, Unity MCP `Unity_RunCommand` 인엔진 모듈 단위/통합 테스트 전수 통과.
