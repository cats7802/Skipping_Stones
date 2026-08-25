# [Script History] LobbyStoneShowcaseController.cs

## 1. 개요 및 목적
* **스크립트 경로**: [Assets/Scripts/Visuals/LobbyStoneShowcaseController.cs](file:///d:/Git_Hub/Skipping_Stones/Assets/Scripts/Visuals/LobbyStoneShowcaseController.cs)
* **목적**: 로비 3D 디오라마에서 조작 다이얼(30도)과 돌 스탠드(120도)를 회전시키며 도감의 해금된 돌들을 3D 턴테이블 형태로 전시하는 컨트롤러.

---

## 2. 변경 이력

### 2026-08-25: 초판 구현 및 1차 리팩토링
* **작업 내용**:
  * 터치/마우스 좌우 스와이프 드래그 감지 (New Input System 및 Legacy Input 동시 지원).
  * `StoneSelector` 30도 회전 및 `Stone_Stand` 120도 부드러운 가감속(SmoothStep) 회전 구현.
  * 더미 3개(`Stone_Stage_01, 02, 03`)에 돌 인스턴스화 및 등 뒤 슬롯 순환 버퍼 갱신 로직 추가.
* **⚠️ 현재 알려진 문제점 (Known Issues - 점검 필수)**:
  * 회전 시 더미(`Stone_Stage_01~03`)와 돌들이 스탠드 접시 위 정위치를 유지하지 못하고 밖으로 궤도를 이탈하여 돌아다니는 이상 현상 지속 발생 중.
  * 맥스 FBX 내부의 `Stone_Stand` 중심 피벗과 더미 간의 로컬 좌표계 상관관계 정밀 재조사 및 디버깅 필요.
