# 📋 최종 작업 인수인계서 (Work Handover Note & Future Architecture)

**작성 일시**: 2026-09-01 00:22  
**현재 브랜치**: `feature/sandbox-momentum-rhythm`  
**상태 요약**: 리듬 아케이드 모드 전략 패턴 객체화(`IGameModeHandler`), 오디오 시스템 자동화 & 침몰 숏 페이드아웃, 모드별 독립 카메라 프로필 분리 완료 및 골든 스크립트 백업 완료.

---

## 1. 🛠️ 금일 완료된 작업 현황

### ✅ 1. 게임 모드 전략 패턴 객체화 (`Assets/Scripts/Gameplay/Modes/`)
- **모드 분기 객체화**: `IGameModeHandler` 인터페이스 기반으로 `LongDistanceModeHandler`, `TargetAccuracyModeHandler`, `RhythmArcadeModeHandler` 독립 분리.
- **모놀리식 코드 방지**: `GameController.cs`에서 조건문(if-else)을 제거하고 핸들러 객체에 100% 위임.
- **순수 물리 & 정품 링 100% 보존**: 가짜 수학적 보간을 걷어내고, 롱디 모드에서 검증된 정품 `SkippingStone`과 붉은색 과녁 링(`RhythmRingIndicator`)으로 통일.

### ✅ 2. 오디오 매니저 자동화 & BGM 연동 (`Assets/Scripts/Audio/AudioManager.cs`)
- **클립 자동 매핑**: `OnValidate`/`Reset` 시 `Resources/Audio/` 폴더 내 모든 효과음 및 BGM 자동 등록.
- **BGM 템포 연동**: 투구 발사 순간부터 60 BPM 정박 재생 시작.
- **침몰 순간 숏 페이드아웃**: 돌이 침몰(`HandleStoneSunk`)하는 즉시 0.4초 만에 음악이 스르륵 꺼지며 물속 잠김 효과음 극대화.

### ✅ 3. 모드별 독립 카메라 프로필 분리 (`Assets/Scripts/Visuals/DualCameraSetup.cs`)
- `LongDistanceProfile`과 `RhythmArcadeProfile`로 독립 분리하여 디렉터님의 확정 수치(-1.5, 1.5, 2.5, 10, -5.5, 4.2) 완벽 보존 및 인스펙터 자유 튜닝 보장.

---

## 2. 🛡️ 검증 및 빌드 상태

- `dotnet build Assembly-CSharp.csproj`: **0 Errors, 0 Warnings 통과**
- `dotnet build Assembly-CSharp-Editor.csproj`: **0 Errors, 0 Warnings 통과**
- `docs/golden_scripts/` 5대 핵심 스크립트 동기화 완료:
  - `DualCameraSetup.cs.txt`
  - `AudioManager.cs.txt`
  - `GameController.cs.txt`
  - `StoneThrowerCharacter.cs.txt`
  - `RiverSpawner.cs.txt`

---

## 3. 🎯 차기 작업 로드맵 (Roadmap)

### 🚀 1. 실기 플레이 테스트 및 연출 다듬기
- 에디터/씬 내에서 `GameController` 3대 모드(원거리/타깃/아케이드) 전환 실기 테스트.
- 하단 3버튼 uGUI와 `RhythmArcadeModeHandler` 실시간 HUD 바인딩 미세 튜닝.

### 🧩 2. 단계적 모듈화/객체화 리팩토링 과제 (Rule 준수)
1. **`MusicTrackCatalog` (음원 트랙 카탈로그 시스템 구축)**:
   - 각 BGM 음원별 `trackId`, `audioClip`, `originalBPM`, `targetStartBPM`, `previewStartTime` 목록화.
   - 스테이지/테마별 자동 BGM 바인딩 및 BPM 콤보 가속 시 톤 왜곡 없는 피치 싱크 엔진 구현.
2. **`CameraProfile` (모드 데이터와 카메라 분리)**:
   - 각 `IGameModeHandler` 또는 ScriptableObject 모드 데이터가 자체 카메라 값을 소유하고, 카메라는 수동 수치 하드코딩 없이 모드 프로필을 수신받아 투영하도록 고도화.
3. **`MetaUIManager.cs` (화면/모달 분리)**:
   - `IMetaScreenView` 기반 `TitleScreenView`, `LobbyScreenView`, `MapSelectScreenView` 분리.
   - 도감/상점/랭킹/세팅 모달 뷰(`CollectionModalView` 등) 독립 객체화.
4. **`StoneThrowerCharacter.cs` (책임 분리)**:
   - 투구 애니메이션 및 45~55프레임 리드인 코루틴을 `CharacterThrowAnimator`로 분리.
   - 타깃 모드 웨이포인트 가이드 및 포지셔닝을 `CharacterWaypointNavigator`로 분리.
5. **`RiverSpawner.cs` (엔티티 생성 팩토리화)**:
   - `IRiverEntitySpawner` 인터페이스 기반 `FishSpawner`, `ObstacleSpawner`, `BoostPadSpawner` 등으로 분리하여 모드별 조립형 스폰 구조 구축.
