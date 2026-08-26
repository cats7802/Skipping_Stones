# [Script History] LobbyCharacterShowcaseController.cs

## 1. 개요 및 목적
* **스크립트 경로**: [Assets/Scripts/Visuals/LobbyCharacterShowcaseController.cs](file:///d:/Git_Hub/Skipping_Stones/Assets/Scripts/Visuals/LobbyCharacterShowcaseController.cs)
* **목적**: 로비 3D 디오라마에서 `Staging_Position`을 중심으로 캐릭터들을 화면 밖에서 부드럽게 걸어 들어오고/퇴장시키며, 360도 자유 회전 감상 및 인게임 선택 캐릭터를 동기화하는 컨트롤러.

---

## 2. 핵심 아키텍처 및 원칙

### 1. 프리팹 자동 스캔 & 해금 필터링
* `Assets/` 내의 모든 프리팹 중 `StoneThrowerCharacter` 컴포넌트를 보유한 프리팹을 이름 규칙과 무관하게 자동 탐색.
* `GameDataManager.Instance.characterCatalog`에서 `isUnlocked == true`인 캐릭터만 목록에 등록하여 도감 해금 시스템과 100% 연동.

### 2. 45도 쿼터뷰 맞춤 횡이동 트랜지션
* 로비 룸의 회전축을 고려하여 Y+45도 보정된 벡터를 이동축으로 사용.
* 등장 시 진행 방향을 바라보며 걸어오다가(SmoothStep 이징), 중앙 안착 직전(0.4초~) 부드럽게 정면 카메라로 회전 정렬.

### 3. 정밀 3D Raycast 터치 감지
* 마우스/터치 다운 시 캐릭터 콜라이더를 직접 터치했을 때만 360도 드래그 회전 활성화 (하단 돌 선택기 다이얼 터치와 완벽 분리).
* 로비에 배치된 캐릭터 콜라이더는 `isTrigger = true`로 유지하여 물리적 밀림/간섭 원천 차단.

---

## 3. 변경 이력

### [2026-08-26] 2차 수정: 카메라 직접 연결 슬롯 및 트랜지션 각도 보정 (55도)
* **작업 내역**:
  1. `targetCamera` 직렬화 필드 추가: 씬의 `Camera001` 또는 로비 카메라 직접 할당 지원 및 자동 폴백.
  2. `entryAngleOffset` 추가: 진입/퇴장 횡이동 각도를 기존 45도에서 **55도(+10도)**로 보정하여 쿼터뷰 카메라 시선과 완벽 일치.
* **컴파일 검증**: `dotnet build` 경고 0개, 오류 0개 완료.

