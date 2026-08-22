# 📋 작업 인수인계 및 진행 내역 백업 (2026-08-22 이전 작업분)

> **프로젝트명**: Stone Skipping (물수제비 마스터 3D)  
> **엔진 및 해상도**: Unity 6 (6000.5.8f1) / 모바일 세로 9:16 (720x1280 Virtual Native)  
> **정리 시점**: 카카오/스팀/게스트 인증 계층, 데이터 DTO, 메타 UI 매니저, 인게임 분리 C# 프로그래밍 구현 및 컴파일 검증 완료 (0 Error / 0 Warning)

---

## 🎯 1. 이전 구현 완료된 핵심 C# 프로그래밍 내역

### 1) 다중 인증 추상화 계층 (`SkippingStones.Auth`)
* **`IAuthService.cs`**: 게스트(`GuestAuthService`), 카카오(`KakaoAuthService`), 스팀(`SteamAuthService`)을 단일 인터페이스로 다형성 제어.
* **`AuthManager.cs`**: 전역 싱글톤을 통해 로그인/로그아웃/계정 연동을 비동기(`Task`)로 처리하고 유저 프로필을 `GameDataManager`에 즉시 반영.

### 2) 통합 데이터 & DTO 계층 (`SkippingStones.Data`)
* **`GameDataDTO.cs`**:
  - `MatchSessionData`: 선택 캐릭터, 돌, 맵, 게임 모드(`LongDistance` / `TargetAccuracy`)를 인게임으로 주입하는 파라미터 DTO.
  - `InGameResultData`: 최종 거리, 튕김 수, 퍼펙트 횟수, 획득 코인, 포획 물고기를 결과창/저장소로 반환하는 DTO.
  - `UserPersistentData`: 재화(골드 🪙, 다이아 💎, 스태미나 ⚡), 해금 인벤토리, 최고기록의 JSON 영구 세이브 모델.
* **1번 타이틀 화면 (Title Screen)**:
  - 3D 일출 호숫가 배경 & 파티클 연출.
  - **카카오 토큰 미보유 시 (최초 접속 / 로그아웃 상태)**: `[ 🟡 카카오 계정으로 로그인 ]` + `[ 👤 게스트로 시작 ]` 2대 버튼 표출.
  - **카카오 로그인 완료 상태 (토큰 보유 시)**: 버튼 없이 중앙에 **`(( • Touch to Start • ))`** 부드럽게 깜빡이는 펄스 연출 표시 ➜ 화면 어디를 터치하든 즉시 2번 로비로 직행.
* **로비 ➜ 설정(⚙️ Settings) 창 로그아웃 연동**:
  - `[ 🚪 카카오 로그아웃 ]` 버튼 지원 ➜ 클릭 시 세션 토큰을 삭제하고 타이틀 화면의 2개 로그인 버튼 상태로 복귀.
* **`GameDataManager.cs`**:
  - 마스터 카탈로그(캐릭터 3종, 맵 3종) 관리.
  - 10분당 ⚡+1 자동 자연 회복 타이머 및 재화 트랜잭션 처리.
  - 인게임 결과 정산(`ProcessMatchResult`) 및 로컬 세이브/로드.

### 3) 메타 UI 통합 컨트롤러 (`SkippingStones.UI.MetaUIManager.cs`)
* **화면 관리**: 타이틀 ➜ 로비 ➜ 맵 선택 ➜ 인게임 ➜ 결과 화면의 완전한 상태 머신 전환.
* **모달 관리**: 도감(캐릭터/돌/수족관 3단 탭), 상점(가챠/스태미나), 랭킹, 설정, 스태미나 부족 팝업.
* **검증된 반응형 버튼 엔진 (`DrawResponsiveButton`) 이식**:
  - 9:16 모바일 가상 720x1280 뷰포트 종횡비 유지(Letterbox) 및 마우스/터치 좌표 1:1 완벽 동기화.
  - New Input System / Mouse / Touch 단일 프레임 다운 신호 완벽 감지 ➜ **클릭 시 0초 즉각 로비/화면 전환 보장**.
  - 버튼 클릭 효과음(`ButtonClick`) 및 햅틱 진동 연동.

### 4) 인게임 uGUI 결과창 및 리플레이 100% 전담 구조
* **구형 `StoneSkippingUI.cs` 100% 완전 비활성화 (인풋 스틸 원천 차단)**:
  - 구형 IMGUI 스크립트 내부의 모든 OnGUI 및 인풋 감지 코드를 완전히 제거/비활성화하여, 인게임 마우스 클릭 신호 스틸 및 중복 검은 상자 렌더링을 100% 영구 박멸.
