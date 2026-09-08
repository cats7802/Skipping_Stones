using UnityEngine;
using SkippingStones.Data;

namespace SkippingStones.UI
{
    /// <summary>
    /// 메타 UI 통합 모달 허브 핸들러
    /// - 도감(Collection), 상점(Shop), 랭킹(Rank), 설정(Settings), 스태미나 충전 전담
    /// </summary>
    public class MetaModalHubHandler
    {
        private readonly MetaUIManager manager;
        private Vector2 scrollPos;

        public MetaModalHubHandler(MetaUIManager manager)
        {
            this.manager = manager;
        }

        public void DrawModal(MetaModal modal, float scale, float offsetX, float offsetY)
        {
            if (modal == MetaModal.None) return;

            // 1. 모달 전체 반투명 딤 배경 및 메인 컨테이너
            GUI.Box(new Rect(30, 120, 660, 1040), string.Empty, manager.CardBoxStyle);

            // 2. 우측 상단 닫기(X) 버튼
            if (manager.DrawResponsiveButton(new Rect(620, 135, 55, 55), "✕", manager.GlassBtnStyle))
            {
                manager.CloseModal();
                return;
            }

            // 모달별 내부 내용 분기
            switch (modal)
            {
                case MetaModal.Collection:
                    DrawCollectionModal(scale, offsetX, offsetY);
                    break;
                case MetaModal.Shop:
                    DrawShopModal(scale, offsetX, offsetY);
                    break;
                case MetaModal.Rank:
                    DrawRankModal(scale, offsetX, offsetY);
                    break;
                case MetaModal.Settings:
                    DrawSettingsModal(scale, offsetX, offsetY);
                    break;
                case MetaModal.StaminaRefill:
                    DrawStaminaModal(scale, offsetX, offsetY);
                    break;
            }
        }

        private void DrawCollectionModal(float scale, float offsetX, float offsetY)
        {
            // 헤더 타이틀
            Rect titleRect = manager.GetScaledRect(60, 100, 500, 60, scale, offsetX, offsetY);
            GUI.Label(titleRect, "📖 마스터 도감 (Collection)", manager.HeaderStyle);

            // 3단 탭 ([👤 캐릭터] | [🪨 조약돌] | [🐠 수족관])
            float tabW = 190;
            float tabH = 65;
            float startX = 60;
            float gap = 20;

            Rect tab1 = manager.GetScaledRect(startX, 180, tabW, tabH, scale, offsetX, offsetY);
            if (manager.DrawResponsiveButton(tab1, "👤 캐릭터", manager.collectionTabIndex == 0 ? manager.TabActiveStyle : manager.TabInactiveStyle))
                manager.collectionTabIndex = 0;

            Rect tab2 = manager.GetScaledRect(startX + (tabW + gap) * 1, 180, tabW, tabH, scale, offsetX, offsetY);
            if (manager.DrawResponsiveButton(tab2, "🪨 조약돌", manager.collectionTabIndex == 1 ? manager.TabActiveStyle : manager.TabInactiveStyle))
                manager.collectionTabIndex = 1;

            Rect tab3 = manager.GetScaledRect(startX + (tabW + gap) * 2, 180, tabW, tabH, scale, offsetX, offsetY);
            if (manager.DrawResponsiveButton(tab3, "🐠 수족관", manager.collectionTabIndex == 2 ? manager.TabActiveStyle : manager.TabInactiveStyle))
                manager.collectionTabIndex = 2;

            // 탭별 목록 출력
            if (manager.collectionTabIndex == 0) DrawCharacterCollection(scale, offsetX, offsetY);
            else if (manager.collectionTabIndex == 1) DrawStoneCollection(scale, offsetX, offsetY);
            else DrawAquariumCollection(scale, offsetX, offsetY);
        }

