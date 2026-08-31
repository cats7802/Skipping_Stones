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

        private SkippingStone trackedStone;
        private Vector3 cycleStartPos;
        private Vector3 cycleEndPos;
        private Vector3 currentForwardDir;
        private float currentStepDist = 5.0f; // 기본 1회 바운스 거리 5m
        private float currentArcHeight = 2.2f;
        private float currentCycleDuration = 1.00f; // 기본 60 BPM = 1.00초
        private float currentBPM = 60f;
        private int currentCombo = 0;
        private float cycleElapsedTime = 0f;
        private bool isArcadeFlying = false;

        public void OnLaunchStone(GameController controller, Vector3 direction, float powerMultiplier)
        {
            GameObject prefabToSpawn = controller.defaultStonePrefab ?? Resources.Load<GameObject>("Stone");
            Vector3 spawnPos = (controller.character != null) ? controller.character.GetHandPosition() : controller.transform.position + new Vector3(0.35f, 1.2f, 0.8f);
            Quaternion spawnRot = Quaternion.LookRotation(direction, Vector3.up);

            GameObject newStoneObj = (prefabToSpawn != null) ? Object.Instantiate(prefabToSpawn, spawnPos, spawnRot) : new GameObject("Stone");
            newStoneObj.name = "Stone";

            trackedStone = newStoneObj.GetComponent<SkippingStone>();
            if (trackedStone != null)
            {
                controller.stone = trackedStone;
                trackedStone.OnSkipBounced += controller.HandleSkipBounced;
                trackedStone.OnStoneSunk += controller.HandleStoneSunk;
                trackedStone.isGodMode = controller.devGodMode;
                trackedStone.godModeTargetDistance = controller.devGodModeTargetDistance;

                // 물리 Rigidbody 간섭 비활성화 (BPM 수학적 포물선 제어)
                Rigidbody rb = trackedStone.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    rb.isKinematic = true;
                    rb.useGravity = false;
                }

                trackedStone.UpdateWaterLevel();
                trackedStone.isThrown = true;
                trackedStone.isSunk = false;
                trackedStone.isCrashed = false;
                trackedStone.isSkimming = false;
                trackedStone.skipCount = 0;
                trackedStone.totalDistance = 0f;

                currentForwardDir = new Vector3(direction.x, 0f, direction.z).normalized;
                if (currentForwardDir.sqrMagnitude < 0.01f) currentForwardDir = Vector3.forward;

                currentStepDist = 5.0f * Mathf.Clamp(powerMultiplier, 0.8f, 1.2f);
                currentArcHeight = 2.2f;
                currentCombo = 0;
                UpdateBPM();

                cycleStartPos = spawnPos;
                cycleEndPos = cycleStartPos + currentForwardDir * currentStepDist;
                cycleEndPos.y = trackedStone.waterLevel;

                cycleElapsedTime = 0f;
                isArcadeFlying = true;

                if (controller.dualCamera != null)
                {
                    controller.dualCamera.targetStone = trackedStone.transform;
                    controller.dualCamera.SetCameraMode(DualCameraSetup.CameraMode.DynamicFlight);
                }

                // 🎵 투구 발사 순간부터 원곡 100 BPM BGM 첫 비트 시작!
                if (AudioManager.Instance != null)
                {
                    AudioManager.Instance.PlayBGM(null, 100f);
                    AudioManager.Instance.SetBGMPitchByBPM(100f, 100f);
                }
            }
        }

        public void OnPositioningUpdate(GameController controller) { }

        public void OnFlyingUpdate(GameController controller)
        {
            if (!isArcadeFlying || trackedStone == null || trackedStone.isSunk || trackedStone.isCrashed) return;

            cycleElapsedTime += Time.deltaTime;
            float t = cycleElapsedTime / currentCycleDuration;

            // 1. BPM 고정 주기 수학적 포물선 위치 갱신
            Vector3 horizPos = Vector3.Lerp(cycleStartPos, cycleEndPos, Mathf.Clamp01(t));
            // y = waterLevel + 4 * H * t * (1 - t)
            float yPos = trackedStone.waterLevel + 4f * currentArcHeight * t * (1f - t);
            trackedStone.transform.position = new Vector3(horizPos.x, Mathf.Max(trackedStone.waterLevel, yPos), horizPos.z);

            // 2. 비행 회전 갱신
            Vector3 flightDir = (cycleEndPos - cycleStartPos).normalized;
            if (flightDir.sqrMagnitude > 0.01f)
            {
                trackedStone.transform.rotation = Quaternion.LookRotation(flightDir, Vector3.up) * Quaternion.Euler(-15f, 0f, 0f);
            }

            // 3. 착수 시점(t >= 1.0) 도달 시 자연 MISS 처리
            if (cycleElapsedTime >= currentCycleDuration)
            {
                OnEvaluateTiming(controller, 0f);
            }
        }

        public void OnEvaluateTiming(GameController controller, float steerAngleDegrees)
        {
            if (trackedStone == null || trackedStone.isSunk || trackedStone.isCrashed) return;

            // 판정 잔여 시간 계산
            float timeRemaining = currentCycleDuration - cycleElapsedTime;
            string grade;
            float distDelta = 0f;

            if (timeRemaining <= 0.120f && timeRemaining >= -0.080f)
            {
                grade = "🔥 PERFECT! 🔥 (+0.4m)";
                distDelta = +0.4f; // 🎯 퍼펙트 시 +0.4m 점진 가속
                currentCombo++;
            }
            else if (timeRemaining <= 0.250f && timeRemaining >= -0.150f)
            {
                grade = "⚡ GREAT! ⚡ (+0.2m)";
                distDelta = +0.2f; // 🌟 그레이트 시 +0.2m 점진 가속
                currentCombo++;
            }
            else if (timeRemaining <= 0.400f)
            {
                grade = "✨ GOOD (유지)";
                distDelta = 0.0f; // 👍 굿 시 비거리 유지
                currentCombo++;
            }
            else
            {
                grade = "⚠️ LATE (-0.5m)";
                distDelta = -0.5f; // ⚠️ 레이트 시 -0.5m 감속
                currentCombo = 0; // 콤보 리셋
            }

            // 조향 적용
            if (Mathf.Abs(steerAngleDegrees) > 0.1f)
            {
                currentForwardDir = Quaternion.Euler(0f, steerAngleDegrees, 0f) * currentForwardDir;
            }

            // 점진적 비거리 및 BPM 갱신
            currentStepDist = Mathf.Clamp(currentStepDist + distDelta, 3.5f, 15.0f);
            trackedStone.totalDistance += currentStepDist;
            trackedStone.skipCount++;
            UpdateBPM();

            controller.HandleSkipBounced(trackedStone.skipCount, grade);
            if (AudioManager.Instance != null)
            {
                if (grade.Contains("PERFECT")) AudioManager.Instance.Play(SoundType.BouncePerfect);
                else if (grade.Contains("GREAT") || grade.Contains("GOOD")) AudioManager.Instance.Play(SoundType.BounceGood);
                else AudioManager.Instance.Play(SoundType.BounceWater);

                AudioManager.Instance.SetBGMPitchByBPM(currentBPM, 100f);
            }

            // 다음 바운스 포물선 설정 (1회 착수 $\rightarrow$ 다음 점프 시작)
            cycleStartPos = trackedStone.transform.position;
            cycleStartPos.y = trackedStone.waterLevel;
            cycleEndPos = cycleStartPos + currentForwardDir * currentStepDist;
            cycleEndPos.y = trackedStone.waterLevel;
            cycleElapsedTime = 0f;
        }

        private void UpdateBPM()
        {
            // 원곡 100 BPM(0.60초) 기준 콤보별 점진적 가속
            if (currentCombo >= 20) currentBPM = 135f;      // FEVER (0.44s)
            else if (currentCombo >= 15) currentBPM = 125f; // 0.48s
            else if (currentCombo >= 10) currentBPM = 115f; // 0.52s
            else if (currentCombo >= 5) currentBPM = 108f;  // 0.55s
            else currentBPM = 100f;                         // 원곡 시작 (0.60s)

            currentCycleDuration = 60f / currentBPM;
        }

        public void OnExitMode(GameController controller)
        {
            isArcadeFlying = false;
        }
    }
}
