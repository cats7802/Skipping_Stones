# [Work Handover Note] 2026-08-25: 로비 3D 쇼케이스 및 UI 배치 작업

## 1. 개요 및 작업 상태
* **일자**: 2026-08-25
* **상태**: ⚠️ **이상 현상 확인 및 점검 필요 (진행 중 / Ongoing)**
* **주요 작업 대상**: [LobbyStoneShowcaseController.cs](file:///d:/Git_Hub/Skipping_Stones/Assets/Scripts/Visuals/LobbyStoneShowcaseController.cs), [MetaUIManager.cs](file:///d:/Git_Hub/Skipping_Stones/Assets/Scripts/UI/MetaUIManager.cs)

---

## 2. 작업 완료 및 반영 내역
1. **로비 UI 레이아웃 정리 ([MetaUIManager.cs](file:///d:/Git_Hub/Skipping_Stones/Assets/Scripts/UI/MetaUIManager.cs))**:
   * 상단 바 / 하단 독 바 최하단 밀착 정렬.
   * 중앙 어두운 창 및 불필요한 텍스트 제거.
   * 하단 3D 스톤 셀렉터 다이얼 좌우 안내 화살표 배치 (`X=110, 275`, `Y=865`).
   * 로비 진입 시 `Assets/prefab/Lobby.prefab` 인스턴스화 및 씬 메인카메라 전환 연동.
2. **로비 쇼케이스 컨트롤러 구현 ([LobbyStoneShowcaseController.cs](file:///d:/Git_Hub/Skipping_Stones/Assets/Scripts/Visuals/LobbyStoneShowcaseController.cs))**:
   * 모바일 스와이프 / 마우스 좌우 드래그 감지 (New Input System 및 Legacy Input 통합).
   * 다이얼(`StoneSelector`) 30도 + 스탠드(`Stone_Stand`) 120도 회전 구조.
   * 더미 3개(`Stone_Stage_01, 02, 03`)에 돌 프리팹 자식 인스턴스화 및 등 뒤 슬롯 순환 버퍼 갱신 로직.

---

## 3. ⚠️ 핵심 이슈 및 이상 현상 (Current Issues - 디버깅 필수)

> [!CAUTION]
> **스톤 쇼케이스 회전 및 더미 안착 이상 현상**:
> 1. **더미 위치/궤도 이탈 현상**:
>    * 스탠드(`Stone_Stand`) 회전 시 더미 3개(`Stone_Stage_01~03`)가 스탠드의 접시(홈) 위에 안착되지 않고 접시 밖으로 튀어나가거나 궤도를 이탈하여 돌아다니는 이상 현상 지속 발생.
> 2. **돌 프리팹 안착 회전 및 위치 불량**:
>    * 더미에 생성된 돌 오브젝트가 접시 한가운데 정위치에 놓이지 않고 치우치거나 각도가 비틀리는 문제.
> 3. **회전 축 및 계층 구조 점검 필요**:
>    * `SelectStage.fbx` / `Stone_Select.fbx` 내부의 `Stone_Stand` 로컬 피벗 및 `Stone_Stage_01~03` 더미의 좌표계가 런타임 회전 시 정확히 일치하는지 에디터 씬 뷰에서 정밀 재검증 필요.

---

## 4. 다음 작업 계획
1. 유니티 에디터 상에서 `Stone_Stand`의 피벗과 `Stone_Stage_01~03` 더미의 Transform 관계를 명확히 재확인하고, 더미 궤도 이탈 문제 원인 규명 및 수정.
2. 정상 회전 및 돌 안착 검증 후 도감/인벤토리 해금 데이터 연동.
