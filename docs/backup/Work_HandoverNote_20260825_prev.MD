# 📋 작업 인수인계 및 진행 내역 정리 (2026-08-25)

> **프로젝트명**: Stone Skipping (물수제비 마스터 3D)  
> **엔진 및 해상도**: Unity 6 (6000.5.8f1) / 모바일 세로 9:16 (720x1280 Virtual Native)  
> **정리 시점**: 리듬 판정/연타 방지 개편, 카메라 3/6 황금 구도 복원 & 지터(떨림) 100% 제거, 스크립트 히스토리/골든 스냅샷 동기화 완료 (0 Error / 0 Warning)

---

## 🎯 1. 어제/오늘 구현 및 문제 해결 완료된 핵심 내역

### 1) ⚡ 리듬 판정 관용도 표준화 & 연타 방지 (Single-Shot Input) ([SkippingStone.cs](file:///d:/Git_Hub/Skipping_Stones/Assets/Scripts/Gameplay/SkippingStone.cs))
* **단 1회 입력 소비 (Single-Shot Input Consumption)**:
  - 타이밍 윈도우 진입 후 첫 번째 탭만 유효 판정으로 소비 (`hasTappedInCurrentBounce`).
  - 연타 꼼수 시 첫 터치로 인한 Early/Miss 처리로 연타 차단.
  - 돌이 바운스되어 상승(`linearVelocity.y > 0.5f`)할 때 다음 하강을 위해 플래그 안전 리셋.
* **수면 도달 예상 시간(Time-to-Impact) 기반 판정 전환**:
  - 기존 높이/거리 기준 불균일 판정에서 `거리 / 수직속도` 시간(초) 기반으로 전환.
  - 판정 관용도: **PERFECT: 100ms (0.10s)**, **GREAT: 220ms (0.22s)**, **GOOD: 380ms (0.38s)**, `timingWindowHeight = 2.8m`.

### 2) 🎥 세로 9:16 '3/6 황금 카메라 구도' 완벽 복원 ([DualCameraSetup.cs](file:///d:/Git_Hub/Skipping_Stones/Assets/Scripts/Visuals/DualCameraSetup.cs))
* **화면 6등분 중 3번째 칸(3/6) 뷰포트 배치 수치 및 공식 복원**:
  - `flightDistBack = 5.5f`, `flightHeight = 2.4f`, `flightLookForward = 7.5f`, **`flightLookHeight = -2.2f`**
* **수면 연동 다이내믹 Y축 비율 추적 공식 복원**:
  - 돌의 수면 위 상대 높이에 따라 부드럽게 카메라 높이(`dynamicCamY`) 및 시선(`dynamicLookY`)을 연동하여 타격감과 시야각 극대화.

