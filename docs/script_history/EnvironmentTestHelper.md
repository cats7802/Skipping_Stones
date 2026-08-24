# 📜 Script History: EnvironmentTestHelper.cs

## 🎯 1. 역할 및 책임 (Core Responsibility)
- 장거리 맵 스트리밍, 동적 바이옴 라이팅(Day/Sunset/Twilight/Night), 리플레이 연동 및 3,500m 갓모드(GodMode) 자동 비행 테스트 제어.
- 숫자키(1~4, F1) 단축키 및 uGUI 테스트 메뉴를 통한 실시간 거리별 환경 프리뷰 지원.

## 🚫 2. 절대 금지 규칙 및 불변 표준 (Must NOT Do)
- ❌ **수면 높이를 0m 또는 상수로 하드코딩 금지** ➜ 반드시 `WaterSurface`의 `BoxCollider` 높이(`bounds.max.y`)를 기준으로 궤적 높이를 계산할 것.
- ❌ **비행 X좌표를 직선 고정(0m 또는 발사점)으로 두지 말 것** ➜ 강이 S자로 굽이치므로 반드시 `RiverValleyTerrainGenerator.GetRiverCenterX(z)`를 실시간으로 추적하여 물길 위를 비행할 것.

## 🕒 3. 수정 및 진화 히스토리 (Change Log)

### [2026-08-25] 갓모드 수면 높이(Y=16m) 및 S자 물길 곡선 추적 비행 정상화
- **수정 목적**: 지형과 수면 높이가 개편된 후 갓모드 돌이 강바닥/땅속(Y=0m)에 갇히고 직선으로 날아가 산을 관통하던 현상 해결.
- **핵심 구조**:
  - `WaterSurface`의 `BoxCollider.bounds.max.y`를 읽어 실제 수면 위 통통 튀는 바운스 궤적 보정.
  - `RiverValleyTerrainGenerator.GetRiverCenterX(z)`를 매 프레임 추적하여 S자 강 한가운데를 비행하도록 X좌표 연동.
  - 3,500m 완주 후 `TopDownReplayManager` 궤적 맵 및 결과창으로의 매끄러운 상태 전이 보장.
