# 🛠️ Stone Skipping - 현재 구현 현황 및 시스템 아키텍처 명세서 (Implementation Status)

> **문서 목적**: 기획 검토 및 신규 기능 연동 시, 현재까지 구현 완료된 C# 스크립트, 물리/게임플레이 로직, 씬 구조를 명확히 파악하기 위한 기술 인수인계/현황 문서.  
> **엔진 및 플랫폼**: Unity 6 (6000.5.8f1) / Mobile Portrait (9:16)  
> **기준 일자**: 2026-08-22

---

## 🏗️ 1. 현재 구현 완료된 핵심 시스템 요약

```mermaid
graph TD
    subgraph Core_Gameplay [인게임 핵심 루프 (완료)]
        GC[GameController.cs] --> STC[StoneThrowerCharacter.cs\n(각도/파워 조준 및 투구)]
        GC --> SS[SkippingStone.cs\n(물리 바운스/스키밍/감속)]
        GC --> RS[RiverSpawner.cs\n(무한 1,500m 청크/BG_01 스트리밍)]
    end

    subgraph Mode_Logic [2대 게임 모드 (완료)]
        M1[모드 1: 원거리 기록 모드\n(+Z축 1,500m 릴레이, 부스트/장애물)]
        M2[모드 2: 타깃 과녁 모드\n(+X축 강 건너기, PP01~PP10 스폰, FloatingTargetZone)]
    end

    subgraph Visuals_Camera [연출 및 카메라 (완료)]
        DCS[DualCameraSetup.cs\n(메인 쿼터뷰 + 타깃 PIP)]
        TDR[TopDownReplayManager.cs\n(90도 탑다운 직교 리플레이/자동 프레이밍)]
        RRI[RhythmRingIndicator.cs\n(바운스 타이밍 링 연출)]
    end

    subgraph UI_Audio [UI 및 피드백 (완료)]
        SUI[StoneSkippingUI.cs & uGUI\n(단일 터치/릴리즈 락 안전성 확보)]
        AUD[AudioManager.cs & HapticHelper\n(10채널 2D 오디오 풀링 & 모바일 햅틱)]
    end
```

---

## 📂 2. 모듈별 상세 구현 현황 및 스크립트 목록

