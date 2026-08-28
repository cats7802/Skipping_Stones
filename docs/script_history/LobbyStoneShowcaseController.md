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


