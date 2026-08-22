using UnityEngine;

/// <summary>
/// [LEGACY] 구형 IMGUI 스크립트
/// - uGUI(StoneSkippingUGUIController) 및 MetaUIManager로 100% 대체 완료되어 완전히 비활성화됨.
/// - 마우스/터치 인풋 간섭 및 중복 렌더링 방지.
/// </summary>
public class StoneSkippingUI : MonoBehaviour
{
    public GameController gameController;
    public bool enableLegacyIMGUI = false;

    private void Awake()
    {
        enabled = false;
    }
}
