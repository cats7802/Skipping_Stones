using UnityEngine;
using System.Collections;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class GameController : MonoBehaviour
{
    public static GameController Instance { get; private set; }

    public enum GameMode
    {
        LongDistance,   // 🏆 장거리 도전 모드 (최대 비거리 & 랭킹 레이스)
        TargetAccuracy  // 🎯 타겟 맞추기 모드 (강변 PP 위치 선정 & 건너편 타겟 정밀 투구)
    }

    public enum GameState
    {
        ModeSelect,         // 모드/캐릭터/맵 선택 로비 (앞단 UI 영역)
        Positioning,        // 0단계: 위치 선정 (Top-Down 뷰)
        AimingAngle,        // 1단계: 방향 조준 (Shoulder 뷰)
        ChargingPower,      // 2단계: 파워 충전 (Shoulder 뷰)
        ThrowingAnimation,  // 2.5단계: 캐릭터 투구 스윙 모션 재생 중
        Flying,             // 3단계: 비행 및 리듬 바운스
        Replay,             // 3.5단계: 직교 탑다운 궤적 맵 리플레이
        Result              // 4단계: 최종 결과창
    }

    [Header("게임 모드 및 상태")]
    public GameMode currentMode = GameMode.LongDistance;
    public GameState currentState = GameState.ModeSelect;

    [Header("기본 프리팹 (앞단 UI 없을 때의 기본값)")]
    public GameObject defaultCharacterPrefab;
    public GameObject defaultStonePrefab;
    public GameObject defaultMapPrefab;

    [Header("핵심 인게임 참조 (컴포넌트 자동 연결)")]
    public StoneThrowerCharacter character;
    public SkippingStone stone;
    public DualCameraSetup dualCamera;
    public TopDownReplayManager topDownReplay;
    public Transform currentLaunchPier;

    [Header("게이지 값 (실시간)")]
    public float startPosX = 0f;
    public float aimGaugeValue = 0f;    // -1 ~ 1
    public float powerGaugeValue = 0f;  // 0 ~ 1
    public string lastTimingText = "";
    public string bannerNotificationText = "";

    [Header("UI 모달 상태")]
    public bool showAquariumModal = false;
    public bool showStoneSelectorModal = false;
    public bool requireTouchRelease = false;

    [Header("타깃 모드 발판 설정")]
    [Tooltip("강 건너기 모드에서 사용할 발판 번호 (0 = PP01, 1 = PP02 ...)")]
    public int targetPlatformIndex = 0;

    private void SetupCharacterSpawn(GameMode mode)
    {
        if (character == null) return;

        // 1. 강 건너기(타깃 모드): Player_Position 하위의 PP01~PP10 활용
        if (mode == GameMode.TargetAccuracy)
        {
            GameObject playerPosRoot = GameObject.Find("Player_Position");
            if (playerPosRoot != null && playerPosRoot.transform.childCount > 0)
            {
                int safeIndex = Mathf.Clamp(targetPlatformIndex, 0, playerPosRoot.transform.childCount - 1);
                Transform targetSpawn = playerPosRoot.transform.GetChild(safeIndex);

                character.transform.position = targetSpawn.position;
                character.transform.rotation = targetSpawn.rotation;
                return;
            }
        }

        // 2. 기본 장거리 모드: 발판 자동 탐색 및 변수 등록
        if (currentLaunchPier == null)
        {
            var colliders = FindObjectsByType<BoxCollider>();
            foreach (var col in colliders)
            {
                string colName = col.gameObject.name.ToLower();
                if (colName.Contains("pier") || colName.Contains("platform") || colName.Contains("start"))
                {
                    currentLaunchPier = col.transform;
                    break;
                }
            }
        }

        // 발판이 찾아졌다면 발판의 실제 X, Z 위치와 꼭대기 Y를 읽어서 캐릭터 안착
        if (currentLaunchPier != null)
        {
            var pierCol = currentLaunchPier.GetComponent<Collider>();
            float topY = (pierCol != null) ? pierCol.bounds.max.y : (currentLaunchPier.position.y + 0.5f);

            // 발판의 X, Z 좌표 위에 정확히 캐릭터를 올림
            character.transform.position = new Vector3(currentLaunchPier.position.x, topY, currentLaunchPier.position.z);
            character.transform.rotation = Quaternion.identity;
        }
        else
        {
            // 비상 예외 처리 (수면 위 기본 위치)
            character.transform.position = new Vector3(0f, 17.0f, -2.1f);
            character.transform.rotation = Quaternion.identity;
        }
    }


    private float aimSpeed = 2.4f;
    private float powerSpeed = 3.0f;
    private float aimDirection = 1f;
    private float powerDirection = 1f;
    private float lastStateChangeTime = 0f;
    private const float STATE_COOLDOWN = 0.35f;

    private void Awake()
    {
        // 1. 발판 자동 찾기 (Lakeside_Platform 등)
        if (currentLaunchPier == null)
        {
            var colliders = FindObjectsByType<BoxCollider>();
            foreach (var col in colliders)
            {
                string colName = col.gameObject.name.ToLower();
                if (colName.Contains("platform") || colName.Contains("pier") || colName.Contains("start"))
                {
                    currentLaunchPier = col.transform;
                    break;
                }
            }
        }

        // 2. 캐릭터 컴포넌트 자동 찾기
        if (character == null)
        {
            character = FindAnyObjectByType<StoneThrowerCharacter>();
        }

        // 3. 돌(SkippingStone) 자동 찾기
        if (stone == null)
        {
            stone = FindAnyObjectByType<SkippingStone>();
        }

        // 4. 카메라 리그 자동 찾기
        if (dualCamera == null)
        {
            dualCamera = FindAnyObjectByType<DualCameraSetup>();
        }

        // 5. 발판 위치에 맞춰 캐릭터 최초 안착
        if (character != null && currentLaunchPier != null)
        {
            var pierCol = currentLaunchPier.GetComponent<Collider>();
            float topY = (pierCol != null) ? pierCol.bounds.max.y : (currentLaunchPier.position.y + 0.5f);

            // 발판의 X, Z 좌표와 상단 높이(Y)에 캐릭터 배치
            character.transform.position = new Vector3(currentLaunchPier.position.x, topY, currentLaunchPier.position.z);
            character.transform.rotation = Quaternion.identity;
        }
    }

    private void Start()
    {
        // 🌟 현재 앞단 UI가 없으므로 시작 시 기본 세션으로 즉시 진입
        StartGameSession(defaultCharacterPrefab, defaultStonePrefab, defaultMapPrefab, currentMode);
    }

    /// <summary>
    /// 🌟 나중에 앞단(캐릭터/돌/맵/모드 선택 UI)에서 최종 [게임 시작]을 누를 때 호출할 공용 진입점
    /// </summary>
    public void StartGameSession(GameObject charPrefab, GameObject stonePrefab, GameObject mapPrefab, GameMode mode)
    {
        currentMode = mode;

        // 1. 맵 환경 구성 및 컴포넌트 자동 인식
        SetupMapEnvironment(mapPrefab);

        // 2. 캐릭터 및 카메라 구성
        SetupCharacterAndCamera(charPrefab);

        // 3. 인게임 0단계(Positioning)로 전환
        ResetToPositioning();
    }

    private void SetupMapEnvironment(GameObject mapPrefab)
    {
        // 씬에 이미 배치된 배경이 없는데 프리팹이 넘어온 경우 인스턴스화
        Terrain existingTerrain = FindAnyObjectByType<Terrain>();
        if (existingTerrain == null && mapPrefab != null)
        {
            Instantiate(mapPrefab, Vector3.zero, Quaternion.identity);
        }

        // 발판(Collider) 컴포넌트 자동 탐색 (이름 무관)
        currentLaunchPier = null;
        // 기존: var colliders = FindObjectsByType<BoxCollider>(FindObjectsSortMode.None);
        // 변경: var colliders = FindObjectsByType<BoxCollider>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        // 또는 더 간단히:
        var colliders = FindObjectsByType<BoxCollider>();

        foreach (var col in colliders)
        {
            if (col.gameObject.name.ToLower().Contains("pier") || col.gameObject.name.ToLower().Contains("dock"))
            {
                currentLaunchPier = col.transform;
                col.gameObject.SetActive(currentMode == GameMode.LongDistance);
                break;
            }
        }

        // PP(Player Position) 오브젝트 모드별 상태 제어
        GameObject ppObj = GameObject.Find("Player_Position");
        if (ppObj != null)
        {
            ppObj.SetActive(currentMode == GameMode.TargetAccuracy);
        }

        if (MapPIPManager.Instance != null)
        {
            MapPIPManager.Instance.UpdatePIPState(currentMode == GameMode.TargetAccuracy);
        }

        // 강 엔티티 스포너 모드별 재배치
        var spawner = FindAnyObjectByType<RiverSpawner>();
        if (spawner != null && character != null)
        {
            spawner.startBankPos = character.basePosition;
            spawner.spawnDirection = character.baseRotation * Vector3.forward;
            spawner.GenerateRiverEntitiesForMode(currentMode);
        }
    }

    private void OnGUI()
    {
        // ModeSelect 상태일 때만 화면에 임시 선택 UI 표시
        if (currentState == GameState.ModeSelect)
        {
            GUI.Box(new Rect(20, 20, 260, 160), "<b>게임 모드 선택 (임시 UI)</b>");

            if (GUI.Button(new Rect(35, 60, 230, 40), "🏆 장거리 도전 모드"))
            {
                SelectModeAndStart(GameMode.LongDistance);
            }

            if (GUI.Button(new Rect(35, 110, 230, 40), "🎯 타깃 맞추기 모드 (강 건너기)"))
            {
                SelectModeAndStart(GameMode.TargetAccuracy);
            }
        }
    }

    // 모드 선택 후 인게임 진입 처리
    public void SelectModeAndStart(GameMode mode)
    {
        currentMode = mode;

        // 선택된 모드에 맞춰 캐릭터 스폰 위치 세팅
        SetupCharacterSpawn(currentMode);

        // 모드에 따른 시작 상태 분기
        if (currentMode == GameMode.LongDistance)
        {
            currentState = GameState.Positioning; // 장거리: 0단계(발판 위치 선정)
        }
        else
        {
            currentState = GameState.AimingAngle; // 타깃 모드: 발판 이동 없이 바로 1단계(조준)
        }
    }
    private void SetupCharacterAndCamera(GameObject charPrefab)
    {
        if (character == null)
        {
            character = FindAnyObjectByType<StoneThrowerCharacter>();
        }

        if (character == null && charPrefab != null)
        {
            GameObject cObj = Instantiate(charPrefab);
            character = cObj.GetComponent<StoneThrowerCharacter>() ?? cObj.AddComponent<StoneThrowerCharacter>();
        }

        if (character != null)
        {
            // 발판 높이 또는 수면 높이 기준 시작 위치 정렬
            float baseSurfaceY = 0.5f;
            if (currentLaunchPier != null)
            {
                Collider pierCol = currentLaunchPier.GetComponent<Collider>();
                baseSurfaceY = (pierCol != null) ? pierCol.bounds.max.y : currentLaunchPier.position.y;
            }
            else
            {
                WaterSurface ws = FindAnyObjectByType<WaterSurface>();
                if (ws != null)
                {
                    Collider wCol = ws.GetComponent<Collider>();
                    baseSurfaceY = (wCol != null) ? wCol.bounds.max.y : ws.transform.position.y;
                }
            }

            Vector3 startPos = new Vector3(0f, baseSurfaceY, 0f);
            character.basePosition = startPos;
            character.currentPosition = startPos;
            character.baseRotation = (currentMode == GameMode.LongDistance) ? Quaternion.identity : Quaternion.Euler(0f, 90f, 0f);
            character.transform.position = startPos;
            character.transform.rotation = character.baseRotation;
            character.InitializeCharacter();
        }

        if (dualCamera == null) dualCamera = FindAnyObjectByType<DualCameraSetup>();
        if (dualCamera != null && character != null)
        {
            dualCamera.targetCharacter = character.transform;
            dualCamera.SetCameraMode(DualCameraSetup.CameraMode.TopDownPosition);
            dualCamera.SnapCameraImmediate();
        }
    }

    public void SelectGameMode(GameMode mode)
    {
        currentMode = mode;
        SetupMapEnvironment(null);
        ResetToPositioning();
    }

    public void ReturnToModeSelect()
    {
        currentState = GameState.ModeSelect;
        lastStateChangeTime = Time.time;
        requireTouchRelease = true;
        if (MapPIPManager.Instance != null) MapPIPManager.Instance.UpdatePIPState(false);
    }

    private void Update()
    {
        if (showAquariumModal || showStoneSelectorModal) return;

        switch (currentState)
        {
            case GameState.Positioning:
                UpdatePositioning();
                break;
            case GameState.AimingAngle:
                UpdateAimingAngle();
                break;
            case GameState.ChargingPower:
                UpdateChargingPower();
                break;
            case GameState.ThrowingAnimation:
                break;
            case GameState.Flying:
                UpdateFlying();
                break;
            case GameState.Result:
                if (Time.time - lastStateChangeTime > 0.7f && (IsKeyTriggered(KeyCode.R) || IsKeyTriggered(KeyCode.Space)))
                {
                    RestartGame();
                }
                break;
        }
    }

    [Header("0단계 위치 선정 파라미터")]
    public float minPositionX = -12f;
    public float maxPositionX = 12f;
    private bool isDraggingMap = false;
    private Vector2 prevDragPos;

    private void UpdatePositioning()
    {
        if (currentMode == GameMode.LongDistance)
        {
            if (currentLaunchPier != null)
            {
                var bc = currentLaunchPier.GetComponent<BoxCollider>();
                float halfW = (bc != null && bc.bounds.extents.x > 1f) ? (bc.bounds.extents.x * 0.85f) : 12f;
                minPositionX = -halfW;
                maxPositionX = halfW;
            }

            float hInput = GetHorizontalInput();
            if (Mathf.Abs(hInput) > 0.001f)
            {
                startPosX = Mathf.Clamp(startPosX + hInput * Time.deltaTime * 7.5f, minPositionX, maxPositionX);
            }

            HandlePierDragSlide();

            if (character != null)
            {
                character.UpdatePositioning(startPosX, 0f);
                if (stone != null) stone.transform.position = character.GetHandPosition();
            }
        }
        else
        {
            if (IsKeyTriggered(KeyCode.LeftArrow) || IsKeyTriggered(KeyCode.A))
            {
                if (character != null) character.MoveToPreviousWaypoint();
            }
            if (IsKeyTriggered(KeyCode.RightArrow) || IsKeyTriggered(KeyCode.D))
            {
                if (character != null) character.MoveToNextWaypoint();
            }

            HandleTargetSwipeStep();

            if (character != null)
            {
                character.UpdatePositioning(startPosX, 0f);
                if (stone != null) stone.transform.position = character.GetHandPosition();
            }
        }

        if (Time.time - lastStateChangeTime > STATE_COOLDOWN)
        {
            if (IsKeyTriggered(KeyCode.Space) || IsKeyTriggered(KeyCode.Return))
            {
                ConfirmPosition();
            }
        }
    }

    private void HandlePierDragSlide()
    {
        Vector2 curPos = Vector2.zero;
        bool isPressed = false;

#if ENABLE_INPUT_SYSTEM
        var touch = Touchscreen.current;
        var mouse = Mouse.current;
        if (touch != null && touch.primaryTouch.press.isPressed)
        {
            isPressed = true;
            curPos = touch.primaryTouch.position.ReadValue();
        }
        else if (mouse != null && (mouse.leftButton.isPressed || mouse.rightButton.isPressed))
        {
            isPressed = true;
            curPos = mouse.position.ReadValue();
        }
#else
        if (Input.GetMouseButton(0) || Input.GetMouseButton(1))
        {
            isPressed = true;
            curPos = Input.mousePosition;
        }
#endif

        if (isPressed)
        {
            if (!isDraggingMap)
            {
                isDraggingMap = true;
                prevDragPos = curPos;
            }
            else
            {
                float deltaX = (curPos.x - prevDragPos.x) * 0.016f;
                startPosX = Mathf.Clamp(startPosX + deltaX, minPositionX, maxPositionX);
                prevDragPos = curPos;
            }
        }
        else
        {
            isDraggingMap = false;
        }
    }

    private bool isSwipingTarget = false;
    private Vector2 swipeStartPos;
    private float swipeThreshold = 35f;

    private void HandleTargetSwipeStep()
    {
        Vector2 curPos = Vector2.zero;
        bool isPressed = false;

#if ENABLE_INPUT_SYSTEM
        var touch = Touchscreen.current;
        var mouse = Mouse.current;
        if (touch != null && touch.primaryTouch.press.isPressed)
        {
            isPressed = true;
            curPos = touch.primaryTouch.position.ReadValue();
        }
        else if (mouse != null && (mouse.leftButton.isPressed || mouse.rightButton.isPressed))
        {
            isPressed = true;
            curPos = mouse.position.ReadValue();
        }
#else
        if (Input.GetMouseButton(0) || Input.GetMouseButton(1))
        {
            isPressed = true;
            curPos = Input.mousePosition;
        }
#endif

        if (isPressed)
        {
            if (!isSwipingTarget)
            {
                isSwipingTarget = true;
                swipeStartPos = curPos;
            }
            else
            {
                float dx = curPos.x - swipeStartPos.x;
                if (dx > swipeThreshold)
                {
                    if (character != null) character.MoveToNextWaypoint();
                    swipeStartPos = curPos;
                }
                else if (dx < -swipeThreshold)
                {
                    if (character != null) character.MoveToPreviousWaypoint();
                    swipeStartPos = curPos;
                }
            }
        }
        else
        {
            isSwipingTarget = false;
        }
    }

    public void ConfirmPosition()
    {
        currentState = GameState.AimingAngle;
        lastStateChangeTime = Time.time;
        requireTouchRelease = true;
        aimGaugeValue = 0f;
        aimDirection = 1f;

        if (MapPIPManager.Instance != null)
        {
            MapPIPManager.Instance.UpdatePIPState(false);
        }

        if (character != null) character.UpdateAiming(0f);
        if (dualCamera != null) dualCamera.SetCameraMode(DualCameraSetup.CameraMode.ShoulderAim);
    }

    private void UpdateAimingAngle()
    {
        aimGaugeValue += aimDirection * aimSpeed * Time.deltaTime;
        if (aimGaugeValue > 1f) { aimGaugeValue = 1f; aimDirection = -1f; }
        else if (aimGaugeValue < -1f) { aimGaugeValue = -1f; aimDirection = 1f; }

        if (character != null)
        {
            character.UpdateAiming(aimGaugeValue);
            if (stone != null) stone.transform.position = character.GetHandPosition();
        }

        if (Time.time - lastStateChangeTime > STATE_COOLDOWN && IsActionTriggered())
        {
            ConfirmAngle();
        }
    }

    public void ConfirmAngle()
    {
        currentState = GameState.ChargingPower;
        lastStateChangeTime = Time.time;
        requireTouchRelease = true;
        powerGaugeValue = 0.1f;
        powerDirection = 1f;
    }

    private void UpdateChargingPower()
    {
        powerGaugeValue += powerDirection * powerSpeed * Time.deltaTime;
        if (powerGaugeValue > 1f) { powerGaugeValue = 1f; powerDirection = -1f; }
        else if (powerGaugeValue < 0f) { powerGaugeValue = 0f; powerDirection = 1f; }

        if (character != null)
        {
            character.UpdateWindup(powerGaugeValue);
            if (stone != null) stone.transform.position = character.GetHandPosition();
        }

        if (Time.time - lastStateChangeTime > STATE_COOLDOWN && IsActionTriggered())
        {
            LaunchStone();
        }
    }

    public void LaunchStone()
    {
        currentState = GameState.ThrowingAnimation;
        lastStateChangeTime = Time.time;
        requireTouchRelease = true;

        if (character != null)
        {
            character.PlayThrowAnimation(
                onCameraLeadInCallback: (anchorPos, forwardDir) =>
                {
                    if (dualCamera != null) dualCamera.StartLaunchLeadIn(anchorPos, forwardDir);
                },
                onReleaseCallback: () =>
                {
                    ExecuteLaunchPhysics();
                }
            );
        }
        else
        {
            if (dualCamera != null) dualCamera.SetCameraMode(DualCameraSetup.CameraMode.DynamicFlight);
            ExecuteLaunchPhysics();
        }
    }

    private void ExecuteLaunchPhysics()
    {
        currentState = GameState.Flying;
        lastStateChangeTime = Time.time;
        requireTouchRelease = true;

        float angleDegrees = aimGaugeValue * 25f;
        Vector3 baseForward = (character != null) ? (character.baseRotation * Vector3.forward) : Vector3.forward;
        Vector3 direction = Quaternion.Euler(0f, angleDegrees, 0f) * baseForward;

        if (character != null)
        {
            character.currentAimRotation = Quaternion.Euler(0f, angleDegrees, 0f) * character.baseRotation;
            character.transform.rotation = character.currentAimRotation;
            direction = character.currentAimRotation * Vector3.forward;
        }

        float stoneMultiplier = (StoneInventory.Instance != null) ? StoneInventory.Instance.GetCurrentStone().forwardPowerMultiplier : 1.0f;
        float finalPowerMultiplier = Mathf.Lerp(0.6f, 1.4f, powerGaugeValue) * stoneMultiplier;

        Vector3 spawnPos = (character != null) ? character.GetHandPosition() : transform.position + new Vector3(0.35f, 1.2f, 0.8f);
        Quaternion spawnRot = Quaternion.LookRotation(direction, Vector3.up);

        // 기존 돌 인스턴스 파괴 및 새 돌 스폰
        if (stone != null)
        {
            stone.OnSkipBounced -= HandleSkipBounced;
            stone.OnStoneSunk -= HandleStoneSunk;
            if (Application.isPlaying) Destroy(stone.gameObject);
            else DestroyImmediate(stone.gameObject);
            stone = null;
        }

        GameObject prefabToSpawn = defaultStonePrefab ?? Resources.Load<GameObject>("Stone");
        GameObject newStoneObj = (prefabToSpawn != null) ? Instantiate(prefabToSpawn, spawnPos, spawnRot) : new GameObject("Stone");
        newStoneObj.name = "Stone";
        if (prefabToSpawn == null)
        {
            newStoneObj.transform.position = spawnPos;
            newStoneObj.transform.rotation = spawnRot;
        }

        stone = newStoneObj.GetComponent<SkippingStone>() ?? newStoneObj.AddComponent<SkippingStone>();

        stone.OnSkipBounced += HandleSkipBounced;
        stone.OnStoneSunk += HandleStoneSunk;

        if (dualCamera != null)
        {
            dualCamera.targetStone = stone.transform;
            dualCamera.SetCameraMode(DualCameraSetup.CameraMode.DynamicFlight);
        }
        if (topDownReplay != null)
        {
            topDownReplay.stone = stone;
        }

        stone.Launch(direction, finalPowerMultiplier);
    }

    private Vector2 flightTouchStartPos;
    private float flightTouchStartTime = 0f;
    private bool isTrackingFlightTouch = false;

    private void UpdateFlying()
    {
        if (IsKeyTriggered(KeyCode.Escape))
        {
            Application.Quit();
        }

        float hInput = GetHorizontalInput();
        float keySteer = 0f;
        if (hInput < -0.1f) keySteer = -3f;
        else if (hInput > 0.1f) keySteer = 3f;

        if (IsKeyTriggered(KeyCode.Space) || IsKeyTriggered(KeyCode.Return))
        {
            EvaluateRhythmTiming(keySteer);
            return;
        }

        if (LakeEnvironmentManager.Instance != null && stone != null)
        {
            LakeEnvironmentManager.Instance.UpdateEnvironmentByDistance(stone.totalDistance);
        }

        HandleFlightFlickSteering();
    }

    private void HandleFlightFlickSteering()
    {
        Vector2 curPos = Vector2.zero;
        bool isDown = false;
        bool isUp = false;

#if ENABLE_INPUT_SYSTEM
        var touch = Touchscreen.current;
        var mouse = Mouse.current;
        if (touch != null && touch.primaryTouch.press.wasPressedThisFrame)
        {
            isDown = true;
            curPos = touch.primaryTouch.position.ReadValue();
        }
        else if (touch != null && touch.primaryTouch.press.wasReleasedThisFrame)
        {
            isUp = true;
            curPos = touch.primaryTouch.position.ReadValue();
        }
        else if (mouse != null && mouse.leftButton.wasPressedThisFrame)
        {
            isDown = true;
            curPos = mouse.position.ReadValue();
        }
        else if (mouse != null && mouse.leftButton.wasReleasedThisFrame)
        {
            isUp = true;
            curPos = mouse.position.ReadValue();
        }
#else
        try
        {
            if (Input.touchCount > 0)
            {
                var t = Input.GetTouch(0);
                if (t.phase == UnityEngine.TouchPhase.Began) { isDown = true; curPos = t.position; }
                else if (t.phase == UnityEngine.TouchPhase.Ended || t.phase == UnityEngine.TouchPhase.Canceled) { isUp = true; curPos = t.position; }
            }
            else if (Input.GetMouseButtonDown(0)) { isDown = true; curPos = Input.mousePosition; }
            else if (Input.GetMouseButtonUp(0)) { isUp = true; curPos = Input.mousePosition; }
        }
        catch { }
#endif

        if (isDown)
        {
            flightTouchStartPos = curPos;
            flightTouchStartTime = Time.time;
            isTrackingFlightTouch = true;
            EvaluateRhythmTiming(0f);
        }
        else if (isUp && isTrackingFlightTouch)
        {
            isTrackingFlightTouch = false;
            float deltaX = curPos.x - flightTouchStartPos.x;
            float duration = Time.time - flightTouchStartTime;

            if (duration < 0.35f && Mathf.Abs(deltaX) > 25f)
            {
                float steerAngle = (deltaX > 0f) ? 3.0f : -3.0f;
                if (stone != null && !stone.isSunk && !stone.isCrashed)
                {
                    stone.ApplySteerAngle(steerAngle);
                    lastTimingText += (steerAngle > 0f) ? " \n👉 [RIGHT 3° 조향]" : " \n👈 [LEFT 3° 조향]";
                }
            }
        }
    }

    public void EvaluateRhythmTiming(float steerAngleDegrees = 0f)
    {
        if (stone == null || stone.isSunk || stone.isCrashed) return;

        if (stone.TryRhythmBounce(steerAngleDegrees, out string timingGrade))
        {
            lastTimingText = timingGrade;
        }
        else
        {
            lastTimingText = "💦 너무 이름 (Too Early!)";
        }

        StopCoroutine(nameof(ClearTimingTextAfterDelay));
        StartCoroutine(ClearTimingTextAfterDelay(1.2f));
    }

    [Header("이번 라운드 결과 점수 통계")]
    public int distanceScore = 0;
    public int skipScore = 0;
    public int specialScore = 0;
    public int totalScore = 0;
    public int earnedCoins = 0;
    public int perfectTimingCount = 0;
    public int fishSnipeCount = 0;
    public int friendOvertakeCount = 0;
    public int boostPadCount = 0;
    public float lastSkimBonusDist = 0f;
    private bool hasCalculatedResult = false;

    public void TriggerFishSnipeEffect(string speciesName)
    {
        fishSnipeCount++;
        bannerNotificationText = $"🎯 FISH SNIPE! [{speciesName}] 저격 성공! (+1,000점 & 코인)";
        lastTimingText = "🔥 FISH SNIPE! 🔥";
        StartCoroutine(HitStopSlowMo(0.25f));
        StopCoroutine(nameof(ClearBannerAfterDelay));
        StartCoroutine(ClearBannerAfterDelay(2.5f));
    }

    public void TriggerFriendOvertake(string friendName, string rank)
    {
        friendOvertakeCount++;
        bannerNotificationText = $"🚩 [{friendName}] 추월 달성! ({rank} 랭킹 진입! +800점)";
        StopCoroutine(nameof(ClearBannerAfterDelay));
        StartCoroutine(ClearBannerAfterDelay(2.5f));
    }

    public void TriggerBoostPadEffect() => boostPadCount++;

    private IEnumerator HitStopSlowMo(float duration)
    {
        Time.timeScale = 0.4f;
        yield return new WaitForSecondsRealtime(duration);
        Time.timeScale = 1.0f;
    }

    private IEnumerator ClearTimingTextAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        lastTimingText = "";
    }

    private IEnumerator ClearBannerAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        bannerNotificationText = "";
    }

    private void HandleSkipBounced(int count, string text)
    {
        lastTimingText = text;
        if (text.Contains("PERFECT")) perfectTimingCount++;
        StopCoroutine(nameof(ClearTimingTextAfterDelay));
        StartCoroutine(ClearTimingTextAfterDelay(0.8f));
    }

    private void HandleStoneSunk(float dist)
    {
        StartCoroutine(DelayedShowResultRoutine(dist, 1.5f));
    }

    private IEnumerator DelayedShowResultRoutine(float dist, float delay)
    {
        yield return new WaitForSeconds(delay);
        if (currentState == GameState.Replay || currentState == GameState.Result) yield break;
        if (topDownReplay != null && (topDownReplay.isReplayActive || topDownReplay.isDrawing)) yield break;

        currentState = GameState.Replay;
        lastStateChangeTime = Time.time;
        requireTouchRelease = true;

        if (topDownReplay == null) topDownReplay = FindAnyObjectByType<TopDownReplayManager>();
        if (topDownReplay != null)
        {
            topDownReplay.isFromFlightTest = false;
            topDownReplay.StartReplay(dist);
        }
    }

    public void ShowFinalResultDirect(float dist)
    {
        if (EnvironmentTestHelper.Instance != null)
        {
            EnvironmentTestHelper.Instance.StopAutoFly();
            EnvironmentTestHelper.Instance.showTestUI = false;
        }

        currentState = GameState.Result;
        lastStateChangeTime = Time.time;
        requireTouchRelease = true;
        CalculateFinalScores(dist);
    }

    public void CalculateFinalScores(float dist)
    {
        if (hasCalculatedResult) return;
        hasCalculatedResult = true;

        distanceScore = Mathf.RoundToInt(dist * 10f);
        int skips = (stone != null) ? stone.skipCount : 0;
        skipScore = skips * 500;
        lastSkimBonusDist = (stone != null) ? stone.skimDistance : 0f;
        int skimScore = Mathf.RoundToInt(lastSkimBonusDist * 15f);

        specialScore = (perfectTimingCount * 300) + (fishSnipeCount * 1000) + (friendOvertakeCount * 800) + (boostPadCount * 500) + skimScore;
        totalScore = distanceScore + skipScore + specialScore;
        earnedCoins = Mathf.Max(5, Mathf.RoundToInt(totalScore / 25f));

        if (AquariumManager.Instance != null)
        {
            AquariumManager.Instance.AddCoins(earnedCoins);
        }
    }

    public void RestartGame()
    {
        ResetToPositioning();
    }

    private void ResetToPositioning()
    {
        StopAllCoroutines();
        if (topDownReplay != null) topDownReplay.isReplayActive = false;

        currentState = GameState.Positioning;
        lastStateChangeTime = Time.time + 0.35f;
        startPosX = 0f;
        aimGaugeValue = 0f;
        powerGaugeValue = 0f;
        lastTimingText = "";
        bannerNotificationText = "";

        perfectTimingCount = 0;
        fishSnipeCount = 0;
        friendOvertakeCount = 0;
        boostPadCount = 0;
        distanceScore = 0;
        skipScore = 0;
        specialScore = 0;
        totalScore = 0;
        earnedCoins = 0;
        hasCalculatedResult = false;

        if (LakeEnvironmentManager.Instance != null)
        {
            LakeEnvironmentManager.Instance.ResetEnvironment();
        }

        if (dualCamera != null)
        {
            dualCamera.SetCameraMode(DualCameraSetup.CameraMode.TopDownPosition);
        }

        requireTouchRelease = true;
        isTrackingFlightTouch = false;

        SetupCharacterAndCamera(null);
        SpawnNewStone();
    }

    public void SpawnNewStone()
    {
        if (stone != null)
        {
            stone.OnSkipBounced -= HandleSkipBounced;
            stone.OnStoneSunk -= HandleStoneSunk;
            if (Application.isPlaying) Destroy(stone.gameObject);
            else DestroyImmediate(stone.gameObject);
            stone = null;
        }

        if (character != null)
        {
            character.RestoreVisibility();
        }

        ApplyCurrentStoneVisuals();
    }

    public void ApplyCurrentStoneVisuals()
    {
        if (stone == null) return;

        Color targetTrailColor = Color.white;
        if (StoneInventory.Instance != null)
        {
            StoneItem item = StoneInventory.Instance.GetCurrentStone();
            if (item != null)
            {
                Renderer r = stone.GetComponentInChildren<Renderer>();
                if (r != null && r.sharedMaterial != null)
                {
                    r.sharedMaterial.SetColor("_BaseColor", item.color);
                }
                targetTrailColor = item.trailColor;
            }
        }

        if (stone.trail != null)
        {
            stone.trailStartColor = targetTrailColor;
            stone.trail.startWidth = 0.075f;
            stone.trail.endWidth = 0.005f;

            if (stone.trail.material != null)
            {
                stone.trail.material.color = Color.white;
            }

            Gradient g = new Gradient();
            g.SetKeys(
                new GradientColorKey[] { new GradientColorKey(targetTrailColor, 0.0f), new GradientColorKey(targetTrailColor, 1.0f) },
                new GradientAlphaKey[] { new GradientAlphaKey(0.70f, 0.0f), new GradientAlphaKey(0.0f, 1.0f) }
            );
            stone.trail.colorGradient = g;
        }
    }

    #region 입력 처리 유틸

    public float GetHorizontalInput()
    {
        float h = 0f;
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null)
        {
            if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed) h -= 1f;
            if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed) h += 1f;
        }
