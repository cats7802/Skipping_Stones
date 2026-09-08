using UnityEngine;
using SkippingStones.Auth;
using SkippingStones.Data;

namespace SkippingStones.UI
{
    /// <summary>
    /// 메타 UI 타이틀 화면 핸들러
    /// - 소셜/게스트 로그인 및 타이틀 연출 전담
    /// </summary>
    public class TitleScreenHandler : IMetaScreenHandler
    {
        private readonly MetaUIManager manager;

        public TitleScreenHandler(MetaUIManager manager)
        {
            this.manager = manager;
        }

        public void OnEnter()
        {
            // 타이틀 진입 시 초기화
        }

        public void OnExit()
        {
            // 타이틀 퇴장 시 정리
        }

        public void DrawScreen(float scale, float offsetX, float offsetY)
        {
            // 로고
            GUI.Label(new Rect(60, 220, 600, 80), "🌊 물수제비 마스터 3D", manager.TitleStyle);
            GUI.Label(new Rect(60, 310, 600, 40), "✨ Stone Skipping 3D ✨", manager.HeaderStyle);

            var dm = GameDataManager.Instance;
            bool hasKakao = dm != null && dm.UserData != null && dm.UserData.hasKakaoAccount;

            if (hasKakao)
            {
                // 카카오 토큰 보유 시: 부드럽게 깜빡이는 펄스 안내 문구 및 전면 터치 진입
                float alpha = 0.4f + Mathf.PingPong(Time.unscaledTime * 1.5f, 0.6f);
                Color prevColor = GUI.color;
                GUI.color = new Color(1f, 1f, 1f, alpha);
                GUI.Label(new Rect(60, 840, 600, 50), "• 화면을 터치하여 시작 (Touch to Start) •", manager.HeaderStyle);
                GUI.color = prevColor;

                // 화면 어디를 터치해도 즉시 로비 진입
                if (manager.DrawResponsiveButton(new Rect(0, 0, 720, 1280), string.Empty, GUIStyle.none))
                {
                    manager.LoginAndEnterLobby(AuthProviderType.Kakao);
                }
            }
            else
            {
                // 미로그인 상태: 1차 로그인 선택 2종 버튼 (카카오 & 게스트)
                if (manager.DrawResponsiveButton(new Rect(80, 820, 560, 80), "🟡 카카오 계정으로 로그인", manager.PrimaryBtnStyle))
                {
                    manager.LoginAndEnterLobby(AuthProviderType.Kakao);
                }

                if (manager.DrawResponsiveButton(new Rect(80, 920, 560, 75), "👤 게스트로 시작", manager.GlassBtnStyle))
                {
                    manager.LoginAndEnterLobby(AuthProviderType.Guest);
                }
            }
        }
    }
}
