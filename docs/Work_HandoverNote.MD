# 📋 작업 인수인계 및 진행 내역 정리 (2026-08-25)

> **프로젝트명**: Stone Skipping (물수제비 마스터 3D)  
> **엔진 및 해상도**: Unity 6 (6000.5.8f1) / 모바일 세로 9:16 (720x1280 Virtual Native)  
> **정리 시점**: 로비 UI 단순화, 3D 디오라마/카메라 동적 로딩, 스톤 셀렉터 캐러셀 구현 및 ⚠️ 더미 궤도 이탈 이슈 기록 (0 Error / 0 Warning)

---

## 🎯 1. 오늘 구현 및 문제 해결 완료된 핵심 내역

### 1) 🎨 로비 UI 레이아웃 정리 & 안내 인디케이터 정렬 ([MetaUIManager.cs](file:///d:/Git_Hub/Skipping_Stones/Assets/Scripts/UI/MetaUIManager.cs))
* **중앙 텍스트 및 어두운 배경창 제거**: 3D 로비 룸 디오라마가 시원하게 보이도록 불필요한 중앙 레이아웃 걷어냄.
* **하단 독 바 밀착 정렬**: 상점, 도감, 랭킹, GO! 버튼을 최하단(`Y=1140~1150`)으로 밀착 배치.
* **3D 스톤 셀렉터 안내 화살표**: 다이얼 링 좌우 지점(`X=110, 275`, `Y=865`)에 안내용 화살표(`⇦`, `⇨`) 배치.
* **로비 3D 디오라마 및 카메라 전환**:
  - `MetaScreen.Lobby` 진입 시 `Assets/prefab/Lobby.prefab` 인스턴스화.
  - 씬 메인 카메라를 끄고 로비 내부의 전용 카메라(`Camera001`)로 자동 전환.
  - 로비 퇴장 시 인스턴스 파괴 및 메인 카메라 자동 복구.

### 2) 🪨 로비 스톤 쇼케이스 컨트롤러 구현 ([LobbyStoneShowcaseController.cs](file:///d:/Git_Hub/Skipping_Stones/Assets/Scripts/Visuals/LobbyStoneShowcaseController.cs))
* **입력 감지 엔진**: New Input System (`Touchscreen`, `Mouse`) 및 Legacy Input을 통합 지원하는 스와이프 드래그 인식.
* **회전 연출 엔진**:
  - 하단 다이얼(`StoneSelector`): 30도 스냅 회전.
  - 상단 스탠드(`Stone_Stand`): 120도 부드러운 가감속(SmoothStep) 회전.
* **3슬롯 순환 버퍼 & 등 뒤 갱신**:
  - 시작 시 슬롯 3개에 돌 3종을 미리 올려두고, 회전 완료 시 등 뒤(시야 밖)로 돌아간 슬롯만 다음 순서의 돌로 조용히 교체.

---

## ⚠️ 2. 현재 알려진 이상 현상 및 긴급 점검 필요 항목 (CRITICAL ISSUES)

> [!CAUTION]
> **스톤 쇼케이스 회전 시 더미 궤도 이탈 현상 (진행 중 / Ongoing)**:
> 1. **더미 위치/궤도 이탈**:
>    - `Stone_Stand`가 회전할 때, 자식으로 물려있는 더미 3개(`Stone_Stage_01, 02, 03`)가 스탠드의 접시(홈) 위에 안착되어 있지 않고 접시 밖으로 튀어나가거나 궤도를 이탈하여 돌아다니는 이상 현상이 지속 발생 중.
> 2. **원인 분석 및 점검 과제**:
>    - 맥스 FBX(`SelectStage.fbx` / `Stone_Select.fbx`)의 `Stone_Stand` 중심축(피벗) 및 더미들의 로컬 좌표계 상관관계를 에디터 씬 뷰에서 정밀 재검증하여, 스탠드 회전 시 더미가 왜 원래 위치를 벗어나는지 근본적인 원인 규명 및 수정 필요.

---

## 📁 3. 관련 핵심 파일 및 문서 목록

| 분류 | 파일 경로 | 설명 |
| :--- | :--- | :--- |
| **UI** | [MetaUIManager.cs](file:///d:/Git_Hub/Skipping_Stones/Assets/Scripts/UI/MetaUIManager.cs) | 로비 UI 배치 및 로비 3D 디오라마 라이프사이클 관리 |
| **Visuals** | [LobbyStoneShowcaseController.cs](file:///d:/Git_Hub/Skipping_Stones/Assets/Scripts/Visuals/LobbyStoneShowcaseController.cs) | 3D 스톤 셀렉터 회전 및 슬롯 관리 컨트롤러 |
| **Prefab** | [Assets/prefab/Lobby.prefab](file:///d:/Git_Hub/Skipping_Stones/Assets/prefab/Lobby.prefab) | 로비 3D 룸 디오라마 프리팹 (카메라, 스탠드, 다이얼 포함) |
| **Script History** | [MetaUIManager.md](file:///d:/Git_Hub/Skipping_Stones/docs/script_history/MetaUIManager.md) | UI 매니저 변경 이력 |
| **Script History** | [LobbyStoneShowcaseController.md](file:///d:/Git_Hub/Skipping_Stones/docs/script_history/LobbyStoneShowcaseController.md) | 쇼케이스 컨트롤러 이력 및 미해결 이상 현상 기록 |
| **Rules** | [docs/rules/](file:///d:/Git_Hub/Skipping_Stones/docs/rules/) | 에이전트 협업 및 안전 개발 규칙 |

---

## 🚀 4. 다음 작업 계획
1. **쇼케이스 더미 궤도 이탈 버그 완벽 해결**:
   - `Stone_Stand`와 `Stone_Stage_01~03` 더미 간의 계층 구조 및 회전 축 일치화.
2. **돌 인벤토리/도감 해금 데이터 연동**:
   - `StoneInventory.cs`의 실제 해금 돌 목록과 턴테이블 쇼케이스 연동.