#endif
        try
        {
            float legacyH = Input.GetAxisRaw("Horizontal");
            if (Mathf.Abs(legacyH) > 0.01f) h = legacyH;
        }
        catch { }
        return h;
    }

    public bool IsActionTriggered()
    {
        if (Time.time - lastStateChangeTime < STATE_COOLDOWN) return false;

        bool isCurrentlyHeld = false;
#if ENABLE_INPUT_SYSTEM
        if (Mouse.current != null && Mouse.current.leftButton.isPressed) isCurrentlyHeld = true;
        if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.isPressed) isCurrentlyHeld = true;
#endif
        try
        {
            if (Input.touchCount > 0 || Input.GetMouseButton(0)) isCurrentlyHeld = true;
        }
        catch { }

        if (requireTouchRelease)
        {
            if (!isCurrentlyHeld) requireTouchRelease = false;
            return false;
        }

#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null && (Keyboard.current.spaceKey.wasPressedThisFrame || Keyboard.current.enterKey.wasPressedThisFrame)) return true;
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame) return true;
        if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame) return true;
#endif
        try
        {
            if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return) || Input.GetMouseButtonDown(0)) return true;
        }
        catch { }
        return false;
    }

    private bool IsKeyTriggered(KeyCode key)
    {
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null)
        {
            if (key == KeyCode.Space && Keyboard.current.spaceKey.wasPressedThisFrame) return true;
            if (key == KeyCode.Return && (Keyboard.current.enterKey.wasPressedThisFrame || Keyboard.current.numpadEnterKey.wasPressedThisFrame)) return true;
            if (key == KeyCode.R && Keyboard.current.rKey.wasPressedThisFrame) return true;
            if ((key == KeyCode.A || key == KeyCode.LeftArrow) && (Keyboard.current.aKey.wasPressedThisFrame || Keyboard.current.leftArrowKey.wasPressedThisFrame)) return true;
            if ((key == KeyCode.D || key == KeyCode.RightArrow) && (Keyboard.current.dKey.wasPressedThisFrame || Keyboard.current.rightArrowKey.wasPressedThisFrame)) return true;
        }
#endif
        try
        {
            if (Input.GetKeyDown(key)) return true;
        }
        catch { }
        return false;
    }

    #endregion
}