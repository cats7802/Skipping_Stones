# 🛠️ Stone Skipping - 현재 구현 현황 및 시스템 아키텍처 명세서 (Implementation Status)

> **문서 목적**: 기획 검토 및 신규 기능 연동 시, 현재까지 구현 완료된 C# 스크립트, 물리/게임플레이 로직, 씬 구조를 명확히 파악하기 위한 기술 인수인계/현황 문서.  
> **엔진 및 플랫폼**: Unity 6 (6000.5.8f1) / Mobile Portrait (9:16)  
> **기준 일자**: 2026-08-25 (최신화 완료)

---

## 🏗️ 1. 현재 구현 완료된 핵심 시스템 요약

```mermaid
graph TD
    subgraph Core_Gameplay [인게임 핵심 루프 (완료)]
        GC[GameController.cs\n(라이프사이클/상태머신/발판 탐색)] --> STC[StoneThrowerCharacter.cs\n(각도/파워 조준 및 55F 독립 발사)]
        GC --> SS[SkippingStone.cs\n(Time-to-Impact 시간 판정/Single-Shot)]
        GC --> LEM[LakeEnvironmentManager.cs\n(모듈러 스토리 스트리밍 SM/Loop/EM)]
        GC --> RS[RiverSpawner.cs\n(전폭 BoxCollider 기반 6종 엔티티 스폰)]
    end

    subgraph Mode_Logic [2대 게임 모드 (완료)]
        M1[모드 1: 원거리 기록 모드\n(+Z축 500m 청크 롤링, 부스트/장애물/물고기)]
        M2[모드 2: 타깃 과녁 모드\n(+X축 강 건너기, PP01~PP10 발판 스폰, FloatingTargetZone)]
    end

    subgraph Visuals_Camera [연출 및 카메라 (완료)]
        DCS[DualCameraSetup.cs\n(세로 9:16 3/6 황금 구도 & 수면 Y 추적)]
        TDR[TopDownReplayManager.cs\n(90도 탑다운 직교 리플레이/자동 프레이밍)]
        RRI[RhythmRingIndicator.cs\n(월드 수평 고정 & 지터 0% 리듬 링)]
    end

    subgraph UI_Audio_Data [UI, 데이터 & 인증 (완료)]
        MUI[MetaUIManager.cs\n(타이틀 ➜ 로비 ➜ 맵선택 ➜ 인게임 ➜ 결과)]
        GDM[GameDataManager.cs & DTO\n(MatchSessionData / InGameResultData / 세이브)]
        AUD[AudioManager.cs & HapticHelper\n(10채널 2D 오디오 풀링 & 모바일 햅틱)]
    end
```

---

## 📂 2. 모듈별 상세 구현 현황 및 스크립트 목록

