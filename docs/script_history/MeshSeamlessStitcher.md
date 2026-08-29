# 📜 Script History: MeshSeamlessStitcher.cs

## 1. 개요 및 목적
* **파일 경로**: `Assets/Scripts/Terrain/MeshSeamlessStitcher.cs`
* **에디터 도구**:
  - `Assets/Scripts/Terrain/Editor/MeshSeamlessStitcherEditor.cs` (커스텀 인스펙터)
  - `Assets/Scripts/Editor/MeshSeamlessStitcherWindow.cs` (에디터 상단 메뉴 툴)
* **목적**: 
  - 3D 폴리곤 메쉬(`MeshFilter`, `MeshRenderer`) 지형/청크를 무한 스트리밍하거나 반복 배치할 때, **청크 간 경계면의 정점(Vertices), 법선 벡터(Normals), 버텍스 컬러(Colors)를 완벽히 일치시켜 틈(Gap)과 음영 끊김(Seam)을 제거**하는 에디터 도구.

---

## 2. 핵심 기능 및 지원 모드
1. **`SingleMeshSelfLoop` (단일 청크 자체 무한 루프)**:
   - 한 메쉬의 시작단(Z_min 또는 X_min)과 끝단(Z_max 또는 X_max)의 정점들을 1:1/N:N 매칭.
   - 평균 높이(Y) 및 평균 노멀을 양 끝에 적용하고, 안쪽으로 지정된 거리(`blendDistance`)만큼 S-Curve(`SmoothStep`) 보간.
2. **`TwoMeshesDocking` (두 메쉬 간 도킹 결합)**:
   - 현재 메쉬의 출구와 타깃 메쉬의 입구 정점을 월드 좌표계 기준으로 탐색하여 완벽 스냅.
   - `AverageBoth`, `MatchToTargetMesh`, `MatchToThisMesh` 3가지 결합 기준 지원.
3. **버텍스 컬러 동기화**:
   - 지형 셰이더 버텍스 페인팅(잔디/흙/바위/모래) 가중치 보간 지원.
4. **영구 에셋 저장 (`SaveMeshAsAsset`)**:
   - `Assets/_Project/Meshes/Seamless/` 경로에 `.asset` 파일로 저장 및 `MeshCollider` 자동 갱신.
5. **Undo 및 씬 뷰 기즈모 지원**:
   - `Ctrl + Z` 되돌리기 지원, 경계선 및 보간 영역 씬 뷰 시각화.

---

## 3. 변경 이력
* **2026-08-29**: 최초 설계 및 구현 완료 (유니티 6 URP 호환, 컴파일 경고 0개).