### 1) 게임플레이 & 메인 컨트롤러 (`Assets/Scripts/Gameplay/`)
* **[GameController.cs](file:///d:/Git_Hub/Skipping_Stones/Assets/Scripts/Gameplay/GameController.cs)**:
  - 0단계(준비) ~ 6단계(정산) 전체 게임 상태 머신 총괄.
  - `GameMode` (`LongDistance` vs `TargetMode`) 지원.
  - `SetupCharacterSpawn()`: 모드에 따라 롱디스턴스용 목재 발판(`Lakeside_Platform`) 또는 타깃 모드용 강변 발판(`PP01~PP10`)에 캐릭터 자동 스폰.
* **[StoneThrowerCharacter.cs](file:///d:/Git_Hub/Skipping_Stones/Assets/Scripts/Gameplay/StoneThrowerCharacter.cs)**:
  - 1단계 좌우 스윙 각도 조준기 & 2단계 파워 게이지 차징.
  - 투구 애니메이션 및 돌 발사 시점 이벤트 동기화.
* **[SkippingStone.cs](file:///d:/Git_Hub/Skipping_Stones/Assets/Scripts/Gameplay/SkippingStone.cs)**:
  - 수면 충돌 감지, 입사각/속도 기반 바운스 물리 계산.
  - 리듬 터치(PERFECT / GREAT / NORMAL) 판정 및 5스킵 이상 시 '하이드로' 고속 스키밍 활성화.
  - 돌 침수(Sink) 및 정지 판정.
* **[RiverSpawner.cs](file:///d:/Git_Hub/Skipping_Stones/Assets/Scripts/Gameplay/RiverSpawner.cs)**:
  - 1,500m 강줄기 무한 청크(`chunk0`, `chunk1`...) 풀링 생성 및 재활용.
  - 시작 청크(`chunk0`)에서만 시작 발판 활성화, 이후 청크는 발판 자동 비활성화.
* **기믹/오브젝트**:
  - `BoostPad.cs`: 밟으면 순간 가속 부스트.
  - `ObstacleRock.cs`: 충돌 시 돌 궤적 이탈/감속.
  - `JumpingFish.cs`: 수면 점프 물고기 보너스.
  - `FloatingTargetZone.cs`: 강 건너편 플로팅 과녁 점수 판정.
  - `FriendFlag.cs`: 카카오 친구 기록 지점에 깃발 배치.

### 2) 비주얼, 카메라 & 리플레이 (`Assets/Scripts/Visuals/`)
* **[TopDownReplayManager.cs](file:///d:/Git_Hub/Skipping_Stones/Assets/Scripts/Visuals/TopDownReplayManager.cs)**:
  - 투구 종료 후 탑다운 90도 직교(Orthographic) 카메라로 자동 전환.
  - 전체 이동 궤적을 화면 비율(9:16)에 맞게 자동 확대/축소(`Auto-Framing`)하여 리플레이 재생.
* **[DualCameraSetup.cs](file:///d:/Git_Hub/Skipping_Stones/Assets/Scripts/Visuals/DualCameraSetup.cs)** & **[MapPIPManager.cs](file:///d:/Git_Hub/Skipping_Stones/Assets/Scripts/Visuals/MapPIPManager.cs)**:
  - 메인 쿼터뷰 카메라와 강 지형 전체를 보여주는 상단 PIP 미니맵 동시 렌더링.
* **[RhythmRingIndicator.cs](file:///d:/Git_Hub/Skipping_Stones/Assets/Scripts/Visuals/RhythmRingIndicator.cs)** & **[SplashEffectSpawner.cs](file:///d:/Git_Hub/Skipping_Stones/Assets/Scripts/Visuals/SplashEffectSpawner.cs)**:
  - 수면 접촉 시 타이밍 수축 링 이펙트 및 물보라 파티클 스폰.

### 3) 메타 UI 시스템 & 다중 인증 (`Assets/Scripts/UI/` & `Assets/Scripts/Auth/`)
* **[MetaUIManager.cs](file:///d:/Git_Hub/Skipping_Stones/Assets/Scripts/UI/MetaUIManager.cs)**:
  - 1번 타이틀 ➜ 2번 로비 ➜ 3번 맵/모드 선택 ➜ 인게임 ➜ 결과 화면의 완전한 상태 머신.
  - 도감(캐릭터/돌/수족관 3단 탭), 상점, 랭킹, 설정 모달 관리.
  - `[RuntimeInitializeOnLoadMethod]` 자동 부트스트랩 지원.
  - 화면 전환 시 `requireTouchRelease = true` & 0.25s 디바운스 쿨다운 강제.
* **[IAuthService.cs](file:///d:/Git_Hub/Skipping_Stones/Assets/Scripts/Auth/IAuthService.cs)** & **[AuthManager.cs](file:///d:/Git_Hub/Skipping_Stones/Assets/Scripts/Auth/AuthManager.cs)**:
  - 게스트(`GuestAuthService`), 카카오(`KakaoAuthService`), 스팀(`SteamAuthService`) 다중 인증 추상화.

### 4) 통합 데이터 & DTO (`Assets/Scripts/Data/`)
* **[GameDataDTO.cs](file:///d:/Git_Hub/Skipping_Stones/Assets/Scripts/Data/GameDataDTO.cs)**:
  - `MatchSessionData`: 메타 UI ➜ 인게임 씬 시작 파라미터 DTO.
  - `InGameResultData`: 인게임 ➜ 결과창 및 세이브 정산 DTO.
  - `UserPersistentData`: 골드 🪙, 다이아 💎, 스태미나 ⚡ JSON 영구 저장 모델.
* **[GameDataManager.cs](file:///d:/Git_Hub/Skipping_Stones/Assets/Scripts/Data/GameDataManager.cs)**:
  - 10분당 ⚡+1 자동 자연 회복 타이머, 카탈로그, 결과 정산 및 세이브/로드.
  - `[RuntimeInitializeOnLoadMethod]` 자동 생성 지원.

### 5) 오디오 & 햅틱 (`Assets/Scripts/Audio/`)
* **`AudioManager.cs`**: 10채널 2D Direct 풀링 (`spatialBlend=0f`), 연속 바운스 시 음계 피치 상승($\pm 5\%$).
* **`HapticFeedbackHelper.cs`**: 모바일 진동 (일반 35ms / 퍼펙트 55ms 풀파워 / 침수 75ms).

---

## 🎯 3. 앞으로 진행 가능한 확장 영역

1. **카카오 SDK & Steamworks 네이티브 패키지 플러그인 임베딩**:
   - `KakaoAuthService` 및 `SteamAuthService` 내부를 실제 패키지 API 호출로 바인딩.
2. **uGUI 커스텀 글래스모피즘 비주얼 프리팹 에셋 연결**:
   - 디자이너 UI 스프라이트 및 3D 턴테이블 카메라 연출 에셋 고도화.

