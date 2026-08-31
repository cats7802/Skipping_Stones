# 📋 리듬 아케이드 모드 독립 분리 구현 계획서

## 1. 개요 및 목표
기존의 물리 기반 시스템(`GameController.cs`, `SkippingStone.cs`)을 훼손하거나 코드를 복잡하게 섞지 않고,
독립된 전용 클래스(`ArcadeGameController.cs`, `ArcadeSkippingStone.cs`, `ArcadeRhythmRing.cs`)를 통해 깔끔하게 분리 구현합니다.
타이틀, 로비, 캐릭터/돌/맵 선택 데이터 로딩 및 인게임 진입/투구 애니메이션 등 앞단 시스템은 100% 온전하게 계승합니다.

---

## 2. 세부 설계 및 클래스 구조

### [NEW] `Assets/Scripts/Arcade/ArcadeSkippingStone.cs`
- **역할**: 오직 리듬 아케이드 규칙(BPM 고정 주기 포물선 비행 + 7단계 판정 + 모멘텀/콤보 시스템)만 전담하는 초경량 독립 스크립트.
- **주요 기능**:
  1. **BPM 기반 포물선 비행**: 발사 후 $t$초 동안 $y = \text{waterLevel} + 4h \cdot \frac{t}{T}(1 - \frac{t}{T})$, $x, z$는 판정 등급별 전진 속도로 계산.
  2. **동적 수면 높이 감지**: `WaterSurface` 감지하여 착수 지점 자동 정렬.
  3. **7단계 판정**: `PERFECT` (+30m, +20모멘텀), `GREAT` (+22m, +10), `GOOD` (+16m, +5), `LATE` (+14m, -10), `TOO LATE` (+8m, -15), `TOO EARLY` (기회 1회 보존), `MISS` (+5m, -25).
  4. **콤보 가속**: 0~4(60BPM), 5~9(72BPM), 10~14(85BPM), 15~19(100BPM), 20+(120BPM FEVER).
  5. **조향 지원**: 좌(-5°/-8°), 우(+5°/+8°), 중앙(0°) 조향.
  6. **피니시**: 모멘텀 소진 시 스키밍 연출 후 침몰 이벤트(`OnStoneSunk`) 발화.

### [NEW] `Assets/Scripts/Arcade/ArcadeRhythmRing.cs`
- **역할**: 수면에 생성되어 착수 잔여 시간에 맞춰 수축하는 리듬 링 비주얼 인디케이터.
- 착수 예정 좌표($y = \text{waterLevel}$)에 자동 배치되고, 판정에 따라 색상 변화 및 이펙트 연출.

### [NEW] `Assets/Scripts/Arcade/ArcadeGameController.cs`
- **역할**: 앞단(캐릭터/돌/맵 선택, 발판 배치, 0번 청크 스폰, 투구 45~55프레임 리드인 등)은 기존 파이프라인을 그대로 활용하고, 투구 발사 순간 `ArcadeSkippingStone`을 스폰 및 제어.
- **주요 기능**:
  1. `GameDataManager.Instance.UserData`의 캐릭터/돌/맵 ID로 리소스 자동 로드 및 발판 배치.
  2. 투구 발사 시 `ArcadeSkippingStone` 생성 및 카메라(`DualCameraSetup`) 타깃 연결.
  3. 하단 3버튼(`TouchFlightController`) 및 키보드 입력(`A/D/S`)을 아케이드 판정으로 직결.
  4. 비행 종료 시 기존 점수 계산 및 결과창(`ResultModalPanel`) 호출.

---

## 3. 검증 계획
1. `dotnet build Assembly-CSharp.csproj`: 0 Errors, 0 Warnings 검증.
2. 기존 물리 모드 원본 파일의 무결성 확인.
3. 캐릭터/돌/맵 프리팹 정상 로드 및 인게임 투구 $\rightarrow$ 비행 $\rightarrow$ 판정 $\rightarrow$ 결과창 플로우 검증.
