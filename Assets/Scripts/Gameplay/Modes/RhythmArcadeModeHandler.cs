using UnityEngine;
using SkippingStones.Arcade;

namespace SkippingStones.Gameplay.Modes
{
    /// <summary>
    /// 🎵 3. 신규 리듬 아케이드 모드 핸들러 (BPM 고정 주기 포물선 & 디렉터 확정 판정/프리셋 전담)
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
            // 🧹 1. 씬에 이전 돌(ArcadeStone 또는 Stone)이 남아있다면 중복 누적 방지를 위해 즉시 파괴
            if (controller.stone != null && controller.stone.gameObject != null)
            {
                Object.Destroy(controller.stone.gameObject);
                controller.stone = null;
            }
            if (arcadeStone != null && arcadeStone.gameObject != null)
            {
                Object.Destroy(arcadeStone.gameObject);
                arcadeStone = null;
            }

            GameObject[] existingStones = GameObject.FindGameObjectsWithTag("Untagged");
            foreach (var go in existingStones)
            {
                if (go != null && (go.name == "ArcadeStone" || go.name == "Stone"))
                {
                    Object.Destroy(go);
                }
            }

            GameObject prefabToSpawn = controller.defaultStonePrefab ?? Resources.Load<GameObject>("Stone");
            Vector3 spawnPos = (controller.character != null) ? controller.character.GetHandPosition() : controller.transform.position + new Vector3(0.35f, 1.2f, 0.8f);
            Quaternion spawnRot = Quaternion.LookRotation(direction, Vector3.up);

            GameObject newStoneObj = (prefabToSpawn != null) ? Object.Instantiate(prefabToSpawn, spawnPos, spawnRot) : new GameObject("ArcadeStone");
            newStoneObj.name = "ArcadeStone";

            // 롱디용 SkippingStone 컴포넌트 비활성화
            SkippingStone legacyStone = newStoneObj.GetComponent<SkippingStone>();
            if (legacyStone != null)
            {
                legacyStone.enabled = false;
            }

            arcadeStone = newStoneObj.GetComponent<ArcadeSkippingStone>();
            if (arcadeStone == null)
            {
                arcadeStone = newStoneObj.AddComponent<ArcadeSkippingStone>();
            }

            // 🎯 정품 붉은 과녁 링(RhythmRingIndicator) 바인딩
            RhythmRingIndicator ring = newStoneObj.GetComponentInChildren<RhythmRingIndicator>();
            if (ring != null)
            {
                ring.arcadeStone = arcadeStone;
                ring.stone = null;
            }

            // 🎛️ GameController 인스펙터에 설정된 밸런스 프리셋 및 커스텀 수치 주입
            arcadeStone.activePreset = controller.rhythmPreset;
            arcadeStone.fixedBounceArcHeight = controller.rhythmArcadeHeight;
            arcadeStone.customPreset = controller.customRhythmPreset;

            arcadeStone.OnSkipBounced += (skipCount, grade) =>
            {
                controller.HandleSkipBounced(skipCount, grade);
            };

            arcadeStone.OnStoneSunk += (totalDist) =>
            {
                controller.HandleStoneSunk(totalDist);
            };

            arcadeStone.Launch(direction, powerMultiplier);

            if (controller.dualCamera != null)
            {
                controller.dualCamera.targetStone = arcadeStone.transform;
                controller.dualCamera.SetCameraMode(DualCameraSetup.CameraMode.DynamicFlight);
            }

            // 🎵 투구 발사 순간부터 60 BPM 여유로운 템포 BGM 재생
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlayBGM(null, 60f);
            }
        }

        public void OnPositioningUpdate(GameController controller) { }

        public void OnFlyingUpdate(GameController controller) { }

        public void OnEvaluateTiming(GameController controller, float steerAngleDegrees)
        {
            if (arcadeStone != null && !arcadeStone.isCrashed && !arcadeStone.isSunk)
            {
                bool tapped = arcadeStone.TryRhythmTap(steerAngleDegrees, out string timingGrade);
                if (tapped)
                {
                    controller.lastTimingText = timingGrade;
                }
            }
        }

        public void OnExitMode(GameController controller)
        {
            arcadeStone = null;
        }
    }
}
