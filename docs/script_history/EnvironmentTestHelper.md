# 📜 Script History: EnvironmentTestHelper.cs

## 🎯 1. 역할 및 책임 (Core Responsibility)
- 장거리 맵 스트리밍, 동적 바이옴 라이팅(Day/Sunset/Twilight/Night), 리플레이 연동 및 3,500m 갓모드(GodMode) 자동 비행 테스트 제어.
- 숫자키(1~4, F1) 단축키 및 uGUI 테스트 메뉴를 통한 실시간 거리별 환경 프리뷰 지원.

## 🚫 2. 절대 금지 규칙 및 불변 표준 (Must NOT Do)
- ❌ **수면 높이를 0m 또는 상수로 하드코딩 금지** ➜ 반드시 `WaterSurface`의 `BoxCollider` 높이(`bounds.max.y`)를 기준으로 궤적 높이를 계산할 것.
- ❌ **비행 X좌표를 직선 고정(0m 또는 발사점)으로 두지 말 것** ➜ 강이 S자로 굽이치므로 반드시 `RiverValleyTerrainGenerator.GetRiverCenterX(z)`를 실시간으로 추적하여 물길 위를 비행할 것.

## 🕒 3. 수정 및 진화 히스토리 (Change Log)

### [2026-08-30] 싱글톤 자멸 버그 수정 및 인게임 실제 물리/갓모드 시스템 단일화
- **수정 목적**: 중복 싱글톤 판정 시 부모 게임오브젝트 전체(`Destroy(gameObject)`)를 날려 500m 맵이 증발하던 치명적 버그 수정 및 가짜 돌 스폰/독자 시뮬레이션 코루틴 폐기.
- **핵심 구조**:
  - `Awake()`에서 `Destroy(this)`로 변경하여 맵 오브젝트 보존.
  - `Instance` getter에서 불필요한 자동 부트스트랩 객체 생성을 제거하여 안전 참조로 전환.
  - `ToggleAutoFlyGodMode()` 및 `StartAutoFlyNative()`가 실제 `GameController.devGodMode`를 트리거하여, 실제 플레이어가 선택한 조약돌 모델/스케일/물리 엔진 그대로 갓모드가 동작하도록 단일화.
- **컴파일 검증**: 0 Warnings, 0 Errors.

