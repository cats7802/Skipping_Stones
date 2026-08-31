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

        public void OnPositioningUpdate(GameController controller) { }

        public void OnLaunchStone(GameController controller, Vector3 direction, float powerMultiplier)
        {
            GameObject prefabToSpawn = controller.defaultStonePrefab ?? Resources.Load<GameObject>("Stone");
            Vector3 spawnPos = (controller.character != null) ? controller.character.GetHandPosition() : controller.transform.position + new Vector3(0.35f, 1.2f, 0.8f);
            Quaternion spawnRot = Quaternion.LookRotation(direction, Vector3.up);

            GameObject newStoneObj = (prefabToSpawn != null) ? Object.Instantiate(prefabToSpawn, spawnPos, spawnRot) : new GameObject("ArcadeStone");
            newStoneObj.name = "ArcadeStone";

            arcadeStone = newStoneObj.GetComponent<ArcadeSkippingStone>() ?? newStoneObj.AddComponent<ArcadeSkippingStone>();

            arcadeStone.OnSkipBounced += (count, grade) =>
            {
                controller.HandleSkipBounced(count, grade);
            };

            arcadeStone.OnStoneSunk += (dist) =>
            {
                controller.HandleStoneSunk(dist);
            };

            if (controller.dualCamera != null)
            {
                controller.dualCamera.targetStone = arcadeStone.transform;
                controller.dualCamera.SetCameraMode(DualCameraSetup.CameraMode.DynamicFlight);
            }

            arcadeStone.Launch(direction, powerMultiplier);

            // 🎵 투구 발사 순간부터 60 BPM BGM 첫 비트 시작!
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlayBGM(null, 60f);
                AudioManager.Instance.SetBGMPitchByBPM(60f, 60f);
            }
        }

        public void OnFlyingUpdate(GameController controller)
        {
            if (arcadeStone != null)
            {
                controller.lastTimingText = (arcadeStone.currentCombo > 1) ? $"{arcadeStone.currentCombo} COMBO! ({arcadeStone.currentBPM:F0} BPM)" : "";
            }
        }

        public void OnEvaluateTiming(GameController controller, float steerAngleDegrees)
        {
            if (arcadeStone != null && !arcadeStone.isSunk)
            {
                arcadeStone.TryRhythmTap(steerAngleDegrees, out string grade);
                controller.lastTimingText = grade;
            }
        }

        public void OnExitMode(GameController controller)
        {
            if (arcadeStone != null)
            {
                Object.Destroy(arcadeStone.gameObject);
                arcadeStone = null;
            }
        }
    }
}
