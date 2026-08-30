# 📜 Script History: SkippingStone.cs

## 🎯 1. 역할 및 책임 (Core Responsibility)
- 돌의 비행 궤적 물리(중력, 양력, 수면 바운스 반발, 스핀 감쇠), 물수제비 판정, 리듬 링 인디케이터 트리거 및 사운드/햅틱 연동.

## 🚫 2. 절대 금지 규칙 및 불변 표준 (Must NOT Do)
- ❌ **수면 높이를 고정 상수로 하드코딩 금지** ➜ `WaterSurface` 및 지형 수면 높이를 동적 감지.
- ❌ **연속 입력 시 중복 바운스 트리거 금지** ➜ 1바운스 1터치 판정 소모 구조 유지.

## 🕒 3. 수정 및 진화 히스토리 (Change Log)
### [2026-08-30] 갓모드 스키밍(도로록) 피니시 완료 후 정상 침몰/정지 지원
- **수정 목적**: 갓모드 완주 시 스키밍(도로록)이 끝난 후 `Sink()`가 `isGodMode`에 막혀 영구히 팽이처럼 제자리 회전하던 버그 해결.
- **핵심 구조**:
  - `Sink()`에서 `if (isGodMode && !isSkimming) return;`으로 개선하여 비행 중 실수 착수는 막되, 스키밍 완주 후에는 정상 침몰/완주 루틴(`WaterSinkRoutine`)으로 진입하도록 보장.
  - `godModeTargetDistance <= 0`일 경우 맵 끝(엔딩)까지 무한 비행 완주 지원.
- **컴파일 검증**: 0 Errors.

### [2026-08-30] 갓모드 Pure Pursuit (전방 15m Look-Ahead 타겟팅) 자율주행 유도 탑재
- **수정 목적**: 기존 단순 중심선 복원 시 오버슈트로 인해 발생하던 좌우 와리가리(진동) 및 강둑 충돌 이탈 현상을 원천 해결.
- **핵심 구조**:
  - `GetClosestPointOnRiver`로 현재 위치 기준 최근접 스플라인 호 거리(`distAlongRiver`) 산출.
  - 전방 15m 지점의 목표점(`Look-Ahead Target`)을 계산하여 자율주행차처럼 완만하고 부드럽게 선회 유도 (`Damped Lerp`).
- **컴파일 검증**: 0 Errors.

### [2026-08-23] 리듬 링 에디터 슬라이더 통합 및 Unlit 시인성 극대화
- **수정 목적**: 리듬 링 테두리 자글거림 제거 및 수직 가이드 레이저 선(`dropLine`) 부활.
- **핵심 구조**: `SkippingStoneEditor`를 통해 선 두께, 링 반경, 수축 배율 원스톱 조절 지원.

### [2026-08-25] 단 1회 입력 소비(Single-Shot) 및 Time-to-Impact 리듬 판정 개편 (방안 B)
* **표준 리듬 게임 타이밍 판정 관용도 적용**:
  * `perfectWindowTime`: 75ms ➔ **100ms** (0.10s)
  * `greatWindowTime`: 160ms ➔ **220ms** (0.22s)
  * `goodWindowTime`: 280ms ➔ **380ms** (0.38s)
  * `timingWindowHeight`: 2.4m ➔ **2.8m** (인디케이터 인지 시간 확보)
  * **목적**: Single-Shot 1회 탭 기회 소모 규칙 하에서 모바일/캐주얼 표준 리듬 액션 게임의 쾌적하고 쫀득한 손맛 제공.
* **단 1회 탭 소비 (Single-Shot) & Time-to-Impact 기반 판정 개편**:
  * `남은높이 / 하강속도` 기반의 물리 시간 판정 적용.
  * 윈도우 진입 후 첫 탭 즉시 1회 기회 소모하여 연타 꼼수 차단 및 바운스 상승 시 리셋.
* **물리 보간(`Interpolate`) 활성화**:
  * 50Hz 물리 주기와 고주사율 모니터 간의 떨림 제거.
  - **물리 렌더링 보간 영구 고정**: `rb.interpolation = RigidbodyInterpolation.Interpolate;`를 `Awake()` 및 라이프사이클 전반에 영구 고정하여 고주사율 모니터 상의 링 미세 떨림/고스팅 잔상 원천 차단.

### [2026-08-30] 갓모드(isGodMode) 베이크된 강줄기 스플라인(GlobalRiverPath) 곡선 추적 비행 연동
- **수정 목적**: 갓모드 실행 시 직선 관성 비행을 탈피하고, 어제 베이크해 둔 맵의 S자 강줄기 중심 곡선(`GlobalRiverPath`)을 따라 물길 한가운데로 자연스럽게 진행 방향(Velocity/Heading)을 유도하도록 정석 연동.
- **핵심 구조**:
  - `FixedUpdate`: `isGodMode`일 때 `GlobalRiverPath.Instance.EvaluateAtDistance`로 물길 중심선 좌표 및 접선 방향(Tangent)을 실시간 조회하여, 속도 크기를 유지한 채 부드럽게 S자 물길 궤적을 그리도록 속도 벡터와 회전 보간.
  - `TryRhythmBounce`: 갓모드 바운스 반발 시에도 강 중심 및 물길 접선 방향으로 반사 각도를 정렬하여 곡선 코스 100% 완주 지원.
  - 플레이어가 선택한 실제 조약돌 모델/스케일/트레일/물리 그대로 유지.
- **컴파일 검증**: 0 Warnings, 0 Errors.

