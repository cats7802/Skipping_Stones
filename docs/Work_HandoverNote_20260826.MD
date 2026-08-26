# [Work Handover Note] 2026-08-26: 로비 3D 캐릭터 쇼케이스 & 프리팹 무결성 검증 체계

## 1. 작업 개요
* **일자**: 2026-08-26
* **핵심 작업**:
  1. 로비 화면 3D 캐릭터 선택 및 쇼케이스 시스템 신규 구축 (`LobbyCharacterShowcaseController.cs`)
  2. 로비 ➔ 인게임 캐릭터 및 돌 선택 ID 100% 동기화 파이프라인 완성
  3. 터치 간섭 방지: 3D Raycast 기반 돌 다이얼 vs 캐릭터 360도 회전 완전 분리
  4. 암묵적 `AddComponent` 전면 제거 및 정직한 참조(Warning 로깅) 아키텍처 수립
  5. 원클릭 프리팹 무결성 검증 툴 신규 제작 (`PrefabHealthCheckTool.cs`)
  6. `Assets/Resources/` 내 구버전 찌꺼기 프리팹/에셋 전면 삭제 정리

---

## 2. 세부 구현 및 아키텍처

### 👤 3D 캐릭터 쇼케이스 (`LobbyCharacterShowcaseController.cs`)
* **프리팹 자동 스캔**: `Assets/` 내 `StoneThrowerCharacter` 컴포넌트를 가진 모든 프리팹 자동 탐색 + 카탈로그 해금(`isUnlocked`) 필터링 적용.
* **45도 쿼터뷰 맞춤 트랜지션**: 로비 Y+45도 보정 축을 기준으로 ◀ / ▶ 클릭 시 화면 좌↔우로 걸어 들어오고/퇴장 (진행 방향 시선 ➔ 중앙 도착 시 정면 카메라 응시).
* **360도 자유 회전**: 캐릭터 콜라이더 터치 시 드래그로 전신 회전 감상.
* **인게임 연동**: 로비에서 선택한 캐릭터가 `GameDataManager.UserData.selectedCharacterId`에 실시간 저장되어, 게임 시작 시 인게임 강가 발판 위에 해당 캐릭터가 즉시 스폰됨.

### 🛡️ 물리 충돌 간섭 차단
* `GameController.LaunchStone()`: 투구 순간 캐릭터와 돌 콜라이더 간 `Physics.IgnoreCollision`을 명시 적용하여 캡슐 콜라이더 내부 튕김(Pop) 현상 0% 차단.

### 🔍 프리팹 무결성 검증 툴 (`PrefabHealthCheckTool.cs`)
* **메뉴 경로**: `Tools -> Skipping Stones -> 🔍 프리팹 무결성 검증 (Health Check)`
* **검사 항목**:
  1. **캐릭터 프리팹**: `Animator`, `Controller`, `CapsuleCollider`, `rightHandBone`, `dummy01Socket` 연결 검사
  2. **돌 프리팹**: `Rigidbody` (UseGravity=false, Constraints), `Collider`, `TrailRenderer`, `RhythmRingIndicator` 검사
  3. **수면/환경 프리팹**: `BoxCollider` (IsTrigger=true) 검사
  4. **로비 프리팹**: `LobbyStoneShowcaseController`, `LobbyCharacterShowcaseController`, `Staging_Position`, `StoneSelector`, `Stone_Stand` 검사

---

