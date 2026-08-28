# 📜 [Skipping Stones] 전체 스크립트 명세, 기능 및 부착 가이드

> **문서 목적**: 본 프로젝트(`Skipping_Stones`)에서 제작 및 사용 중인 모든 C# 스크립트의 **이름, 핵심 기능, 부착 대상 게임오브젝트/프리팹 위치**를 체계적으로 정리한 마스터 가이드입니다.

---

## 1. 🎮 Gameplay (인게임 코어 물리 & 규칙)

| 스크립트 이름 | 부착 대상 (Target Object / Prefab) | 핵심 기능 및 역할 |
| :--- | :--- | :--- |
| **[GameController.cs](file:///d:/Git_Hub/Skipping_Stones/Assets/Scripts/Gameplay/GameController.cs)** | 씬 내 **`[GameController]`** 매니저 오브젝트 | 인게임 전체 상태 머신(대기➔차징➔비행➔정산), 투구 세션 파라미터 주입, 리플레이 및 카메라 오케스트레이션 총괄 |
| **[SkippingStone.cs](file:///d:/Git_Hub/Skipping_Stones/Assets/Scripts/Gameplay/SkippingStone.cs)** | **`Stone.prefab`**, **`Stone_xxx.prefab`** (모든 돌 프리팹 루트) | 물수제비 수면 바운스 물리 역학 시뮬레이터 (입사각, 회전 스핀, 양력/항력, 수면 충돌 감지) |
| **[StoneThrowerCharacter.cs](file:///d:/Git_Hub/Skipping_Stones/Assets/Scripts/Gameplay/StoneThrowerCharacter.cs)** | **`Thrower_Minwoo.prefab`**, **`Thrower_xxx.prefab`** (모든 캐릭터 프리팹 루트) | 캐릭터 애니메이션 재생, 오른손 본(`Dummy001`) 소켓 관리, 55프레임 정밀 릴리즈(발사) 제어 |
| **[RiverSpawner.cs](file:///d:/Git_Hub/Skipping_Stones/Assets/Scripts/Gameplay/RiverSpawner.cs)** | 씬 내 **`[RiverSpawner]`** 또는 `GameController` 하위 | 돌의 비행 거리에 맞춰 전방 강물 지형 청크와 환경 오브젝트를 무한 동적 스트리밍 생성/재활용 |
| **[BoostPad.cs](file:///d:/Git_Hub/Skipping_Stones/Assets/Scripts/Gameplay/BoostPad.cs)** | **`BoostPad.prefab`** (가속 발판 프리팹 루트) | 수면에 부유하는 가속 발판 기믹. 접촉 시 전방 순간 가속 및 높은 양력 바운스 부여 |
| **[ObstacleRock.cs](file:///d:/Git_Hub/Skipping_Stones/Assets/Scripts/Gameplay/ObstacleRock.cs)** | **`ObstacleRock.prefab`** (장애물 바위 프리팹 루트) | 강물 중간에 솟아 있는 장애물. 충돌 시 속도 급감 및 궤적 굴절 판정 |
| **[JumpingFish.cs](file:///d:/Git_Hub/Skipping_Stones/Assets/Scripts/Gameplay/JumpingFish.cs)** | **`JumpingFish.prefab`** (물고기 프리팹 루트) | 수면에서 뛰어오르는 물고기 포물선 애니메이션 및 돌과 접촉 시 보너스 점수 부여 |
| **[FloatingTargetZone.cs](file:///d:/Git_Hub/Skipping_Stones/Assets/Scripts/Gameplay/FloatingTargetZone.cs)** | **`FloatingTargetZone.prefab`** (목표 타깃 링 프리팹) | 타깃 챌린지 모드에서 수면에 배치되는 원형 목표 지점 적중 판정 |
| **[FriendFlag.cs](file:///d:/Git_Hub/Skipping_Stones/Assets/Scripts/Gameplay/FriendFlag.cs)** | **`FriendFlag.prefab`** (친구 기록 깃발 프리팹) | 친구/랭커들의 최고 기록 지점에 꽂히는 3D 깃발 아바타 표시 |
| **[PlayerPositionPath.cs](file:///d:/Git_Hub/Skipping_Stones/Assets/Scripts/Gameplay/PlayerPositionPath.cs)** | 씬 내 **`Player_Position`** (가이드 리본 오브젝트) | 강변 투구 구역의 좌우 이동 곡선 가이드 레일 웨이포인트 샘플링 및 기즈모 표시 |

---

## 2. 🪨 Data & Catalog (도감 및 세이브 데이터)

| 스크립트 이름 | 부착 대상 (Target Object / Prefab) | 핵심 기능 및 역할 |
| :--- | :--- | :--- |
| **[StoneCatalogManager.cs](file:///d:/Git_Hub/Skipping_Stones/Assets/Scripts/Data/StoneCatalogManager.cs)** | 씬 내 **`[StoneCatalogManager]`** (빈 오브젝트 생성 후 부착, 작업 후 삭제 가능) | 조약돌 도감의 단일 진실 공급원 관리 컴포넌트 (`Resources/Data/StoneCatalogData.json`과 양방향 동기화) |
| **[GameDataManager.cs](file:///d:/Git_Hub/Skipping_Stones/Assets/Scripts/Data/GameDataManager.cs)** | 씬 내 **`[GameDataManager]`** (부트스트랩 시 자동 생성 또는 씬 배치) | 싱글톤 매니저 (골드, 다이아, 스태미나, 해금된 돌/캐릭터 도감, 세이브/로드 총괄) |
| **[GameDataDTO.cs](file:///d:/Git_Hub/Skipping_Stones/Assets/Scripts/Data/GameDataDTO.cs)** | **부착 불필요** (순수 C# 데이터 클래스) | `StoneInfoData`, `CharacterInfoData`, `MapInfoData`, `MatchSessionData` 등 DTO 구조체 정의 |
| **[StoneInventory.cs](file:///d:/Git_Hub/Skipping_Stones/Assets/Scripts/Data/StoneInventory.cs)** | 씬 내 **`[GameDataManager]`** 또는 인벤토리 매니저 | 유저가 보유한 조약돌 인벤토리 및 장착 슬롯 관리 |
| **[AquariumManager.cs](file:///d:/Git_Hub/Skipping_Stones/Assets/Scripts/Data/AquariumManager.cs)** | 수조 씬 내 **`[AquariumManager]`** 오브젝트 | 플레이 중 획득한 수집형 물고기 도감 및 3D 수조 전시 데이터 관리 |

---

## 3. 🌊 Visuals & Environment (렌더링, 카메라 & 쇼케이스)

| 스크립트 이름 | 부착 대상 (Target Object / Prefab) | 핵심 기능 및 역할 |
| :--- | :--- | :--- |
| **[LobbyStoneShowcaseController.cs](file:///d:/Git_Hub/Skipping_Stones/Assets/Scripts/Visuals/LobbyStoneShowcaseController.cs)** | **`Lobby.prefab`** 또는 씬 내 **`Stone_Diorama`** 루트 | 로비 3D 디오라마의 하단 다이얼(30도)과 상단 3슬롯 턴테이블(120도) 무한 캐러셀 및 돌 스탠드 제어 |
| **[LobbyCharacterShowcaseController.cs](file:///d:/Git_Hub/Skipping_Stones/Assets/Scripts/Visuals/LobbyCharacterShowcaseController.cs)** | **`Lobby.prefab`** 또는 씬 내 **`Lobby_Showcase`** 루트 | 로비 3D 캐릭터 슬라이드 등장/퇴장 트랜지션 및 360도 마우스/터치 드래그 감상 제어 |
| **[LakeEnvironmentManager.cs](file:///d:/Git_Hub/Skipping_Stones/Assets/Scripts/Visuals/LakeEnvironmentManager.cs)** | 씬 내 **`[LakeEnvironmentManager]`** 또는 `Environment` 루트 | 호수/강 주변 환경 조명(Directional Light), 포그(Fog), 스카이박스, 수면 반사광 프리셋 관리 |
| **[WaterSurface.cs](file:///d:/Git_Hub/Skipping_Stones/Assets/Scripts/Visuals/WaterSurface.cs)** | 씬 내 **`Water`** / **`WaterSurface`** (수면 평면 메쉬) | 수면 높이(Y축)와 유속 기준값을 단일 진실 공급원으로 제공하여 모든 물리 시스템에 기준점 전달 |
| **[DualCameraSetup.cs](file:///d:/Git_Hub/Skipping_Stones/Assets/Scripts/Visuals/DualCameraSetup.cs)** | 씬 내 **`[CameraRig]`** 또는 메인 카메라 부모 오브젝트 | 3인칭 쿼터뷰 메인 추적 카메라와 직하향 탑다운(PIP) 카메라 듀얼 렌더링/뷰포트 제어 |
| **[MapPIPManager.cs](file:///d:/Git_Hub/Skipping_Stones/Assets/Scripts/Visuals/MapPIPManager.cs)** | 씬 내 **`[MapPIPManager]`** 또는 UI 캔버스 | 상단 미니맵 픽처인픽처(PIP) 렌더텍스처 뷰 및 카메라 동기화 제어 |
| **[TopDownReplayManager.cs](file:///d:/Git_Hub/Skipping_Stones/Assets/Scripts/Visuals/TopDownReplayManager.cs)** | 씬 내 **`[TopDownReplayManager]`** 오브젝트 | 비행 종료 후 전체 비행 궤적을 하늘에서 직하향으로 부드럽게 되감기/줌인/줌아웃하는 리플레이 연출기 |
| **[RhythmRingIndicator.cs](file:///d:/Git_Hub/Skipping_Stones/Assets/Scripts/Visuals/RhythmRingIndicator.cs)** | **`RhythmRing.prefab`** (수면 타이밍 링 프리팹) | 돌이 수면에 닿기 직전 수면에 축소되는 리듬 판정 링 표시 (Perfect/Great/Good 비주얼 피드백) |
| **[SplashEffectSpawner.cs](file:///d:/Git_Hub/Skipping_Stones/Assets/Scripts/Visuals/SplashEffectSpawner.cs)** | 씬 내 **`[SplashEffectSpawner]`** 또는 `GameController` 하위 | 수면 바운스 시 물보라 파티클, 물방울, 수면 파문(Ripple) 이펙트 오브젝트 풀 스폰 |
| **[PebbleMeshGenerator.cs](file:///d:/Git_Hub/Skipping_Stones/Assets/Scripts/Visuals/PebbleMeshGenerator.cs)** | 씬 내 절차적 메쉬 생성용 테스트 오브젝트 | 절차적(Procedural)으로 납작 유선형 조약돌 3D 메쉬와 UV를 런타임 자동 생성 |
| **[WindowsAspectRatioController.cs](file:///d:/Git_Hub/Skipping_Stones/Assets/Scripts/Visuals/WindowsAspectRatioController.cs)** | 씬 내 **`[WindowsAspectRatioController]`** 또는 `[GameController]` | PC 빌드/에디터 실행 시 지정된 모바일 화면비(9:16)로 윈도우 해상도 및 레터박스 강제 고정 |
| **[EnvironmentTestHelper.cs](file:///d:/Git_Hub/Skipping_Stones/Assets/Scripts/Visuals/EnvironmentTestHelper.cs)** | 씬 내 **`[EnvironmentTestHelper]`** (테스트 씬 전용) | 에디터 상에서 지형, 수면, 조명 등을 즉석에서 테스트하고 투구 시뮬레이션을 실행하는 디버그 툴 |

---

## 4. 🌄 Terrain (지형 절차적 생성 & 버텍스)

| 스크립트 이름 | 부착 대상 (Target Object / Prefab) | 핵심 기능 및 역할 |
| :--- | :--- | :--- |
| **[RiverValleyTerrainGenerator.cs](file:///d:/Git_Hub/Skipping_Stones/Assets/Scripts/Terrain/RiverValleyTerrainGenerator.cs)** | 씬 내 **`Terrain_Generator`** 또는 `River_Valley` 오브젝트 | 강줄기를 따라 계곡 양옆의 언덕, 바위 절벽, 모래사장을 절차적 3D 메쉬로 생성하는 지형 엔진 |
| **[RiverValleyTerrainPreset.cs](file:///d:/Git_Hub/Skipping_Stones/Assets/Scripts/Terrain/RiverValleyTerrainPreset.cs)** | **부착 불필요** (`.asset` ScriptableObject 파일) | 지형 너비, 협곡 깊이, 노이즈 빈도, 텍스처 블렌딩 설정을 담는 에셋 프리셋 |
| **[RiverValleyObjectSpawner.cs](file:///d:/Git_Hub/Skipping_Stones/Assets/Scripts/Terrain/RiverValleyObjectSpawner.cs)** | 씬 내 **`Object_Spawner`** 또는 지형 하위 오브젝트 | 생성된 지형 표면에 나무, 바위, 풀, 갈대 등의 자연물을 규칙에 따라 자동 분배 배치 |
| **[TerrainSeamless.cs](file:///d:/Git_Hub/Skipping_Stones/Assets/Scripts/Terrain/TerrainSeamless.cs)** | 씬 내 지형 청크들의 부모 오브젝트 | 분할된 지형 청크들 간의 경계선(Seam) 정점 높이 및 노멀 벡터를 일치시켜 이음새 제거 |

---

## 5. 📱 UI & HUD (사용자 인터페이스)

| 스크립트 이름 | 부착 대상 (Target Object / Prefab) | 핵심 기능 및 역할 |
| :--- | :--- | :--- |
| **[MetaUIManager.cs](file:///d:/Git_Hub/Skipping_Stones/Assets/Scripts/UI/MetaUIManager.cs)** | 씬 내 **`[MetaUIManager]`** (UI 매니저 캔버스) | 로비, 도감, 캐릭터/맵 선택, 상점, 세팅 등 메타 UI 모달 팝업 및 화면 전환 총괄 |
| **[StoneSkippingUGUIController.cs](file:///d:/Git_Hub/Skipping_Stones/Assets/Scripts/UI/StoneSkippingUGUIController.cs)** | 인게임 메인 HUD 캔버스 (**`[InGame_Canvas]`**) | 인게임 메인 HUD (비행 거리 게이지, 콤보 카운터, 바운스 횟수, 속도계 오버레이) |
| **[StoneSkippingUI.cs](file:///d:/Git_Hub/Skipping_Stones/Assets/Scripts/UI/StoneSkippingUI.cs)** | 인게임 HUD 캔버스 오브젝트 | 인게임 터치 게이지 바, 파워 바운스 타이밍 인디케이터 UI 제어 |

---

## 6. 🔊 Audio & Feedback (사운드 및 햅틱)

| 스크립트 이름 | 부착 대상 (Target Object / Prefab) | 핵심 기능 및 역할 |
| :--- | :--- | :--- |
| **[AudioManager.cs](file:///d:/Git_Hub/Skipping_Stones/Assets/Scripts/Audio/AudioManager.cs)** | 씬 내 **`[AudioManager]`** 싱글톤 오브젝트 | BGM, 수면 퐁당/찰랑 효과음, 바람 소리, UI 클릭음 오디오 클립 재생 및 볼륨 페이딩 |
| **[SoundSynthesizer.cs](file:///d:/Git_Hub/Skipping_Stones/Assets/Scripts/Audio/SoundSynthesizer.cs)** | 씬 내 **`[SoundSynthesizer]`** 또는 `AudioManager` 하위 | 돌의 크기/무게/속도에 맞춘 실시간 절차적 물 튀는 소리 합성기 |
| **[HapticFeedbackHelper.cs](file:///d:/Git_Hub/Skipping_Stones/Assets/Scripts/Audio/HapticFeedbackHelper.cs)** | **부착 불필요** (정적 유틸리티 C# 클래스) | 모바일 터치 및 수면 바운스 시점에 진동/햅틱 피드백(Light/Medium/Heavy) 트리거 |

---

## 7. 🔐 Auth (계정 & 인증)

| 스크립트 이름 | 부착 대상 (Target Object / Prefab) | 핵심 기능 및 역할 |
| :--- | :--- | :--- |
| **[AuthManager.cs](file:///d:/Git_Hub/Skipping_Stones/Assets/Scripts/Auth/AuthManager.cs)** | 씬 내 **`[AuthManager]`** 싱글톤 오브젝트 | 게스트 로그인, 구글/애플 계정 연동 및 유저 고유 UID 발급/관리 |
| **[IAuthService.cs](file:///d:/Git_Hub/Skipping_Stones/Assets/Scripts/Auth/IAuthService.cs)** | **부착 불필요** (C# 인터페이스 규격) | 인증 서비스 인터페이스 규격 정의 |

---

## 8. 🛠️ Editor Tools (에디터 전용 - 오브젝트 부착 불필요)

> 💡 **에디터 툴 안내**: 아래 스크립트들은 `Assets/Scripts/Editor/` 폴더에 위치하여 유니티 에디터 상단 메뉴나 타깃 인스펙터에 **자동 연동**되므로 씬 내 게임오브젝트에 직접 부착하지 않습니다.

| 스크립트 이름 | 동작 위치 / 메뉴 경로 | 핵심 기능 및 역할 |
| :--- | :--- | :--- |
| **[StoneCatalogManagerEditor.cs](file:///d:/Git_Hub/Skipping_Stones/Assets/Scripts/Editor/StoneCatalogManagerEditor.cs)** | `StoneCatalogManager` 컴포넌트 인스펙터 | 프리팹 드래그&드롭 기반 돌 신규 등록/편집/삭제 및 JSON 원클릭 저장 GUI 제공 |
| **[SimpleVertexPainterWindow.cs](file:///d:/Git_Hub/Skipping_Stones/Assets/Scripts/Editor/SimpleVertexPainterWindow.cs)** | 상단 메뉴: **`Tools ➔ Skipping Stones ➔ Simple Terrain Vertex Painter`** | 씬 뷰 지형 메쉬에 4-Layer 텍스처(잔디/흙/바위/모래)를 직접 칠하는 유니티 6 버텍스 페인터 창 |
| **[PrefabHealthCheckTool.cs](file:///d:/Git_Hub/Skipping_Stones/Assets/Scripts/Editor/PrefabHealthCheckTool.cs)** | 상단 메뉴: **`Tools ➔ Skipping Stones ➔ Prefab Health Check & Auto Fix`** | 캐릭터/돌/로비 프리팹의 필수 컴포넌트 누락을 전수 검사하고 원클릭 자동 복구하는 헬스체크 툴 |
| **[LakeEnvironmentManagerEditor.cs](file:///d:/Git_Hub/Skipping_Stones/Assets/Scripts/Visuals/Editor/LakeEnvironmentManagerEditor.cs)** | `LakeEnvironmentManager` 컴포넌트 인스펙터 | 환경 조명 및 수면 프리셋을 인스펙터 버튼으로 즉시 프리뷰/적용하는 커스텀 인스펙터 |
| **[RiverValleyTerrainGeneratorEditor.cs](file:///d:/Git_Hub/Skipping_Stones/Assets/Scripts/Terrain/Editor/RiverValleyTerrainGeneratorEditor.cs)** | `RiverValleyTerrainGenerator` 컴포넌트 인스펙터 | 에디터에서 계곡 지형 메쉬를 원클릭으로 베이크/생성/초기화하는 커스텀 인스펙터 |
| **[RiverValleyObjectSpawnerEditor.cs](file:///d:/Git_Hub/Skipping_Stones/Assets/Scripts/Terrain/Editor/RiverValleyObjectSpawnerEditor.cs)** | `RiverValleyObjectSpawner` 컴포넌트 인스펙터 | 지형 위 나무/바위 자동 배치를 에디터에서 즉시 시뮬레이션하고 저장하는 커스텀 인스펙터 |
| **[TerrainSeamlessEditor.cs](file:///d:/Git_Hub/Skipping_Stones/Assets/Scripts/Terrain/Editor/TerrainSeamlessEditor.cs)** | `TerrainSeamless` 컴포넌트 인스펙터 | 지형 청크들의 경계선 노멀/버텍스 스무딩을 일괄 실행하는 에디터 툴 |
| **[SkippingStoneEditor.cs](file:///d:/Git_Hub/Skipping_Stones/Assets/Scripts/Gameplay/Editor/SkippingStoneEditor.cs)** | `SkippingStone` 컴포넌트 인스펙터 | 인스펙터에서 돌의 물리 계수(양력, 항력, 수면 마찰)를 실시간 기즈모와 함께 조정하는 커스텀 인스펙터 |
