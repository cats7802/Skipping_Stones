# TopDownReplayManager.cs 히스토리 및 변경 관리

## 1. 개요
* **경로**: `Assets/Scripts/Visuals/TopDownReplayManager.cs`
* **주요 역할**:
  * 게임 종료 후 물수제비 비행 궤적 및 바운스(Skim, Perfect, Boost, Ring Boost, Start, Finish) 지점을 유려한 드로잉 연출과 함께 탑다운(Top-Down) 수직 직교(Orthographic) 리플레이로 재생.
  * 리플레이 완료 후 마우스 좌/우클릭, 드래그 패닝, 모바일 멀티 터치 핀치 줌, 그리고 마우스 휠 스크롤 줌을 포함한 자유로운 탑다운 네비게이션 제어.

---

## 2. 🚨 절대 불변 규칙 (Invariant Rules - 임의 수정 절대 금지)

1. **자유 이동 및 줌 작동 제한 조건**:
   * 비행 궤적 및 마커 드로잉 연출이 완전히 끝나기 전에는 절대로 자유 패닝 및 줌 조작을 허용하지 않습니다.
   * 조건: `if (!isReplayFinished || isDrawing) return;`

2. **직교(Orthographic) 카메라 프레임 독립적 보간 및 스케일 비례 제어**:
   * 카메라 orthographic size는 프레임레이트에 독립적으로 부드럽게 보간되어야 합니다.
   * `currentOrthoSize = Mathf.Lerp(currentOrthoSize, targetOrthoSize, Time.unscaledDeltaTime * 16f);`
   * 직교 크기가 바뀜에 따라 리플레이 궤적 라인의 두께 및 동적 마커 링의 크기가 적절히 리스케일링되어 해상도 및 시인성을 유지해야 합니다. (`UpdateVisualsScale`)

3. **카메라 중심 좌표에 따른 지형 동적 배치 동기화**:
   * 자유 탐색으로 카메라 중심(Z축)이 바뀔 때, 해당 위치에 맞는 지형 페이지와 수면(WaterSurface)이 즉시 배치 동기화되어야 시각적 잘림 현상이 발생하지 않습니다.
   * `SyncTerrainByZ(currentCamCenter.z);`

---

## 3. 변경 이력 (Changelog)

### [2026-09-04] 리플레이 종료 후 마우스 휠 줌(Zoom) 안정성 강화 및 레거시/하이브리드 입력 백업 추가
- **수정 목적**: 일부 플랫폼 및 유니티 에디터 창의 입력 포커스 상태, 또는 Active Input Handling 설정("Both" 등)에 따라 New Input System의 `Mouse.current.scroll`이 스크롤 입력을 받지 못해 리플레이 카메라의 줌인/줌아웃 작동이 중단되던 치명적인 문제 해결.
- **상세 구현 내용**:
  - `HandleFreeNavigation` 메서드의 마우스 휠 입력 부를 보강하여 하이브리드 입력 감지 구축.
  - 신형 입력(`Mouse.current.scroll`)을 우선 판독한 뒤, 입력값이 검출되지 않을 경우 레거시 `Input.mouseScrollDelta.y` 및 `Input.GetAxis("Mouse ScrollWheel")`를 순차적 예외 처리(`try-catch`) 백업으로 동작하도록 구현.
  - 이를 통해 새로운 입력 시스템 패키지 구성 및 하이브리드 입력 시스템 구성 모두에서 디바이스 포커스 유실 여부와 무관하게 100% 안정적으로 부드러운 스크롤 줌인이 동작하도록 조치 완료.