## 3. 수정/생성된 핵심 파일 목록
* `[NEW]` [Assets/Scripts/Visuals/LobbyCharacterShowcaseController.cs](file:///d:/Git_Hub/Skipping_Stones/Assets/Scripts/Visuals/LobbyCharacterShowcaseController.cs)
* `[NEW]` [Assets/Scripts/Editor/PrefabHealthCheckTool.cs](file:///d:/Git_Hub/Skipping_Stones/Assets/Scripts/Editor/PrefabHealthCheckTool.cs)
* `[NEW]` [docs/script_history/LobbyCharacterShowcaseController.md](file:///d:/Git_Hub/Skipping_Stones/docs/script_history/LobbyCharacterShowcaseController.md)
* `[MODIFY]` [Assets/Scripts/UI/MetaUIManager.cs](file:///d:/Git_Hub/Skipping_Stones/Assets/Scripts/UI/MetaUIManager.cs) (캐릭터 쇼케이스 연동 및 AddComponent 제거)
* `[MODIFY]` [Assets/Scripts/Gameplay/GameController.cs](file:///d:/Git_Hub/Skipping_Stones/Assets/Scripts/Gameplay/GameController.cs) (인게임 캐릭터 동적 교체 및 IgnoreCollision)
* `[MODIFY]` [Assets/Scripts/Gameplay/SkippingStone.cs](file:///d:/Git_Hub/Skipping_Stones/Assets/Scripts/Gameplay/SkippingStone.cs) (암묵적 AddComponent 제거 및 Warning 전환)
* `[MODIFY]` [Assets/Scripts/Visuals/WaterSurface.cs](file:///d:/Git_Hub/Skipping_Stones/Assets/Scripts/Visuals/WaterSurface.cs) (BoxCollider AddComponent 제거)
* `[MODIFY]` [Assets/Scripts/Visuals/LobbyStoneShowcaseController.cs](file:///d:/Git_Hub/Skipping_Stones/Assets/Scripts/Visuals/LobbyStoneShowcaseController.cs) (다이얼 Raycast 터치 분리 및 선택 기억)
* `[MODIFY]` [Assets/Scripts/Data/GameDataDTO.cs](file:///d:/Git_Hub/Skipping_Stones/Assets/Scripts/Data/GameDataDTO.cs) (`CharacterInfoData.prefabPath` 추가)

---

## 4. 🚀 차기 핵심 과제 등록 (Roadmap & Task Plan)

### 📌 [주말/집중 세션 예정] 가벼운 3D 메쉬 기반 소켓 앵커 & 스마트 물길 추적 맵 시스템 개편
* **추진 배경**:
  - 기존의 Z축 일직선 스트리밍 한계를 극복하고, **원형 서킷 호수, 급커브 강, 다채로운 테마의 모듈러 맵**을 지원.
  - 무거운 터레인 대신 **가벼운 3D 메쉬 프리팹**으로 제작하여 런타임 스폰 부하 0% 달성.
* **핵심 구현 파이프라인**:
  1. **진입 앵커 (`Start_Anchor`)**:
     - 이전 청크의 출구와 100% 밀착 결합(위치 및 회전 매트릭스 일치).
  2. **스마트 강줄기 자동 추적 (`River Stream Centerline`)**:
     - 수작업 노가다 없이, 지형 골짜기/단면 스캔을 통해 굽이치는 물길 중심선을 코드가 자동 계산 (갓모드 및 카메라 스플라인 곡선 비행).
  3. **끝 앵커 (`End_Anchor`)**:
     - 다음 청크가 도킹할 위치와 각도를 제공하여 원형 서킷/무한 루프 완벽 지원.
  4. **탑다운 직교 리플레이 연동 (`TopDownReplayManager`)**:
     - 곡선/원형 비행 궤적에 맞춰 리플레이 카메라의 영역(Bounds) 자동 중앙 정렬 및 줌아웃(Auto-Framing) 연동.

### 📌 [기획 결정] 모드별 전용 맵 매니저 분리 및 자동 스캔 아키텍처
* **결정 사항**:
  1. **롱디스턴스 전용 맵 매니저**: `LakeEnvironmentManager` (또는 `LongDistanceMapManager`)
     - `mapTitle`, `mapThumbnail`, `targetClearDistance`, 소켓 앵커 모듈러 스트리밍 보유.
  2. **타깃 모드 전용 맵 매니저**: `TargetAccuracyMapManager` (신규 분리)
     - `mapTitle`, `mapThumbnail`, `targetDistance`, 정적 과녁/바위섬 단일 프리팹 보유.
  3. **UI 스마트 스캔 (`MetaUIManager.ScanEnvManagers`)**:
     - 상단 모드 탭(`LongDistance` vs `TargetAccuracy`)에 따라 해당 컴포넌트(`GetComponent<T>()`)를 가진 맵 프리팹만 1초 만에 100% 자동 필터링 스캔하여 썸네일과 목록 표출.
  4. 현재는 **`롱디스턴스(원거리 비행)`** 모드의 완성도와 손맛에 100% 집중하여 먼저 오픈하고, 타깃 모드는 추후 독립 모드로 1.5단계에서 오픈.

---

## 5. 컴파일 검증
* `dotnet build Assembly-CSharp.csproj`: **경고 0개, 오류 0개 (Clean Build)**
* `dotnet build Assembly-CSharp-Editor.csproj`: **경고 0개, 오류 0개 (Clean Build)**

