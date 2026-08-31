using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SkippingStones.RhythmArcade
{
    /// <summary>
    /// 🎮 [RhythmArcadeGameController] BPM 콤보 가속 & 판정 거리 차등 매트릭스 총괄 컨트롤러
    /// - 0~4 콤보: BPM 60 (1.00s)
    /// - 5~9 콤보: BPM 72 (0.83s)
    /// - 10~14 콤보: BPM 85 (0.70s)
    /// - 15~19 콤보: BPM 100 (0.60s)
    /// - 20+ 콤보: BPM 120 (0.50s / FEVER!)
    /// - PERFECT: +30m, GREAT: +20m, EARLY/LATE: +12m, MISS: +5m
    /// </summary>
    public class RhythmArcadeGameController : MonoBehaviour
    {
        public static RhythmArcadeGameController Instance { get; private set; }

        [Header("컴포넌트 참조")]
        public RhythmArcadeStone arcadeStone;
        public RhythmArcadeRingIndicator ringIndicator;

        [Header("현재 비트 및 콤보 상태")]
        public float baseBPM = 60f;
        public float currentBPM = 60f;
        public int currentCombo = 0;
        public int maxCombo = 0;
        public float currentMomentum = 100f; // 0~100 스태미나
        public string lastGradeText = "";

        [Header("BPM 콤보 단계 설정")]
        public float[] comboBPMThresholds = new float[] { 60f, 72f, 85f, 100f, 120f };
        public int[] comboSteps = new int[] { 0, 5, 10, 15, 20 };

        [Header("판정 거리 설정 (단위: m)")]
        public float distPerfect = 30.0f;
        public float distGreat = 20.0f;
        public float distEarlyLate = 12.0f;
        public float distMiss = 5.0f;

        [Header("타이밍 판정 윈도우 (비트 주기 대비 오차 비율)")]
        public float windowPerfectRatio = 0.10f; // ±10% (BPM 60 기준 ±0.10s)
        public float windowGreatRatio = 0.22f;   // ±22% (BPM 60 기준 ±0.22s)

        private float currentStepProgress = 0f;
        private bool hasTappedThisStep = false;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else if (Instance != this) Destroy(gameObject);
        }

        private void Start()
        {
            if (ringIndicator == null)
            {
                ringIndicator = gameObject.AddComponent<RhythmArcadeRingIndicator>();
                ringIndicator.Initialize();
            }
        }

        public void StartArcadeGame(Vector3 startPos, Vector3 forwardDir)
        {
            if (arcadeStone == null)
            {
                GameObject stoneObj = GameObject.Find("RhythmArcade_Stone");
                if (stoneObj == null)
                {
                    stoneObj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    stoneObj.name = "RhythmArcade_Stone";
                    stoneObj.transform.localScale = new Vector3(0.35f, 0.12f, 0.45f);
                }
                arcadeStone = stoneObj.GetComponent<RhythmArcadeStone>() ?? stoneObj.AddComponent<RhythmArcadeStone>();
            }

            currentCombo = 0;
            currentMomentum = 100f;
            currentBPM = baseBPM;
            hasTappedThisStep = false;

            arcadeStone.OnBounceTriggered -= OnStoneBounceTriggered;
            arcadeStone.OnBounceTriggered += OnStoneBounceTriggered;
            arcadeStone.OnFlightProgress -= OnStoneFlightProgress;
            arcadeStone.OnFlightProgress += OnStoneFlightProgress;

            arcadeStone.Initialize(startPos, forwardDir, currentBPM, distGreat);
            arcadeStone.StartFlight();
        }

        private void OnStoneBounceTriggered(Vector3 landingPos, float duration)
        {
            hasTappedThisStep = false;
            if (ringIndicator != null)
            {
                ringIndicator.ShowRing(landingPos, duration);
            }
        }

        private void OnStoneFlightProgress(float progress)
        {
            currentStepProgress = progress;
        }

        private void Update()
        {
            if (arcadeStone == null || !arcadeStone.isFlying || arcadeStone.isFinished) return;

            // 1. 키보드 입력 체크 (A/D/S, 방향키)
            HandleKeyboardInput();
        }

        private void HandleKeyboardInput()
        {
            if (IsKeyTriggered(KeyCode.A) || IsKeyTriggered(KeyCode.LeftArrow))
            {
                ProcessRhythmInput(-6.0f);
            }
            else if (IsKeyTriggered(KeyCode.D) || IsKeyTriggered(KeyCode.RightArrow))
            {
                ProcessRhythmInput(6.0f);
            }
            else if (IsKeyTriggered(KeyCode.S) || IsKeyTriggered(KeyCode.DownArrow) || IsKeyTriggered(KeyCode.Space))
            {
                ProcessRhythmInput(0.0f);
            }
        }

        public void ProcessRhythmInput(float steerAngle)
        {
            if (arcadeStone == null || !arcadeStone.isFlying || arcadeStone.isFinished) return;
            if (hasTappedThisStep) return; // 1바운스 당 1회 입력 제한

            hasTappedThisStep = true;

            // 판정 계산: 착수 시점(progress = 1.0)과의 오차 측정
            float timingError = Mathf.Abs(1.0f - currentStepProgress);
            float nextDist = distGreat;
            string grade = "GREAT";

            if (timingError <= windowPerfectRatio)
            {
                grade = "🌟 PERFECT!!";
                nextDist = distPerfect;
                currentCombo++;
                currentMomentum = Mathf.Min(100f, currentMomentum + 8f);
            }
            else if (timingError <= windowGreatRatio)
            {
                grade = "👍 GREAT!";
                nextDist = distGreat;
                currentCombo++;
                currentMomentum = Mathf.Min(100f, currentMomentum + 2f);
            }
            else
            {
                grade = (currentStepProgress < 0.7f) ? "💦 TOO EARLY" : "⌛ LATE";
                nextDist = distEarlyLate;
                currentCombo = 0; // 콤보 리셋
                currentMomentum -= 15f;
            }

            maxCombo = Mathf.Max(maxCombo, currentCombo);
            lastGradeText = $"{grade} (콤보: {currentCombo} | 거리: +{nextDist:F0}m)";

            // 콤보에 따른 BPM 단계적 가속 계산
            currentBPM = CalculateBPMByCombo(currentCombo);

            // 모멘텀 고갈 시 완주 처리
            if (currentMomentum <= 0f)
            {
                lastGradeText = "🏁 모멘텀 소진! 피니시 완주!";
                arcadeStone.FinishFlight();
                return;
            }

            // 다음 스텝 파라미터 갱신 및 실행
            arcadeStone.SetNextBounceStep(nextDist, steerAngle, currentBPM);
        }

        private float CalculateBPMByCombo(int combo)
        {
            for (int i = comboSteps.Length - 1; i >= 0; i--)
            {
                if (combo >= comboSteps[i])
                {
                    return comboBPMThresholds[i];
                }
            }
            return baseBPM;
        }

        private bool IsKeyTriggered(KeyCode key)
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null)
            {
                if (key == KeyCode.Space && Keyboard.current.spaceKey.wasPressedThisFrame) return true;
                if ((key == KeyCode.A || key == KeyCode.LeftArrow) && (Keyboard.current.aKey.wasPressedThisFrame || Keyboard.current.leftArrowKey.wasPressedThisFrame)) return true;
                if ((key == KeyCode.D || key == KeyCode.RightArrow) && (Keyboard.current.dKey.wasPressedThisFrame || Keyboard.current.rightArrowKey.wasPressedThisFrame)) return true;
                if ((key == KeyCode.S || key == KeyCode.DownArrow) && (Keyboard.current.sKey.wasPressedThisFrame || Keyboard.current.downArrowKey.wasPressedThisFrame)) return true;
            }
#endif
            try
            {
                if (Input.GetKeyDown(key)) return true;
            }
            catch { }
            return false;
        }

        private void OnGUI()
        {
            if (arcadeStone == null || !arcadeStone.isFlying) return;

            // 디버그 HUD
            GUIStyle style = new GUIStyle();
            style.fontSize = 20;
            style.normal.textColor = Color.white;

            GUILayout.BeginArea(new Rect(20, 20, 400, 250), GUI.skin.box);
            GUILayout.Label($"<b>🎵 [BPM 리듬 아케이드 모드]</b>", style);
            GUILayout.Label($"BPM: {currentBPM:F0} (1비트 = {60f/currentBPM:F2}초)", style);
            GUILayout.Label($"현재 콤보: <color=yellow>{currentCombo} COMBO</color> (최고: {maxCombo})", style);
            GUILayout.Label($"모멘텀(체력): {currentMomentum:F0}%", style);
            GUILayout.Label($"총 도달 거리: {arcadeStone.totalDistance:F1} m (스킵: {arcadeStone.skipCount}회)", style);
            GUILayout.Label($"최근 판정: <color=cyan>{lastGradeText}</color>", style);
            GUILayout.EndArea();
        }
    }
}
