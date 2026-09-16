using System;
using System.Collections.Generic;
using UnityEngine;

namespace SkippingStones.Data
{
    /// <summary>
    /// 마스터 음원 트랙 데이터베이스 ScriptableObject
    /// - 10종 음원 트랙 및 BPM 타임라인 메타데이터를 중앙 집중 관리
    /// </summary>
    [CreateAssetMenu(fileName = "MusicDatabase", menuName = "Skipping Stones/Music Database", order = 3)]
    public class MusicDatabaseSO : ScriptableObject
    {
        [Header("🎵 마스터 음원 트랙 목록")]
        [SerializeField] private List<MusicDataSO> tracks = new List<MusicDataSO>();

        public IReadOnlyList<MusicDataSO> Tracks => tracks;

        public MusicDataSO GetTrackById(string id)
        {
            if (string.IsNullOrEmpty(id) || tracks == null) return null;
            return tracks.Find(t => t != null && t.id.Equals(id, StringComparison.OrdinalIgnoreCase));
        }

        public MusicDataSO GetTrackByIndex(int index)
        {
            if (tracks == null || index < 0 || index >= tracks.Count) return null;
            return tracks[index];
        }

        public int Count => tracks != null ? tracks.Count : 0;

        public int IndexOf(MusicDataSO track)
        {
            if (tracks == null || track == null) return -1;
            return tracks.IndexOf(track);
        }

        public int IndexOfId(string id)
        {
            if (string.IsNullOrEmpty(id) || tracks == null) return -1;
            return tracks.FindIndex(t => t != null && t.id.Equals(id, StringComparison.OrdinalIgnoreCase));
        }

#if UNITY_EDITOR
        public void SetTracksEditor(List<MusicDataSO> newList)
        {
            tracks = newList ?? new List<MusicDataSO>();
        }
#endif
    }
}
