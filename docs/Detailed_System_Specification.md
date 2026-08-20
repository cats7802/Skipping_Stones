# 📄 Stone Skipping (물수제비 타이밍 리듬 액�> **문서 버전**: v2.3.0 (Approved - BG_01 루트 1,500m 통합 스트리밍, 동적 4단계 환경 라이팅, 레이캐스트 강폭 자동감지 & 동적 수면 엔티티 재스폰 완료)  
> **엔진 및 대상 플랫폼**: Unity 6 (6000.5.8f1) / Windows Standalone (PC) / Android (APK) / iOS (Xcode)  
> **화면 모드**: 모바일 세로 9:16 (Portrait) 고정 최적화  
> **깃허브 저장소 루트**: `d:\Git_Hub\Test_AI` (Subfolder: `Test_AI`)

---

## 1. 🎮 게임 개요 및 핵심 플레이 루프

### 1.1 게임 개요
- **타이틀**: Stone Skipping (물수제비 마스터 3D)
- **핵심 재미**: 완벽한 각도와 파워로 돌을 던진 후, 수면에 닿는 찰나마다 칼타이밍 리듬 터치로 끝없이 도약하며 마지막 **'도로록~' 스키밍 피니시**까지 연결되는 짜릿한 손맛(사운드+햅틱 진동) 제공

```mermaid
flowchart TD
    P0["0단계: 투구 위치 선정\n(장거리 발판 / PP01~PP29 강변 이동)"] --> P1["1단계: 각도 조준\n(좌우 스윙 게이지)"]
    P1 --> P2["2단계: 파워 충전\n(상하 와인드업 파워 바)"]
    P2 --> P3["3단계: 리듬 액션 바운스\n(다이내믹 Y-바운스 쿼터뷰 / 풀스크린 탭 / 사운드 & 햅틱)"]
    P3 --> P4["4단계: '도로록~' 스키밍 피니시\n(5스킵 이상 시 수면 고속 활주 보너스)"]
    P4 --> P5["5단계: 탑다운 직교(Orthographic) 궤적 리플레이\n(전체 궤적 맵 자동 프레이밍 / 파문 애니메이션)"]
    P5 --> P6["6단계: 최종 결과 정산 및 보상\n(거리/스킵/타겟/도감 점수 집계)"]
    P6 --> P0
```

---

## 2. 🕹️ 2가지 게임 모드 사양

### 2.1 🏆 모드 1: 장거리 기록 경신 모드 (Long Distance Mode)
- **시작 위치**: 수상 나무 발판(`Lakeside_WoodenPier`) 위 좌우 이동
- **투구 방향**: 월드 `+Z`축 물줄기 방향 (1,500m 릴레이 무한 강줄기 코스)
- **목표**: 최적의 각도와 리듬 터치로 최대 스킵 횟수 및 도달 거리 기록 경신
- **수면 환경**: 5열 레인(-18m, -9m, 0m, 9m, 18m)을 따라 가속 부스트 패드, 장애물 바위, 점핑 물고기, 친구 기록 깃발 배치

