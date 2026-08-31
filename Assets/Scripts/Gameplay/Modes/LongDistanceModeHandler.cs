using UnityEngine;

namespace SkippingStones.Gameplay.Modes
{
    /// <summary>
    /// 🌊 1. 장거리 물리 모드 핸들러 (기존 물리 시뮬레이션 100% 보존)
    /// </summary>
    public class LongDistanceModeHandler : IGameModeHandler
    {
        public GameController.GameMode Mode => GameController.GameMode.LongDistance;

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

        public void OnPositioningUpdate(GameController controller)
        {
            // 기존 GameController의 기본 위치 선정(좌우 이동) 수행
        }

        public void OnLaunchStone(GameController controller, Vector3 direction, float powerMultiplier)
        {
            // 기존의 SkippingStone 물리 발사 수행
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

                if (AudioManager.Instance != null)
                {
                    AudioManager.Instance.PlayBGM();
                }
            }
        }

        public void OnFlyingUpdate(GameController controller)
        {
            // 기존 물리 비행 업데이트는 SkippingStone 자체 FixedUpdate/Update에서 처리
        }

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
