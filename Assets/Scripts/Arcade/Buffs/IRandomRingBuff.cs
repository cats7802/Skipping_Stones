namespace SkippingStones.Arcade.Buffs
{
    /// <summary>
    /// 🌀 랜덤 링 버프 전략 인터페이스 (Strategy Pattern)
    /// </summary>
    public interface IRandomRingBuff
    {
        string BuffName { get; }
        int Weight { get; }
        int DurationBounces { get; }
        
        void OnApply(ArcadeSkippingStone stone);
        void OnBounceTick(ArcadeSkippingStone stone, int remainingBounces);
        void OnRemove(ArcadeSkippingStone stone);
    }
}
