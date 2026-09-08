using UnityEngine;
using SkippingStones.Data;

namespace SkippingStones.UI
{
    /// <summary>
    /// 메타 UI 로비 화면 핸들러
    /// - 상단 재화 헤더 바, 캐릭터/돌 쇼케이스 연동, 하단 독 바 전담
    /// </summary>
    public class LobbyScreenHandler : IMetaScreenHandler
    {
        private readonly MetaUIManager manager;

        public LobbyScreenHandler(MetaUIManager manager)
        {
            this.manager = manager;
        }

        public void OnEnter()
        {
            // 로비 진입 시 쇼케이스 갱신
        }

        public void OnExit()
        {
            // 로비 퇴장 시 정리
        }

        public void DrawScreen(float scale, float offsetX, float offsetY)
        {
            var dm = GameDataManager.Instance;
            var user = dm != null ? dm.UserData : null;

            // 1. 상단 재화 및 프로필 헤더 바
            DrawTopHeader(dm, user);

            // 2. 캐릭터 좌/우 스위처
            DrawCharacterSwitcher(dm);

            // 3. 하단 독 바 & GO 버튼
            DrawBottomDock();
        }

        private void DrawTopHeader(GameDataManager dm, UserPersistentData user)
        {
            GUI.Box(new Rect(20, 20, 680, 95), string.Empty, manager.HeaderStyle);

            string nick = user != null ? user.nickname : "플레이어";
            int gold = user != null ? user.gold : 0;
            int dia = user != null ? user.diamonds : 0;
            int stam = user != null ? user.stamina : 10;
            int maxStam = user != null ? user.maxStamina : 10;

            if (manager.DrawResponsiveButton(new Rect(30, 25, 400, 85), string.Empty, GUIStyle.none))
            {
                dm?.TriggerSecretTap();
            }

            GUI.Label(new Rect(40, 30, 220, 40), $"👤 {nick}", manager.LabelStyle);
            GUI.Label(new Rect(40, 65, 220, 40), $"🪙 {gold:N0}  💎 {dia}", manager.LabelStyle);
            GUI.Label(new Rect(450, 35, 160, 40), $"⚡ {stam}/{maxStam}", manager.LabelStyle);

            if (manager.DrawResponsiveButton(new Rect(615, 35, 65, 65), "⚙️", manager.GlassBtnStyle))
            {
                manager.ShowModal(MetaModal.Settings);
            }
        }

        private void DrawCharacterSwitcher(GameDataManager dm)
        {
            // 1. 캐릭터 스위처 (좌/우 화살표 클릭 시 3D 쇼케이스 트랜지션 연동)
            if (manager.DrawResponsiveButton(new Rect(60, 320, 80, 80), "◀", manager.GlassBtnStyle))
            {
                manager.SwitchCharacter(-1);
            }
            if (manager.DrawResponsiveButton(new Rect(580, 320, 80, 80), "▶", manager.GlassBtnStyle))
            {
                manager.SwitchCharacter(1);
            }
        }

        private void DrawBottomDock()
        {
            // 3. 하단 독 바 & GO 버튼 (최하단 Y=1150으로 바짝 밀착)
            if (manager.DrawResponsiveButton(new Rect(30, 1150, 130, 95), "🛒\n상점", manager.GlassBtnStyle))
            {
                manager.ShowModal(MetaModal.Shop);
            }
            if (manager.DrawResponsiveButton(new Rect(175, 1150, 130, 95), "📖\n도감", manager.GlassBtnStyle))
            {
                manager.ShowModal(MetaModal.Collection);
            }
            if (manager.DrawResponsiveButton(new Rect(320, 1150, 130, 95), "🏆\n랭킹", manager.GlassBtnStyle))
            {
                manager.ShowModal(MetaModal.Rank);
            }

            if (manager.DrawResponsiveButton(new Rect(465, 1140, 225, 110), "🚀 GO!\n(맵 선택)", manager.PrimaryBtnStyle))
            {
                manager.ShowScreen(MetaScreen.MapSelect);
            }
        }
    }
}