### 3) 🧹 리듬 링 및 카메라 시각적 떨림(Jitter / Ghosting) 100% 제거
* **물리 보간 적용**: `SkippingStone.cs` 및 `Stone.prefab` 2종의 `Rigidbody`에 `Interpolate: Interpolate`, `CollisionDetection: ContinuousDynamic` 적용.
* **카메라 추적 보간 안정화**: 고속 비행 시 Lerp 지연 랙 제거로 뷰포트 내 돌 상대 위치 안정화.
* **라인 렌더러 회전 간섭 분리 ([RhythmRingIndicator.cs](file:///d:/Git_Hub/Skipping_Stones/Assets/Scripts/Visuals/RhythmRingIndicator.cs))**:
  - 돌의 초당 1440도 스핀 회전이 링의 노멀에 영향을 주지 않도록 `LateUpdate`에서 월드 수평(`Euler(90, 0, 0)`)으로 회전 강제 고정.

### 4) 🌊 수면 엔티티 프리팹 전환 & `BoxCollider` 기반 스폰 정상화 ([RiverSpawner.cs](file:///d:/Git_Hub/Skipping_Stones/Assets/Scripts/Gameplay/RiverSpawner.cs))
* 6종 수면 엔티티 프리팹 정식 등록 및 런타임 더미 생성 콘솔 Warning 100% 박멸.
* 수면 `BoxCollider`의 전폭 `[minX, maxX]` 기준 레이캐스트 안전 스폰.

### 5) ⚙️ 인게임 초기화 라이프사이클 순서 보장 ([GameController.cs](file:///d:/Git_Hub/Skipping_Stones/Assets/Scripts/Gameplay/GameController.cs))
* `SetupCharacter` ➜ `SetupMapEnvironment` (지형/수면/콜라이더 완료) ➜ `RiverSpawner.GenerateRiverEntitiesForMode` 순차 실행으로 원점 스폰 버그 원천 해결.

### 6) 🚀 갓모드(자동 비행) 수면 높이(Y=16m) & S자 물길 곡선 추적 ([EnvironmentTestHelper.cs](file:///d:/Git_Hub/Skipping_Stones/Assets/Scripts/Visuals/EnvironmentTestHelper.cs))
* S자 굽이치는 물길 곡선 추적 및 3,500m 맵 스트리밍 테스트 완료.

### 7) 🗺️ 모듈러 스토리 맵 스트리밍 & 직관적 에디터 완비 ([LakeEnvironmentManager.cs](file:///d:/Git_Hub/Skipping_Stones/Assets/Scripts/Visuals/LakeEnvironmentManager.cs) & [LakeEnvironmentManagerEditor.cs](file:///d:/Git_Hub/Skipping_Stones/Assets/Scripts/Visuals/Editor/LakeEnvironmentManagerEditor.cs))
* **스핀박스 기반 N-슬롯 루프 구성**: `루프 맵 개수 (슬롯 수) : [ ▲ 1 ▼ ]` 조작으로 `[ 슬롯 1 ]`, `[ 슬롯 2 ]` 직관적 생성.
* **`Base Map` + `변주(랜덤) 체크박스` 분리**: 기본 맵 프리팹을 기준 삼고, 체크 시에만 변주 목록 리스트가 열리는 초슬림 인스펙터 구축.
* **Fail-Fast 엄격한 검증**: Base Map 누락 또는 시작 지점 프리팹에 `Platform` 누락 시 즉시 명시적 에러 로그 출력.
* **0m 독립 청크 스폰**: 씬에 미리 `BG_01`을 올려두지 않아도 슬롯 1번 BaseMap(또는 SM)을 알아서 0m에 인스턴스화.

### 8) 🗂️ 프리팹 폴더 단일화 (Single Source of Truth)
* **`Assets/prefab/BG_Deco/` 신규 분리**: `BoostPad`, `ObstacleRock`, `TargetZone`, `FriendFlag`, `JumpingFish`, `LilyPadCluster`를 한곳에서 안전하게 편집 가능하도록 통합.
* **`BG_01.prefab` 단일화**: Assets 루트 및 Resources 구형 중복 파일 완전 삭제.
* **Resources 메모리 다이어트**: 미사용 대형 에셋 걷어내어 게임 시작 로딩 속도 및 메모리 안정성 확보.

### 9) 🖼️ 3번 맵 선택 UI ➜ `Assets/prefab/BG_Env/` 자동 스캔 & 썸네일/타이틀 연동 ([MetaUIManager.cs](file:///d:/Git_Hub/Skipping_Stones/Assets/Scripts/UI/MetaUIManager.cs))
* `Assets/prefab/BG_Env/`에 저장된 환경 매니저 프리팹들을 자동 스캔하여 `Map Title`과 `Map Thumbnail (2D)`를 3번 화면 카드 뷰에 실시간 드로잉.
* `[⚡-1 GAME START !]` 클릭 시 선택된 환경 매니저가 씬에 동적 스폰되어 인게임 맵을 완벽하게 초기화.

---

## 📁 2. 관련 핵심 파일 및 문서 목록

1. **스크립트 히스토리 문서**:
   - [SkippingStone.md](file:///d:/Git_Hub/Skipping_Stones/docs/script_history/SkippingStone.md)
   - [DualCameraSetup.md](file:///d:/Git_Hub/Skipping_Stones/docs/script_history/DualCameraSetup.md)
   - [RhythmRingIndicator.md](file:///d:/Git_Hub/Skipping_Stones/docs/script_history/RhythmRingIndicator.md)
   - [RiverSpawner.md](file:///d:/Git_Hub/Skipping_Stones/docs/script_history/RiverSpawner.md)
   - [GameController.md](file:///d:/Git_Hub/Skipping_Stones/docs/script_history/GameController.md)
   - [LakeEnvironmentManager.md](file:///d:/Git_Hub/Skipping_Stones/docs/script_history/LakeEnvironmentManager.md)
   - [EnvironmentTestHelper.md](file:///d:/Git_Hub/Skipping_Stones/docs/script_history/EnvironmentTestHelper.md)
2. **골든 베이스라인 스냅샷 (`docs/golden_scripts/`)**:
   - `SkippingStone.cs.txt`, `DualCameraSetup.cs.txt`, `RhythmRingIndicator.cs.txt`, `RiverSpawner.cs.txt`, `GameController.cs.txt`, `LakeEnvironmentManager.cs.txt`, `MetaUIManager.cs.txt`, `EnvironmentTestHelper.cs.txt`
3. **엄격 규칙 및 프로토콜**:
   - [rule_compliance_and_exceptions.md](file:///d:/Git_Hub/Skipping_Stones/docs/rules/rule_compliance_and_exceptions.md)
   - [script_history_protocol.md](file:///d:/Git_Hub/Skipping_Stones/docs/rules/script_history_protocol.md)

---

## 🎯 3. 다음 작업 예정 항목 (Next Action Items)
- **1) 2번 로비 3D 쇼케이스 디오라마 룸 분리 연동**:
  - 1번 타이틀 ➜ 2번 로비 진입 시 전용 3D 디오라마(캐릭터/돌 쇼룸) 로드 및 인게임 진입 시 정리.
- **2) ☀️ 유니티 표준 환경광/조명(Lighting & Ambient) 시스템 개편**:
  - `RenderSettings.ambientMode (Trilight)` + `Directional Light` + `Fog` 글로벌 라이팅 프리셋 체계로 전환.
- **3) 🌊 수면 인터랙션 오브젝트 규칙 및 점수/피드백 정립**:
  - 부스트 패드, 장애물 바위, 점핑 피쉬 충돌 물리 및 점수 연동.



