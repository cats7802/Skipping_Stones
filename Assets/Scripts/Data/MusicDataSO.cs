using System;
using System.Collections.Generic;
using UnityEngine;

namespace SkippingStones.Data
{
    [Serializable]
    public struct BpmTimelineNode
    {
        public float timeSeconds;
        public float targetBpm;

        public BpmTimelineNode(float time, float bpm)
        {
            timeSeconds = time;
            targetBpm = bpm;
        }
    }

    /// <summary>
    /// 개별 BGM 음원 트랙의 메타데이터, 난이도 및 실시간 BPM 가속 타임라인 ScriptableObject
    /// </summary>
    [CreateAssetMenu(fileName = "MusicData_", menuName = "Skipping Stones/Music Data", order = 20)]
    public class MusicDataSO : ScriptableObject
    {
        [Header("🎵 기본 곡 정보")]
        [Tooltip("고유 식별 ID")]
        public string id = "track_01_crossing_horizon";

        [Tooltip("곡 제목")]
        public string title = "Crossing the Horizon Line";

        [TextArea(2, 4)]
        [Tooltip("장르 및 서사 설명")]
        public string genreConcept = "로파이 힙합 / 아늑한 바이닐 노이즈와 따뜻한 피아노로 시작해 점점 밝고 선명해지는 여행의 시작";

        [Tooltip("재생할 오디오 클립")]
        public AudioClip audioClip;

        [Header("⭐ 난이도 및 템포")]
        [Range(1, 4)]
        [Tooltip("시작 BPM 기준 난이도 (1성: 60~120, 2성: 90~150, 3성: 120~200, 4성: 180~250)")]
        public int starDifficulty = 1;

        [Tooltip("시작 기본 BPM")]
        public float startBpm = 60f;

        [Tooltip("최대 도달 BPM")]
        public float maxBpm = 120f;

        [Header("⏱️ BPM 변화 타임라인")]
        [Tooltip("비행 시간(초)에 따른 목표 BPM 노드 리스트")]
        public List<BpmTimelineNode> bpmTimeline = new List<BpmTimelineNode>();

        public float EvaluateBpmAtTime(float elapsedFlightTime)
        {
            if (bpmTimeline == null || bpmTimeline.Count == 0) return startBpm;
            if (elapsedFlightTime <= bpmTimeline[0].timeSeconds) return bpmTimeline[0].targetBpm;

            float currentBpm = bpmTimeline[0].targetBpm;
            for (int i = 0; i < bpmTimeline.Count; i++)
            {
                if (elapsedFlightTime >= bpmTimeline[i].timeSeconds)
                {
                    currentBpm = bpmTimeline[i].targetBpm;
                }
                else
                {
                    break;
                }
            }
            return currentBpm;
        }

        public string GetStarString()
        {
            switch (starDifficulty)
            {
                case 1: return "⭐";
                case 2: return "⭐⭐";
                case 3: return "⭐⭐⭐";
                case 4: return "⭐⭐⭐⭐";
                default: return "⭐";
            }
        }
    }
}
