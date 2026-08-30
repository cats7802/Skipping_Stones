# DualCameraSetup.cs 히스토리 및 변경 관리

## 1. 개요
* **경로**: `Assets/Scripts/Visuals/DualCameraSetup.cs`
* **주요 역할**: 
  * 0단계(조준/투구), 2.5단계(발사 선행 리드인), 3단계(비행 추적 `DynamicFlight`), 탑다운 리플레이 뷰 등 전반적인 인게임 카메라 뷰포트 및 앵글 전환 총괄.

---

## 2. 🚨 절대 불변 규칙 (Invariant Rules - 임의 수정 절대 금지)

> [!CAUTION]
> 아래 명시된 수치와 공식은 디렉터님의 수많은 인게임 테스트와 검증을 통해 확정된 **절대 기준(Ground Truth)**입니다. 어떠한 패치, 리팩토링, 수면 높이 지형 변경 시에도 **아래 규칙을 임의로 삭제하거나 기본값으로 덮어쓰지 마십시오.**

1. **비행 추적 카메라 4대 황금 수치 (세로 9:16 화면 6등분 중 3번째 칸 배치)**:
   * `flightDistBack = 5.5f;` (후방 거리 5.5m)
   * `flightHeight = 2.4f;` (카메라 높이 2.4m)
   * `flightLookForward = 7.5f;` (전방 주시 거리 7.5m)
   * `flightLookHeight = -2.2f;` (수면을 내려다보는 각도로 돌과 링을 화면 3/6 지점에 안착시키는 핵심 피치각)
   * **목적**: 스마트폰 세로 화면에서 손가락 터치 시 링이 가려지지 않고, 화면 중앙(위에서 3/6 지점)에서 타이밍을 보며 전방 시야를 완벽하게 확보하기 위함.

2. **수면 연동 다이내믹 Y축 바운스 추적 공식 보존**:
   ```csharp
   float relativeStoneY = Mathf.Max(0f, stonePos.y - waterLevel);
   float dynamicCamY = waterLevel + (relativeStoneY * 0.75f) + flightHeight;
   float dynamicLookY = waterLevel + (relativeStoneY * 0.35f) + flightLookHeight;

   Vector3 stoneXZ = new Vector3(stonePos.x, 0f, stonePos.z);
   targetOffset = stoneXZ - (moveDir * flightDistBack) + (Vector3.up * dynamicCamY);
   targetLookOffset = stoneXZ + (forwardDir * flightLookForward) + (Vector3.up * dynamicLookY);
   ```
   * 돌이 공중 높이 솟구칠 때와 착수할 때 시선 각도와 높이를 비례 계산하여 부드러운 Y축 추종감 유지.

3. **비행 중 카메라 추적 지연(Lerp Rubber-Banding) 금지**:
   * 비행 중 카메라 위치는 고속 돌(18m/s)을 쫓아갈 때 느슨한 `Vector3.Lerp` 지연으로 인한 잔상/고스팅을 원천 차단하기 위해 `mainCam.transform.position = targetOffset;`으로 즉각 밀착 추적할 것.

---

## 3. 변경 이력 (Changelog)

### [2026-08-30] 코너/곡선 강줄기 시간차 후방 추적 및 전방 시야 정렬 복원
- **수정 목적**: 곡선 강줄기 맵에서 코너를 돌 때 카메라가 22도 각도 제한 및 `forwardDir` 고정으로 인해 앞을 보지 못하고 측면 산맥만 바라보던 현상 해결.
- **핵심 구조**:
  - 22도 회전각 하드코딩 제한을 해제하고 돌의 실제 진행 방향(`velXZ.normalized` / `forward`)을 100% 반영.
  - 돌이 먼저 코너로 꺾인 후 카메라가 시간차를 두고 부드럽게 후방으로 정렬(`Vector3.Slerp`)되도록 복원.
  - 카메라 시선 타깃(`targetLookOffset`)을 `forwardDir`에서 `moveDir`로 교체하여 코너를 돌 때도 화면 정면에 시원하게 굽이치는 앞쪽 물길이 보이도록 개선.
- **컴파일 검증**: 0 Errors.

### [2026-08-25]
* **원인 규명 및 3/6 확정 수치 복원**:
  * 과거 수면 높이 패치 당시 뭉개졌던 과거의 3/6 구도 확정값(`flightDistBack=5.5`, `flightHeight=2.4`, `flightLookForward=7.5`, `flightLookHeight=-2.2`)을 스크립트와 `SampleScene.unity` 씬 직렬화 값에 완벽 복원.
  * 수면 높이(`waterLevel`)에 연동되는 다이내믹 Y축 비율 공식(`relativeStoneY * 0.75 / 0.35`) 복원.
* **카메라 Lerp 지연 지터 제거**:
  * 비행 중 카메라 추적 시 고무줄 랙을 제거하여 링과 돌의 고스팅/떨림 현상 완벽 제거.
