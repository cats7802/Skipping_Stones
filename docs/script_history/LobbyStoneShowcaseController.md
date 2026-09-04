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

### 2026-08-28: 9차 수정 (왼쪽 드래그 ➔ 시계방향 회전 ➔ 도감 정방향 완벽 동기화 & 머티리얼 복구)
* **작업 내역**:
  1. `Stone.prefab` 머티리얼 정상화: 기본 조약돌 머티리얼을 원래의 `Stone_Pebble_Mat.mat`(회색)으로 복원, `Stone_red.prefab`은 `Stone_RED_MAT.mat`(빨간색)으로 오버라이드 명시 적용.
  2. 디렉터 인터랙션 가이드 반영:
     - 왼쪽 드래그: 다이얼/스탠드가 **시계 방향(+Y)**으로 회전하며 **도감 등록 순서 정방향(회색 ➔ 파랑 ➔ 초록 ➔ 빨강)** 돌이 정면으로 진입.
     - 오른쪽 드래그: 다이얼/스탠드가 **반시계 방향(-Y)**으로 회전하며 **도감 등록 순서 역방향(이전 돌)** 돌이 정면으로 진입.
     - 스탠드 슬롯 인덱스(`Stage_01` 정면, `Stage_02` 다음 돌, `Stage_03` 이전 돌) 매핑으로 1:1 완벽 동기화.
* **컴파일 검증**: `dotnet build Assembly-CSharp.csproj` / `Assembly-CSharp-Editor.csproj` 경고 0개, 오류 0개 완료.

### [2026-09-04] 초반 돌 스폰 이상 원인 분석 및 해결 방안 수립
* **현상**: 로비 진입 시 3D 쇼케이스 스탠드에 돌이 즉시 스폰되지 않거나 일부 슬롯이 누락되는 현상.
* **원인 분석**:
  1. `GameDataManager`의 싱글톤/카탈로그 로드 시점과 `LobbyStoneShowcaseController.Awake()/Start()`의 스캔 시점 불일치 시 해금 목록이 불완전하게 로드될 가능성.
  2. `InitializeShowcase()` 시점에 이전 오브젝트/슬롯 인덱스 캐시(`slotStoneIndices`)가 남아있어 `SpawnStoneAtSlot`의 캐시 스킵 조건에 걸려 스폰이 누락됨.
  3. 해금된 돌 개수가 3개 미만(1~2개)일 때의 슬롯별 인덱스 분배 예외 처리 필요.
  4. `MetaUIManager`의 `UpdateLobbyShowcase()`에서 `OnSelectedStoneChanged` 이벤트가 중복 구독되는 이슈.
* **해결 방안**:
  - `InitializeShowcase()` 시 슬롯의 기존 자식 오브젝트 완전 정리(`ClearAllSlots()`) 및 슬롯 인덱스 캐시 초기화(`[-1, -1, -1]`).
  - 카탈로그 로드 실패 시에도 4종 돌(`Stone`, `Stone_Blue`, `Stone_Green`, `Stone_red`)을 즉각 확보하도록 안전 폴백 보강.
  - 1개, 2개, 4개 이상 모든 조건에서 3개 슬롯 매핑 안정화 및 `MetaUIManager` 이벤트 중복 바인딩 방지.
