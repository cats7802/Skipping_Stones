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

## 3. 변경 이력 (Changelog)

### [2026-08-25]
* **부모 귀속 및 고아 오브젝트 누수 차단**:
  * 링 오브젝트들을 `SkippingStone` 하위 자식으로 안전하게 귀속.
* **라인 렌더러 수평 고정 및 메쉬 뒤틀림 지터 제거**:
  * `LineAlignment.TransformZ`와 `Quaternion.Euler(90f, 0f, 0f)`를 적용하여 돌의 1440도 회전 간섭을 차단하고 수면에 납작하게 밀착 렌더링 보장.
