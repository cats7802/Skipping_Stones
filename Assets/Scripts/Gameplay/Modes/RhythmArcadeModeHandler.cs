using UnityEngine;
using SkippingStones.Arcade;

namespace SkippingStones.Gameplay.Modes
{
    /// <summary>
    /// 🎵 3. 신규 리듬 아케이드 모드 핸들러 (BPM 고정 주기 포물선 & 7단계 판정 전담)
    /// </summary>
    public class RhythmArcadeModeHandler : IGameModeHandler
    {
        public GameController.GameMode Mode => GameController.GameMode.RhythmArcade;
        private ArcadeSkippingStone arcadeStone;

        public void OnEnterMode(GameController controller)
        {
            if (controller.currentLaunchPlatform != null)
            {
                controller.currentLaunchPlatform.gameObject.SetActive(true);
            }
            if (controller.playerPositionRoot != null)
            {
                controller.playerPositionRoot.gameObject.SetActive(false);
            }
            if (MapPIPManager.Instance != null)
            {
                MapPIPManager.Instance.UpdatePIPState(false);
            }
        }

        public void OnLaunchStone(GameController controller, Vector3 direction, float powerMultiplier)
        {
            GameObject prefabToSpawn = controller.defaultStonePrefab ?? Resources.Load<GameObject>("Stone");
            Vector3 spawnPos = (controller.character != null) ? controller.character.GetHandPosition() : controller.transform.position + new Vector3(0.35f, 1.2f, 0.8f);
            Quaternion spawnRot = Quaternion.LookRotation(direction, Vector3.up);

            GameObject newStoneObj = (prefabToSpawn != null) ? Object.Instantiate(prefabToSpawn, spawnPos, spawnRot) : new GameObject("Stone");
            newStoneObj.name = "Stone";

            SkippingStone stone = newStoneObj.GetComponent<SkippingStone>();
            if (stone != null)
            {
                controller.stone = stone;
                stone.OnSkipBounced += controller.HandleSkipBounced;
                stone.OnStoneSunk += controller.HandleStoneSunk;
                stone.isGodMode = controller.devGodMode;
                stone.godModeTargetDistance = controller.devGodModeTargetDistance;
                stone.Launch(direction, powerMultiplier);

                if (controller.dualCamera != null)
                {
                    controller.dualCamera.targetStone = stone.transform;
                    controller.dualCamera.SetCameraMode(DualCameraSetup.CameraMode.DynamicFlight);
                }

                // 🎵 투구 발사 순간부터 BGM 재생
                if (AudioManager.Instance != null)
                {
                    AudioManager.Instance.PlayBGM(null, 100f);
                }
            }
        }

        public void OnPositioningUpdate(GameController controller) { }

        public void OnFlyingUpdate(GameController controller) { }

        public void OnEvaluateTiming(GameController controller, float steerAngleDegrees)
        {
            if (controller.stone != null && !controller.stone.isCrashed)
            {
                bool bounced = controller.stone.TryRhythmBounce(steerAngleDegrees, out string timingGrade);
                controller.lastTimingText = timingGrade;
            }
        }

        public void OnExitMode(GameController controller) { }
    }
}
