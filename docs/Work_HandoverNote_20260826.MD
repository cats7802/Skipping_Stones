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

## 4. 컴파일 검증
* `dotnet build Assembly-CSharp.csproj`: **경고 0개, 오류 0개 (Clean Build)**
* `dotnet build Assembly-CSharp-Editor.csproj`: **경고 0개, 오류 0개 (Clean Build)**
