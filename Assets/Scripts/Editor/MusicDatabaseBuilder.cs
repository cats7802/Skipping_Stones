#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using SkippingStones.Data;

namespace SkippingStones.Editor
{
    [InitializeOnLoad]
    public static class MusicDatabaseBuilder
    {
        static MusicDatabaseBuilder()
        {
            EditorApplication.delayCall += () =>
            {
                if (!File.Exists("Assets/Resources/Data/MusicDatabase.asset"))
                {
                    BuildMusicDatabase();
                }
            };
        }

        private class TrackDef
        {
            public string id;
            public string title;
            public string genreConcept;
            public string assetPath;
            public int starDifficulty;
            public float startBpm;
            public float maxBpm;
            public List<BpmTimelineNode> timeline;
        }

        [MenuItem("Skipping Stones/Build Music Database")]
        public static void BuildMusicDatabase()
        {
            string folderPath = "Assets/Resources/Data/Music";
            if (!Directory.Exists(folderPath))
            {
                Directory.CreateDirectory(folderPath);
                AssetDatabase.Refresh();
            }

            var defs = new List<TrackDef>
            {
                new TrackDef
                {
                    id = "track_01_crossing_horizon",
                    title = "Crossing the Horizon Line",
                    genreConcept = "로파이 힙합 / 아늑한 바이닐 노이즈와 따뜻한 피아노로 시작해 점점 밝고 선명해지는 여행의 시작",
                    assetPath = "Assets/Audio/Crossing_the_Horizon_Line(60-120).mp3",
                    starDifficulty = 1,
                    startBpm = 60f,
                    maxBpm = 120f,
                    timeline = new List<BpmTimelineNode>
                    {
                        new BpmTimelineNode(0f, 60f),
                        new BpmTimelineNode(30f, 75f),
                        new BpmTimelineNode(60f, 90f),
                        new BpmTimelineNode(90f, 105f),
                        new BpmTimelineNode(120f, 120f)
                    }
                },
                new TrackDef
                {
                    id = "track_02_miles_from_hearth",
                    title = "Miles from the Hearth",
                    genreConcept = "아이리시 포크 → 런던 대도시 / 시골길의 서정적인 아이리시 휘슬로 시작해 대도시 런던에 도달하는 오케스트라 퓨전",
                    assetPath = "Assets/Audio/Miles_from_the_Hearth(60-120).mp3",
                    starDifficulty = 1,
                    startBpm = 60f,
                    maxBpm = 120f,
                    timeline = new List<BpmTimelineNode>
                    {
                        new BpmTimelineNode(0f, 60f),
                        new BpmTimelineNode(30f, 75f),
                        new BpmTimelineNode(60f, 90f),
                        new BpmTimelineNode(90f, 105f),
                        new BpmTimelineNode(120f, 120f)
                    }
                },
                new TrackDef
                {
                    id = "track_03_tall_grass_open_sky",
                    title = "Tall Grass and Open Sky",
                    genreConcept = "컨트리 → 대자연의 풍경 / 신나는 어쿠스틱/반조 컨트리로 시작해 마지막엔 탁 트인 대자연을 마주하는 감동적인 서사",
                    assetPath = "Assets/Audio/Tall_Grass_and_Open_Sky(90-150).mp3",
                    starDifficulty = 2,
                    startBpm = 90f,
                    maxBpm = 150f,
                    timeline = new List<BpmTimelineNode>
                    {
                        new BpmTimelineNode(0f, 90f),
                        new BpmTimelineNode(30f, 110f),
                        new BpmTimelineNode(60f, 130f),
                        new BpmTimelineNode(90f, 150f)
                    }
                },
                new TrackDef
                {
                    id = "track_04_last_dance_daybreak",
                    title = "Last Dance at Daybreak",
                    genreConcept = "라틴 포크 → 떠나는 여행자 / 모닥불가의 아늑한 라틴/쿰비아 축제 분위기에서 새벽녘 길을 떠나는 아련하고 역동적인 여정",
                    assetPath = "Assets/Audio/Last_Dance_at_Daybreak(90-150).mp3",
                    starDifficulty = 2,
                    startBpm = 90f,
                    maxBpm = 150f,
                    timeline = new List<BpmTimelineNode>
                    {
                        new BpmTimelineNode(0f, 90f),
                        new BpmTimelineNode(30f, 110f),
                        new BpmTimelineNode(60f, 130f),
                        new BpmTimelineNode(90f, 140f),
                        new BpmTimelineNode(120f, 150f)
                    }
                },
                new TrackDef
                {
                    id = "track_05_carnival_burn",
                    title = "Carnival Burn",
                    genreConcept = "카리브해 레게 → 정글/래거 / 여유로운 해변 더브/레게에서 시작해 광란의 섬 축제로 치닫는 초고속 트로피컬 사운드",
                    assetPath = "Assets/Audio/Carnival_Burn(120-200).mp3",
                    starDifficulty = 3,
                    startBpm = 120f,
                    maxBpm = 200f,
                    timeline = new List<BpmTimelineNode>
                    {
                        new BpmTimelineNode(0f, 120f),
                        new BpmTimelineNode(24f, 140f),
                        new BpmTimelineNode(48f, 160f),
                        new BpmTimelineNode(72f, 180f),
                        new BpmTimelineNode(96f, 200f)
                    }
                },
                new TrackDef
                {
                    id = "track_06_pavement_under_heels",
                    title = "Pavement Under My Heels",
                    genreConcept = "블루지 쿨 재즈 → 빅밴드 비밥 / 고향을 그리워하는 쓸쓸한 색소폰 솔로에서 용기를 얻고 폭발하는 빅밴드 질주",
                    assetPath = "Assets/Audio/Pavement_Under_My_Heels(120-200).mp3",
                    starDifficulty = 3,
                    startBpm = 120f,
                    maxBpm = 200f,
                    timeline = new List<BpmTimelineNode>
                    {
                        new BpmTimelineNode(0f, 120f),
                        new BpmTimelineNode(30f, 140f),
                        new BpmTimelineNode(60f, 160f),
                        new BpmTimelineNode(90f, 180f),
                        new BpmTimelineNode(120f, 200f)
                    }
                },
                new TrackDef
                {
                    id = "track_07_bone_and_bronze",
                    title = "Bone and Bronze",
                    genreConcept = "트라이벌 제사 의식 / 사냥을 떠나는 청년들을 위한 웅장하고 엄숙한 원시적 제사 북소리와 폭발적인 신앙의 광기",
                    assetPath = "Assets/Audio/아프로비트/Bone_and_Bronze(180-250).mp3",
                    starDifficulty = 4,
                    startBpm = 180f,
                    maxBpm = 250f,
                    timeline = new List<BpmTimelineNode>
                    {
                        new BpmTimelineNode(0f, 180f),
                        new BpmTimelineNode(24f, 200f),
                        new BpmTimelineNode(48f, 220f),
                        new BpmTimelineNode(72f, 235f),
                        new BpmTimelineNode(96f, 250f)
                    }
                },
                new TrackDef
                {
                    id = "track_08_spears_at_dawn",
                    title = "Spears at Dawn",
                    genreConcept = "아프로비트 / 명상적이고 아늑한 로파이/어쿠스틱 분위기에서 시작해 점차 그루브가 고조되는 현대적 아프로비트",
                    assetPath = "Assets/Audio/아프로비트/Spears_at_Dawn(60-120).mp3",
                    starDifficulty = 1,
                    startBpm = 60f,
                    maxBpm = 120f,
                    timeline = new List<BpmTimelineNode>
                    {
                        new BpmTimelineNode(0f, 60f),
                        new BpmTimelineNode(30f, 75f),
                        new BpmTimelineNode(60f, 90f),
                        new BpmTimelineNode(90f, 105f),
                        new BpmTimelineNode(120f, 120f)
                    }
                },
                new TrackDef
                {
                    id = "track_09_red_clay_rhythms",
                    title = "Red Clay Rhythms",
                    genreConcept = "아프로비트 / 서정적인 일렉 기타 리프와 다채로운 퍼커션이 어우러져 화려하게 터지는 축제 스타일",
                    assetPath = "Assets/Audio/아프로비트/Red_Clay_Rhythms(90-150).mp3",
                    starDifficulty = 2,
                    startBpm = 90f,
                    maxBpm = 150f,
                    timeline = new List<BpmTimelineNode>
                    {
                        new BpmTimelineNode(0f, 90f),
                        new BpmTimelineNode(30f, 105f),
                        new BpmTimelineNode(60f, 120f),
                        new BpmTimelineNode(90f, 135f),
                        new BpmTimelineNode(120f, 150f)
                    }
                },
                new TrackDef
                {
                    id = "track_10_sunrise_on_dashboard",
                    title = "Sunrise On The Dashboard",
                    genreConcept = "아프로비트 / 아마피아노 로그드럼과 신스 사운드가 결합하여 초고속으로 몰아치는 시네마틱 아프로-퓨전",
                    assetPath = "Assets/Audio/아프로비트/Sunrise_On_The_Dashboard(120-200).mp3",
                    starDifficulty = 3,
                    startBpm = 120f,
                    maxBpm = 200f,
                    timeline = new List<BpmTimelineNode>
                    {
                        new BpmTimelineNode(0f, 120f),
                        new BpmTimelineNode(30f, 140f),
                        new BpmTimelineNode(60f, 160f),
                        new BpmTimelineNode(90f, 180f),
                        new BpmTimelineNode(120f, 200f)
                    }
                }
            };

            var createdTracks = new List<MusicDataSO>();

            foreach (var def in defs)
            {
                string assetFilePath = $"{folderPath}/MusicData_{def.id}.asset";
                MusicDataSO so = AssetDatabase.LoadAssetAtPath<MusicDataSO>(assetFilePath);
                if (so == null)
                {
                    so = ScriptableObject.CreateInstance<MusicDataSO>();
                    AssetDatabase.CreateAsset(so, assetFilePath);
                }

                so.id = def.id;
                so.title = def.title;
                so.genreConcept = def.genreConcept;
                so.audioClip = AssetDatabase.LoadAssetAtPath<AudioClip>(def.assetPath);
                so.starDifficulty = def.starDifficulty;
                so.startBpm = def.startBpm;
                so.maxBpm = def.maxBpm;
                so.bpmTimeline = def.timeline;

                EditorUtility.SetDirty(so);
                createdTracks.Add(so);
            }

            string dbPath = "Assets/Resources/Data/MusicDatabase.asset";
            MusicDatabaseSO db = AssetDatabase.LoadAssetAtPath<MusicDatabaseSO>(dbPath);
            if (db == null)
            {
                db = ScriptableObject.CreateInstance<MusicDatabaseSO>();
                AssetDatabase.CreateAsset(db, dbPath);
            }

            db.SetTracksEditor(createdTracks);
            EditorUtility.SetDirty(db);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"🎵 [MusicDatabaseBuilder] 총 {createdTracks.Count}곡의 MusicDataSO 및 MusicDatabase.asset 구축 완료!");
        }
    }
}
#endif
