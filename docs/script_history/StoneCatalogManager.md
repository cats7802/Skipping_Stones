# [Script History] StoneCatalogManager.cs & StoneCatalogManagerEditor.cs

## 1. 개요 및 목적
* **스크립트 경로**:
  - [Assets/Scripts/Data/StoneCatalogManager.cs](file:///d:/Git_Hub/Skipping_Stones/Assets/Scripts/Data/StoneCatalogManager.cs) (데이터 관리 컴포넌트)
  - [Assets/Scripts/Editor/StoneCatalogManagerEditor.cs](file:///d:/Git_Hub/Skipping_Stones/Assets/Scripts/Editor/StoneCatalogManagerEditor.cs) (커스텀 인스펙터 에디터)
  - `Assets/Resources/Data/StoneCatalogData.json` (도감 JSON 영구 저장소)
* **목적**:
  - 하이어라키에서 빈 오브젝트에 `[AddComponentMenu("Skipping Stones/Stone Catalog Manager")]`를 추가하여 인스펙터에서 시각적으로 조약돌 도감을 등록/편집/삭제할 수 있는 저작 툴.
  - 프리팹 드래그&드롭으로 파일 경로 자동 인식 및 단일 진실 공급원(`Resources/Data/StoneCatalogData.json`) 영구 동기화.

---

## 2. 변경 이력

### 2026-08-28: 최초 구축
* **작업 내역**:
  1. `StoneCatalogManager.cs`: `Resources/Data/StoneCatalogData.json` 로드/저장/시드 헬퍼 구현.
  2. `StoneCatalogManagerEditor.cs`:
     - 등록된 돌 목록 카드 뷰 (편집/삭제/순서 이동 지원).
     - 프리팹 드래그&드롭 기반 신규 돌 등록 폼 구현 (ID/이름 자동 채우기).
  3. `GameDataManager.cs`: `StoneCatalogManager.LoadMasterCatalog()` 호출로 도감 데이터 통합.
* **컴파일 검증**: `dotnet build Assembly-CSharp.csproj` / `Assembly-CSharp-Editor.csproj` 경고 0개, 오류 0개 완료.
