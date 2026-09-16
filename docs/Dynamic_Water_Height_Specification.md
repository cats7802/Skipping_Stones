# 🌊 작업 명세서: 실시간 수면 높이 감지 및 입체 지형 대응 시스템
> **문서 ID**: SPEC-20260914-DYNAMIC-WATER-HEIGHT  
> **작성 일시**: 2026-09-14  
> **적용 대상**: `SkippingStone.cs`, `ArcadeSkippingStone.cs`, `WaterSurface.cs`, `GlobalRiverPath.cs`  
> **관련 문서**: [`docs/Master_Game_Design_and_Architecture.md`](file:///d:/github/Skipping_Stones/docs/Master_Game_Design_and_Architecture.md), [`GEMINI.md`](file:///d:/github/Skipping_Stones/GEMINI.md)

---

## 1. 배경 및 목적 (Background & Goals)

### 1.1 현상 및 문제점
* 현재 `SkippingStone`은 시작 시 단 1개의 수면 높이(`waterLevel`, float)를 캐싱하여 고정 기준으로 사용합니다.
* `distToWater = transform.position.y - waterLevel` 공식으로 바운스 타이밍과 착수를 판정하기 때문에:
  1. **경사진 강(Sloped River)**: 하류로 갈수록 수면이 낮아질 경우 타이밍 윈도우(PERFECT/GREAT)가 공중에서 발동하거나 심해로 잘못 인식됨.
  2. **계단식 수면 / 폭포**: 높낮이가 다른 수면 구역 간 이동 시 비행 중에 다음 수면 높이를 인지하지 못함.
  3. **물레방아 / 점프대 등 기믹**: 돌이 닿는 즉시 `OnCollisionEnter`에서 "지형 착지/바위 충돌"로 처리되어 강제 사망(Crash) 발생.

### 1.2 개발 목표
* 돌의 현재 (X, Z) 좌표 발밑의 **실제 수면 높이를 실시간(매 프레임)으로 정확하게 측정**하여 입체적인 레벨 디자인(경사, 단차, 폭포)을 지원.
* 물레방아, 점프대, 연잎 등 **특수 기믹 오브젝트와 접촉 시 사망하지 않고 위로 도약/리프트**할 수 있는 인터랙션 기반 마련.

---

## 2. 상세 시스템 설계 (Detailed Architecture)

### 2.1 실시간 수면 높이 프로바이더 (`WaterHeightSampler`)
돌 발밑의 수면 높이를 2단계 우선순위로 샘플링:

```
[돌의 현재 위치 (X, Y, Z)]
       │
       ▼
 1차: Physics.Raycast (수직 하향 탐색)
   - 발밑(Y + 5.0m)에서 아래로 Water 레이어/콜라이더 탐색
   - WaterSurface 콜라이더 적중 시 hit.point.y를 현재 수면 높이로 채택
       │ (미적중 시)
       ▼
 2차: GlobalRiverPath 스플라인 보간
   - 맵에 스플라인이 존재할 경우, 현재 투영 거리의 riverWaterY 채택
       │ (스플라인 부재 시)
       ▼
 3차: 기본 고정 waterLevel (Fallback)
```

```csharp
// 권장 구현 구조 (WaterHeightSampler.cs)
public static class WaterHeightSampler
{
    private static int waterLayerMask = -1;

    public static float GetWaterLevelAt(Vector3 position, float fallbackLevel = 0f)
    {
        if (waterLayerMask == -1)
        {
            waterLayerMask = LayerMask.GetMask("Water");
            if (waterLayerMask == 0) waterLayerMask = ~0; // 기본 전체 탐색
        }

        // 1. 발밑 수직 레이캐스트 (상공 5m -> 하향 25m)
        Vector3 rayStart = new Vector3(position.x, position.y + 5.0f, position.z);
        if (Physics.Raycast(rayStart, Vector3.down, out RaycastHit hit, 30.0f, waterLayerMask, QueryTriggerInteraction.Collide))
        {
            if (hit.collider.GetComponent<WaterSurface>() != null || hit.collider.name.ToLower().Contains("water"))
            {
                return hit.point.y;
            }
        }

        // 2. 스플라인 경로 참조 (GlobalRiverPath)
        if (GlobalRiverPath.Instance != null && GlobalRiverPath.Instance.GetClosestPointOnRiver(position, out _, out _, out float distAlong))
        {
            if (GlobalRiverPath.Instance.EvaluateAtDistance(distAlong, out _, out _, out _, out float riverY))
            {
                return riverY;
            }
        }

        return fallbackLevel;
    }
}
```

---

### 2.2 `SkippingStone.cs` 변경 사항

1. **실시간 높이 갱신**:
   * `FixedUpdate` 진입 시 `currentWaterLevel = WaterHeightSampler.GetWaterLevelAt(transform.position, waterLevel);`
   * `distToWater = transform.position.y - currentWaterLevel;`
2. **리듬 판정 윈도우 정렬**:
   * `TryRhythmBounce`에서 현재 측정된 `currentWaterLevel`을 기준으로 타이밍 및 스플래시 생성 위치 정렬.
3. **그림자 투영기 (`StoneShadowController`)**:
   * 돌 그림자가 비치는 수면의 Y좌표를 `currentWaterLevel`로 실시간 동기화.

---

### 2.3 물레방아 / 점프대 기믹 처리 (`IInteractiveGimmick`)

1. **충돌 처리 분기 (`OnCollisionEnter` / `OnTriggerEnter`)**:
   * 지형 충돌 검사 시 `IInteractiveGimmick` 컴포넌트 여부를 확인하여 크래시 예외 처리:
   ```csharp
   var gimmick = other.GetComponentInParent<IInteractiveGimmick>();
   if (gimmick != null)
   {
       gimmick.OnStoneInteract(this);
       return; // 즉시 사망(Crash) 방지!
   }
   ```
2. **물레방아 거동**:
   * 날개에 닿는 순간 일정 시간 동안 물레방아의 회전 궤적(또는 상승 곡선)을 따라 위치를 보정.
   * 최상단 도달 시 정면 상승 속도(`rb.linearVelocity = new Vector3(hSpd, upwardBoost, forwardSpd)`)를 부여하여 힘차게 재도약.

---

## 3. 작업 단계 (Implementation Roadmap)

| 단계 | 작업 내용 | 대상 파일 | 검증 항목 |
| :---: | :--- | :--- | :--- |
| **Phase 1** | 수면 높이 샘플러 클래스 구현 | `Assets/Scripts/Gameplay/Calculators/WaterHeightSampler.cs` [NEW] | 단위 테스트 및 레이캐스트 성능(GC 0) 확인 |
| **Phase 2** | `SkippingStone` 동적 높이 연동 | `Assets/Scripts/Gameplay/SkippingStone.cs` [MODIFY] | 경사/단차 수면에서 바운스 판정 일치 여부 |
| **Phase 3** | 그림자 및 이펙트 높이 동기화 | `StoneShadowController.cs`, `SplashEffectSpawner.cs` | 그림자가 수면 틈새로 꺼지거나 뜨지 않는지 확인 |
| **Phase 4** | 기믹 인터페이스 & 물레방아 프로토타입 | `IInteractiveGimmick.cs`, `WaterWheelGimmick.cs` [NEW] | 물레방아 접촉 시 사망하지 않고 위로 도약 확인 |

---

## 4. 검증 시나리오 (Verification Scenarios)

1. **경사진 수면 테스트**:
   * 100m 구간에 걸쳐 높이가 5m 낮아지는 수면 메쉬를 배치하고, 돌을 투구하여 모든 바운스 지점에서 PERFECT/GREAT 판정이 정상 고도에서 일어나는지 확인.
2. **계단식 폭포/단차 테스트**:
   * 높이 3m 차이가 나는 2개의 호수 수면 사이를 돌이 넘어갈 때, 공중에서 윈도우가 튀지 않고 아랫쪽 수면 위에서 자연스럽게 바운스되는지 확인.
3. **기믹 충돌 테스트**:
   * 회전하는 물레방아 날개에 돌이 충돌했을 때 크래시 없이 리프트된 후 다음 강물로 날아가는지 확인.