### 1) 게임플레이 & 메인 컨트롤러 (`Assets/Scripts/Gameplay/`)
* **[GameController.cs](file:///d:/Git_Hub/Skipping_Stones/Assets/Scripts/Gameplay/GameController.cs)**:
  - 0단계(위치선정) ~ 6단계(정산) 전체 게임 상태 머신 총괄.
  - `FindPlatformInScene()`, `FindPlayerPositionRootInScene()`: 씬 루트 및 배경 프리팹 하위의 `Platform`/`Player_Position` 다중 표준 자동 탐색.
  - 초기화 라이프사이클 순서 보장 (`SetupCharacter` ➜ `SetupMapEnvironment` ➜ `RiverSpawner`).
* **[StoneThrowerCharacter.cs](file:///d:/Git_Hub/Skipping_Stones/Assets/Scripts/Gameplay/StoneThrowerCharacter.cs)**:
  - 1단계 좌우 스윙 각도 조준기 & 2단계 파워 게이지 차징.
  - 45프레임 카메라 선행 가속 및 55프레임 독립 조약돌 인스턴스 물리 발사 파이프라인.
  - 손 소켓 단일화(`HandDummyStone`) 및 `Platform` 렌더러 안전 동기화.
* **[SkippingStone.cs](file:///d:/Git_Hub/Skipping_Stones/Assets/Scripts/Gameplay/SkippingStone.cs)**:
  - **단 1회 입력 소비 (`hasTappedInCurrentBounce`)**: 윈도우 진입 후 첫 탭만 즉시 소비, 연타 차단.
  - **Time-to-Impact 기반 시간 판정**: 수직 속도 연동 `timeToImpact` (PERFECT: 100ms, GREAT: 220ms, GOOD: 380ms).
  - 5스킵 이상 시 '도로록~' 고속 스키밍 피니시 및 침수/정지 판정.
  - `Rigidbody` 물리 보간(`Interpolate`, `ContinuousDynamic`) 적용으로 시각적 진동(Jitter) 100% 제거.
* **[RiverSpawner.cs](file:///d:/Git_Hub/Skipping_Stones/Assets/Scripts/Gameplay/RiverSpawner.cs)**:
  - 6종 수면 엔티티(`BoostPad`, `ObstacleRock`, `TargetZone`, `FriendFlag`, `JumpingFish`, `LilyPadCluster`) 프리팹 정식 연동.
  - 수면 `BoxCollider`의 전폭 `[minX, maxX]` 기준 지형 회피 레이캐스트 안전 스폰.

### 2) 비주얼, 카메라 & 스트리밍 (`Assets/Scripts/Visuals/` & `Assets/Scripts/Terrain/`)
* **[LakeEnvironmentManager.cs](file:///d:/Git_Hub/Skipping_Stones/Assets/Scripts/Visuals/LakeEnvironmentManager.cs)**:
  - **모듈러 스토리 스트리밍**: 시작 맵(SM) ➜ N개 가변 슬롯 루프 변주(`loopSlots`) ➜ 엔딩 맵(EM) 완벽 지원.
  - **단일 BG_01 완벽 호환**: 슬롯 미지정 시 씬의 `BG_01`을 그대로 단일 복제 스트리밍.
  - 복제 청크(1, 2, 3...) 생성 시 중복 발판 및 PP 자동 제거(메모리 세이프).
* **[DualCameraSetup.cs](file:///d:/Git_Hub/Skipping_Stones/Assets/Scripts/Visuals/DualCameraSetup.cs)**:
  - **세로 9:16 '3/6 황금 카메라 구도' 복원**: `flightDistBack: 5.5f`, `flightHeight: 2.4f`, `flightLookForward: 7.5f`, `flightLookHeight: -2.2f`.
  - 수면 높이 연동 다이내믹 Y축 비율 추적 및 고속 밀착 추적으로 카메라 진동 0% 달성.
* **[TopDownReplayManager.cs](file:///d:/Git_Hub/Skipping_Stones/Assets/Scripts/Visuals/TopDownReplayManager.cs)**:
  - 투구 종료 후 탑다운 90도 직교(Orthographic) 카메라 전환 및 자동 프레이밍 궤적 드로잉.
  - `GameController.FindPlatformInScene()` 연동 발판 높이 및 렌더러 복구.
* **[RhythmRingIndicator.cs](file:///d:/Git_Hub/Skipping_Stones/Assets/Scripts/Visuals/RhythmRingIndicator.cs)**:
  - 돌의 1440도 회전 간섭 분리 (`LateUpdate` 월드 수평 Euler(90, 0, 0) 고정)로 링 메쉬 뒤틀림 해결.

### 3) 메타 UI 시스템 & 다중 인증 (`Assets/Scripts/UI/` & `Assets/Scripts/Auth/`)
* **[MetaUIManager.cs](file:///d:/Git_Hub/Skipping_Stones/Assets/Scripts/UI/MetaUIManager.cs)**:
  - 1번 타이틀 ➜ 2번 로비 ➜ 3번 맵/모드 선택 ➜ 인게임 ➜ 결과 화면 상태 머신.
  - 반응형 버튼 터치 좌표 동기화 및 0초 즉각 전환.
* **[IAuthService.cs](file:///d:/Git_Hub/Skipping_Stones/Assets/Scripts/Auth/IAuthService.cs)** & **[AuthManager.cs](file:///d:/Git_Hub/Skipping_Stones/Assets/Scripts/Auth/AuthManager.cs)**:
  - 게스트(`GuestAuthService`), 카카오(`KakaoAuthService`), 스팀(`SteamAuthService`) 다중 인증 추상화.

### 4) 통합 데이터 & DTO (`Assets/Scripts/Data/`)
* **[GameDataDTO.cs](file:///d:/Git_Hub/Skipping_Stones/Assets/Scripts/Data/GameDataDTO.cs)**:
  - `MatchSessionData`, `InGameResultData`, `UserPersistentData`.
* **[GameDataManager.cs](file:///d:/Git_Hub/Skipping_Stones/Assets/Scripts/Data/GameDataManager.cs)**:
  - 10분당 ⚡+1 자동 자연 회복 타이머, 카탈로그, 결과 정산 및 세이브/로드.

### 5) 오디오 & 햅틱 (`Assets/Scripts/Audio/`)
* **`AudioManager.cs`**: 10채널 2D Direct 풀링 (`spatialBlend=0f`), 연속 바운스 시 음계 피치 상승($\pm 5\%$).
* **`HapticFeedbackHelper.cs`**: 모바일 진동 (일반 35ms / 퍼펙트 55ms / 침수 75ms).

---

## 🎯 3. 앞으로 진행 예정 항목 (Next Roadmap)

1. **🚀 갓모드 & 스트리밍 테스트 환경 전면 재정비 ([EnvironmentTestHelper.cs](file:///d:/Git_Hub/Skipping_Stones/Assets/Scripts/Visuals/EnvironmentTestHelper.cs))**:
   - 모듈러 스트리밍(SM ➜ N-Slot Loop ➜ EM) 연동 3,500m+ 장거리 자동 비행 검증.
2. **☀️ 유니티 표준 환경광/조명 시스템 개편 ([LakeEnvironmentManager.cs](file:///d:/Git_Hub/Skipping_Stones/Assets/Scripts/Visuals/LakeEnvironmentManager.cs))**:
   - 유니티 표준 `RenderSettings.ambientMode (Trilight)` + `Directional Light` + `Fog` 글로벌 라이팅 프리셋 체계로 전환.
3. **🌊 수면 인터랙션 오브젝트 규칙 및 점수 피드백 정립**:
   - 부스트 패드, 장애물 바위, 점핑 피쉬 충돌 물리 및 점수 연동.