### 2.2 🎯 모드 2: 타겟 맞추기 / 물 건너편 투구 모드 (Target Accuracy Mode)
- **시작 위치**: 강변을 따라 자연 정렬된 `PP01` ~ `PP29` 웨이포인트([PlayerPositionPath.cs](file:///d:/Git_Hub/Test_AI/Test_AI/Assets/Scripts/Gameplay/PlayerPositionPath.cs)) 중 선택
- **시점 연출**:
  - **메인 뷰**: 캐릭터 숄더 뷰 & 3D 쿼터뷰
  - **상단 PIP 뷰**: `Ground` 지형 메쉬에 자동 피팅된 직교(Orthographic) 탑다운 맵 카메라([MapPIPManager.cs](file:///d:/Git_Hub/Test_AI/Test_AI/Assets/Scripts/Visuals/MapPIPManager.cs))
- **투구 방향**: 강 건너편 (`+X`축)
- **목표**: 수면 전역에 떠 있는 **플로팅 타겟 과녁 링([FloatingTargetZone.cs](file:///d:/Git_Hub/Test_AI/Test_AI/Assets/Scripts/Gameplay/FloatingTargetZone.cs))** 을 명중시켜 대량의 보너스 점수 획득

---

## 3. 🌄 1,500m 무한 스트리밍 & 동적 환경 라이팅 시스템

### 3.1 `BG_01` 루트 통합 2-청크 스트리밍 ([LakeEnvironmentManager.cs](file:///d:/Git_Hub/Test_AI/Test_AI/Assets/Scripts/Visuals/LakeEnvironmentManager.cs))
- **통합 청크 단위**: `BG_01` (지형 `Ground` + 수면 `Water_Surface` + 좌/우 산맥 `Left/Right_Mountains`)를 하나의 청크(1,500m)로 취급
- **A-B 핑퐁 릴레이**: 원본(`BG_01`, 0~1500m)과 복제본(`BG_01_Chunk1`, 1500~3000m)을 번갈아 순간이동 배치
- **트리거 시점**: 돌/카메라가 뒷 청크 기준 `1,000m`를 통과하는 순간 뒷 청크를 앞쪽(`+3,000m`)으로 이동
- **릴레이 이벤트 콜백 (`OnChunkRelayed`)**: 지형이 앞으로 이동할 때 이벤트를 발행하여 `RiverSpawner`가 새로 생성된 1,500m 수면 구간에 엔티티를 동적 재스폰

### 3.2 0m ~ 4,500m+ 4단계 동적 환경 라이팅 (Dynamic Journey Lighting)
거리 비례 실시간 `Lerp` 보간으로 자연스러운 시간의 흐름(낮 $\rightarrow$ 노을 $\rightarrow$ 밤) 연출:

| 비거리 구간 | 테마 이름 | 주 조명(Sun Light) | 스카이박스/안개(Fog) | 산맥 틴트 |
| :--- | :---: | :---: | :---: | :---: |
| **0m ~ 1,500m** | ☀️ **ClearDay** | $48^\circ$, 밝은 온백색 (1.45 Lux) | 청명한 스카이블루 / 연한 블루 안개 | 청록빛 실루엣 |
| **1,500m ~ 3,000m** | 🌅 **Sunset** | $16^\circ$, 황금빛 오렌지 (1.55 Lux) | 짙은 노을 주황 / 노을 안개 | 보랏빛 노을 산 |
| **3,000m ~ 4,500m** | 🌆 **Twilight** | $6^\circ$, 붉은 석양 (1.15 Lux) | 땅거미 마젠타 / 석양 안개 | 짙은 다크 바이올렛 |
| **4,500m+** | 🌙 **MoonlitNight** | $-25^\circ$, 은은한 달빛 (0.45 Lux) | 달빛 네이비 / 딥 블루 안개 | 칠흑빛 밤하늘 실루엣 |

---

## 4. 🌊 동적 수면 엔티티 스폰 & 레이캐스트 물리 검증 ([RiverSpawner.cs](file:///d:/Git_Hub/Test_AI/Test_AI/Assets/Scripts/Gameplay/RiverSpawner.cs))

- **강폭 자동 감지 (`GetWaterXBounds`)**: `WaterSurface`의 BoxCollider 너비를 읽어와 수면 폭에 맞춰 좌우 스폰 범위 자동 적응
- **수직 물리 레이캐스트 (`IsValidWaterPosition`)**:
  - 스폰 후보 지점 상공($Y=+20\text{m}$)에서 아래로 수직 Raycast 발사
  - 수면($Y=0\text{m}$)보다 높은 지형($Y>0.3\text{m}$)에 먼저 닿으면 스폰을 즉시 취소하여 **지형 관통 및 땅속 스폰 원천 차단**
- **청크 동적 재스폰 (`SpawnChunkEntities`)**:
  - `LakeEnvironmentManager.OnChunkRelayed` 콜백 발생 시 호출
  - 새로 생성된 `chunkStartZ ~ chunkStartZ + 1500m` 구간의 지나간 오브젝트를 청소하고, 새 수면에 부스트 패드, 장애물 바위, 물고기, 연잎 군락을 랜덤 재배치

---

## 5. 🪨 스톤 물리 및 캐릭터 애니메이션 파이프라인

### 5.1 3D 프리팹 모델 연동 ([SkippingStone.cs](file:///d:/Git_Hub/Test_AI/Test_AI/Assets/Scripts/Gameplay/SkippingStone.cs))
- **0점 초기화 프리팹(`Assets/3D/prefab/Stone.prefab`) 인스턴스화**: `localPosition = Vector3.zero`, `localScale = Vector3.one`
- **콜라이더 자동 동기화**: 프리팹 메쉬 바운드(`mesh.bounds.size` 및 `center`)를 읽어와 `BoxCollider`로 1:1 자동 정렬 (납작한 조약돌 형태와 100% 일치)
- **조약돌 전용 머티리얼([Stone_Pebble_Mat.mat](file:///d:/Git_Hub/Test_AI/Test_AI/Assets/Resources/Stone_Pebble_Mat.mat))**:
  - 선명한 톤 및 `Smoothness = 0.80` 반사광 적용, 텍스처 교체 가능

### 5.2 ✋ 손 소켓 고정 & 스윙 릴리즈 ([StoneThrowerCharacter.cs](file:///d:/Git_Hub/Test_AI/Test_AI/Assets/Scripts/Gameplay/StoneThrowerCharacter.cs))
- **더미 소켓 회전 정렬**: `Dummy001` 소켓의 Z축(전방)과 조약돌의 Y축(상단)을 1:1로 일치 정렬 (`Euler(90, 0, 0)`)
- **0~54프레임 (와인드업 및 스윙 중)**: 손 소켓 본에 매 프레임 위치/회전을 100% 찰싹 달라붙어 함께 휘둘러지도록 고정 (`isKinematic = true`)
- **55프레임 릴리즈 (발사 순간)**: 캐릭터 몸체 렌더러만 숨기고, 조약돌 3D 메쉬 렌더러는 100% 활성화 상태 유지 (`isKinematic = false`, `useGravity = true`)
- **Y-Up 수평 유지 및 순수 Y축(Yaw) 자전 회전**: 비행 중 Pitch(X)와 Roll(Z) 회전을 강제 고정(`FreezeRotationX | FreezeRotationZ`)하여 항상 납작한 면이 하늘을 향하도록 유지, 자전 스핀은 Y축(Yaw) $45\text{ rad/s}$ 고속 회전

---

## 6. 🎧 프로시저럴 사운드 효과음(SFX) 시스템

### 6.1 9종 프로시저럴 오디오 에셋 ([SoundSynthesizer.cs](file:///d:/Git_Hub/Test_AI/Test_AI/Assets/Scripts/Audio/SoundSynthesizer.cs) & [SoundEffectGenerator.cs](file:///d:/Git_Hub/Test_AI/Test_AI/Assets/Editor/SoundEffectGenerator.cs))
수학적 파형 합성 알고리즘(PCM 16-bit Mono 44.1kHz)을 통해 외부 라이브러리 없이 9종의 전용 오디오 에셋을 생성 및 장착 완료:

1. 💨 **`Throw_Whoosh.wav`**: 시원하게 날아가는 포물선 바람소리 (화이트 노이즈 + 피치 스윕)
2. 🌊 **`Bounce_Water.wav`**: 맑고 찰진 수면 착수 퐁당음 (삼각파 버블 모듈레이션)
3. ⚡ **`Bounce_Good.wav`**: 쫀득한 탄력 타격음 (고조파 하모닉스)
4. 🔔 **`Bounce_Perfect.wav`**: PERFECT 타이밍 저격 시 영롱한 4화음 크리스탈 차임벨
5. 🏄 **`Skim_Slide.wav`**: 수면을 가르는 고속 활주 플러터 사운드 (28Hz 펄스)
6. 🚀 **`Boost_Pad.wav`**: 초고속 가속 레이저 워프 사운드 (톱니파 피치 스윕)
7. 💰 **`Coin_Jingle.wav`**: 물고기 저격 시 챠링 금화음 (B5 $\rightarrow$ E6 2단 아르페지오)
8. 🫧 **`Stone_Sink.wav`**: 묵직한 꼬르륵 수중 침몰음 (14Hz 저주파 버블링)
9. 🔘 **`Button_Click.wav`**: 촉각적인 UI 터치 클릭음

### 6.2 오디오 풀링 & 모바일 최적화 ([AudioManager.cs](file:///d:/Git_Hub/Test_AI/Test_AI/Assets/Scripts/Audio/AudioManager.cs))
- **10채널 2D Direct AudioSource 풀링**: `spatialBlend = 0f`로 모바일 스피커/이어폰에서 선명하고 펀치력 있는 출력 보장
- **피치 미세 무작위 변조 ($\pm 5\%$)**: 연속 바운스 시 귀 피로도를 줄이고 리얼한 연타 손맛 구현
- **`PlayOneShot` & `BeforeSceneLoad` 사전 로드**: 씬 전환 시 딜레이 없이 즉시 사운드 재생
- **AudioListener 단일화 가드**: `Map_Camera` 등의 중복 리스너를 비활성화하여 오디오 음소거 충돌 원천 차단

---

## 7. 📳 크로스플랫폼 네이티브 햅틱(진동) 시스템 ([HapticFeedbackHelper.cs](file:///d:/Git_Hub/Test_AI/Test_AI/Assets/Scripts/Audio/HapticFeedbackHelper.cs))

안드로이드 및 iOS 환경에서 플레이어의 조작 및 바운스 타이밍에 따라 세분화된 진동 피드백 제공:

| 상황 | 진동 패턴 및 강도 | 용도 및 체감 |
| :--- | :---: | :--- |
| **터치 / 조준 / 버튼 탭** | `15ms` (강도 80/255) | 미세한 햅틱 탭 피드백 |
| **일반 / GOOD 바운스** | `35ms` (강도 160/255) | 경쾌하고 쫀득한 수면 착수 타격감 |
| **PERFECT 바운스 & 부스트** | `55ms` (강도 255/255 풀파워) | 묵직하고 짜릿한 최고 등급 임팩트 진동 |
| **물속 침몰 (Sunk)** | `75ms` (강도 100/255) | 부드러운 수중 럼블 진동 |

*유니티 `Handheld.Vibrate()` 내장 fallback을 완비하여 구형 기기 및 모든 모바일 기기 100% 호환 보장.*

---

## 8. 🗺️ 직교(Orthographic) 탑다운 바운스 궤적 맵 리플레이 ([TopDownReplayManager.cs](file:///d:/Git_Hub/Test_AI/Test_AI/Assets/Scripts/Visuals/TopDownReplayManager.cs))

### 8.1 시스템 흐름
1. **1.5초 딜레이 진입**: 돌 침몰/착지 후 1.5초간 침몰 연출 감상 $\rightarrow$ 메인 카메라가 하늘에서 수직(90도)으로 내려다보는 직교(Orthographic) 탑다운 뷰로 자동 전환
2. **전 구간 자동 화각 피팅 (Auto-Framing)**: 출발지점부터 최종 도착지점까지의 전체 거리를 계산하여 화면(세로 9:16)에 쏙 맞게 `orthographicSize` 자동 조절
3. **실시간 궤적 드로잉 & 착수점 파문 링**: 출발점에서 시작하여 돌이 날아간 실제 경로를 따라 발광 라인이 촤르륵 그려지며, 각 바운스 지점마다 물결 파문 링과 등급 뱃지(START/PERFECT/GREAT/FINISH) 순차 팝업

### 8.2 UI 버튼 흐름
- **드로잉 진행 중**: 하단 **`[스킵 (SKIP) ⏩]`** 버튼 노출 $\rightarrow$ 클릭 시 1프레임 만에 궤적 전체를 완성하고 즉시 최종 결과창으로 이동
- **드로잉 완료 후**:
  - **`[다시 보기 ↺]`**: 궤적과 마커를 지우고 처음부터 다시 하나씩 그려나가는 재생 애니메이션 실행
  - **`[결과 보기 (완료) ✔]`**: 리플레이를 종료하고 카메라를 원복한 뒤 최종 결과창(점수/코인 정산) 표시

---

## 9. 📱 모바일 터치 반응성 & UI 시스템 ([StoneSkippingUI.cs](file:///d:/Git_Hub/Test_AI/Test_AI/Assets/Scripts/UI/StoneSkippingUI.cs))

- **720p 기준 가상 해상도 반응형 레이아웃**: 노치/카메라 홀(Safe Area) 자동 계산
- **모바일 원터치 반응성 (`GetPointerDownVirtualPos`)**: 손가락이 화면에 닿는 첫 프레임(`pointerDownConsumedThisFrame`)에 즉시 버튼 클릭 감지 및 사운드/햅틱 트리거
- **모드 전환 시 터치 오작동 방지 (`requireTouchRelease`)**: 게임 오버 후 결과창이나 리플레이 화면에서는 불필요한 터치 락 없이 즉각적인 버튼 탭 허용

---

## 10. 🚀 1클릭 멀티플랫폼 빌드 시스템 ([BuildPlayerHelper.cs](file:///d:/Git_Hub/Test_AI/Test_AI/Assets/Editor/BuildPlayerHelper.cs))

- **Windows Standalone (PC)**:
  - 메뉴: `Tools -> Build -> Build Windows Standalone (EXE)`
  - 세로 모바일 9:16 창모드 (540x960) 자동 세팅 및 플랫폼 스위칭 지원
- **Android (Mobile)**:
  - 메뉴: `Tools -> Build -> Build Android (APK)`
  - 유니티 6 기본 런처(`UnityPlayerGameActivity`) 완벽 유지로 설치 시 **[열기] 버튼 및 홈 화면 앱 서랍 아이콘 $100\%$ 정상 등록**
  - 최신 64비트 ARM64 및 API Level 26+ 최적화
- **빌드 결과물 위치**: `d:\Git_Hub\Test_AI\Test_AI\Builds\` (빌드 완료 시 윈도우 탐색기 자동 열림)

---

## 11. 🛡️ 프로젝트 아키텍처 및 품질 보증 규칙

1. **Git 최상위 루트 규칙**: 진짜 깃 루트는 항상 `D:\Git_Hub\Test_AI`이며 모든 `git add/commit/push`는 최상위에서 수행 ([git_root_structure.md](file:///d:/Git_Hub/Test_AI/Test_AI/.agents/rules/git_root_structure.md))
2. **사전 승인 후 코딩 규칙**: 질문/증상 리포트 시 분석을 먼저 제시하고 유저의 명시적 승인 후 코딩 진행 ([confirm_before_coding.md](file:///d:/Git_Hub/Test_AI/Test_AI/.agents/rules/confirm_before_coding.md))
3. **무결점 컴파일 검증**: 모든 코드 변경 후 `dotnet build Assembly-CSharp.csproj` 및 `Editor.csproj` **0 경고, 0 오류** 필수 통과
4. **회귀 분석 규칙**: 기능 이상 발생 시 이전 성공 시점과의 변경점을 비교 대조하여 원인 파악 ([diff_against_working_baseline.md](file:///d:/Git_Hub/Test_AI/Test_AI/.agents/rules/diff_against_working_baseline.md))
 |
| **PERFECT 바운스 & 부스트** | `55ms` (강도 255/255 풀파워) | 묵직하고 짜릿한 최고 등급 임팩트 진동 |
| **물속 침몰 (Sunk)** | `75ms` (강도 100/255) | 부드러운 수중 럼블 진동 |

*유니티 `Handheld.Vibrate()` 내장 fallback을 완비하여 구형 기기 및 모든 모바일 기기 100% 호환 보장.*

---

## 6. 🗺️ 직교(Orthographic) 탑다운 바운스 궤적 맵 리플레이 ([TopDownReplayManager.cs](file:///d:/Git_Hub/Test_AI/Test_AI/Assets/Scripts/Visuals/TopDownReplayManager.cs))

### 6.1 시스템 흐름
1. **1.5초 딜레이 진입**: 돌 침몰/착지 후 1.5초간 침몰 연출 감상 $\rightarrow$ 메인 카메라가 하늘에서 수직(90도)으로 내려다보는 직교(Orthographic) 탑다운 뷰로 자동 전환
2. **전 구간 자동 화각 피팅 (Auto-Framing)**: 출발지점부터 최종 도착지점까지의 전체 거리를 계산하여 화면(세로 9:16)에 쏙 맞게 `orthographicSize` 자동 조절
3. **실시간 궤적 드로잉 & 착수점 파문 링**: 출발점에서 시작하여 돌이 날아간 실제 경로를 따라 발광 라인이 촤르륵 그려지며, 각 바운스 지점마다 물결 파문 링과 등급 뱃지(START/PERFECT/GREAT/FINISH) 순차 팝업

### 6.2 UI 버튼 흐름
- **드로잉 진행 중**: 하단 **`[스킵 (SKIP) ⏩]`** 버튼 노출 $\rightarrow$ 클릭 시 1프레임 만에 궤적 전체를 완성하고 즉시 최종 결과창으로 이동
- **드로잉 완료 후**:
  - **`[다시 보기 ↺]`**: 궤적과 마커를 지우고 처음부터 다시 하나씩 그려나가는 재생 애니메이션 실행
  - **`[결과 보기 (완료) ✔]`**: 리플레이를 종료하고 카메라를 원복한 뒤 최종 결과창(점수/코인 정산) 표시

---

## 7. 📱 모바일 터치 반응성 & UI 시스템 ([StoneSkippingUI.cs](file:///d:/Git_Hub/Test_AI/Test_AI/Assets/Scripts/UI/StoneSkippingUI.cs))

- **720p 기준 가상 해상도 반응형 레이아웃**: 노치/카메라 홀(Safe Area) 자동 계산
- **모바일 원터치 반응성 (`GetPointerDownVirtualPos`)**: 손가락이 화면에 닿는 첫 프레임(`pointerDownConsumedThisFrame`)에 즉시 버튼 클릭 감지 및 사운드/햅틱 트리거
- **모드 전환 시 터치 오작동 방지 (`requireTouchRelease`)**: 게임 오버 후 결과창이나 리플레이 화면에서는 불필요한 터치 락 없이 즉각적인 버튼 탭 허용

---

## 8. 🚀 1클릭 멀티플랫폼 빌드 시스템 ([BuildPlayerHelper.cs](file:///d:/Git_Hub/Test_AI/Test_AI/Assets/Editor/BuildPlayerHelper.cs))

- **Windows Standalone (PC)**:
  - 메뉴: `Tools -> Build -> Build Windows Standalone (EXE)`
  - 세로 모바일 9:16 창모드 (540x960) 자동 세팅 및 플랫폼 스위칭 지원
- **Android (Mobile)**:
  - 메뉴: `Tools -> Build -> Build Android (APK)`
  - 유니티 6 기본 런처(`UnityPlayerGameActivity`) 완벽 유지로 설치 시 **[열기] 버튼 및 홈 화면 앱 서랍 아이콘 $100\%$ 정상 등록**
  - 최신 64비트 ARM64 및 API Level 26+ 최적화
- **빌드 결과물 위치**: `d:\Git_Hub\Test_AI\Test_AI\Builds\` (빌드 완료 시 윈도우 탐색기 자동 열림)

---

## 9. 🛡️ 프로젝트 아키텍처 및 품질 보증 규칙

1. **Git 최상위 루트 규칙**: 진짜 깃 루트는 항상 `D:\Git_Hub\Test_AI`이며 모든 `git add/commit/push`는 최상위에서 수행 ([git_root_structure.md](file:///d:/Git_Hub/Test_AI/Test_AI/.agents/rules/git_root_structure.md))
2. **사전 승인 후 코딩 규칙**: 질문/증상 리포트 시 분석을 먼저 제시하고 유저의 명시적 승인 후 코딩 진행 ([confirm_before_coding.md](file:///d:/Git_Hub/Test_AI/Test_AI/.agents/rules/confirm_before_coding.md))
3. **무결점 컴파일 검증**: 모든 코드 변경 후 `dotnet build Assembly-CSharp.csproj` 및 `Editor.csproj` **0 경고, 0 오류** 필수 통과
4. **회귀 분석 규칙**: 기능 이상 발생 시 이전 성공 시점과의 변경점을 비교 대조하여 원인 파악 ([diff_against_working_baseline.md](file:///d:/Git_Hub/Test_AI/Test_AI/.agents/rules/diff_against_working_baseline.md))
