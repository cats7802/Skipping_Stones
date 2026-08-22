# Unity Performance & Coding Standards (STRICT)

## 1. 메모리 할당 최소화 (Zero-GC in Update / Object Pooling)
- **절대 금지**: `Update()`, `FixedUpdate()`, `LateUpdate()` 등 매 프레임 실행되는 반복 루틴 내에서 `new` 키워드를 사용한 힙 메모리 할당 금지.
- **오브젝트 풀링**: 빈번하게 생성/파괴되는 인스턴스(돌멩이, 이펙트, 사운드 노드, 파티클 등)는 반드시 오브젝트 풀링(Object Pooling) 패턴을 적용하여 재사용.

## 2. 씬 참조 방식 고정 (No FindObjectOfType)
- **절대 금지**: `FindObjectOfType`, `FindObjectsOfType`, `GameObject.Find` 런타임 탐색 API 사용 금지.
- **명시적 연결**:
  - 인스펙터에서 직관적으로 할당 가능한 필드는 `[SerializeField] private` 필드로 노출.
  - 전역 관리자나 매니저는 정적 싱글톤(Singleton) 구조(`Instance`)를 통해 직접 연결.

## 3. 컴포넌트 캐싱 (Cache in Awake/Start)
- **절대 금지**: `Update()`, 렌더링 루틴, 혹은 빈번히 호출되는 메서드 내에서 `GetComponent<T>()`, `GetComponentInChildren<T>()` 호출 금지.
- **캐싱 원칙**: 모든 컴포넌트 참조는 `Awake()` 또는 `Start()` 시점에 한 번만 조회하여 private 멤버 변수에 캐싱 후 재사용.

## 4. 매직 넘버 금지 (No Magic Numbers)
- **절대 금지**: 코드 본문에 의미를 알 수 없는 리터럴 숫자(하드코딩된 값) 직접 작성 금지.
- **상수화**: 모든 물리 상수, 제한값, 타이머, 계수 등은 `const` 또는 `private static readonly` 필드로 명확한 이름을 부여하여 선언.

## 5. Null 안전성 보장 (Null Safety)
- **사전 검증**: 참조형 변수나 Unity Object 접근 전 항상 null 검사(`if (target != null)`)를 수행하거나 C# 널 조건부 연산자(`?.`)를 적용하여 `NullReferenceException` 방지.
- **안전한 이벤트 호출**: Action/Delegate 호출 시 `OnEvent?.Invoke()` 패턴 준수.
