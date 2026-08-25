# [Work Handover Note] 2026-08-25: 로비 3D 스톤 쇼케이스 회전 시스템 완벽 가이드

> **작업자 필독**: 이 문서는 로비 3D 돌 선택기(쇼케이스)의 3D 모델 구조, 기하학적 좌표계, 회전 메커니즘, 코딩 가이드라인을 100% 명확히 정리한 문서입니다. 디렉터님께 2번 설명 요청 없이 이 가이드대로만 즉시 구현하시면 됩니다.

---

## 1. 3D 모델 구조 및 Transform 데이터 (검증 완료)

* **프리팹 경로**: `Assets/prefab/Lobby.prefab`
* **3D FBX 모델**: `Assets/3D/BG/Stone_Select.fbx`
* **하이어라키 구조**:
  ```text
  [Lobby] (루트)
   └── SelectStage (룸 및 카메라)
   └── Stone_Select (선택기 루트)
        ├── Stone_Stand (★ 상단 3개 접시 스탠드 - 회전 대상 1)
        │    ├── Stone_Stage_01 (정면 접시 홈 더미)
        │    ├── Stone_Stage_02 (좌측 뒤 접시 홈 더미)
        │    └── Stone_Stage_03 (우측 뒤 접시 홈 더미)
        ├── StoneSelector (★ 하단 조작 다이얼 - 회전 대상 2)
        └── Stone_Cover (고정 바닥/커버)
  ```

### 📌 부모-자식 Transform 좌표 데이터 (로그 실측값)
1. **`Stone_Stand` (회전 본체)**:
   * `LocalPos`: `(0.00, 0.80, 0.00)`
   * `LocalRot`: `(0.00, 0.00, 0.00)`
   * **원점 피벗(0, 0.8, 0)을 중심으로 로컬 Y축만 회전하면 자식 3개 더미가 스탠드 접시 홈에 딱 붙어서 완벽히 함께 회전함.**
2. **더미 3개 (접시 홈 - `Stone_Stand`의 직계 자식들)**:
   * `Stone_Stage_01` (정면): `LocalPos (0.00, 0.02, 0.15)`, `LocalRot (0, 0, 0)`
   * `Stone_Stage_03` (우측 뒤): `LocalPos (0.13, 0.02, -0.07)`, `LocalRot (0, 120, 0)`
   * `Stone_Stage_02` (좌측 뒤): `LocalPos (-0.13, 0.02, -0.07)`, `LocalRot (0, 240, 0)`
3. **`StoneSelector` (하단 다이얼)**:
   * 로컬 Y축을 30도씩 회전하는 조작 링.

---

## 2. ★★★ 절대 규칙: 구현 핵심 원칙 (Architecture Rules)

### 🚨 [Rule 1] 더미 3개는 절대 직접 회전/이동시키지 말 것
* `Stone_Stage_01~03` 더미는 스탠드(`Stone_Stand`)의 자식입니다.
* **돌 프리팹은 더미 3개의 자식으로 `Instantiate`하고 `localPosition = Vector3.zero; localRotation = Quaternion.identity;`로 얹어두기만 하면 끝입니다.**
* 더미의 좌표나 인덱스를 코드로 복잡하게 계산하거나 옮기지 마십시오.

### 🚨 [Rule 2] 회전 대상은 오직 2개 파츠의 로컬 Y축뿐임
1. **상단 스탠드 (`Stone_Stand`)**: **로컬 Y축 120도**씩 회전 (`Quaternion.Euler(0, 누적각도, 0)`)
2. **하단 다이얼 (`StoneSelector`)**: **로컬 Y축 30도**씩 회전 (`Quaternion.Euler(0, 누적각도, 0)`)
* 스탠드가 120도 돌면 스탠드 자식인 더미 3개와 돌 3개도 접시 홈 위에서 물리적으로 정확히 함께 120도 돕니다.

### 🚨 [Rule 3] 돌이 4개 이상일 때 무한 캐러셀(교체) 원리
* 돌 프리팹 목록: `Assets/prefab/Stone/`
  1. `Stone.prefab` (기본 검은돌)
  2. `Stone_Blue.prefab` (파란돌)
  3. `Stone_Green.prefab` (초록돌)
  4. `Stone_red.prefab` (빨간돌)
* **초기 상태**: 시작 시 더미 3개(`Stone_Stage_01, 02, 03`)에 각각 돌 0번, 1번, 2번을 올려둠.
* **회전 시**:
  * 스탠드를 120도 회전시킵니다.
  * **회전이 끝난 후, 카메라 시야 뒤(등 뒤)로 돌아간 단 1개의 더미에 대해서만 다음 순서의 돌(예: 3번 돌)로 바꿔치기**합니다.
  * 눈앞에 보이는 정면과 좌/우 돌은 절대 건드리지 않으므로, 유저는 3D 턴테이블이 끊김 없이 무한히 도는 것처럼 보입니다.
* **돌 생성 시 주의**: 새 돌을 올리기 전 해당 더미 아래의 이전 자식 오브젝트를 완전히 삭제(`Destroy`)하여 중복 겹침(Z-fighting)을 방지할 것.

---

