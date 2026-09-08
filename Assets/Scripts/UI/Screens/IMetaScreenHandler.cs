namespace SkippingStones.UI
{
    /// <summary>
    /// 메타 UI 화면(Screen) 및 모달(Modal) 상태 핸들러 인터페이스
    /// - 단일 파일 과밀을 방지하고 각 화면의 렌더링, 이벤트, 수명주기를 캡슐화
    /// </summary>
    public interface IMetaScreenHandler
    {
        /// <summary>
        /// 해당 화면/모달 진입 시 호출
        /// </summary>
        void OnEnter();

        /// <summary>
        /// 해당 화면/모달 퇴장 시 호출
        /// </summary>
        void OnExit();

        /// <summary>
        /// 화면 렌더링 및 UI 그리기 (OnGUI 또는 프레임 업데이트)
        /// </summary>
        void DrawScreen(float scale, float offsetX, float offsetY);
    }
}
