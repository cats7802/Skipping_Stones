# 📋 최종 작업 인수인계서 (Work Handover Note & Future Architecture)

**작성 일시**: 2026-08-31 22:58  
**현재 브랜치**: `feature/sandbox-momentum-rhythm`  
**상태 요약**: 리듬 아케이드 모드 전용 독립 컴포넌트(`ArcadeGameController`, `ArcadeSkippingStone`, `ArcadeRhythmRing`) 구축 완료 및 컴파일 무결점(0 Errors, 0 Warnings) 검증 완료.

---

## 1. 🛠️ 금일 완료된 작업 현황

### ✅ 1. 리듬 아케이드 모드 전용 독립 시스템 구축 (`Assets/Scripts/Arcade/`)
- **기존 원본 파일 무결성 보존**: `GameController.cs` 및 `SkippingStone.cs`를 전혀 오염시키지 않고 100% 보존.
- **`ArcadeSkippingStone.cs`**:
  - BPM 고정 주기(60~120 BPM) 수학적 포물선 비행 ($y = \text{waterLevel} + 4H \cdot t(1-t)$).
  - 수면 착수 7단계 판정 (`PERFECT`, `GREAT`, `GOOD`, `LATE`, `TOO LATE`, `TOO EARLY`, `MISS`).
  - 콤보 단계별 BPM 가속 (60 $\rightarrow$ 72 $\rightarrow$ 85 $\rightarrow$ 100 $\rightarrow$ 120 FEVER).
  - 모멘텀(스태미나 100) 기반 라이프 관리 및 소진 시 스키밍 감속 피니시 연출.
- **`ArcadeRhythmRing.cs`**:
  - 수면 착수 예정 좌표에 자동 배치 및 하강 잔여 시간에 따른 수축 링 비주얼 제공.
- **`ArcadeGameController.cs`**:
  - 앞단의 캐릭터/돌/맵 선택(`GameDataManager`, `MetaUIManager`), 0번 청크 스폰, 투구 45~55프레임 리드인 애니메이션 100% 계승.
  - 투구 발사 순간 `ArcadeSkippingStone` 생성 및 카메라/UI 연동.
  - A/D/S 키보드 및 하단 3버튼 터치 판정 연동.

---

## 2. 🛡️ 검증 및 빌드 상태

- `dotnet build Assembly-CSharp.csproj`: **0 Errors, 0 Warnings 통과**
- `dotnet build Assembly-CSharp-Editor.csproj`: **0 Errors, 0 Warnings 통과**

---

## 3. 🎯 차기 작업 로드맵 (Roadmap)

### 🚀 1. 실기 플레이 테스트 및 연출 다듬기
- 에디터/씬 내에서 `GameController` 3대 모드(원거리/타깃/아케이드) 전환 실기 테스트.
- 하단 3버튼 uGUI와 `RhythmArcadeModeHandler` 실시간 HUD 바인딩 미세 튜닝.

### 🧩 2. 단계적 모듈화/객체화 리팩토링 과제 (Rule 준수)
1. **`MetaUIManager.cs` (화면/모달 분리)**:
   - `IMetaScreenView` 기반 `TitleScreenView`, `LobbyScreenView`, `MapSelectScreenView` 분리.
   - 도감/상점/랭킹/세팅 모달 뷰(`CollectionModalView` 등) 독립 객체화.
2. **`StoneThrowerCharacter.cs` (책임 분리)**:
   - 투구 애니메이션 및 45~55프레임 리드인 코루틴을 `CharacterThrowAnimator`로 분리.
   - 타깃 모드 웨이포인트 가이드 및 포지셔닝을 `CharacterWaypointNavigator`로 분리.
3. **`RiverSpawner.cs` (엔티티 생성 팩토리화)**:
   - `IRiverEntitySpawner` 인터페이스 기반 `FishSpawner`, `ObstacleSpawner`, `BoostPadSpawner` 등으로 분리하여 모드별 조립형 스폰 구조 구축.
