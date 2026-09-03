using System.Collections.Generic;
using UnityEngine;

namespace SkippingStones.Arcade.Buffs
{
    /// <summary>
    /// 🌀 랜덤 링 버프 목록 관리 및 가중치 룰렛 추첨기
    /// </summary>
    public static class RandomRingBuffManager
    {
        private static readonly List<IRandomRingBuff> registeredBuffs = new List<IRandomRingBuff>
        {
            new HighJumpBuff(),
            new SpeedBoostBuff(),
            new SharpSteerBuff(),
            new MomentumMaxBuff(),
            new TempoSlowBuff(),
            new SlightWindBuff()
        };

        private static int totalWeight = -1;

        private static void EnsureWeightCalculated()
        {
            if (totalWeight > 0) return;
            totalWeight = 0;
            for (int i = 0; i < registeredBuffs.Count; i++)
            {
                totalWeight += registeredBuffs[i].Weight;
            }
        }

        /// <summary>
        /// 🎲 가중치 기반 랜덤 버프 1개 추첨
        /// </summary>
        public static IRandomRingBuff RollRandomBuff()
        {
            EnsureWeightCalculated();
            int roll = Random.Range(0, totalWeight);
            int accumulated = 0;

            for (int i = 0; i < registeredBuffs.Count; i++)
            {
                accumulated += registeredBuffs[i].Weight;
                if (roll < accumulated)
                {
                    return registeredBuffs[i];
                }
            }

            return registeredBuffs[0];
        }
    }
}
