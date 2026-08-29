using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using SkippingStones.Terrain;

public class EnvironmentTestHelper : MonoBehaviour
{
    private static EnvironmentTestHelper _instance;
    public static EnvironmentTestHelper Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindAnyObjectByType<EnvironmentTestHelper>();
                if (_instance == null)
                {
                    GameObject helperObj = new GameObject("[AutoBootstrap_EnvironmentTestHelper]");
                    _instance = helperObj.AddComponent<EnvironmentTestHelper>();
                    DontDestroyOnLoad(helperObj);
                }
            }
            return _instance;
        }
    }

    [Header("테스트 UI 표시 여부 (F1 키로 토글)")]
    public bool showTestUI = false;
    public bool isAutoFlying = false;

    private float simulatedDistance = 0f;

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
    }

    private void Update()
    {
        // 🌟 키보드 숫자키 단축키 지원 (상단 숫자키 1~4: 프리뷰 전용)
        bool press1 = false, press2 = false, press3 = false, press4 = false, pressF1 = false;

#if ENABLE_INPUT_SYSTEM
        var keyboard = Keyboard.current;
        if (keyboard != null)
        {
            if (keyboard.digit1Key.wasPressedThisFrame || keyboard.numpad1Key.wasPressedThisFrame) press1 = true;
            if (keyboard.digit2Key.wasPressedThisFrame || keyboard.numpad2Key.wasPressedThisFrame) press2 = true;
            if (keyboard.digit3Key.wasPressedThisFrame || keyboard.numpad3Key.wasPressedThisFrame) press3 = true;
            if (keyboard.digit4Key.wasPressedThisFrame || keyboard.numpad4Key.wasPressedThisFrame) press4 = true;
            if (keyboard.f1Key.wasPressedThisFrame) pressF1 = true;
        }
#endif

        try
        {
            if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1)) press1 = true;
            if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2)) press2 = true;
            if (Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Keypad3)) press3 = true;
            if (Input.GetKeyDown(KeyCode.Alpha4) || Input.GetKeyDown(KeyCode.Keypad4)) press4 = true;
            if (Input.GetKeyDown(KeyCode.F1)) pressF1 = true;
        }
        catch { }

        if (press1) SetPreviewDistance(0f);
        if (press2) SetPreviewDistance(2000f);
        if (press3) SetPreviewDistance(3600f);
        if (press4) SetPreviewDistance(4800f);
        if (pressF1) showTestUI = !showTestUI;
    }

    public void SetPreviewDistance(float dist)
    {
        simulatedDistance = dist;
        if (LakeEnvironmentManager.Instance != null)
        {
            LakeEnvironmentManager.Instance.UpdateEnvironmentByDistance(dist);
        }
        Debug.Log($"[EnvironmentTestHelper] 🌍 환경 미리보기 비거리 설정: {dist:F0}m");
    }

    public void StopAutoFly()
    {
        StopAllCoroutines();
        isAutoFlying = false;
        var gc = GameController.Instance != null ? GameController.Instance : FindAnyObjectByType<GameController>();
        if (gc != null)
        {
            if (gc.stone != null)
            {
                gc.stone.isGodMode = false;
                gc.stone.isThrown = false;
            }
            if (gc.character != null)
            {
                gc.character.RestoreVisibility();
                Transform pier = GameController.FindPlatformInScene();
                gc.character.transform.rotation = pier != null ? pier.rotation : Quaternion.identity;
            }
        }
    }

    public void ToggleAutoFlyGodMode()
    {
        if (isAutoFlying)
        {
            StopAutoFly();
        }
        else
        {
            showTestUI = false; // 🌟 비행 시작 시 테스트 메뉴를 닫고 화면을 쾌적하게 유지
            StartCoroutine(AutoFlyRoutine());
        }
    }

    private IEnumerator AutoFlyRoutine()
    {
        isAutoFlying = true;

        var gc = GameController.Instance != null ? GameController.Instance : FindAnyObjectByType<GameController>();
        if (gc == null)
        {
            Debug.LogError("[EnvironmentTestHelper] ❌ GameController를 찾을 수 없습니다!");
            isAutoFlying = false;
            yield break;
        }

        // 1. 인게임 화면으로 확실히 전환 및 세션 리셋
        if (SkippingStones.UI.MetaUIManager.Instance != null)
        {
            SkippingStones.UI.MetaUIManager.Instance.ShowScreen(SkippingStones.UI.MetaScreen.InGame);
        }

        gc.ResetToPositioning();

        // 2. 씬에 이미 세팅된 환경 매니저(브룩 등)를 파괴하지 않고 유지 & 발판 갱신
        if (LakeEnvironmentManager.Instance != null)
        {
            LakeEnvironmentManager.Instance.SetupBGChunks();
        }

        // 3. 캐릭터 바인딩 및 발판 정중앙 정면 배치
        if (gc.character == null)
        {
            gc.character = FindAnyObjectByType<StoneThrowerCharacter>();
        }

        if (gc.character == null && gc.defaultCharacterPrefab != null)
        {
            GameObject spawnedObj = Instantiate(gc.defaultCharacterPrefab);
            spawnedObj.name = gc.defaultCharacterPrefab.name;
            gc.character = spawnedObj.GetComponentInChildren<StoneThrowerCharacter>(true);
        }

        if (gc.character == null)
        {
            Debug.LogError("[EnvironmentTestHelper] ❌ StoneThrowerCharacter를 찾을 수 없습니다!");
            isAutoFlying = false;
            yield break;
        }

        gc.character.gameObject.SetActive(true);
        gc.character.RestoreVisibility();

        gc.startPosX = 0f;
        gc.aimGaugeValue = 0f;
        gc.powerGaugeValue = 1.0f;
        gc.currentMode = GameController.GameMode.LongDistance;

        Transform pier = GameController.FindPlatformInScene();
        if (pier != null)
        {
            gc.currentLaunchPier = pier;
            Collider pierCol = pier.GetComponent<Collider>();
            Vector3 pierTopPos = pierCol != null ? new Vector3(pierCol.bounds.center.x, pierCol.bounds.max.y, pierCol.bounds.center.z) : (pier.position + Vector3.up * 0.5f);
            gc.character.basePosition = pierTopPos;
            gc.character.transform.position = pierTopPos;
            gc.character.transform.rotation = pier.rotation;
        }

        if (gc.dualCamera != null)
        {
            gc.dualCamera.targetCharacter = gc.character.transform;
            gc.dualCamera.SetCameraMode(DualCameraSetup.CameraMode.TopDownPosition);
            gc.dualCamera.SnapCameraImmediate();
        }

        // 4. 경로 사전 베이킹
        GlobalRiverPath riverPath = GlobalRiverPath.Instance;
        riverPath.RebuildPath();

        yield return new WaitForSeconds(0.2f);

        gc.currentState = GameController.GameState.ThrowingAnimation;
        bool isReleased = false;
        Vector3 spawnWorldPos = Vector3.zero;

        // 🌟 5. 45프레임 카메라 선행 가속 & 55프레임 돌 스폰 콜백 등록 후 투구 실행
        gc.character.PlayThrowAnimation(
            // 45프레임 카메라 리드인 콜백
            (anchorPos, forwardDir) =>
            {
                if (gc.dualCamera != null)
                {
                    gc.dualCamera.SetCameraMode(DualCameraSetup.CameraMode.DynamicFlight);
                }
            },
            // 55프레임 릴리즈(발사) 콜백
            () =>
            {
                // 손의 Dummy 소켓 월드 좌표에서 정확히 스폰
                spawnWorldPos = gc.character.GetHandPosition();

                // 기존 돌이 없다면 프리팹 로드 후 생성
                GameObject stonePrefab = Resources.Load<GameObject>("Stone");
#if UNITY_EDITOR
                if (stonePrefab == null)
                {
                    stonePrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/3D/prefab/Stone.prefab");
                }
#endif
                GameObject stoneObj = null;
                if (stonePrefab != null)
                {
                    stoneObj = Instantiate(stonePrefab, spawnWorldPos, Quaternion.identity);
                }
                else if (gc.stone != null)
                {
                    stoneObj = Instantiate(gc.stone.gameObject, spawnWorldPos, Quaternion.identity);
                }

                if (stoneObj != null)
                {
                    stoneObj.name = "GodMode_SkippingStone";
                    stoneObj.SetActive(true);

                    SkippingStone ss = stoneObj.GetComponent<SkippingStone>();
                    if (ss == null) ss = stoneObj.AddComponent<SkippingStone>();
                    gc.stone = ss;

                    // 카메라 타깃 바인딩
                    if (gc.dualCamera != null)
                    {
                        gc.dualCamera.targetStone = stoneObj.transform;
                        gc.dualCamera.SetCameraMode(DualCameraSetup.CameraMode.DynamicFlight);
                    }
                }

                isReleased = true;
            }
        );

        // 5. 캐릭터가 55프레임에 도달해 돌을 놓을 때까지 대기 (타임아웃 2.5초 안전망)
        float waitElapsed = 0f;
        while (!isReleased && waitElapsed < 2.5f)
        {
            waitElapsed += Time.deltaTime;
            yield return null;
        }

        if (!isReleased && gc.stone == null)
        {
            spawnWorldPos = gc.character.GetHandPosition();
            GameObject stoneObj = new GameObject("GodMode_SkippingStone");
            stoneObj.transform.position = spawnWorldPos;
            var ss = stoneObj.AddComponent<SkippingStone>();
            gc.stone = ss;
            if (gc.dualCamera != null)
            {
                gc.dualCamera.targetStone = stoneObj.transform;
                gc.dualCamera.SetCameraMode(DualCameraSetup.CameraMode.DynamicFlight);
            }
        }

        if (gc.stone == null)
        {
            Debug.LogError("[EnvironmentTestHelper] ❌ 돌멩이 생성 실패!");
            isAutoFlying = false;
            yield break;
        }

        // 5. 생성된 돌에 갓모드 물리 세팅 적용
        gc.currentState = GameController.GameState.Flying;

        var rb = gc.stone.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.useGravity = false;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        gc.stone.isThrown = true;
        gc.stone.isCrashed = false;
        gc.stone.isSunk = false;
        gc.stone.isGodMode = true;

        gc.stone.bounceHistory.Clear();
        gc.stone.bounceHistory.Add(new SkippingStone.BounceRecord { position = spawnWorldPos, skipIndex = 0, grade = "START", distance = 0f });

        // 6. 강줄기 스플라인 기반 곡선 수면 관통 비행 루프 (GlobalRiverPath 연동)
        riverPath.RebuildPath();

        // 맵 시퀀스의 실제 끝단 완주 거리 산출
        float targetDist = riverPath.totalRiverLength > 100f ? riverPath.totalRiverLength : 3500f;
        float currentDist = 0f;
        float flySpeed = 110f;
        float bounceWavelength = 130f;
        float lastBounceZ = 0f;
        int skipCounter = 0;

        RiverValleyTerrainGenerator terrainGen = FindAnyObjectByType<RiverValleyTerrainGenerator>();
        WaterSurface ws = FindAnyObjectByType<WaterSurface>();
        float baseWaterY = (ws != null && ws.GetComponent<BoxCollider>() != null) ? ws.GetComponent<BoxCollider>().bounds.max.y : 16.0f;

        while (currentDist < targetDist && isAutoFlying)
        {
            currentDist += flySpeed * Time.deltaTime;
            if (LakeEnvironmentManager.Instance != null)
            {
                LakeEnvironmentManager.Instance.UpdateEnvironmentByDistance(currentDist);
            }

            Vector3 centerPos;
            Vector3 tangentDir = Vector3.forward;
            float waterY = baseWaterY;

            if (riverPath.EvaluateAtDistance(currentDist, out centerPos, out tangentDir, out _, out float ptWaterY))
            {
                waterY = ptWaterY;
            }
            else
            {
                float currentZ = spawnWorldPos.z + currentDist;
                float centerOffset = (terrainGen != null) ? (terrainGen.GetRiverCenterX(currentZ) - terrainGen.sizeX * 0.5f) : spawnWorldPos.x;
                centerPos = new Vector3(centerOffset, waterY, currentZ);
            }

            float wavePhase = (currentDist % bounceWavelength) / bounceWavelength;
            float stoneY = waterY + Mathf.Sin(wavePhase * Mathf.PI) * 2.2f + 0.15f;

            float pitchAngle = Mathf.Cos(wavePhase * Mathf.PI) * 35f;
            float spinYaw = (currentDist * 18f) % 360f;
            Quaternion baseLookRot = (tangentDir != Vector3.zero) ? Quaternion.LookRotation(tangentDir) : Quaternion.identity;
            gc.stone.transform.rotation = baseLookRot * Quaternion.Euler(-pitchAngle, spinYaw, 0f);

            gc.stone.totalDistance = currentDist;
            gc.stone.transform.position = new Vector3(centerPos.x, stoneY, centerPos.z);
            simulatedDistance = currentDist;

            // 바운스 이펙트 및 히스토리 기록
            if (currentDist - lastBounceZ >= bounceWavelength)
            {
                lastBounceZ = currentDist;
                skipCounter++;

                string grade = (currentDist >= targetDist - 150f) ? "FINISH" :
                               (skipCounter % 4 == 0) ? "🔥 BOOST" : "🔥 PERFECT";

                Vector3 bouncePos = new Vector3(centerPos.x, waterY + 0.05f, centerPos.z);
                gc.stone.bounceHistory.Add(new SkippingStone.BounceRecord
                {
                    position = bouncePos,
                    skipIndex = skipCounter,
                    grade = grade,
                    distance = currentDist
                });
                gc.stone.skipCount = skipCounter;

                if (SplashEffectSpawner.Instance != null)
                {
                    SplashEffectSpawner.Instance.SpawnSplash(bouncePos, 1.3f);
                }
                if (AudioManager.Instance != null)
                {
                    AudioManager.Instance.PlaySound(SoundType.BouncePerfect, 1.0f);
                }
                HapticFeedbackHelper.TriggerMediumBounce();
            }

            yield return null;
        }

        isAutoFlying = false;
        if (gc.stone != null)
        {
            gc.stone.isGodMode = false;
            gc.stone.totalDistance = targetDist;
        }

        // 7. 리플레이 화면 자동 진입
        if (gc.topDownReplay == null)
        {
            gc.topDownReplay = FindAnyObjectByType<TopDownReplayManager>();
        }

        if (gc.topDownReplay != null)
        {
            gc.currentState = GameController.GameState.Replay;
            gc.topDownReplay.isFromFlightTest = true;
            gc.topDownReplay.StartReplay(targetDist);
        }
        else
        {
            gc.ShowFinalResultDirect(targetDist);
        }
    }

}