## 3. 터치 및 드래그 조작 스펙
* New Input System / Legacy Input 통합 지원:
  * 마우스 좌클릭 드래그 / 모바일 터치 스와이프
  * `dragDelta.x > 0` (오른쪽 드래그): 이전 돌 (반대 방향 회전)
  * `dragDelta.x < 0` (왼쪽 드래그): 다음 돌 (정방향 회전)
  * 임계값: 35픽셀 이상 드래그 시 회전 발동.
* 회전 보간: `Mathf.SmoothStep(0f, 1f, t)`을 사용한 0.5초 부드러운 가감속(Ease In-Out).

---

## 4. 연동 대상 매니저
* `Assets/Scripts/UI/MetaUIManager.cs`:
  * 로비 화면 진입 시 `Lobby.prefab`을 자동 스폰하고 관리함.
  * 스톤 쇼케이스의 돌이 변경되면 `OnSelectedStoneChanged` 이벤트를 받아 현재 선택된 돌 프리팹을 게임 데이터에 동기화.

---

## 5. 돌 카탈로그 시스템 (구축 완료)
* `GameDataDTO.cs`에 `StoneInfoData` 클래스 추가 완료 (id, name, description, prefabPath, unlockGoldCost, isUnlocked).
* `GameDataManager.cs`에 `stoneCatalog` 필드 추가 + 4종 돌 등록 완료.
* `LobbyStoneShowcaseController.cs`가 `Start()` 시 `GameDataManager.stoneCatalog`에서 `isUnlocked == true`인 돌만 자동으로 `unlockedStonePrefabs`에 등록.
* **현재 테스트를 위해 4종 전부 임시 해금 상태** (`GameDataManager.cs`의 `emerald_pebble`, `crimson_flint`에 `// TODO: 테스트 후 false로 복원` 주석 있음).

### 5.1 최신 조치 사항 (쇼케이스 땜질 코드 전면 청소 및 인게임 생명주기 완벽 분리)
* **[LobbyStoneShowcaseController.cs](file:///d:/Git_Hub/Skipping_Stones/Assets/Scripts/Visuals/LobbyStoneShowcaseController.cs)**:
  * `SpawnStoneAtSlot()`을 프리팹 원형 그대로 더미에 링크시키는 가장 순수한 형태로 전면 청소 (인위적인 회전 조작 코드 일절 없음).
* **[LakeEnvironmentManager.cs](file:///d:/Git_Hub/Skipping_Stones/Assets/Scripts/Visuals/LakeEnvironmentManager.cs)**:
  * 타이틀/로비/맵선택 화면에서 맵 청크 및 수면 엔티티 사전 스폰 차단.
  * 맵 선택 UI에서 `GAME START`를 눌러 인게임(`MetaScreen.InGame`)에 진입할 때만 인게임 환경이 구동되도록 생명주기 완벽 분리.
* **컴파일 검증**: `dotnet build Assembly-CSharp.csproj` 경고 0개, 오류 0개 완료.

| ID | 이름 | 프리팹 | 해금 상태 |
|---|---|---|---|
| `default` | 기본 조약돌 | `Stone.prefab` | ✅ 기본 해금 |
| `flat_slate` | 납작 청석판 | `Stone_Blue.prefab` | ✅ 기본 해금 |
| `emerald_pebble` | 에메랄드 자갈 | `Stone_Green.prefab` | ✅ 임시 해금 (원래 🔒 2000골드) |
| `crimson_flint` | 붉은 부싯돌 | `Stone_red.prefab` | ✅ 임시 해금 (원래 🔒 5000골드) |

---

## 6. ⚠️ 현재 미해결 이슈: 터치 입력 안 먹힘

> [!CAUTION]
> **IMGUI ↔ 3D 컨트롤러 입력 충돌 문제**:
> `MetaUIManager`가 IMGUI(`OnGUI`)로 전체 화면을 잡고 있어서, `LobbyStoneShowcaseController`의 `Update()`에서 읽는 터치/마우스 입력이 먹히지 않음.

### 수정 방향 (확정)
**입력은 IMGUI(`MetaUIManager`)가 전담, 회전 애니메이션은 컨트롤러가 전담**으로 역할 분리:

1. **`MetaUIManager`에 쇼케이스 컨트롤러 참조 캐싱**:
   * `UpdateLobbyShowcase()`에서 `Lobby.prefab` 스폰 직후, `spawnedLobbyInstance.GetComponentInChildren<LobbyStoneShowcaseController>()` (없으면 `AddComponent`) 후 캐싱.
2. **화살표를 버튼으로 변경** (`DrawLobbyScreen()` 561~562라인):
   * `GUI.Label("⇦")` → `DrawResponsiveButton("⇦")` → 클릭 시 `controller.RotatePrev()` 호출.
   * `GUI.Label("⇨")` → `DrawResponsiveButton("⇨")` → 클릭 시 `controller.RotateNext()` 호출.
3. **(선택) IMGUI 드래그 감지**:
   * `DrawLobbyScreen()` 안에서 `Event.current`의 `MouseDown/MouseUp` deltaX 계산 후 `RotateNext()`/`RotatePrev()` 호출.
4. **`LobbyStoneShowcaseController`의 `HandleInput()` 호출 제거**:
   * `Update()`에서 `HandleInput()` 빼고 `UpdateRotation()`만 남김 (회전 애니메이션만 자체 처리).
