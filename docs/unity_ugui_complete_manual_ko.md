# 📱 유니티 uGUI (Unity UI) 실전 정석 완벽 매뉴얼

본 문서는 유니티 공식 UI 시스템(uGUI)을 사용하여 모바일/PC 반응형 해상도 대응, 스마트폰 노치(Safe Area) 회피, 드로우콜 최적화 및 터치 인터랙션을 구축하는 완벽 가이드입니다.

---

## 1. uGUI 4대 필수 컴포넌트

모든 uGUI 화면은 아래 4개의 핵심 요소로 구성됩니다.

| 컴포넌트 | 역할 | 실전 필수 세팅 |
|:---|:---|:---|
| **Canvas** | UI가 렌더링되는 루트 도화지 | • `Render Mode`: Screen Space - Overlay (또는 Camera)<br>• `Pixel Perfect`: 체크 (UI 선명도 유지) |
| **Canvas Scaler** | 화면 해상도 변화에 따른 UI 자동 스케일링 | • `UI Scale Mode`: **Scale With Screen Size**<br>• `Reference Resolution`: **720 x 1280** (모바일 표준 세로)<br>• `Screen Match Mode`: **Match Width Or Height** (`Match`: 0 또는 0.5) |
| **Graphic Raycaster** | 마우스/터치 클릭을 감지하는 광선 투사기 | • 캔버스에 반드시 1개 부착<br>• `Blocking Objects`: None |
| **Event System** | 클릭/드래그/키보드 입력을 UI 버튼에 전달 | • 씬에 반드시 1개 존재 (`EventSystem` + `InputModule`)<br>• New Input System 사용 시: `InputSystemUIInputModule` 필수 |

---

## 2. 모바일 반응형 화면비 & 캔버스 스케일러 공식

### 🎯 왜 해상도마다 UI가 찌그러지거나 잘리는가?
스마트폰은 9:16 (구형), 9:19.5 (아이폰/갤럭시), 1:1 (폴더블), 4:3 (태블릿) 등 비율이 천차만별입니다.

### 📐 Canvas Scaler 3대 모드 비교
1. **`Match Width (0.0)` (가장 추천)**:
   * 캔버스 가로 너비를 **720px로 무조건 고정**.
   * 화면이 세로로 길어지면 위아래에 공간이 늘어나고, 좌우 여백은 항상 일정하게 유지됨.
2. **`Match Height (1.0)`**:
   * 캔버스 세로 높이를 **1280px로 무조건 고정**.
   * 가로로 넓은 태블릿/PC 화면 대응에 유리.
3. **`Shrink` (전체 화면 맞춤)**:
   * UI 전체가 화면 밖으로 1픽셀도 나가지 않도록 엔진이 자동으로 전체 축소.

---

## 3. RectTransform 앵커(Anchors) & 피벗(Pivot) 정석

UI 요소의 위치 기준점을 설정하는 핵심 규칙입니다.

```
[Top-Left]       [Top-Center]       [Top-Right]
     ┌──────────────────────────────────┐
     │  상단바 (Top-Center / Top-Stretch)│
     ├──────────────────────────────────┤
     │                                  │
     │      모달창 (Middle-Center)       │
     │                                  │
     ├──────────────────────────────────┤
     │    조작 버튼 (Bottom-Center)      │
     └──────────────────────────────────┘
[Bottom-Left]   [Bottom-Center]    [Bottom-Right]
```

* **상단바 (코인/프로필/메뉴)**: `Anchor: Top-Center` (또는 Top-Stretch)
* **팝업/모달/로비 카드**: `Anchor: Middle-Center` (중앙 정렬)
* **하단 버튼/발사 게이지**: `Anchor: Bottom-Center` (바닥 밀착)

---

## 4. 스마트폰 노치 & 펀치홀(Safe Area) 자동 회피 공식

아이폰 M자 탈모 노치나 안드로이드 카메라홀에 UI가 가려지지 않게 하는 정석 C# 코드입니다.

```csharp
using UnityEngine;

public class SafeAreaFitter : MonoBehaviour
{
    private RectTransform rectTransform;

    void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        ApplySafeArea();
    }

    void ApplySafeArea()
    {
        Rect safeArea = Screen.safeArea;
        
        // 0.0 ~ 1.0 정규화 좌표 계산
        Vector2 minAnchor = safeArea.position;
        Vector2 maxAnchor = minAnchor + safeArea.size;

        minAnchor.x /= Screen.width;
        minAnchor.y /= Screen.height;
        maxAnchor.x /= Screen.width;
        maxAnchor.y /= Screen.height;

        rectTransform.anchorMin = minAnchor;
        rectTransform.anchorMax = maxAnchor;
    }
}
```

---

## 5. 자동 레이아웃 (VerticalLayoutGroup) & 겹침 방지

글자 수나 해상도에 따라 UI가 겹치는 현상을 100% 방지하는 유니티 내장 레이아웃 시스템입니다.

1. **부모 오브젝트에 `VerticalLayoutGroup` 추가**:
   * `Padding`: 상하좌우 여백 지정 (예: Left 20, Right 20, Top 20, Bottom 20)
   * `Spacing`: 자식들 사이의 간격 (예: 12px)
   * `Child Alignment`: `Upper Center` 또는 `Middle Center`
   * `Control Child Size`: `Width: 체크`, `Height: 체크 해제` (높이는 자식이 스스로 결정)
2. **자식 요소에 `LayoutElement` 추가**:
   * 각 자식(제목, 설명, 버튼)마다 `Min Height` 또는 `Preferred Height`를 지정하면 위에서 아래로 차례대로 자동 정렬되어 **절대 겹치지 않습니다.**

---

## 6. 터치 & 버튼 인터랙션 최적화 규칙

1. **`Raycast Target` 불필요한 체크 해제**:
   * 클릭할 필요가 없는 단순 배경 이미지, 텍스트, 장식 아이콘은 **`Raycast Target = false`**로 꺼두어야 불필요한 연산이 줄고 클릭 씹힘이 사라집니다.
2. **버튼 중복 연타 방지 (Debounce)**:
   * 버튼 클릭 시 최소 0.2초 쿨다운을 두어 모달이 중복으로 열리거나 오작동하는 것을 방지합니다.
3. **손가락 뗌 락 (Touch Release)**:
   * 화면이 전환될 때 손가락을 완전히 뗐을 때만 다음 화면 버튼이 눌리도록 처리하여 씬 전환 시 의도치 않은 버튼 터치를 원천 차단합니다.

---

## 7. 성능 및 드로우콜(Draw Call) 최적화 3대 원칙

1. **동적 UI와 정적 UI Canvas 분리**:
   * 비거리/점수처럼 매 프레임 숫자가 바뀌는 텍스트는 **별도의 서브 캔버스(Sub Canvas)**에 두어, 정적 배경 전체가 매 프레임 리빌드(Rebuild)되는 것을 방지합니다.
2. **스프라이트 아틀라스(Sprite Atlas) 사용**:
   * 버튼, 아이콘, 테두리 이미지를 1장의 아틀라스로 묶어 **드로우콜(Draw Call)을 1회로 압축**합니다.
3. **가비지(GC) 0% 텍스트 갱신**:
   * 매 프레임 `text = "Score: " + score;`처럼 문자열을 더하면 메모리 쓰레기가 발생하므로, 값이 실제로 바뀌었을 때만 갱신합니다.
