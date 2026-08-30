# RhythmRingIndicator.cs 히스토리 및 변경 관리

## 1. 개요
* **경로**: `Assets/Scripts/Visuals/RhythmRingIndicator.cs`
* **주요 역할**: 
  * 물수제비 바운스 착수 직전 타이밍 인디케이터(수면 고정 안쪽 링, 수축하는 바깥쪽 링, 수직 가이드 드롭 라인)의 시각적 렌더링 및 버스트 파티클 총괄.

---

## 2. 🚨 절대 불변 규칙 (Invariant Rules - 임의 수정 절대 금지)

> [!CAUTION]
> 아래 명시된 렌더링 방식과 회전 고정 규칙은 돌의 고속 스핀(1440도/s)과 렌더링 지터를 방지하기 위해 검증된 **절대 기준(Ground Truth)**입니다.

1. **수면 평면 완전 밀착 (`LineAlignment.TransformZ` 및 수평 90도 고정)**:
   * 링은 수면(XZ 평면)에 도장처럼 납작하게 누운 평면 원형태여야 함.
   * `innerRing.alignment = LineAlignment.TransformZ;`
   * `outerRing.alignment = LineAlignment.TransformZ;`
   * `innerObj.transform.rotation = Quaternion.Euler(90f, 0f, 0f);`
   * **금지**: `LineAlignment.View` 등으로 임의 변경하여 링이 카메라를 향해 일어서서 콘 형태로 왜곡되게 만들지 말 것.

2. **부모 회전 간섭 차단 (`LateUpdate` 월드 회전 고정)**:
   * `innerObj`와 `outerObj`는 씬 고아 오브젝트 누수를 막기 위해 돌의 자식(`SetParent(transform)`)으로 묶이되, 매 `LateUpdate()`에서 `Quaternion.Euler(90f, 0f, 0f)`로 월드 수평 회전을 강제 고정하여 돌의 1440도 스핀이 라인 Z축 노멀에 간섭하지 않도록 할 것.

---

### [2026-08-30] 포물선 궤적 예측 기반 수면 착수 과녁(Target Impact Ring) 고정 시스템 적용
- **수정 목적**: 돌 아래에 붙어 다니던 기존 링 방식을 탈피하여, 돌이 튕겨 오르는 즉시 다음 착수 지점(과녁)이 수면에 먼저 선명하게 고정되어 리듬 액션의 조준 및 타이밍 판정 직관성을 극대화.
- **적용 내용**:
  - `LateUpdate()`에서 2차 방정식 물리 포물선 수식($0.5g t^2 - v_y t - \Delta y = 0$)을 풀어 착수 예상 지점($(X_{impact}, Z_{impact})$) 및 남은 시간($T_{impact}$)을 실시간 계산.
  - 수면의 착수 예상 위치에 타깃 링(`innerRing`)을 과녁처럼 미리 고정 배치.
  - 돌이 날아오는 동안 외곽 링(`outerRing`)이 착수 시간 비율에 맞춰 점진적으로 수축.
  - 돌의 현재 위치와 수면 과녁을 잇는 가이드 궤적 라인(`dropLine`) 연결.
- **컴파일 검증**: 0 Errors.

