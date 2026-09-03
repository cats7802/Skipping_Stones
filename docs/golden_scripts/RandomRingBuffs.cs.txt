using UnityEngine;

namespace SkippingStones.Arcade.Buffs
{
    /// <summary>
    /// 🦘 하이 점프 & 장애물 무적 관통 버프
    /// </summary>
    public class HighJumpBuff : IRandomRingBuff
    {
        public string BuffName => "🦘 HIGH JUMP! (장애물 무시)";
        public int Weight => 25;
        public int DurationBounces => 2;

        public void OnApply(ArcadeSkippingStone stone)
        {
            stone.isInvincibleToObstacles = true;
        }

        public void OnBounceTick(ArcadeSkippingStone stone, int remainingBounces) { }

        public void OnRemove(ArcadeSkippingStone stone)
        {
            stone.isInvincibleToObstacles = false;
        }
    }

    /// <summary>
    /// 🚀 스피드 & 비거리 부스트 (+25%)
    /// </summary>
    public class SpeedBoostBuff : IRandomRingBuff
    {
        public string BuffName => "🚀 SPEED BOOST! (+25%)";
        public int Weight => 25;
        public int DurationBounces => 4;

        public void OnApply(ArcadeSkippingStone stone)
        {
            stone.speedMultiplierBonus = 1.25f;
        }

        public void OnBounceTick(ArcadeSkippingStone stone, int remainingBounces) { }

        public void OnRemove(ArcadeSkippingStone stone)
        {
            stone.speedMultiplierBonus = 1.0f;
        }
    }

    /// <summary>
    /// 🎯 샤프 스티어 (조향 꺾임 각도 +5° 추가 가산)
    /// </summary>
    public class SharpSteerBuff : IRandomRingBuff
    {
        public string BuffName => "🎯 SHARP STEER! (조향 각도 +5°)";
        public int Weight => 25;
        public int DurationBounces => 3;

        public void OnApply(ArcadeSkippingStone stone)
        {
            stone.steerAngleBonus = 5.0f;
        }

        public void OnBounceTick(ArcadeSkippingStone stone, int remainingBounces) { }

        public void OnRemove(ArcadeSkippingStone stone)
        {
            stone.steerAngleBonus = 0f;
        }
    }

    /// <summary>
    /// 🔥 모멘텀 100% 풀 충전 (즉발)
    /// </summary>
    public class MomentumMaxBuff : IRandomRingBuff
    {
        public string BuffName => "🔥 MOMENTUM MAX!";
        public int Weight => 15;
        public int DurationBounces => 0;

        public void OnApply(ArcadeSkippingStone stone)
        {
            stone.currentMomentum = stone.maxMomentum;
        }

        public void OnBounceTick(ArcadeSkippingStone stone, int remainingBounces) { }
        public void OnRemove(ArcadeSkippingStone stone) { }
    }

    /// <summary>
    /// 🎵 템포 슬로우 완화 (BPM 10 감소, 판정 여유)
    /// </summary>
    public class TempoSlowBuff : IRandomRingBuff
    {
        public string BuffName => "🎵 TEMPO SLOW! (타이밍 여유)";
        public int Weight => 6;
        public int DurationBounces => 0;

        public void OnApply(ArcadeSkippingStone stone)
        {
            stone.currentBPM = Mathf.Max(60f, stone.currentBPM - 10f);
            stone.currentCycleDuration = 60f / stone.currentBPM;
        }

        public void OnBounceTick(ArcadeSkippingStone stone, int remainingBounces) { }
        public void OnRemove(ArcadeSkippingStone stone) { }
    }

    /// <summary>
    /// 💨 미미한 감속 (-8% 살짝 바람)
    /// </summary>
    public class SlightWindBuff : IRandomRingBuff
    {
        public string BuffName => "💨 SLIGHT WIND (-8%)";
        public int Weight => 4;
        public int DurationBounces => 2;

        public void OnApply(ArcadeSkippingStone stone)
        {
            stone.speedMultiplierBonus = 0.92f;
        }

        public void OnBounceTick(ArcadeSkippingStone stone, int remainingBounces) { }

        public void OnRemove(ArcadeSkippingStone stone)
        {
            stone.speedMultiplierBonus = 1.0f;
        }
    }
}