        private void DrawCharacterCollection(float scale, float offsetX, float offsetY)
        {
            var dm = GameDataManager.Instance;
            if (dm == null || dm.characterCatalog == null) return;

            float startY = 270;
            float itemH = 140;
            float gap = 20;

            for (int i = 0; i < dm.characterCatalog.Count; i++)
            {
                var c = dm.characterCatalog[i];
                float y = startY + i * (itemH + gap);

                Rect card = manager.GetScaledRect(60, y, 600, itemH, scale, offsetX, offsetY);
                GUI.Box(card, "", manager.CardBoxStyle);

                Rect name = manager.GetScaledRect(80, y + 15, 350, 40, scale, offsetX, offsetY);
                GUI.Label(name, $"{c.name} ({c.title})", manager.HeaderStyle);

                Rect desc = manager.GetScaledRect(80, y + 60, 340, 65, scale, offsetX, offsetY);
                GUI.Label(desc, c.description, manager.LabelStyle);

                Rect actBtn = manager.GetScaledRect(440, y + 30, 200, 80, scale, offsetX, offsetY);
                if (c.isUnlocked)
                {
                    bool isSelected = (manager.selectedCharIndex == i);
                    if (manager.DrawResponsiveButton(actBtn, isSelected ? "✅ 선택됨" : "선택하기", isSelected ? manager.PrimaryBtnStyle : manager.GlassBtnStyle))
                    {
                        manager.selectedCharIndex = i;
                        dm.UserData.selectedCharacterId = c.id;
                        dm.SaveUserData();
                    }
                }
                else
                {
                    if (manager.DrawResponsiveButton(actBtn, $"🔒 해금하기\n(🪙{c.unlockGoldCost:N0})", manager.PrimaryBtnStyle))
                    {
                        if (dm.UserData.gold >= c.unlockGoldCost)
                        {
                            dm.UserData.gold -= c.unlockGoldCost;
                            dm.UserData.unlockedCharacterIds.Add(c.id);
                            c.isUnlocked = true;
                            dm.SaveUserData();
                        }
                    }
                }
            }
        }

        private void DrawStoneCollection(float scale, float offsetX, float offsetY)
        {
            var dm = GameDataManager.Instance;
            if (dm == null) return;

            // ScriptableObject 도감 우선 참조
            var db = dm.StoneDatabase;
            int count = db != null ? db.Count : (dm.stoneCatalog != null ? dm.stoneCatalog.Count : 0);

            float startY = 270;
            float itemH = 140;
            float gap = 20;

            for (int i = 0; i < count; i++)
            {
                string id = "";
                string displayName = "";
                string descStr = "";
                int cost = 0;
                bool isUnlocked = false;

                if (db != null && i < db.Count)
                {
                    var s = db.GetStoneByIndex(i);
                    id = s.id;
                    displayName = s.displayName;
                    descStr = s.description;
                    cost = s.unlockGoldCost;
                    isUnlocked = (dm.UserData != null && dm.UserData.unlockedStoneIds != null && dm.UserData.unlockedStoneIds.Contains(id));
                }
                else if (dm.stoneCatalog != null && i < dm.stoneCatalog.Count)
                {
                    var s = dm.stoneCatalog[i];
                    id = s.id;
                    displayName = s.name;
                    descStr = s.description;
                    cost = s.unlockGoldCost;
                    isUnlocked = s.isUnlocked;
                }

                float y = startY + i * (itemH + gap);

                Rect card = manager.GetScaledRect(60, y, 600, itemH, scale, offsetX, offsetY);
                GUI.Box(card, "", manager.CardBoxStyle);

                Rect name = manager.GetScaledRect(80, y + 15, 350, 40, scale, offsetX, offsetY);
                GUI.Label(name, $"🪨 {displayName}", manager.HeaderStyle);

                Rect desc = manager.GetScaledRect(80, y + 60, 340, 65, scale, offsetX, offsetY);
                GUI.Label(desc, descStr, manager.LabelStyle);

                Rect actBtn = manager.GetScaledRect(440, y + 30, 200, 80, scale, offsetX, offsetY);
                if (isUnlocked)
                {
                    bool isSelected = (dm.UserData.selectedStoneId == id);
                    if (manager.DrawResponsiveButton(actBtn, isSelected ? "✅ 장착 중" : "장착하기", isSelected ? manager.PrimaryBtnStyle : manager.GlassBtnStyle))
                    {
                        dm.UserData.selectedStoneId = id;
                        dm.SaveUserData();
                    }
                }
                else
                {
                    if (manager.DrawResponsiveButton(actBtn, $"🔒 해금\n(🪙{cost:N0})", manager.PrimaryBtnStyle))
                    {
                        if (dm.UserData.gold >= cost)
                        {
                            dm.UserData.gold -= cost;
                            if (!dm.UserData.unlockedStoneIds.Contains(id))
                                dm.UserData.unlockedStoneIds.Add(id);
                            dm.SyncCatalogWithUserData();
                            dm.SaveUserData();
                        }
                    }
                }
            }
        }

