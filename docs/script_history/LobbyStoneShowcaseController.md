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

### [2026-09-08] 상단 접시(슬롯) 정면 추적 및 무한 캐러셀 회전 동기화 완벽 정규화
* **문제 상황**:
  - 회전 시 현재 정면에 위치한 접시 슬롯(`currentFacingSlotIndex`)과 실제 물리 위치(0°, 240°, 120°)가 어긋나 돌이 순간 이동하거나 잘못된 돌이 생성되는 현상.
  - F5(전체 해금 디버그 키)를 눌렀을 때 `unlockedStonePrefabs` 스캔 조건 불일치 및 자식 비동기 파괴 타이밍으로 인해 상단 접시가 빈 접시로 남거나 돌이 사라지던 현상.
* **작업 내역**:
  1. `ScanUnlockedStonesFromCatalog()` 보강: `GameDataManager.UserData.unlockedStoneIds` 직접 포함 여부를 교차 검증하고, 스캔 실패 시 기본 4종 폴백 프리팹을 100% 로드하도록 강화.
  2. `currentFacingSlotIndex` 정규화: 스탠드 회전(+120°, -120°)에 맞춰 정면에 도착한 슬롯 인덱스를 정확하게 동기화.
  3. `SpawnStoneAtSlot()` 및 `RefreshAllSlots()` 인스턴스 정리: 기존 접시 자식을 즉시 클리어하고 해금된 돌 개수(1~4개)에 상관없이 3개 접시에 돌이 비지 않도록 안전 분배.
  4. `MetaUIManager` 이벤트 구독 중복 방지(`-=` 후 `+=` 등록).
* **컴파일 검증**: `dotnet build Assembly-CSharp.csproj` & `Assembly-CSharp-Editor.csproj` 0 Warnings, 0 Errors 완료.

### [2026-09-08] (2차 세션) 정면 빈접시 및 뒤쪽 출몰 버그 완벽 근결
* **근본 원인 분석**:
  1. **기본 돌(Stone) 바닥 파묻힘 & 스탠드 일체화**:
     - `Stone.prefab` 머티리얼(`Stone_Pebble_Mat`)과 스탠드 머티리얼(`Lit`) 색상이 `RGBA(0.25, 0.30, 0.35)`로 100% 동일.
     - `localPosition = Vector3.zero`로 생성되어 돌의 최고점(1.030)이 스탠드 홈 턱(1.031)보다 낮아 접시 홈 속에 완전히 파묻혀 카메라 쿼터뷰에서 '완전한 빈접시'로 오인됨.
     - F5(ALL 해금) 시 0번 조약돌이 정면에 올라오면서 되려 빈접시로 보이는 원인이었음.
  2. **회전 시 돌 사전 스폰 누락**:
     - 회전이 끝난 후 등 뒤 슬롯만 갱신하던 구조에서, 회전 도중 정면으로 들어오는 슬롯에 돌이 미배치되어 있거나 지연 생성되어 빈접시가 노출됨.
  3. **Lobby.prefab 직렬화 누락**:
     - `Lobby.prefab`의 `stageSlots` 및 `unlockedStonePrefabs` 배열이 null로 비어 있어 런타임 의존성 발생.
* **해결 내역**:
  1. **돌 높이 & 볼륨감 보정**:
     - `stoneLocalOffset = (0, 0.015, 0)` 오프셋을 주어 접시 홈 위에 도톰하게 안착.
     - `stoneLocalScale = 1.35x`로 확대하여 접시와 완벽하게 어우러지는 볼륨감 부여.
  2. **기본 조약돌 가시성 대비 틴트 적용**:
     - `MaterialPropertyBlock`을 통해 기본 조약돌에 밝고 깨끗한 돌 톤 `Color(0.50f, 0.54f, 0.58f)`을 적용하여 스탠드 회색과 100% 뚜렷하게 대비되도록 가시성 확보.
  3. **회전 전 진입 슬롯 사전 스폰 (`RotateRoutine`)**:
     - 회전 애니메이션 시작 전에 정면으로 들어올 슬롯에 차기 돌을 100% 사전 스폰한 뒤 회전하여, 회전 도중이나 완료 시 빈접시가 보이는 현상 원천 차단.
  4. **수학적 정면 슬롯 불변 공식 (`GetFacingSlotIndex`)**:
     - `((currentStep % 3) + 3) % 3` 수학 공식으로 물리 회전 각도와 정면 슬롯 인덱스 1:1 완벽 일치.
  5. **프리팹 직렬화 참조 동기화**:
     - `Assets/prefab/Lobby.prefab` 및 `Assets/Resources/Lobby.prefab`에 `stageSlots[0~2]` 및 기본 4종 돌 프리팹을 정식 직렬화 저장.
* **검증 결과**:
  - `dotnet build Assembly-CSharp.csproj` / `Assembly-CSharp-Editor.csproj`: **0 Warnings, 0 Errors**.
  - 유니티 에디터 시뮬레이션에서 초기화, 1회 회전, 2회 회전, 3회 회전 전체 스텝에서 정면 슬롯에 항상 올바른 돌이 도톰하게 안착됨을 전수 확인.


