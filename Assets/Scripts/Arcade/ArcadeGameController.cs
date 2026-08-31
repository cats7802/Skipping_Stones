using System.Collections;
using UnityEngine;
using SkippingStones.Data;
using SkippingStones.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace SkippingStones.Arcade
{
    /// <summary>
    /// 🎮 리듬 아케이드 모드 전용 게임 컨트롤러 (완전 독립 구현)
    /// - GameDataManager/MetaUIManager와의 선택 데이터(캐릭터, 돌, 맵) 로딩 완벽 계승
    /// - 0번 청크 스폰, 투구 45~55프레임 리드인 애니메이션, 카메라 연동
    /// - 투구 발사 순간 ArcadeSkippingStone 스폰 및 비행/판정/결과창 처리
    /// </summary>
    public class ArcadeGameController : MonoBehaviour
    {
        public static ArcadeGameController Instance { get; private set; }

        [Header("0. 세션 데이터 및 결과")]
        public MatchSessionData currentSessionData;
        public InGameResultData lastResultData;
        public event System.Action<InGameResultData> OnMatchResultGenerated;

        public enum ArcadeState
        {
            Positioning,
            AimingAngle,
            ChargingPower,
            ThrowingAnimation,
            Flying,
            Result
        }

        [Header("1. 게임 상태")]
        public ArcadeState currentState = ArcadeState.Positioning;

        [Header("2. 인게임 참조")]
        [SerializeField] private StoneThrowerCharacter _character;
        public StoneThrowerCharacter character => _character;

        [SerializeField] private ArcadeSkippingStone _arcadeStone;
        public ArcadeSkippingStone arcadeStone => _arcadeStone;

        [SerializeField] private DualCameraSetup _dualCamera;
        public DualCameraSetup dualCamera => _dualCamera;

        [SerializeField] private Transform _currentLaunchPlatform;
        public Transform currentLaunchPlatform => _currentLaunchPlatform;

        [Header("3. 기본 프리팹")]
        public GameObject defaultCharacterPrefab;
        public GameObject defaultStonePrefab;
        public GameObject defaultMapPrefab;

        [Header("4. 게이지 및 파라미터")]
        public float aimGaugeValue = 0f;
        public float powerGaugeValue = 0f;
        public string lastTimingText = "";
        public string bannerNotificationText = "";

        private float aimSpeed = 2.4f;
        private float powerSpeed = 3.0f;
        private float aimDirection = 1f;
        private float powerDirection = 1f;
        private float lastStateChangeTime = 0f;
        private const float STATE_COOLDOWN = 0.30f;

        [Header("5. 점수 및 결과")]
        public int distanceScore = 0;
        public int skipScore = 0;
        public int specialScore = 0;
        public int totalScore = 0;
        public int earnedCoins = 0;
        public int perfectTimingCount = 0;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else if (Instance != this) { Destroy(gameObject); return; }

            ResolveReferences();
        }

        private void Start()
        {
            // MetaUIManager 또는 GameDataManager 세션 데이터 수신
            MatchSessionData session = null;
            if (GameDataManager.Instance != null)
            {
                session = GameDataManager.Instance.CreateCurrentMatchSession();
            }
            StartArcadeSession(session);
        }

        private void ResolveReferences()
        {
            if (_dualCamera == null) _dualCamera = FindAnyObjectByType<DualCameraSetup>();
            _currentLaunchPlatform = GameController.FindPlatformInScene();
        }

        public void StartArcadeSession(MatchSessionData session)
        {
            currentSessionData = session ?? new MatchSessionData();
            lastResultData = null;
            perfectTimingCount = 0;

            // 1. 맵 환경 로드 및 청크 생성
            GameObject mapPrefab = currentSessionData.mapPrefabOverride ?? defaultMapPrefab;
            SetupMapEnvironment(mapPrefab);

            // 2. 캐릭터 로드 및 발판 배치
            GameObject charPrefab = currentSessionData.characterPrefabOverride;
            if (charPrefab == null && GameDataManager.Instance != null && !string.IsNullOrEmpty(currentSessionData.characterId))
            {
                var cInfo = GameDataManager.Instance.characterCatalog.Find(c => c.id == currentSessionData.characterId);
                if (cInfo != null && !string.IsNullOrEmpty(cInfo.prefabPath))
                {
#if UNITY_EDITOR
                    charPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(cInfo.prefabPath);
#else
                    string rPath = cInfo.prefabPath.Replace("Assets/prefab/", "").Replace(".prefab", "");
                    charPrefab = Resources.Load<GameObject>(rPath);
#endif
                }
            }
            if (charPrefab == null) charPrefab = defaultCharacterPrefab;
            SetupCharacter(charPrefab);

            // 3. 돌 프리팹 확인
            GameObject stonePrefab = currentSessionData.stonePrefabOverride;
            if (stonePrefab == null && GameDataManager.Instance != null && !string.IsNullOrEmpty(currentSessionData.stoneId))
            {
                var sInfo = GameDataManager.Instance.stoneCatalog.Find(s => s.id == currentSessionData.stoneId);
                if (sInfo != null && !string.IsNullOrEmpty(sInfo.prefabPath))
                {
#if UNITY_EDITOR
                    stonePrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(sInfo.prefabPath);
#else
                    string rPath = sInfo.prefabPath.Replace("Assets/prefab/", "").Replace(".prefab", "");
                    stonePrefab = Resources.Load<GameObject>(rPath);
#endif
                }
            }
            if (stonePrefab != null) defaultStonePrefab = stonePrefab;

            ResetToPositioning();
        }

        private void SetupMapEnvironment(GameObject mapPrefab)
        {
            LakeEnvironmentManager existingMgr = LakeEnvironmentManager.Instance != null ? LakeEnvironmentManager.Instance : FindAnyObjectByType<LakeEnvironmentManager>();

            if (mapPrefab == null && existingMgr == null)
            {
                GameObject envPrefab = Resources.Load<GameObject>("New_TestEnvMgr");
#if UNITY_EDITOR
                if (envPrefab == null) envPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/prefab/BG_Env/New_TestEnvMgr.prefab");
#endif
                if (envPrefab != null) mapPrefab = envPrefab;
            }

            if (existingMgr != null)
            {
                existingMgr.SetupBGChunks();
            }
            else if (mapPrefab != null)
            {
                GameObject newMgrObj = Instantiate(mapPrefab, Vector3.zero, Quaternion.identity);
                newMgrObj.name = mapPrefab.name;
                var lem = newMgrObj.GetComponent<LakeEnvironmentManager>();
                if (lem != null)
                {
                    LakeEnvironmentManager.Instance = lem;
                    lem.SetupBGChunks();
                }
            }

            ResolveReferences();
        }

        private void SetupCharacter(GameObject charPrefab)
        {
            if (charPrefab == null) return;

            StoneThrowerCharacter existingChar = FindAnyObjectByType<StoneThrowerCharacter>();
            if (existingChar != null)
            {
                _character = existingChar;
            }
            else
            {
                GameObject charObj = Instantiate(charPrefab);
                charObj.name = charPrefab.name;
                _character = charObj.GetComponentInChildren<StoneThrowerCharacter>(true) ?? charObj.AddComponent<StoneThrowerCharacter>();
            }

            if (_character != null)
            {
                _character.gameObject.SetActive(true);
                _character.RestoreVisibility();
                PositionCharacterOnPlatform();
                _character.InitializeCharacter();
                _character.SetHandStonePrefab(defaultStonePrefab);
            }

            if (_dualCamera != null && _character != null)
            {
                _dualCamera.targetCharacter = _character.transform;
                _dualCamera.SetCameraMode(DualCameraSetup.CameraMode.TopDownPosition);
                _dualCamera.SnapCameraImmediate();
            }
        }

        private void PositionCharacterOnPlatform()
        {
            if (_character == null) return;
            ResolveReferences();

            if (_currentLaunchPlatform != null)
            {
                BoxCollider col = _currentLaunchPlatform.GetComponent<BoxCollider>() ?? _currentLaunchPlatform.GetComponentInChildren<BoxCollider>();
                Vector3 spawnPos = (col != null) ? new Vector3(col.bounds.center.x, col.bounds.max.y, col.bounds.center.z) : _currentLaunchPlatform.position + Vector3.up * 0.5f;

                _character.basePosition = spawnPos;
                _character.currentPosition = spawnPos;
                _character.baseRotation = _currentLaunchPlatform.rotation;
                _character.transform.position = spawnPos;
                _character.transform.rotation = _character.baseRotation;
            }
        }

        public void ResetToPositioning()
        {
            currentState = ArcadeState.Positioning;
            lastStateChangeTime = Time.time;
            aimGaugeValue = 0f;
            powerGaugeValue = 0f;
            lastTimingText = "";
            bannerNotificationText = "";

            if (_dualCamera != null && _character != null)
            {
                _dualCamera.targetCharacter = _character.transform;
                _dualCamera.SetCameraMode(DualCameraSetup.CameraMode.TopDownPosition);
            }
        }

        private void Update()
        {
            switch (currentState)
            {
                case ArcadeState.Positioning:
                    UpdatePositioning();
                    break;
                case ArcadeState.AimingAngle:
                    UpdateAimingAngle();
                    break;
                case ArcadeState.ChargingPower:
                    UpdateChargingPower();
                    break;
                case ArcadeState.Flying:
                    UpdateFlying();
                    break;
                case ArcadeState.Result:
                    if (Time.time - lastStateChangeTime > 0.8f && IsActionTriggered())
                    {
                        ResetToPositioning();
                    }
                    break;
            }
        }

        private void UpdatePositioning()
        {
            if (Time.time - lastStateChangeTime > STATE_COOLDOWN && IsActionTriggered())
            {
                currentState = ArcadeState.AimingAngle;
                lastStateChangeTime = Time.time;
                if (_dualCamera != null) _dualCamera.SetCameraMode(DualCameraSetup.CameraMode.ShoulderAim);
            }
        }

        private void UpdateAimingAngle()
        {
            aimGaugeValue += aimDirection * aimSpeed * Time.deltaTime;
            if (aimGaugeValue > 1f) { aimGaugeValue = 1f; aimDirection = -1f; }
            else if (aimGaugeValue < -1f) { aimGaugeValue = -1f; aimDirection = 1f; }

            if (Time.time - lastStateChangeTime > STATE_COOLDOWN && IsActionTriggered())
            {
                currentState = ArcadeState.ChargingPower;
                lastStateChangeTime = Time.time;
                powerGaugeValue = 0f;
                powerDirection = 1f;
            }
        }

        private void UpdateChargingPower()
        {
            powerGaugeValue += powerDirection * powerSpeed * Time.deltaTime;
            if (powerGaugeValue > 1f) { powerGaugeValue = 1f; powerDirection = -1f; }
            else if (powerGaugeValue < 0f) { powerGaugeValue = 0f; powerDirection = 1f; }

            if (Time.time - lastStateChangeTime > STATE_COOLDOWN && IsActionTriggered())
            {
                LaunchArcadeStone();
            }
        }

        public void LaunchArcadeStone()
        {
            currentState = ArcadeState.ThrowingAnimation;
            lastStateChangeTime = Time.time;

            if (_character != null)
            {
                _character.PlayThrowAnimation(
                    onCameraLeadInCallback: (anchorPos, forwardDir) =>
                    {
                        if (_dualCamera != null) _dualCamera.StartLaunchLeadIn(anchorPos, forwardDir);
                    },
                    onReleaseCallback: () =>
                    {
                        ExecuteArcadeLaunch();
                    }
                );
            }
            else
            {
                ExecuteArcadeLaunch();
            }
        }

        private void ExecuteArcadeLaunch()
        {
            currentState = ArcadeState.Flying;
            lastStateChangeTime = Time.time;

            float angleDegrees = aimGaugeValue * 25f;
            Vector3 baseForward = (_character != null) ? (_character.baseRotation * Vector3.forward) : Vector3.forward;
            Vector3 direction = Quaternion.Euler(0f, angleDegrees, 0f) * baseForward;

            if (_character != null)
            {
                _character.currentAimRotation = Quaternion.Euler(0f, angleDegrees, 0f) * _character.baseRotation;
                _character.transform.rotation = _character.currentAimRotation;
            }

            Vector3 spawnPos = (_character != null) ? _character.GetHandPosition() : transform.position + new Vector3(0.35f, 1.2f, 0.8f);
            Quaternion spawnRot = Quaternion.LookRotation(direction, Vector3.up);

            if (_arcadeStone != null)
            {
                _arcadeStone.OnSkipBounced -= HandleSkipBounced;
                _arcadeStone.OnStoneSunk -= HandleStoneSunk;
                Destroy(_arcadeStone.gameObject);
                _arcadeStone = null;
            }

            GameObject prefabToSpawn = defaultStonePrefab ?? Resources.Load<GameObject>("Stone");
            GameObject newObj = (prefabToSpawn != null) ? Instantiate(prefabToSpawn, spawnPos, spawnRot) : new GameObject("ArcadeStone");
            newObj.name = "ArcadeStone";

            _arcadeStone = newObj.GetComponent<ArcadeSkippingStone>() ?? newObj.AddComponent<ArcadeSkippingStone>();
            _arcadeStone.OnSkipBounced += HandleSkipBounced;
            _arcadeStone.OnStoneSunk += HandleStoneSunk;

            if (_dualCamera != null)
            {
                _dualCamera.targetStone = _arcadeStone.transform;
                _dualCamera.SetCameraMode(DualCameraSetup.CameraMode.DynamicFlight);
            }

            float finalPower = Mathf.Lerp(0.8f, 1.2f, powerGaugeValue);
            _arcadeStone.Launch(direction, finalPower);
        }

        private void UpdateFlying()
        {
            // 키보드 A/D/S 조향 & 탭
            if (IsKeyTriggered(KeyCode.A) || IsKeyTriggered(KeyCode.LeftArrow))
            {
                TriggerRhythmTap(-5.0f);
            }
            else if (IsKeyTriggered(KeyCode.D) || IsKeyTriggered(KeyCode.RightArrow))
            {
                TriggerRhythmTap(5.0f);
            }
            else if (IsKeyTriggered(KeyCode.Space) || IsKeyTriggered(KeyCode.S) || IsKeyTriggered(KeyCode.DownArrow))
            {
                TriggerRhythmTap(0f);
            }
        }

        public void TriggerRhythmTap(float steerAngle)
        {
            if (_arcadeStone == null || _arcadeStone.isSunk) return;

            bool tapped = _arcadeStone.TryRhythmTap(steerAngle, out string grade);
            if (tapped)
            {
                lastTimingText = grade;
            }
            else
            {
                lastTimingText = grade;
            }

            StopCoroutine(nameof(ClearTimingText));
            StartCoroutine(ClearTimingText(1.2f));
        }

        private void HandleSkipBounced(int count, string grade)
        {
            lastTimingText = grade;
            if (grade.Contains("PERFECT")) perfectTimingCount++;
            StopCoroutine(nameof(ClearTimingText));
            StartCoroutine(ClearTimingText(0.8f));
        }

        private void HandleStoneSunk(float totalDist)
        {
            currentState = ArcadeState.Result;
            lastStateChangeTime = Time.time;
            CalculateResults(totalDist);
        }

        private void CalculateResults(float dist)
        {
            distanceScore = Mathf.RoundToInt(dist * 10f);
            int skips = (_arcadeStone != null) ? _arcadeStone.skipCount : 0;
            skipScore = skips * 500;
            specialScore = (perfectTimingCount * 300);
            totalScore = distanceScore + skipScore + specialScore;
            earnedCoins = Mathf.Max(10, Mathf.RoundToInt(totalScore / 20f));

            if (AquariumManager.Instance != null)
            {
                AquariumManager.Instance.AddCoins(earnedCoins);
            }

            lastResultData = new InGameResultData
            {
                finalDistance = dist,
                skipCount = skips,
                perfectTimingCount = perfectTimingCount,
                earnedCoins = earnedCoins,
                totalScore = totalScore
            };

            if (GameDataManager.Instance != null)
            {
                GameDataManager.Instance.ProcessMatchResult(lastResultData);
            }

            OnMatchResultGenerated?.Invoke(lastResultData);
        }

        private IEnumerator ClearTimingText(float delay)
        {
            yield return new WaitForSeconds(delay);
            lastTimingText = "";
        }

        private bool IsActionTriggered()
        {
#if ENABLE_INPUT_SYSTEM
            var k = Keyboard.current;
            var m = Mouse.current;
            var t = Touchscreen.current;
            if (k != null && (k.spaceKey.wasPressedThisFrame || k.enterKey.wasPressedThisFrame)) return true;
            if (m != null && m.leftButton.wasPressedThisFrame) return true;
            if (t != null && t.primaryTouch.press.wasPressedThisFrame) return true;
#else
            if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return) || Input.GetMouseButtonDown(0) || (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began)) return true;
#endif
            return false;
        }

        private bool IsKeyTriggered(KeyCode key)
        {
#if ENABLE_INPUT_SYSTEM
            var k = Keyboard.current;
            if (k == null) return false;
            if (key == KeyCode.A) return k.aKey.wasPressedThisFrame;
            if (key == KeyCode.D) return k.dKey.wasPressedThisFrame;
            if (key == KeyCode.S) return k.sKey.wasPressedThisFrame;
            if (key == KeyCode.LeftArrow) return k.leftArrowKey.wasPressedThisFrame;
            if (key == KeyCode.RightArrow) return k.rightArrowKey.wasPressedThisFrame;
            if (key == KeyCode.DownArrow) return k.downArrowKey.wasPressedThisFrame;
            if (key == KeyCode.Space) return k.spaceKey.wasPressedThisFrame;
            if (key == KeyCode.Return) return k.enterKey.wasPressedThisFrame;
            return false;
#else
            return Input.GetKeyDown(key);
#endif
        }
    }
}
