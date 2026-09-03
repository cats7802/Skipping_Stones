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
### [2026-09-03] 🌀 아케이드 전용 랜덤 링 (Random Ring) 시스템 & 매트릭스 웜홀 FOV 연출 완성
- **수정 목적**: 아케이드 모드에서 부스트 패드를 대체하여 공중 상하 바운스 링 진입, 2박 홀드 둥-둥- 비트 펄스 연출, 1박 초고속 워프 런치, 기분 좋은 하이리턴 랜덤 버프 및 곡률 기반 안전 스폰 구축.
- **적용 내용**:
  1. `RandomRing.cs`:
     - `Assets/3D/Ingame_Object/Random_Ring.fbx` 메쉬 자동 연동 및 기본 자전(Idle Spin).
     - 물수제비 포물선 정점 높이 및 리듬과 싱크되는 공중 상하 바운스(Bobbing) 구현.
     - 돌 안착 시 2박자 동안 음악 비트에 맞춘 둥-둥- 스케일 펄스(`PlayBeatPulse`) 연출.
     - 돌 흡입 자석 스냅(Magnetic Snap), 발사 후 축소 소멸(`DisappearAndDestroy`), 추후 포탈/집중선 VFX 슬롯 제공.
  2. `ArcadeSkippingStone.cs`:
     - 링 진입 시 2박 홀드 ➔ 1박 쓩~ 초고속 3바운스 거리 워프 런치 시퀀스(`CoProcessRandomRingSequence`).
     - 하이리턴 & 로우리스크 랜덤 버프 룰렛 (🦘 하이점프 2회 + 지상 장애물 충돌 무시, 🚀 스피드 부스트 +25%, 🔥 모멘텀 MAX, 🎵 템포 슬로우).
     - 음악 비트 그리드(Bar/Beat) 완벽 일치 보존 및 정박 착수 연결.
  3. `DualCameraSetup.cs`:
     - 링 홀드 시 클로즈업 줌인 긴장감(`SetRingHoldCinematic`).
     - 발사 순간 매트릭스 웜홀 광각 FOV 워프(`TriggerWarpSpeedFOV`: 60° ➔ 105° ➔ 정박 착수 복귀).
  4. `RiverSpawner.cs`:
     - 아케이드 모드 시 지상 패드 대신 공중 `RandomRing` 스폰.
     - 베이킹된 스플라인 접선(`tangent`)을 통해 전방 35m 곡률 검증(15° 이상 급커브/헤어핀 구간 스폰 제외) 적용.
- **컴파일 검증**: 0 Errors, 0 Warnings 통과.

