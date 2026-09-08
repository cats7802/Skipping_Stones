using System;
using System.Collections.Generic;
using UnityEngine;

namespace SkippingStones.Data
{
    /// <summary>
    /// 마스터 조약돌 도감 데이터베이스 ScriptableObject
    /// - 프로젝트 내 모든 해금 가능한 조약돌 목록을 중앙 집중 관리
    /// </summary>
    [CreateAssetMenu(fileName = "StoneDatabase", menuName = "Skipping Stones/Stone Database", order = 1)]
    public class StoneDatabaseSO : ScriptableObject
    {
        [Header("🪨 조약돌 마스터 도감 목록")]
        [SerializeField] private List<StoneDataSO> stones = new List<StoneDataSO>();

        public IReadOnlyList<StoneDataSO> Stones => stones;

        public StoneDataSO GetStoneById(string id)
        {
            if (string.IsNullOrEmpty(id) || stones == null) return null;
            return stones.Find(s => s != null && s.id.Equals(id, StringComparison.OrdinalIgnoreCase));
        }

        public StoneDataSO GetStoneByIndex(int index)
        {
            if (stones == null || index < 0 || index >= stones.Count) return null;
            return stones[index];
        }

        public int Count => stones != null ? stones.Count : 0;

        public int IndexOf(StoneDataSO stone)
        {
            if (stones == null || stone == null) return -1;
            return stones.IndexOf(stone);
        }

        public int IndexOfId(string id)
        {
            if (string.IsNullOrEmpty(id) || stones == null) return -1;
            return stones.FindIndex(s => s != null && s.id.Equals(id, StringComparison.OrdinalIgnoreCase));
        }

#if UNITY_EDITOR
        public void SetStonesEditor(List<StoneDataSO> newList)
        {
            stones = newList ?? new List<StoneDataSO>();
        }
#endif
    }
}
