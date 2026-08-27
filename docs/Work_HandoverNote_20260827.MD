# [Work Handover Note] 2026-08-27: 지형 텍스처 버텍스 페인터 툴 구축 & 탑다운 리플레이/수면 비주얼 정비

> **문서 목적**: 오늘 작업한 유니티 6 대응 초경량 지형 버텍스 페인터 툴, URP 지형 셰이더, 탑다운 리플레이 스톤 아바타 스케일링, 수면(Water) 머티리얼 안정화 작업 내용을 완벽히 정리한 인수인계 문서.

---

## 1. 🖌️ Unity 6 초경량 지형 버텍스 페인터 툴 구축 완료

### 1) 신규 제작 파일
* **[SimpleVertexPainterWindow.cs](file:///d:/Git_Hub/Skipping_Stones/Assets/Scripts/Editor/SimpleVertexPainterWindow.cs)** (에디터 툴 윈도우)
* **[URP_TerrainVertexBlend.shader](file:///d:/Git_Hub/Skipping_Stones/Assets/Shaders/URP_TerrainVertexBlend.shader)** (4-Layer 정규화 지형 셰이더)

### 2) 핵심 기능 및 알고리즘
* **메시 폴리곤 직접 인터섹션 (`RaycastTargetMesh`)**:
  - 콜라이더나 가상 수평면에 의존하지 않고, 지형 메시의 실제 삼각형 폴리곤 면들과 마우스 광선 간의 3D 교차점을 매 프레임 정밀 계산.
  - 지형의 언덕/경사면 고저차에 관계없이 100% 정밀한 브러시 감지.
* **외곽/끝 정점 마그넷 보정 (`FindClosestVertexToRay`)**:
  - 마우스가 메시 외곽 경계선 밖으로 나가더라도 가장 가까운 모서리/끝 정점을 감지하여 페인팅 누락 방지.
* **중복/하드 엣지 정점(Coincident Vertices) 동시 전파**:
  - UV Seam이나 하드 엣지로 인해 한 점에 겹쳐 있는 2~4개의 모든 정점에 동일한 블렌딩 가중치를 동시 주입하여 정점 구멍 현상 완전 해결.
* **4-Layer 정규화 부드러운 블렌딩**:
  - Base Grass(기본 잔디), Layer 1 R(흙/길), Layer 2 G(바위), Layer 3 B(모래) 4개 텍스처 합성.
  - 새 레이어를 칠할 때 다른 잔여 채널 간섭을 부드럽게 감소시키며 자연스러운 그라데이션 유지.
* **커스텀 레이어 라벨 & 머티리얼 텍스처 자동 감지**:
  - 지형 선택 시 머티리얼에 꽂힌 텍스처 이름(`[T_Grass]`, `[T_Dirt]`, `[T_Rock]`)을 실시간 표시.
  - 페인터 창에서 레이어 이름을 직접 타이핑하여 편하게 식별 및 선택 가능.

---

## 2. 🪨 탑다운 리플레이 스톤 아바타 & 수면 비주얼 정비

### 1) 탑다운 리플레이 스톤 비주얼 ([TopDownReplayManager.cs](file:///d:/Git_Hub/Skipping_Stones/Assets/Scripts/Visuals/TopDownReplayManager.cs))
* **선택한 실제 조약돌 모델 연동**: 임의 Primitive Sphere 제거 및 `GameDataManager`의 선택된 돌 프리팹(`Stone`, `Stone_Blue`, `Stone_Green`, `Stone_red`) 인스턴스화.
* **시인성 강화 배율 적용**: `avatarBaseScale = Mathf.Clamp(orthoSize * 0.18f, 2.5f, 25f)` 적용으로 상공 80m 직하향 뷰에서도 궤적 리본 두께에 딱 맞는 선명한 조약돌 렌더링.
* **다이내믹 피치 틸팅 & 점프 팝업 유지**: 최고점 2.5배 팝업 확대 및 비행 궤적에 따른 $\pm 38^\circ$ 피치 틸팅 연출.

### 2) 수면 머티리얼 직하향 투명도 & 커스틱스 안정화
* **[M_StylizedWater.mat](file:///d:/Git_Hub/Skipping_Stones/Assets/Design_sources/3D/Environments/SoStylized/Environment/Water/Materials/M_StylizedWater.mat) & [Resources/M_StylizedWater.mat](file:///d:/Git_Hub/Skipping_Stones/Assets/Resources/M_StylizedWater.mat)**:
  - `_ShallowWaterColor.a`: `0.298` ➔ `0.88` (90도 직하향 탑다운 뷰에서도 바닥이 훤히 뚫리지 않고 맑은 수면 유지).
  - `_Caustic_Strength`: `40` ➔ `8` (바닥에 얼룩덜룩 뚫려 깜빡이던 과도한 커스틱스 완화).
  - `_Caustic_Distortion`: `0.07` ➔ `0.02` (수면 물결 왜곡 완화).

---

## 3. 🎯 향후 핵심 작업 예정 (주말 로드맵)
1. **🗺️ 맵 청크 베이커 도구 및 모듈러 하천 스트리밍 구축** (`LakeEnvironmentManager` / `MapData` / 가변 곡률 청크)
2. **☀️ 글로벌 환경광 / 조명 및 포그(Fog) 프리셋 시스템 정비**
3. **🌊 수면 인터랙션 오브젝트(부스트패드, 장애물 바위, 점핑피쉬) 비주얼/물리 피드백 완성**

---

## 4. 🛡️ 검증 및 빌드 상태
* `dotnet build Assembly-CSharp.csproj`: **경고 0개 / 오류 0개 (Pass)**
* `dotnet build Assembly-CSharp-Editor.csproj`: **경고 0개 / 오류 0개 (Pass)**
