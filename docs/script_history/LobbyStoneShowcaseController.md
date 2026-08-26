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

### 2026-08-26: 8차 수정 (인스펙터 타깃 카메라 직접 연결 및 다이얼 Raycast 개선)
* **작업 내역**:
  1. `targetCamera` 직렬화 필드 추가: 인스펙터에서 `Camera001` 또는 로비 카메라를 직접 할당 가능하게 지원 (미할당 시 자동 탐색 및 폴백).
  2. Raycast 히트 판정 개선: `targetCamera` 기반으로 `StoneSelector`, `Stone_Stand`, 하위 콜라이더 터치 시 100% 드래그가 정상 시작되도록 안전화.
* **컴파일 검증**: `dotnet build` 경고 0개, 오류 0개 완료.