        private void DrawAquariumCollection(float scale, float offsetX, float offsetY)
        {
            Rect label = manager.GetScaledRect(60, 300, 600, 100, scale, offsetX, offsetY);
            GUI.Label(label, "🐠 물수제비 호수에서 포획한 물고기 도감입니다.", manager.LabelStyle);
        }

        private void DrawShopModal(float scale, float offsetX, float offsetY)
        {
            Rect titleRect = manager.GetScaledRect(60, 100, 500, 60, scale, offsetX, offsetY);
            GUI.Label(titleRect, "🛒 상점 (Shop)", manager.HeaderStyle);

            var dm = GameDataManager.Instance;
            if (dm == null) return;

            // 골드 충전 패키지
            Rect pack1 = manager.GetScaledRect(60, 200, 600, 180, scale, offsetX, offsetY);
            GUI.Box(pack1, "", manager.CardBoxStyle);
            Rect p1Title = manager.GetScaledRect(80, 220, 350, 45, scale, offsetX, offsetY);
            GUI.Label(p1Title, "🪙 골드 주머니 (10,000 Gold)", manager.HeaderStyle);
            Rect p1Btn = manager.GetScaledRect(430, 240, 210, 90, scale, offsetX, offsetY);
            if (manager.DrawResponsiveButton(p1Btn, "💎 100 구매", manager.PrimaryBtnStyle))
            {
                if (dm.UserData.diamonds >= 100)
                {
                    dm.UserData.diamonds -= 100;
                    dm.UserData.gold += 10000;
                    dm.SaveUserData();
                }
            }
        }

        private void DrawRankModal(float scale, float offsetX, float offsetY)
        {
            Rect titleRect = manager.GetScaledRect(60, 100, 500, 60, scale, offsetX, offsetY);
            GUI.Label(titleRect, "🏆 랭킹 (Leaderboard)", manager.HeaderStyle);

            var dm = GameDataManager.Instance;
            float best = dm != null ? dm.UserData.bestDistance : 0f;

            Rect myRank = manager.GetScaledRect(60, 200, 600, 120, scale, offsetX, offsetY);
            GUI.Box(myRank, "", manager.CardBoxStyle);
            Rect rLabel = manager.GetScaledRect(80, 230, 560, 50, scale, offsetX, offsetY);
            GUI.Label(rLabel, $"내 최고 비거리: {best:F1} m (상위 1%)", manager.HeaderStyle);
        }

        private void DrawSettingsModal(float scale, float offsetX, float offsetY)
        {
            Rect titleRect = manager.GetScaledRect(60, 100, 500, 60, scale, offsetX, offsetY);
            GUI.Label(titleRect, "⚙️ 게임 설정 (Settings)", manager.HeaderStyle);

            Rect box = manager.GetScaledRect(60, 200, 600, 400, scale, offsetX, offsetY);
            GUI.Box(box, "", manager.CardBoxStyle);

            Rect sLabel = manager.GetScaledRect(80, 240, 560, 50, scale, offsetX, offsetY);
            GUI.Label(sLabel, "BGM / SFX 오디오 음량 조절", manager.HeaderStyle);

            // 데이터 초기화 버튼
            Rect resetBtn = manager.GetScaledRect(160, 480, 400, 80, scale, offsetX, offsetY);
            if (manager.DrawResponsiveButton(resetBtn, "🔄 유저 데이터 순정 초기화", manager.GlassBtnStyle))
            {
                GameDataManager.Instance.DevResetToCleanUser();
            }
        }

        private void DrawStaminaModal(float scale, float offsetX, float offsetY)
        {
            Rect titleRect = manager.GetScaledRect(60, 100, 500, 60, scale, offsetX, offsetY);
            GUI.Label(titleRect, "⚡ 스태미나 충전", manager.HeaderStyle);

            Rect box = manager.GetScaledRect(60, 220, 600, 300, scale, offsetX, offsetY);
            GUI.Box(box, "", manager.CardBoxStyle);

            Rect refillBtn = manager.GetScaledRect(160, 350, 400, 100, scale, offsetX, offsetY);
            if (manager.DrawResponsiveButton(refillBtn, "⚡ 완전 회복 (💎 20)", manager.PrimaryBtnStyle))
            {
                var dm = GameDataManager.Instance;
                if (dm != null && dm.UserData.diamonds >= 20)
                {
                    dm.UserData.diamonds -= 20;
                    dm.UserData.stamina = dm.UserData.maxStamina;
                    dm.SaveUserData();
                    manager.CloseModal();
                }
            }
        }
    }
}
