# 📜 Script History: SkippingStone.cs

## 🎯 1. 역할 및 책임 (Core Responsibility)
- 돌의 비행 궤적 물리(중력, 양력, 수면 바운스 반발, 스핀 감쇠), 물수제비 판정, 리듬 링 인디케이터 트리거 및 사운드/햅틱 연동.

## 🚫 2. 절대 금지 규칙 및 불변 표준 (Must NOT Do)
- ❌ **수면 높이를 고정 상수로 하드코딩 금지** ➜ `WaterSurface` 및 지형 수면 높이를 동적 감지.
- ❌ **연속 입력 시 중복 바운스 트리거 금지** ➜ 1바운스 1터치 판정 소모 구조 유지.
- ❌ **수면 물리 콜라이더 접촉 시 지형 충돌(CrashOnLand) 오판정 금지** ➜ 수면과의 물리 접촉은 완전 무시.

## 🕒 3. 수정 및 진화 히스토리 (Change Log)

### [2026-08-31] 순수 모멘텀 생존 체계 확립, 수면 콜라이더 분리 및 도로록 스키밍 연동 완료
- **수정 목적**: 
  1. 수면 아래(-고도)에서 일어나는 모든 바운스(LATE, TOO LATE, BAD) 시 즉시 수면 위(`waterLevel + 0.02m`)로 탈출(Elevation Snap)시켜 다음 프레임 침몰 차단.
  2. 과거 원스트라이크 시절의 레거시 조기 차단문(`distToWater < -0.35f LATE MISS` 등) 및 가짜 지형 충돌(`CheckWaterUnderneath`)을 100% 완전 삭제하고, 순수하게 `currentMomentum > 0` 여부로만 생사를 판정하는 단일 진실 공급원 확립.
  3. `OnCollisionEnter` / `OnTriggerEnter`에서 `WaterSurface` 및 수면 관련 콜라이더는 물리 충돌(지형 충돌)에서 완전히 배제하여, 물속에서 솟구칠 때 수면 아랫면에 부딪혀 자폭하던 현상 원천 박멸.
  4. 15스킵 이상 고스킵 상태에서 `BAD (-3.0)` 구제 시에도 최소 반발력 `2.8m/s`를 보장하여 안전 회생.
  5. 5스킵 이상 달성 후 모멘텀 고갈 시 즉시 침몰하지 않고 '도로록~' 스키밍 피니시(`StartSkimmingFinish`)로 직결되도록 수동 탭 침몰 분기 완비.
- **컴파일 검증**: Assembly-CSharp 0 Errors, 0 Warnings (Clean Build).