* **`EventSystem` New Input System 모듈 자동 바인딩 (`AssignDefaultActions`)**:
  - `StoneSkippingUGUIController.Awake()`에서 `InputSystemUIInputModule`의 마우스/터치 기본 액션(`LeftClickAction`, `PointAction`)을 자동 주입하여 **모든 uGUI 버튼 클릭이 100% 즉각 작동하도록 해결**.
* **`[🎬 리플레이]` ➜ `GameState.Replay` 즉각 상태 전이 보장**:
  - 결과창에서 리플레이 버튼 클릭 시 `gameController.currentState = GameState.Replay`로 즉시 전환되어, 결과창이 닫히고 **uGUI `ReplayPanel` 탑뷰 궤적 맵이 100% 정상 오픈**.
* **완료 버튼(`Result Done Btn`) 리팩토링 & 3번 맵선택 완벽 직행**:
  - 인스펙터 필드명을 `Result Done Btn`(`resultDoneBtn`)으로 변경하고, `[FormerlySerializedAs("resultLobbyBtn")]`를 적용하여 기존 `DoneBtn` 직렬화 유지.
* **`TopDownReplayManager` 인스펙터 슬롯 자동 복구 (Auto-Bootstrap)**:
  - `GameController.ResolveSceneReferences()` 및 `StoneSkippingUGUIController`에서 `_topDownReplay`가 `None`이어도 스스로 `TopDownReplayManager` 컴포넌트를 즉시 장착.
  - 리플레이 버튼 클릭 시 탑다운 90도 직교 카메라(`DualCameraRig`) 전환 및 형광 궤적 애니메이션이 100% 정상 작동하도록 복구 완료.

---

## 📁 2. 📋 [작업 지시서] 로비/맵선택 ➜ 인게임 프리팹 실시간 동적 연동 가이드

### 1) 목표
- 2번 로비(캐릭터, 돌) 및 3번 맵선택(맵, 모드)에서 유저가 선택한 값(`UserData`)을 `MatchSessionData`에 캡슐화하여,
- 인게임 진입 시 `GameController.StartGameSession(session)`이 해당 ID의 실제 프리팹을 `Resources.Load` 또는 카탈로그 딕셔너리를 통해 **실시간으로 스폰하고 인게임 컴포넌트(물리, 카메라, 투구 궤적)에 100% 자동 결합**하도록 구축.

### 2) 데이터 파이프라인 구조
1. **2번 캐릭터 로비 (`MetaUIManager.cs`)**:
   - 캐릭터 스와이프 ➜ `UserData.selectedCharacterId` 갱신 (`char_01` 민우, `char_02` 수아, `char_03` 도사님).
   - 돌 스와이프 ➜ `UserData.selectedStoneId` 갱신 (`stone_basic` 조약돌, `stone_obsidian` 흑요석, `stone_gold` 황금돌).
2. **3번 맵 & 모드 선택 (`MetaUIManager.cs`)**:
   - 맵 카드 선택 ➜ `UserData.selectedMapId` 갱신 (`map_lake` 잔잔한 호수, `map_mist` 안개 낀 강, `map_sunset` 노을 호수).
   - 모드 토글 ➜ `UserData.selectedGameMode` 갱신 (`LongDistance` 1500m 원거리, `TargetAccuracy` 타깃 맞추기).
3. **세션 팩토리 (`GameDataManager.cs`)**:
   - `CreateCurrentMatchSession()` 호출 시 현재 `UserData`를 읽어 `MatchSessionData` 생성.
   - 각 ID에 대응하는 프리팹 경로 매핑 (`Resources/Characters/`, `Resources/Stones/`, `Resources/Maps/` 또는 ScriptableObject Catalog).
4. **인게임 스포너 (`GameController.cs`)**:
   - `StartGameSession(MatchSessionData session)` 실행:
     1. 기존 씬의 캐릭터/돌/맵 정리 (`Destroy` or 재배치).
     2. 선택된 `characterPrefab` 인스턴스화 ➜ `StoneThrowerCharacter` 취득 및 발판 위치 배치.
     3. 선택된 `stonePrefab` 인스턴스화 ➜ `SkippingStone` 취득 및 캐릭터 손에 파지.
     4. 선택된 `mapPrefab` 세팅 ➜ `DualCameraRig` 타깃(`targetCharacter`, `targetStone`) 자동 결합.
     5. `TopDownReplayManager`에 새로운 돌과 발판 레벨 동기화.
