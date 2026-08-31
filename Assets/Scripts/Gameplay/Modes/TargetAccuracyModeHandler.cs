using UnityEngine;

namespace SkippingStones.Gameplay.Modes
{
    /// <summary>
    /// 🎯 2. 타깃 정밀 모드 핸들러 (기존 타깃 모드 100% 보존)
    /// </summary>
    public class TargetAccuracyModeHandler : IGameModeHandler
    {
        public GameController.GameMode Mode => GameController.GameMode.TargetAccuracy;

        public void OnEnterMode(GameController controller)
        {
            if (controller.currentLaunchPlatform != null)
            {
                controller.currentLaunchPlatform.gameObject.SetActive(false);
            }
            if (controller.playerPositionRoot != null)
            {
                controller.playerPositionRoot.gameObject.SetActive(true);
            }
            if (MapPIPManager.Instance != null)
            {
                MapPIPManager.Instance.UpdatePIPState(true);
            }
        }

        public void OnPositioningUpdate(GameController controller)
        {
            // 타깃 웨이포인트 스와이프/선택 처리
        }

        public void OnLaunchStone(GameController controller, Vector3 direction, float powerMultiplier)
        {
            // 타깃 모드 전용 발사
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
            }
        }

        public void OnFlyingUpdate(GameController controller)
        {
        }

        public void OnEvaluateTiming(GameController controller, float steerAngleDegrees)
        {
            if (controller.stone != null && !controller.stone.isCrashed)
            {
                bool bounced = controller.stone.TryRhythmBounce(steerAngleDegrees, out string timingGrade);
                controller.lastTimingText = timingGrade;
            }
        }

        public void OnExitMode(GameController controller)
        {
            if (MapPIPManager.Instance != null)
            {
                MapPIPManager.Instance.UpdatePIPState(false);
            }
        }
    }
}
