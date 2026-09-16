using UnityEngine;
using SkippingStones.Data;
using SkippingStones.Gameplay;

namespace SkippingStones.UI
{
    /// <summary>
    /// 메타 UI 맵 및 게임 모드 선택 화면 핸들러
    /// - 장거리 랭킹 모드 vs 리듬 아케이드 모드 선택 전담
    /// </summary>
    public class MapSelectScreenHandler : IMetaScreenHandler
    {
        private readonly MetaUIManager manager;

        public MapSelectScreenHandler(MetaUIManager manager)
        {
            this.manager = manager;
        }

        public void OnEnter()
        {
        }

        public void OnExit()
        {
        }

        public void DrawScreen(float scale, float offsetX, float offsetY)
        {
            var dm = GameDataManager.Instance;
            if (dm == null) return;

            // 상단 헤더 바
            GUI.Box(new Rect(20, 20, 680, 95), string.Empty, manager.HeaderStyle);
            string nick = dm.UserData != null ? dm.UserData.nickname : "플레이어";
            int gold = dm.UserData != null ? dm.UserData.gold : 0;
            int dia = dm.UserData != null ? dm.UserData.diamonds : 0;
            int stam = dm.UserData != null ? dm.UserData.stamina : 10;
            int maxStam = dm.UserData != null ? dm.UserData.maxStamina : 10;

            GUI.Label(new Rect(40, 30, 220, 40), $"👤 {nick}", manager.LabelStyle);
            GUI.Label(new Rect(40, 65, 220, 40), $"🪙 {gold:N0}  💎 {dia}", manager.LabelStyle);
            GUI.Label(new Rect(450, 35, 160, 40), $"⚡ {stam}/{maxStam}", manager.LabelStyle);

            if (manager.DrawResponsiveButton(new Rect(615, 35, 65, 65), "⚙️", manager.GlassBtnStyle))
            {
                manager.ShowModal(MetaModal.Settings);
            }

            // 뒤로가기 버튼
            if (manager.DrawResponsiveButton(new Rect(40, 140, 90, 60), "⬅️ 뒤로", manager.GlassBtnStyle))
            {
                manager.ShowScreen(MetaScreen.Lobby);
            }

            // 모드 탭 (1500m vs 🎵 아케이드)
            GameController.GameMode curMode = dm.UserData.selectedGameMode;
            if (curMode == GameController.GameMode.TargetAccuracy)
            {
                curMode = GameController.GameMode.LongDistance;
                dm.UserData.selectedGameMode = GameController.GameMode.LongDistance;
            }

            bool isLong = (curMode == GameController.GameMode.LongDistance);
            bool isArcade = (curMode == GameController.GameMode.RhythmArcade);

            if (manager.DrawResponsiveButton(new Rect(140, 140, 200, 60), isLong ? "🔘 1500m" : "⚪ 1500m", isLong ? manager.TabActiveStyle : manager.TabInactiveStyle))
            {
                dm.UserData.selectedGameMode = GameController.GameMode.LongDistance;
            }

            if (manager.DrawResponsiveButton(new Rect(350, 140, 200, 60), isArcade ? "🔘 🎵아케이드" : "⚪ 🎵아케이드", isArcade ? manager.TabActiveStyle : manager.TabInactiveStyle))
            {
                dm.UserData.selectedGameMode = GameController.GameMode.RhythmArcade;
            }

            // 맵 환경 매니저 스캔 및 메타 정보 결정
            manager.ScanEnvManagers();
            var envList = manager.EnvMgrPrefabs;
            int totalMaps = Mathf.Max(1, envList.Count);
            if (manager.selectedMapIndex >= totalMaps) manager.selectedMapIndex = 0;
            int selIdx = manager.selectedMapIndex;

            string mapName = "에메랄드 호수";
            Sprite mapThumb = null;

            if (envList.Count > selIdx && envList[selIdx] != null)
            {
                var lem = envList[selIdx].GetComponent<LakeEnvironmentManager>();
                if (lem != null)
                {
                    if (!string.IsNullOrEmpty(lem.mapTitle)) mapName = lem.mapTitle;
                    else mapName = envList[selIdx].name;
                    mapThumb = lem.mapThumbnail;
                }
                else
                {
                    mapName = envList[selIdx].name;
                }
            }
            else if (dm != null && dm.mapCatalog.Count > selIdx)
            {
                mapName = dm.mapCatalog[selIdx].name;
            }

            // 맵 카드 뷰
            GUI.Box(new Rect(40, 220, 640, 480), string.Empty, manager.CardBoxStyle);
            GUI.Label(new Rect(60, 235, 600, 40), $"<b>🗺️ {mapName}</b>", manager.TitleStyle);

            Rect thumbRect = new Rect(80, 285, 560, 395);
            if (mapThumb != null && mapThumb.texture != null)
            {
                GUI.DrawTexture(thumbRect, mapThumb.texture, ScaleMode.ScaleAndCrop);
            }
            else
            {
                GUI.Box(thumbRect, $"\n\n\n\n🏞️ {mapName}\n({(envList.Count > selIdx && envList[selIdx] != null ? envList[selIdx].name : "기본 환경")})", manager.CardBoxStyle);
            }

            if (manager.DrawResponsiveButton(new Rect(60, 450, 70, 60), "◀", manager.GlassBtnStyle))
            {
                manager.selectedMapIndex = (selIdx - 1 + totalMaps) % totalMaps;
            }
            if (manager.DrawResponsiveButton(new Rect(590, 450, 70, 60), "▶", manager.GlassBtnStyle))
            {
                manager.selectedMapIndex = (selIdx + 1) % totalMaps;
            }

            // 🎵 음원 트랙 선택 카드 뷰
            var musicDb = dm.MusicDatabase;
            int totalTracks = (musicDb != null) ? musicDb.Count : 0;
            int currentTrackIdx = 0;
            if (musicDb != null && !string.IsNullOrEmpty(dm.UserData.selectedMusicId))
            {
                currentTrackIdx = Mathf.Max(0, musicDb.IndexOfId(dm.UserData.selectedMusicId));
            }

            var track = (musicDb != null && totalTracks > 0) ? musicDb.GetTrackByIndex(currentTrackIdx) : null;

            GUI.Box(new Rect(40, 715, 640, 185), string.Empty, manager.CardBoxStyle);

            if (track != null)
            {
                string starStr = track.GetStarString();
                GUI.Label(new Rect(60, 725, 600, 32), $"<b>🎵 {track.title}</b>   <color=#FFD700><b>{starStr}</b></color> <color=#90CAF9>({track.startBpm:0}~{track.maxBpm:0} BPM)</color>", manager.TitleStyle);
                
                GUIStyle descStyle = new GUIStyle(manager.LabelStyle);
                descStyle.fontSize = 18;
                descStyle.wordWrap = true;
                descStyle.normal.textColor = new Color(0.85f, 0.92f, 1.0f, 0.9f);
                GUI.Label(new Rect(60, 765, 520, 125), $"{track.genreConcept}", descStyle);
            }
            else
            {
                GUI.Label(new Rect(60, 750, 600, 40), "🎵 기본 BGM 트랙", manager.TitleStyle);
            }

            if (totalTracks > 1)
            {
                if (manager.DrawResponsiveButton(new Rect(50, 800, 50, 55), "◀", manager.GlassBtnStyle))
                {
                    int prevIdx = (currentTrackIdx - 1 + totalTracks) % totalTracks;
                    var prevTrack = musicDb.GetTrackByIndex(prevIdx);
                    if (prevTrack != null)
                    {
                        dm.UserData.selectedMusicId = prevTrack.id;
                        dm.SaveUserData();
                    }
                }

                if (manager.DrawResponsiveButton(new Rect(620, 800, 50, 55), "▶", manager.GlassBtnStyle))
                {
                    int nextIdx = (currentTrackIdx + 1) % totalTracks;
                    var nextTrack = musicDb.GetTrackByIndex(nextIdx);
                    if (nextTrack != null)
                    {
                        dm.UserData.selectedMusicId = nextTrack.id;
                        dm.SaveUserData();
                    }
                }
            }

            // GAME START 버튼
            if (manager.DrawResponsiveButton(new Rect(80, 920, 560, 90), "⚡-1   GAME START !", manager.PrimaryBtnStyle))
            {
                manager.StartMatchGame();
            }
        }
    }
}
