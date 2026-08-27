# 📜 Script History: SimpleVertexPainterWindow & URP_TerrainVertexBlend

> **작성일자**: 2026-08-27  
> **용도**: Unity 6 완벽 호환 초경량 지형 텍스처 버텍스 페인터 에디터 윈도우 및 4-Layer URP 버텍스 블렌딩 셰이더

---

## 1. 개요 및 제작 배경
- Unity 6 업데이트로 인해 레거시 Polybrush 패키지 컴파일 에러 및 Tools 메뉴 실종 문제 발생.
- 외부 패키지 의존성 없이 순수 C# 및 HLSL로 동작하는 프로젝트 맞춤형 가벼운 지형 버텍스 페인팅 시스템 구축.

---

## 2. 주요 기능 및 구현 사양

### 1) URP_TerrainVertexBlend.shader (`Assets/Shaders/`)
- **4-Layer 정규화 블렌딩**: Base Grass (기본 잔디), Layer 1 R (Dirt), Layer 2 G (Rock), Layer 3 B (Sand).
- **표준 UV Tiling & Offset**: 유니티 표준 `TRANSFORM_TEX` 적용으로 인스펙터 Tiling(X, Y) 및 Offset 실시간 반영.
- **URP Lit 기본 라이팅**: 디렉셔널 라이트 및 앰비언트 라이팅 완벽 지원.

### 2) SimpleVertexPainterWindow.cs (`Assets/Scripts/Editor/`)
- **메시 삼각형 직접 인터섹션 (`RaycastTargetMesh`)**:
  - 임의 평면이나 콜라이더에 의존하지 않고 실제 메시 폴리곤 삼각형들과 마우스 레이 간의 정확한 3D 교차점 계산.
  - 고저차가 있는 지형 언덕/경사면에서도 정밀한 브러시 감지.
- **중복/하드 엣지 정점 완벽 동기화**: UV Seam이나 하드 엣지로 분할된 겹친 버텍스(Coincident Vertices)에 동일 가중치 동시 주입으로 블렌딩 구멍 방지.
- **선택 박스 가로채기 방지 (`AddDefaultControl`)**: 씬 뷰 드래그 시 박스 선택 차단 및 브러시 페인팅 독점.
- **커스텀 레이어 명칭 및 머티리얼 텍스처 자동 감지**: 머티리얼에 연결된 텍스처 이름 실시간 표시 및 레이어별 사용자 지정 텍스트 라벨 지원.

---

## 3. 관련 파일 목록
- `Assets/Scripts/Editor/SimpleVertexPainterWindow.cs`
- `Assets/Shaders/URP_TerrainVertexBlend.shader`
- `Assets/Materials/River_ground_Blend.mat`
