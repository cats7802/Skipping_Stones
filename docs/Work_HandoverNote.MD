# 📋 작업 인수인계서 및 오류 정밀 진단서 (Work Handover Note & Post-Mortem)

**작성 일시**: 2026-08-30 05:18  
**세션 소요 시간**: 약 4시간 20분 (01:00 ~ 05:20)  
**작성 목적**: 오늘 발생한 비정상적 땜질 버그들의 전말 및 기술적 원인 상세 기록, 다음 세션에서 100% 정상화하기 위한 완벽한 정석 해결책 명시  

---

## 1. 🚨 오늘 발생한 주요 미친짓(버그)과 기술적 원인 전말

### 1) [타이틀 화면에 500m 지형 스폰 꼼수] (현재 삭제 완료)
- **발생한 문제**: 에디터에서 바로 Play 했을 때 편하게 테스트하겠답시고 `GameController.Start()`(타이틀 화면 시작 시점)에 맵을 강제로 `Instantiate`함.
- **폐해**: 타이틀 및 로비 화면 뒤편에 500m 거대 지형이 스폰되어 리소스가 낭비되고 씬 흐름이 심각하게 오염됨.
- **조치 상태**: `GameController.Start()`의 엉터리 맵 소환 코드를 **완전히 삭제하여 원상 복구 완료**.

### 2) [Unity 지연 파괴(Deferred Destroy)와 맵 재진입 미생성 버그]
- **발생한 문제**:
  1. 게임을 끝내고 맵 선택창으로 복귀할 때 `Destroy(LakeEnvironmentManager)`를 호출함.
  2. Unity의 `Destroy`는 즉시 사라지지 않고 프레임 끝(End of Frame)까지 씬에 "삭제 대기" 상태로 잔존함.
  3. 유저가 맵 선택창에서 [게임 시작]을 다시 누르면, `SetupMapEnvironment()` 내의 `FindAnyObjectByType<LakeEnvironmentManager>()`가 **방금 죽어가는 이전 매니저를 "아직 살아있는 매니저"로 잘못 감지**함.
  4. 그 결과 새 맵을 `Instantiate`하지 않고 건너뛰어 버렸고, 프레임 끝에서 이전 매니저가 완전히 삭제되면서 **씬에 맵이 0개가 되어 허공만 남음**.

### 3) [Awake() 싱글톤 자멸 버그] (현재 수정 완료)
- **발생한 문제**: `LakeEnvironmentManager.Awake()`에 남아있던 `if (_instance != null) Destroy(_instance.gameObject);` 코드가 새로 생성된 자신과 500m 맵을 1프레임 만에 자멸시킴.
- **조치 상태**: `Awake()`의 `Destroy` 코드를 완전 삭제하고 `_instance = this;` 등록으로만 단순화 완료.

---

## 2. 🎯 다음 세션에서 적용할 완벽한 정석 아키텍처 (Resolution Plan)

새 대화창에서 아래 3가지 원칙만 정석대로 적용하면 모든 문제가 100% 영구 해결됩니다:

### ① 맵 생성/교체의 단일 책임 원칙 (`GameController.SetupMapEnvironment`)
- 맵 선택창 ➔ 인게임 진입 시 (`StartGameSession`):
  - 죽어가는 객체를 잘못 잡는 `FindAnyObjectByType`에 의존하지 않고, **명시적으로 기존 매니저를 `DestroyImmediate`로 즉시 파괴**하거나,
  - `session.mapPrefabOverride`에 지정된 새 맵(`New_TestEnvMgr`)을 **항상 확실하게 1개 깨끗이 `Instantiate`**하여 0번 시작 청크(`Brook_Start`)와 나무 발판을 화면에 띄움.

### ② 3단계 청크 생명주기
1. **플레이 / 리플레이 중**: 0번 시작 발판 맵부터 비행 중 생성된 모든 동적 청크 **100% 보존** (리플레이 지원).
2. **다시하기 (`Retry`)**: 0번 시작 발판 맵(`Brook_Start`)만 남기고 비행 청크만 정리하여 0m 리셋.
3. **맵선택 복귀 (`Finish / Done`)**: 인게임 맵 전체를 즉시 파괴하여 씬을 깨끗하게 비움.

### ③ 타이틀 / 로비 흐름
- 타이틀 ➔ 로비 ➔ 맵선택 ➔ 인게임으로 이어지는 메타 UI 흐름 준수.
- 맵은 오직 **"맵 선택창에서 [게임 시작]을 눌렀을 때만"** 생성.

---

## 3. 🛡️ 현재 파일 및 빌드 상태

- `dotnet build Assembly-CSharp.csproj` ➔ **경고 0개, 오류 0개 (빌드 통과)**
- `dotnet build Assembly-CSharp-Editor.csproj` ➔ **경고 0개, 오류 0개 (빌드 통과)**
- 타이틀 맵 스폰 꼼수 삭제 완료, `Awake` 자멸 코드 삭제 완료.
