using UnityEngine;

namespace SkippingStones.Gameplay.Modes
{
    /// <summary>
    /// 🎮 게임 모드별 독립 처리 인터페이스 (Strategy Pattern)
    /// </summary>
    public interface IGameModeHandler
    {
        GameController.GameMode Mode { get; }
        void OnEnterMode(GameController controller);
        void OnPositioningUpdate(GameController controller);
        void OnLaunchStone(GameController controller, Vector3 direction, float powerMultiplier);
        void OnFlyingUpdate(GameController controller);
        void OnEvaluateTiming(GameController controller, float steerAngleDegrees);
        void OnExitMode(GameController controller);
    }
}
