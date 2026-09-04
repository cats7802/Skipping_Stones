using UnityEngine;
using SkippingStones.Data;

namespace SkippingStones.Gameplay.Calculators
{
    /// <summary>
    /// 📊 매치 종료 시 거리, 스킵, 스킴 및 특수 보너스/코인 점수를 산출하는 순수 계산기
    /// </summary>
    public static class MatchScoreCalculator
    {
        public struct ScoreCalculationParams
        {
            public float finalDistance;
            public int skipCount;
            public float skimDistance;
            public int perfectTimingCount;
            public int fishSnipeCount;
            public int friendOvertakeCount;
            public int boostPadCount;
        }

        public struct ScoreCalculationResult
        {
            public int distanceScore;
            public int skipScore;
            public int specialScore;
            public int totalScore;
            public int earnedCoins;
            public InGameResultData resultData;
        }

        public static ScoreCalculationResult Calculate(ScoreCalculationParams p)
        {
            int distScore = Mathf.RoundToInt(p.finalDistance * 10f);
            int skipScore = p.skipCount * 500;
            int skimScore = Mathf.RoundToInt(p.skimDistance * 15f);

            int specScore = (p.perfectTimingCount * 300) 
                          + (p.fishSnipeCount * 1000) 
                          + (p.friendOvertakeCount * 800) 
                          + (p.boostPadCount * 500) 
                          + skimScore;

            int totScore = distScore + skipScore + specScore;
            int coins = Mathf.Max(5, Mathf.RoundToInt(totScore / 25f));

            InGameResultData dto = new InGameResultData
            {
                finalDistance = p.finalDistance,
                skipCount = p.skipCount,
                perfectTimingCount = p.perfectTimingCount,
                fishSnipeCount = p.fishSnipeCount,
                friendOvertakeCount = p.friendOvertakeCount,
                boostPadCount = p.boostPadCount,
                earnedCoins = coins,
                totalScore = totScore
            };

            return new ScoreCalculationResult
            {
                distanceScore = distScore,
                skipScore = skipScore,
                specialScore = specScore,
                totalScore = totScore,
                earnedCoins = coins,
                resultData = dto
            };
        }
    }
}
