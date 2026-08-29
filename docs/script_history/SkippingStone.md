# 📜 Script History: SkippingStone.cs

## 🎯 1. 역할 및 책임 (Core Responsibility)
- 돌의 비행 궤적 물리(중력, 양력, 수면 바운스 반발, 스핀 감쇠), 물수제비 판정, 리듬 링 인디케이터 트리거 및 사운드/햅틱 연동.

## 🚫 2. 절대 금지 규칙 및 불변 표준 (Must NOT Do)
- ❌ **수면 높이를 고정 상수로 하드코딩 금지** ➜ `WaterSurface` 및 지형 수면 높이를 동적 감지.
- ❌ **연속 입력 시 중복 바운스 트리거 금지** ➜ 1바운스 1터치 판정 소모 구조 유지.

## 🕒 3. 수정 및 진화 히스토리 (Change Log)
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

### [2026-08-30] 오토 바운스(isGodMode) 지형 충돌 및 물길 이탈 착지 정상 피니시 처리
- **수정 목적**: `Dev God Mode` 또는 물리 비행 중 돌이 물길을 벗어나 지형/강둑/바위에 닿았을 때 충돌이 무시되어 무한 멈춤이 발생하던 문제를 해결하고 즉시 착지 피니시(Crash/Landing)로 결과창에 정상 연결.
- **핵심 구조**:
  - `OnCollisionEnter`, `OnTriggerEnter`: `isGodMode` 상태에서도 땅/바위 충돌 시 무시하지 않고 `CrashOnLand()`를 정상 호출.
  - `FixedUpdate`: `!hasWaterBelow` 상태에서 지면에 닿았을 때 `CrashOnLand("물길 이탈 / 지형 착지")`로 최종 비거리를 확정하고 정상 종료.
  - 빌드 및 컴파일 0 Warnings, 0 Errors 완료.
