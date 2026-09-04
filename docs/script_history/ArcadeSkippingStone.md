# 📜 Script History: ArcadeSkippingStone.cs

## 🎯 1. 역할 및 책임 (Core Responsibility)
- **리듬 아케이드 모드 전용 비행 & 판정 엔진**:
  - 물리 시뮬레이션(중력/마찰) 대신 **BPM 고정 주기($T = 60/\text{BPM}$) 수학적 포물선 비행** 전담.
  - 착수 전 잔여 시간 기반 **6단계 판정(PERFECT, GREAT, GOOD, LATE, TOO LATE, MISS)**.
  - 귀엽고 통통 튀는 **일정 포물선 높이($1.8\text{m}$) 고정**.
  - 판정별 **바운스 거리 증감 및 미스 시 Base 거리 롤백 시스템**.
  - 실시간 실험을 위한 **프리셋 3종(아기자기 10m / 스탠다드 12m / 스피드 15m) 및 Custom 튜닝 시스템**.

---

## 🚫 2. 절대 금지 규칙 및 불변 표준 (Must NOT Do)
- ❌ **판정에 따른 포물선 높이 임의 변경 금지** ➜ 아케이드 통통 튀는 감성을 위해 높이($1.8\text{m}$)는 고정 유지.
- ❌ **미스(MISS) 발생 시 임의의 감속 수치 적용 금지** ➜ 반드시 해당 프리셋의 **기초 거리($D_{\text{base}}$)로 즉시 롤백**.
- ❌ **임의의 엉뚱한 거리 점프(30m 등) 하드코딩 금지** ➜ 디렉터 확정 증감 테이블 준수.
- ❌ **1주기 다중 탭 허용 금지** ➜ 1바운스당 1회 정밀 판정 소비.

---

## 📊 3. 디렉터 확정 프리셋 및 판정 증감 테이블

| 프리셋 이름 | 기본 거리 ($D_{\text{base}}$) | Perfect | Great | Good | Late | Too Late | Miss |
| :--- | :---: | :---: | :---: | :---: | :---: | :---: | :---: |
| **🟢 아기자기 (Cute)** | **10.0m** | **+0.5m** | **+0.2m** | **±0.0m** | **-0.3m** | **-0.6m** | **10.0m 롤백** |
| **🔵 스탠다드 (Standard)** | **12.0m** | **+0.6m** | **+0.3m** | **±0.0m** | **-0.4m** | **-0.8m** | **12.0m 롤백** |
| **🟣 스피드 (Speed)** | **15.0m** | **+1.0m** | **+0.5m** | **±0.0m** | **-0.5m** | **-1.0m** | **15.0m 롤백** |
| **⚙️ 커스텀 (Custom)** | 인스펙터 지정 | 인스펙터 지정 | 인스펙터 지정 | 0.0m | 인스펙터 지정 | 인스펙터 지정 | Custom Base 롤백 |

* **모멘텀(라이프) 변동**: PERFECT(+20), GREAT(+10), GOOD(+5), LATE(-10), TOO LATE(-20), MISS(-30).
* **BPM 비거리 비례 가속**: 0~200m(60 BPM / 1.00s), 200~500m(72 BPM / 0.83s), 500~1000m(85 BPM / 0.70s), 1000~1600m(100 BPM / 0.60s), 1600m+(120 BPM / 0.50s).

---

## 🕒 4. 수정 및 진화 히스토리 (Change Log)

### [2026-09-04] 🏗️ ArcadeSkippingStone 모듈화 및 공용 수면 그림자/BPM 궤적 계산기 분리
- **수정 목적**: 1,115줄에 달하던 대형 아케이드 돌 스크립트에서 중복 코드를 제거하고, 공용 모듈 재사용 및 BPM 포물선 궤적 연산을 독립 분리하여 유지보수성 극대화.
- **적용 내용**:
  1. `WaterReflectionShadowController.cs` (공용 모듈 재사용): 수면 Quad 생성, 텍스처 베이킹 및 `SafeDestroy` 클린업 일원화 (중복 코드 완전 제거).
  2. `ArcadeRhythmTrajectoryCalculator.cs` (신규 분리): 고정 포물선 궤적 위치/회전 계산(`EvaluateFlightPosition`), 거리 비례 BPM 가속(`CalculateBPM`), 잔여 시간별 6단계 판정(`EvaluateTimingGrade`) 분리.
  3. `ArcadeSkippingStone.cs`: 분리된 궤적 계산기와 공용 그림자 컨트롤러를 바인딩하여 600줄대로 경량화 및 안정성 확보.
