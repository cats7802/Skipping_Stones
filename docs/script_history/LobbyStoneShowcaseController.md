# [Script History] LobbyStoneShowcaseController.cs

## 1. 개요 및 목적
* **스크립트 경로**: [Assets/Scripts/Visuals/LobbyStoneShowcaseController.cs](file:///d:/Git_Hub/Skipping_Stones/Assets/Scripts/Visuals/LobbyStoneShowcaseController.cs)
* **목적**: 로비 3D 디오라마에서 조작 다이얼(30도)과 돌 스탠드(120도)를 회전시키며 도감의 해금된 돌들을 3D 턴테이블 형태로 전시하는 컨트롤러.

---

## 2. 변경 이력

### [2026-08-25] 쇼케이스 회전 및 스폰 상태 점검 세션
- **작업 내역**:
  - `GameController.cs`: `RiverSpawner` 부트스트랩 안전 처리 추가 (`[Auto_RiverSpawner]`).
  - `LobbyStoneShowcaseController.cs`:
    - 쇼케이스 인스턴스에서 물리 간섭 방지를 위해 `Rigidbody`, `Collider`, `SkippingStone` 컴포넌트 제거(`Destroy`) 적용.
    - 절대 누적 스텝 정수 기반 회전 구조 적용.
  - 빌드 상태: **0 Warnings, 0 Errors**.
- **현상 및 잔여 이슈**:
  - `Stone_Stand` 회전 시 더미(`Stone_Stage_01, 02, 03`)와 돌 인스턴스 간의 회전/위치 동기화 이슈 추가 조정 대기.

### 2026-08-26: 7차 수정 (로비 돌 선택값 인게임 동기화 및 손 소켓 단일화)
* **작업 내역**:
  1. **로비 UI 정리**:
     - `MetaUIManager.cs` 내 임시 `⇦`, `⇨` 버튼 완전 삭제 (순수 마우스/터치 드래그로만 전환).
     - 로비에서 회전 완료 시 선택된 돌 ID(`selectedStoneId`)를 `GameDataManager.UserData`에 저장하도록 이벤트 연동.
  2. **인게임 선택 돌 연동**:
     - `GameController.StartGameSession`: 로비에서 선택한 `selectedStoneId`를 기반으로 해당 돌 프리팹을 로드하여 캐릭터 손 및 인게임 비행에 전달.
  3. **캐릭터 손 소켓 중복 생성 방지 및 정리**:
     - `StoneThrowerCharacter.cs`:
       - `InitializeCharacter()` 내의 불필요한 위치 간섭 사족 코드 완전 삭제.
       - `SetHandStonePrefab()`에서 손 소켓(`Dummy001`) 하위의 기존 돌을 `DestroyImmediate`로 즉시 0개로 비운 후 선택된 돌 1개만 정확히 쥐어주도록 정리.
* **컴파일 검증**: `dotnet build Assembly-CSharp.csproj` 경고 0개, 오류 0개 완료.