- **컴파일 & MCP 검증**: 0 Errors, 0 Warnings 통과 및 Unity MCP 인엔진 단위 테스트 통과 (정점 높이 17.8m, 1500m BPM 100, 조기 탭 방어 검증).

### [2026-09-01] 리듬 아케이드 모드 고도화 및 곡선 강줄기 스폰 완벽 호환
- **수정 목적**: 물리 모드 오연결 해소, 디렉터 확정 리듬 룰 완성, 충돌/조향/가속 물리 고도화 및 굽이치는 맵 스폰 연동.
- **적용 내용**:
  1. `ArcadeSkippingStone.cs`:
     - 60 BPM(1.00s) 하프비트 정박 및 도달 거리(m) 비례 점진적 코스 템포 가속(60~120 BPM) 구현.
     - 공중 직진 관성 유지 및 수면 착수 순간 박차는 조향 예약(`pendingSteerAngle`) 시스템 완성.
     - Kinematic 0.12m 연속 충돌 검출(`OverlapSphere`) 및 지형 충돌 시 텀블링 튕김 연출(`CoCrashTumble`).
     - 체력 소진 시 수면 밥빙 & 물보라 & 거리 합산 도로록 스키밍 피니시(`CoSkimmingFinish`) 구현.
  2. `RiverSpawner.cs` & `GlobalRiverPath.cs`:
     - 굽이치는 곡선 청크 스플라인의 실제 곡선 거리(`GetSegmentDistanceRange`) 순회 연동.
     - 섬으로 분기되는 좌/우 양쪽 물길 모두에 물고기/패드가 고르게 스폰되도록 다중 채널(`DetectSplitWaterChannels`) 스캔 확장.
  3. `LakeEnvironmentManager.cs`:
     - `GetTrackingZ`에 `ArcadeSkippingStone` 실시간 위치 추적 연동.
### [2026-09-04] 🌀 랜덤 링 탈출 시 지형 베이킹 강심(Centerline) 궤적 발사 & 급커브 스폰 제한 해제
- **수정 목적**: 링 탈출 시 단순 직선 발사로 인해 곡선/커브 코스에서 땅이나 강둑에 처박히는 문제를 해결하고, 지형에 구워진 강심선과 돌의 현재 속도/거리를 계산하여 완벽한 강심 방향 곡선 유도 런치 구현.
- **적용 내용**:
  1. `ArcadeSkippingStone.cs`:
     - 링 발사(`CoProcessRandomRingSequence`) 시 `GlobalRiverPath.Instance.GetClosestPointOnRiver`로 현재 강 위치 거리 탐색.
     - `startRiverDist + launchDistance` 목표 지점의 강 중심선 좌표(`targetRiverCenter`)와 접선(`targetRiverTangent`) 쿼리.
     - 1박자 발사 비행 중 매 프레임 `curRiverDist`를 따라 곡선 보간 비행하도록 구현하여 급커브에서도 강줄기를 따라 매끄럽게 비행 후 정박 착수.
     - 발사 완료 시 돌의 진행 방향(`currentForwardDir`)을 강의 접선 방향으로 자동 정렬하여 이후 바운스도 강 중앙을 따라 이어지도록 처리.
  2. `RiverSpawner.cs`:
     - 링이 강심 방향으로 곡선 궤적 유도 발사를 완벽 지원함에 따라, 링 스폰 시 걸려있던 `15도 이상 곡률 필터`를 제거하여 모든 강 코스(급커브 포함)에서 자유롭게 링이 등장하도록 확장.
### [2026-09-04] 🌟 캐릭터 고유 패시브 고도 오프셋 & 단일 진실 공급원(Single Source of Truth) 고도 아키텍처 병합
- **수정 목적**: 집(물고기 10종 및 강심선 런치) 및 회사(캐릭터 패시브 고도 오프셋 & 링 마중 높이 동기화) 작업 충돌 없는 완전 병합.
- **적용 내용**:
  1. `characterHeightModifier` 및 `CurrentBounceArcHeight` 프로퍼티 신설.
  2. `UpdateFlight`에서 `CurrentBounceArcHeight`를 참조하여 캐릭터 패시브 고도 증감 및 하이점프(+1.2m) 버프를 단일 수식으로 일괄 처리.
  3. `RandomRing`과의 완벽한 궤적 및 고도 싱크 유지.
- **컴파일 검증**: 0 Errors, 0 Warnings 통과.


