using UnityEngine;
using System.Collections;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class GameController : MonoBehaviour
{
    public enum GameMode
    {
        LongDistance,   // 🏆 장거리 도전 모드 (1,500m 강줄기 최대 비거리 & 랭킹 레이스)
        TargetAccuracy  // 🎯 타겟 맞추기 모드 (강변 PP 위치 선정 & 건너편 타겟 정밀 투구)
    }

    public enum GameState
    {
        ModeSelect,         // 모드 선택 로비 화면
        Positioning,        // 0단계: 위치 선정 (Top-Down 뷰)
        AimingAngle,        // 1단계: 방향 조준 (Shoulder 뷰)
        ChargingPower,      // 2단계: 파워 충전 (Shoulder 뷰)
        ThrowingAnimation,  // 2.5단계: 캐릭터 투구 스윙 모션 재생 중 (수면 탭 차단)
        Flying,             // 3단계: 비행 및 리듬 바운스 (다이내믹 쿼터뷰)
        Replay,             // 3.5단계: 1.5초 후 직교 탑다운 궤적 맵 리플레이
        Result              // 4단계: 게임 오버 및 최종 결과창
    }

    [Header("게임 모드")]
    public GameMode currentMode = GameMode.LongDistance;

    [Header("핵심 오브젝트 참조")]
    public SkippingStone stone;
    public DualCameraSetup dualCamera;
    public Transform launchPlatform;
    public StoneThrowerCharacter character;
    public TopDownReplayManager topDownReplay;

    [Header("게임 상태")]
    public GameState currentState = GameState.ModeSelect;

    [Header("게이지 값 (실시간)")]
    public float startPosX = 0f;
    public float aimGaugeValue = 0f;    // -1 ~ 1
    public float powerGaugeValue = 0f;  // 0 ~ 1
    public string lastTimingText = "";
    public string bannerNotificationText = "";

    [Header("UI 모달 상태")]
    public bool showAquariumModal = false;
    public bool showStoneSelectorModal = false;

    private float aimSpeed = 2.4f;      // 🌟 원래 상태로 원복 (기존 2.4f)
    private float powerSpeed = 3.0f;    // 🌟 원래 상태로 원복 (기존 3.0f)
    private float aimDirection = 1f;
    private float powerDirection = 1f;
    private Vector3 initialStonePos;
    private float lastStateChangeTime = 0f;
    private const float STATE_COOLDOWN = 0.35f;

    private void Awake()
    {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        if (GetComponent<WindowsAspectRatioController>() == null)
        {
            gameObject.AddComponent<WindowsAspectRatioController>();
        }
#endif
        if (topDownReplay == null)
        {
            topDownReplay = GetComponent<TopDownReplayManager>() ?? gameObject.AddComponent<TopDownReplayManager>();
        }
        EnsureCharacterReady();
    }

    private void Start()
    {
        if (stone != null)
        {
            initialStonePos = stone.transform.position;
            stone.OnSkipBounced += HandleSkipBounced;
            stone.OnStoneSunk += HandleStoneSunk;
        }

        EnsureCharacterReady();
        ApplyCurrentStoneVisuals();
        currentState = GameState.ModeSelect;
        if (MapPIPManager.Instance != null) MapPIPManager.Instance.UpdatePIPState(false);
    }

    public void SelectGameMode(GameMode mode)
    {
        currentMode = mode;
        ApplyGameModeEnvironment();
        ResetToPositioning();
    }

    public void ReturnToModeSelect()
    {
        currentState = GameState.ModeSelect;
        lastStateChangeTime = Time.time;
        requireTouchRelease = true;
        if (MapPIPManager.Instance != null) MapPIPManager.Instance.UpdatePIPState(false);
    }

    public GameObject GetWoodenPier()
    {
        GameObject pier = GameObject.Find("Lakeside_WoodenPier");
        if (pier != null) return pier;

        // 비활성화(Inactive) 오브젝트까지 씬 전체 탐색
        var activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (activeScene.isLoaded)
        {
            foreach (var root in activeScene.GetRootGameObjects())
            {
                if (root.name.Equals("Lakeside_WoodenPier", System.StringComparison.OrdinalIgnoreCase))
                {
                    return root;
                }
                var children = root.GetComponentsInChildren<Transform>(true);
                foreach (var t in children)
                {
                    if (t.name.Equals("Lakeside_WoodenPier", System.StringComparison.OrdinalIgnoreCase))
                    {
                        return t.gameObject;
                    }
                }
            }
        }

        // 만약 씬에 존재하지 않는다면 나무 발판(Wooden Dock) 자동 복원 생성
        GameObject newPier = GameObject.CreatePrimitive(PrimitiveType.Cube);
        newPier.name = "Lakeside_WoodenPier";
        newPier.transform.position = new Vector3(0f, 0.2f, 0f);
        newPier.transform.localScale = new Vector3(4.5f, 0.4f, 7.0f);

        var mr = newPier.GetComponent<MeshRenderer>();
        if (mr != null)
        {
            Shader standardShader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            if (standardShader != null)
            {
                Material mat = new Material(standardShader);
                mat.color = new Color(0.48f, 0.32f, 0.18f); // 짙은 원목 나무 색상
                mr.material = mat;
            }
        }
        return newPier;
    }

    public void ApplyGameModeEnvironment()
    {
        // 1. 발판 오브젝트 제어 (비활성화된 발판까지 100% 탐색 및 활성화)
        GameObject pierObj = GetWoodenPier();
        GameObject ppObj = GameObject.Find("Player_Position");
        if (ppObj == null)
        {
            foreach (var go in Resources.FindObjectsOfTypeAll<GameObject>())
            {
                if (go.name == "Player_Position" && go.scene.isLoaded) { ppObj = go; break; }
            }
        }

        if (currentMode == GameMode.LongDistance)
        {
            if (pierObj != null) pierObj.SetActive(true);
            if (ppObj != null) ppObj.SetActive(false);
            if (MapPIPManager.Instance != null) MapPIPManager.Instance.UpdatePIPState(false);
        }
        else
        {
            if (pierObj != null) pierObj.SetActive(false);
            if (ppObj != null) ppObj.SetActive(true);
            if (MapPIPManager.Instance != null) MapPIPManager.Instance.UpdatePIPState(true);
        }

        // 2. 캐릭터 위치 및 회전, 강 엔티티 초기화
        if (character != null)
        {
            if (currentMode == GameMode.LongDistance)
            {
                // 🏆 장거리 모드: 나무 발판 위에서 월드 +Z축 물줄기 방향(Euler 0, 0, 0) 정면 고정
                Vector3 pierPos = (pierObj != null) ? pierObj.transform.position + Vector3.up * 0.45f : new Vector3(0f, 0.5f, 0f);
                character.basePosition = pierPos;
                character.currentPosition = pierPos;
                character.baseRotation = Quaternion.Euler(0f, 0f, 0f);
                character.transform.position = pierPos;
                character.transform.rotation = character.baseRotation;
                character.currentAimRotation = character.baseRotation;
            }
            else
            {
                // 🎯 타겟 맞추기 모드: PP01~PP29 중심 포인트에서 강 건너편(+X / 90도) 방향 시작
                character.InitializeCharacter();
            }
        }

        // 3. 강 엔티티(부스트 패드, 물고기, 바위, 깃발) 모드별 궤적 자동 재배치
        var spawner = FindAnyObjectByType<RiverSpawner>();
        if (spawner != null)
        {
            spawner.GenerateRiverEntitiesForMode(currentMode);
        }

        // 4. 카메라 시점 즉각 동기화
        if (dualCamera != null)
        {
            dualCamera.SnapCameraImmediate();
        }
    }

    public void EnsureCharacterReady()
    {
        // 0. 현재 모드에 맞게 발판 및 Player_Position 상태 동기화 (장거리 모드 시 PP 완전 숨김)
        ApplyGameModeEnvironment();

        // Ground 메쉬에 MeshCollider 보장 (지면 밀착 레이캐스트 정확도 보장)
        var activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (activeScene.isLoaded)
        {
            foreach (var root in activeScene.GetRootGameObjects())
            {
                var meshFilters = root.GetComponentsInChildren<MeshFilter>(true);
                foreach (var mf in meshFilters)
                {
                    if (mf.name.IndexOf("ground", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        var mc = mf.GetComponent<MeshCollider>();
                        if (mc == null) mc = mf.gameObject.AddComponent<MeshCollider>();
                        if (mc.sharedMesh == null) mc.sharedMesh = mf.sharedMesh;
                    }
                }
            }
        }

        // MapPIPManager (상단 1/4 MAP_Camera PIP 매니저) 보장
        if (FindAnyObjectByType<MapPIPManager>() == null)
        {
            gameObject.AddComponent<MapPIPManager>();
        }

        // 1. 씬 내에 사용자가 배치한 Test_Chr 우선 탐색 및 중복 정리
        GameObject userChr = GameObject.Find("Test_Chr");
        GameObject spawnedChr = GameObject.Find("Player_StoneThrower");

        if (userChr != null && spawnedChr != null && userChr != spawnedChr)
        {
            Destroy(spawnedChr);
        }

        GameObject targetObj = (userChr != null) ? userChr : spawnedChr;
        if (targetObj != null)
        {
            character = targetObj.GetComponent<StoneThrowerCharacter>();
            if (character == null) character = targetObj.AddComponent<StoneThrowerCharacter>();
        }

        if (character == null)
        {
            character = FindAnyObjectByType<StoneThrowerCharacter>();
        }

        if (character == null)
        {
            // 씬 내에 사용자가 꺼내둔 캐릭터 (Animator 보유 오브젝트) 자동 탐색
            Animator[] allAnimators = FindObjectsByType<Animator>(FindObjectsInactive.Exclude);
            foreach (var anim in allAnimators)
            {
                if (anim.GetComponent<Camera>() == null && anim.GetComponent<Canvas>() == null)
                {
                    character = anim.gameObject.GetComponent<StoneThrowerCharacter>();
                    if (character == null)
                    {
                        character = anim.gameObject.AddComponent<StoneThrowerCharacter>();
                        Debug.Log($"✅ [GameController] 씬 내의 Animator 오브젝트('{anim.gameObject.name}')에 StoneThrowerCharacter 자동 부착 완료!");
                    }
                    break;
                }
            }
        }

        // 🌟 2. 씬에 캐릭터가 없으면 Thrower_001.prefab 최우선 인스턴스화
        if (character == null)
        {
            GameObject charPrefab = Resources.Load<GameObject>("Thrower_001");
#if UNITY_EDITOR
            if (charPrefab == null)
            {
                charPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/3D/prefab/Thrower_001.prefab");
            }
            if (charPrefab == null)
            {
                charPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/3D/Character/Test_Chr.fbx");
            }
#endif
            if (charPrefab != null)
            {
                GameObject charObj = Instantiate(charPrefab);
                charObj.name = "Thrower_001";
                charObj.transform.position = new Vector3(0f, 0.9f, -3.8f);
                character = charObj.GetComponent<StoneThrowerCharacter>();
                if (character == null) character = charObj.AddComponent<StoneThrowerCharacter>();
                Debug.Log("✅ [GameController] Thrower_001 프리팹을 성공적으로 인스턴스화하여 배치했습니다!");
            }
        }

        // 🌟 3. 배경 (BG_01) 런타임 보장
        GameObject bgObj = GameObject.Find("BG_01");
        if (bgObj == null)
        {
            GameObject bgPrefab = Resources.Load<GameObject>("BG_01");
#if UNITY_EDITOR
            if (bgPrefab == null)
            {
                bgPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/3D/prefab/BG_01.prefab");
            }
#endif
            if (bgPrefab != null)
            {
                bgObj = Instantiate(bgPrefab);
                bgObj.name = "BG_01";
                bgObj.transform.position = Vector3.zero;
                bgObj.transform.rotation = Quaternion.identity;
                Debug.Log("✅ [GameController] Resources에서 BG_01 배경을 성공적으로 인스턴스화했습니다!");
            }
        }

        // 🌟 4. 조약돌 (SkippingStone) 런타임 보장
        if (stone == null)
        {
            stone = FindAnyObjectByType<SkippingStone>();
            if (stone == null)
            {
                GameObject stonePrefab = Resources.Load<GameObject>("Stone");
                GameObject sObj = (stonePrefab != null) ? Instantiate(stonePrefab) : new GameObject("Stone");
                sObj.name = "Stone";
                stone = sObj.GetComponent<SkippingStone>() ?? sObj.AddComponent<SkippingStone>();
                Debug.Log("✅ [GameController] Resources에서 Stone 오브젝트를 성공적으로 인스턴스화했습니다!");
            }
        }

        // 🌟 5. 카메라 리그 (DualCameraSetup) 런타임 보장
        if (dualCamera == null)
        {
            dualCamera = FindAnyObjectByType<DualCameraSetup>();
            if (dualCamera == null)
            {
                GameObject rig = new GameObject("DualCameraRig");
                dualCamera = rig.AddComponent<DualCameraSetup>();
            }
        }

        if (character != null)
        {
            character.InitializeCharacter();
            if (stone != null)
            {
                character.AttachStone(stone);
                stone.OnSkipBounced -= HandleSkipBounced;
                stone.OnSkipBounced += HandleSkipBounced;
                stone.OnStoneSunk -= HandleStoneSunk;
                stone.OnStoneSunk += HandleStoneSunk;
            }
            if (dualCamera != null)
            {
                dualCamera.targetCharacter = character.transform;
                if (stone != null) dualCamera.targetStone = stone.transform;
                dualCamera.SetCameraMode(DualCameraSetup.CameraMode.TopDownPosition);
                dualCamera.SnapCameraImmediate();
            }
        }

        // 🌟 6. 강 엔티티 스포너 보장 및 초기화
        RiverSpawner spawner = FindAnyObjectByType<RiverSpawner>();
        if (spawner == null)
        {
            GameObject spawnerObj = new GameObject("RiverEntitiesSpawner");
            spawner = spawnerObj.AddComponent<RiverSpawner>();
        }
        if (spawner != null && character != null)
        {
            spawner.startBankPos = character.basePosition;
            spawner.spawnDirection = character.baseRotation * Vector3.forward;
            spawner.GenerateRiverEntitiesForMode(currentMode);
        }
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

    private void Update()
    {
        if (character == null) EnsureCharacterReady();
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
                // 🌟 투구 애니메이션(55프레임/1.8초) 중에는 수면 탭을 완전히 차단하여 오작동 방지
                break;
            case GameState.Flying:
                UpdateFlying();
                break;
            case GameState.Result:
                // 결과창 확인 후 명시적 R키/Space 키보드 입력 시 재시작 (화면 탭은 UI '다시하기' 버튼으로 처리)
                if (Time.time - lastStateChangeTime > 0.7f && (IsKeyTriggered(KeyCode.R) || IsKeyTriggered(KeyCode.Space)))
                {
                    RestartGame();
                }
                break;
        }
    }

    [Header("0단계: PP(물가 발판) 이동 및 맵 슬라이드")]
    public float minPositionX = -230f;
    public float maxPositionX = 275f;
    public float dragSensitivity = 0.045f;

    private bool isDraggingMap = false;
    private Vector2 prevDragPos;
    private float dragTotalDistance = 0f;

    private void UpdatePositioning()
    {
        if (currentMode == GameMode.LongDistance)
        {
            // 🏆 장거리 모드: 나무 발판(Lakeside_WoodenPier)의 실제 콜라이더/스케일 너비 자동 반영
            GameObject pierObj = GetWoodenPier();
            if (pierObj != null)
            {
                var bc = pierObj.GetComponent<BoxCollider>();
                float halfW = (bc != null && bc.bounds.extents.x > 1f) ? (bc.bounds.extents.x * 0.85f) : (pierObj.transform.lossyScale.x * 0.45f);
                halfW = Mathf.Clamp(halfW, 4f, 40f);
                minPositionX = -halfW;
                maxPositionX = halfW;
            }
            else
            {
                minPositionX = -12.0f;
                maxPositionX = 12.0f;
            }

            // A / D 및 ◀ / ▶ 연속 이동
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
            // 🎯 타겟 모드: 좌/우 스와이프 드래그 & A/D로 포인트 1칸씩 스냅 이동
            if (IsKeyTriggered(KeyCode.LeftArrow) || IsKeyTriggered(KeyCode.A))
            {
                if (character != null) character.MoveToPreviousWaypoint();
            }
            if (IsKeyTriggered(KeyCode.RightArrow) || IsKeyTriggered(KeyCode.D))
            {
                if (character != null) character.MoveToNextWaypoint();
            }

            HandleTargetSwipeStep();

            if (MapPIPManager.Instance != null)
            {
                MapPIPManager.Instance.UpdatePIPState(true);
            }

            if (character != null)
            {
                character.UpdatePositioning(startPosX, 0f);
                if (stone != null) stone.transform.position = character.GetHandPosition();
            }
        }

        // 0단계에서는 하단 UI 버튼 클릭(또는 키보드 Space/Enter)으로 1단계로 진행
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
        var touch = UnityEngine.InputSystem.Touchscreen.current;
        var mouse = UnityEngine.InputSystem.Mouse.current;

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
        var touch = UnityEngine.InputSystem.Touchscreen.current;
        var mouse = UnityEngine.InputSystem.Mouse.current;

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
                    // 우측으로 스와이프 드래그 ➔ 다음 포인트 1칸 스냅 이동
                    if (character != null) character.MoveToNextWaypoint();
                    swipeStartPos = curPos;
                }
                else if (dx < -swipeThreshold)
                {
                    // 좌측으로 스와이프 드래그 ➔ 이전 포인트 1칸 스냅 이동
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

    private void HandleMapDragSlide()
    {
        Vector2 curPos = Vector2.zero;
        bool isPressed = false;

#if ENABLE_INPUT_SYSTEM
        var touch = UnityEngine.InputSystem.Touchscreen.current;
        var mouse = UnityEngine.InputSystem.Mouse.current;

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
            // 상단 HUD 및 하단 버튼 영역을 제외한 중앙 물 수면 터치/클릭 시 맵 슬라이드 동작
            if (!isDraggingMap)
            {
                if (curPos.y > Screen.height * 0.16f && curPos.y < Screen.height * 0.88f)
                {
                    isDraggingMap = true;
                    prevDragPos = curPos;
                    dragTotalDistance = 0f;
                }
            }
            else
            {
                Vector2 delta = curPos - prevDragPos;
                dragTotalDistance += delta.magnitude;

                float totalSpan = Mathf.Max(10f, maxPositionX - minPositionX);
                // 화면 스크린 가로를 드래그할 때 전체 PP 라인의 전체 범위를 시원하게 탐색할 수 있도록 감도 스케일링
                float dynamicSensitivity = (totalSpan / Mathf.Max(320f, (float)Screen.width)) * 1.5f;
                float moveAmount = delta.x * dynamicSensitivity;
                startPosX = Mathf.Clamp(startPosX + moveAmount, minPositionX, maxPositionX);

                prevDragPos = curPos;
            }
        }
        else
        {
            if (isDraggingMap)
            {
                isDraggingMap = false;
            }
        }
    }

    public bool requireTouchRelease = false;

    public void ConfirmPosition()
    {
        currentState = GameState.AimingAngle;
        lastStateChangeTime = Time.time;
        requireTouchRelease = true;
        aimGaugeValue = 0f;
        aimDirection = 1f;

        // 위치 확정 완료: 상단 1/4 MAP_Camera PIP 창 자동 종료
        if (MapPIPManager.Instance != null)
        {
            MapPIPManager.Instance.UpdatePIPState(false);
        }

        if (character != null)
        {
            character.UpdateAiming(0f);
        }

        if (dualCamera != null)
        {
            dualCamera.SetCameraMode(DualCameraSetup.CameraMode.ShoulderAim);
        }
    }

    private void UpdateAimingAngle()
    {
        aimGaugeValue += aimDirection * aimSpeed * Time.deltaTime;
        if (aimGaugeValue > 1f) { aimGaugeValue = 1f; aimDirection = -1f; }
        else if (aimGaugeValue < -1f) { aimGaugeValue = -1f; aimDirection = 1f; }

        if (character != null)
        {
            character.UpdateAiming(aimGaugeValue);
            if (stone != null)
            {
                stone.transform.position = character.GetHandPosition();
            }
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
            if (stone != null)
            {
                stone.transform.position = character.GetHandPosition();
            }
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
                    // 🌟 45프레임: 55f 발사 앵커 위치를 기준으로 카메라 완만 선행 가속 시작!
                    if (dualCamera != null)
                    {
                        dualCamera.StartLaunchLeadIn(anchorPos, forwardDir);
                    }
                },
                onReleaseCallback: () =>
                {
                    // 🌟 55프레임: 비행 상태 전환 및 물리 발사!
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

        // 1단계 조준 게이지(aimGaugeValue)에서 선택된 방향으로 캐릭터를 틀고 해당 방향으로 투구 발사!
        float angleDegrees = aimGaugeValue * 25f;
        Vector3 baseForward = (character != null) ? (character.baseRotation * Vector3.forward) : Vector3.right;
        Vector3 direction = Quaternion.Euler(0f, angleDegrees, 0f) * baseForward;
        if (character != null)
        {
            character.currentAimRotation = Quaternion.Euler(0f, angleDegrees, 0f) * character.baseRotation;
            character.transform.rotation = character.currentAimRotation;
            direction = character.currentAimRotation * Vector3.forward;
        }

        float stoneMultiplier = (StoneInventory.Instance != null) ? StoneInventory.Instance.GetCurrentStone().forwardPowerMultiplier : 1.0f;
        float finalPowerMultiplier = Mathf.Lerp(0.6f, 1.4f, powerGaugeValue) * stoneMultiplier;

        // 🌟 55프레임 발사 순간: 기존 비행 돌 완전 파괴 및 손 위치에서 신규 조약돌 인스턴스 생성 (포탄 투사체 방식)
        Vector3 spawnPos = (character != null) ? character.GetHandPosition() : transform.position + new Vector3(0.35f, 1.2f, 0.8f);
        Quaternion spawnRot = Quaternion.LookRotation(direction, Vector3.up);

        if (stone != null)
        {
            stone.OnSkipBounced -= HandleSkipBounced;
            stone.OnStoneSunk -= HandleStoneSunk;
            if (Application.isPlaying) Destroy(stone.gameObject);
            else DestroyImmediate(stone.gameObject);
            stone = null;
        }

        GameObject prefabToSpawn = Resources.Load<GameObject>("Stone");
#if UNITY_EDITOR
        if (prefabToSpawn == null) prefabToSpawn = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/3D/prefab/Stone.prefab");
#endif
        GameObject newStoneObj = (prefabToSpawn != null) ? Instantiate(prefabToSpawn, spawnPos, spawnRot) : new GameObject("Stone");
        newStoneObj.name = "Stone";
        if (prefabToSpawn == null)
        {
            newStoneObj.transform.position = spawnPos;
            newStoneObj.transform.rotation = spawnRot;
        }

        stone = newStoneObj.GetComponent<SkippingStone>() ?? newStoneObj.AddComponent<SkippingStone>();

        // 이벤트 콜백 연결
        stone.OnSkipBounced += HandleSkipBounced;
        stone.OnStoneSunk += HandleStoneSunk;

        // 🌟 카메라 및 리플레이 매니저 타깃 100% 동기화
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
        // 1. ESC 키 입력 시 윈도우 스탠드얼론 즉시 안전 종료
        if (IsKeyTriggered(KeyCode.Escape))
        {
            Application.Quit();
        }

        // 2. 키보드 스티어링 (A / D 또는 Left / Right 누른 상태로 탭 시 조향)
        float hInput = GetHorizontalInput();
        float keySteer = 0f;
        if (hInput < -0.1f) keySteer = -3f;
        else if (hInput > 0.1f) keySteer = 3f;

        if (IsKeyTriggered(KeyCode.Space) || IsKeyTriggered(KeyCode.Return))
        {
            EvaluateRhythmTiming(keySteer);
            return;
        }

        // 🌟 실시간 비거리에 따른 낮 -> 노을 -> 밤 4단계 동적 환경 변화
        if (LakeEnvironmentManager.Instance != null && stone != null)
        {
            LakeEnvironmentManager.Instance.UpdateEnvironmentByDistance(stone.totalDistance);
        }

        // 3. 터치 및 마우스 플릭 스티어링 (터치 스와이프 조향)
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
            else if (Input.GetMouseButtonDown(0))
            {
                isDown = true; curPos = Input.mousePosition;
            }
            else if (Input.GetMouseButtonUp(0))
            {
                isUp = true; curPos = Input.mousePosition;
            }
        }
        catch { }
#endif

        if (isDown)
        {
            flightTouchStartPos = curPos;
            flightTouchStartTime = Time.time;
            isTrackingFlightTouch = true;

            // 터치 시작 순간 즉시 기본 탭(0도) 판정
            EvaluateRhythmTiming(0f);
        }
        else if (isUp && isTrackingFlightTouch)
        {
            isTrackingFlightTouch = false;
            float deltaX = curPos.x - flightTouchStartPos.x;
            float duration = Time.time - flightTouchStartTime;

            // 0.35초 이내에 좌/우 25px 이상 플릭한 경우 즉시 추가 각도 조향(±3°) 적용!
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

    public void TriggerFlightTap()
    {
        if (currentState == GameState.Flying)
        {
            EvaluateRhythmTiming(0f);
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
    public string lastGameOverReason = "수면 침몰";
    private bool hasCalculatedResult = false;

    public void TriggerFishSnipeEffect(string speciesName)
    {
        fishSnipeCount++;
        bannerNotificationText = $"🎯 FISH SNIPE! [{speciesName}] 저격 성공! (+1,000점 & 코인)";
        lastTimingText = "🔥 FISH SNIPE! 🔥";

        // 슬로우모션 연출 (0.25초간 0.4배속)
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

    public void TriggerBoostPadEffect()
    {
        boostPadCount++;
    }

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
        if (text.Contains("PERFECT"))
        {
            perfectTimingCount++;
        }

        StopCoroutine(nameof(ClearTimingTextAfterDelay));
        StartCoroutine(ClearTimingTextAfterDelay(0.8f));
    }

    private void HandleStoneSunk(float dist)
    {
        StartCoroutine(DelayedShowResultRoutine(dist, 1.5f));
    }

    private IEnumerator DelayedShowResultRoutine(float dist, float delay)
    {
        // 🌟 1.5초 대기 (돌 침몰/충돌 착지 연출 감상)
        yield return new WaitForSeconds(delay);

        // 🌟 이미 리플레이 또는 결과창이 시작되었으면 중복 실행 원천 차단!
        if (currentState == GameState.Replay || currentState == GameState.Result) yield break;
        if (topDownReplay != null && (topDownReplay.isReplayActive || topDownReplay.isDrawing)) yield break;

        // 🌟 1.5초 후 직교 탑다운 궤적 맵 리플레이로 먼저 전환!
        currentState = GameState.Replay;
        lastStateChangeTime = Time.time;
        requireTouchRelease = true; // 🌟 리플레이 진입 시 터치 릴리즈 락 적용

        if (topDownReplay == null) topDownReplay = FindAnyObjectByType<TopDownReplayManager>();
        if (topDownReplay == null)
        {
            GameObject rObj = new GameObject("TopDownReplayManager");
            topDownReplay = rObj.AddComponent<TopDownReplayManager>();
        }

        topDownReplay.isFromFlightTest = false; topDownReplay.StartReplay(dist);
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
        requireTouchRelease = true; // 🌟 결과창 진입 시 터치 릴리즈 락 적용
        CalculateFinalScores(dist);
    }

    public void CalculateFinalScores(float dist)
    {
        if (hasCalculatedResult) return;
        hasCalculatedResult = true;

        // 1. 도달거리 점수 (1m당 10점)
        distanceScore = Mathf.RoundToInt(dist * 10f);

        // 2. 튕긴 횟수에 따른 점수 (1회당 500점)
        int skips = (stone != null) ? stone.skipCount : 0;
        skipScore = skips * 500;

        // 3. 특별 이벤트 점수
        // (PERFECT 타이밍당 300점 + 물고기 저격당 1000점 + 친구 추월당 800점 + 부스트 패드당 500점 + 도로록 스키밍 1m당 15점)
        lastSkimBonusDist = (stone != null) ? stone.skimDistance : 0f;
        int skimScore = Mathf.RoundToInt(lastSkimBonusDist * 15f);
        specialScore = (perfectTimingCount * 300) + (fishSnipeCount * 1000) + (friendOvertakeCount * 800) + (boostPadCount * 500) + skimScore;

        // 종합 점수 및 코인 보상 계산
        totalScore = distanceScore + skipScore + specialScore;
        earnedCoins = Mathf.Max(5, Mathf.RoundToInt(totalScore / 25f));

        if (AquariumManager.Instance != null)
        {
            AquariumManager.Instance.AddCoins(earnedCoins);
        }

        Debug.Log($"📊 [결과 집계 완료] 도달거리: {dist:F1}m({distanceScore}점) | 바운스: {skips}회({skipScore}점) | 스키밍 보너스: +{lastSkimBonusDist:F1}m({skimScore}점) | 특별이벤트: {specialScore}점 | 총점: {totalScore:N0}점 (+{earnedCoins}코인)");
    }

    public void RestartGame()
    {
        ResetToPositioning();
    }

    private void ResetToPositioning()
    {
        StopAllCoroutines();
        if (topDownReplay != null)
        {
            topDownReplay.isReplayActive = false;
        }
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

        if (LakeEnvironmentManager.Instance != null)
        {
            LakeEnvironmentManager.Instance.ResetEnvironment();
        }
        distanceScore = 0;
        skipScore = 0;
        specialScore = 0;
        totalScore = 0;
        earnedCoins = 0;
        hasCalculatedResult = false;

        if (dualCamera != null)
        {
            dualCamera.SetCameraMode(DualCameraSetup.CameraMode.TopDownPosition);
        }

        if (currentMode == GameMode.TargetAccuracy)
        {
            if (MapPIPManager.Instance != null)
            {
                MapPIPManager.Instance.UpdatePIPState(true);
            }
        }
        else
        {
            if (MapPIPManager.Instance != null)
            {
                MapPIPManager.Instance.UpdatePIPState(false);
            }
        }

        requireTouchRelease = true;
        isTrackingFlightTouch = false;

        ApplyGameModeEnvironment();

        // 🌟 매판 시작 시 지난 게임의 돌을 완전 파괴하고 새 돌을 깨끗하게 스폰!
        SpawnNewStone();
    }

    /// <summary>
    /// 🌟 매판 클린 스타트: 기존 돌을 완전 파괴(Destroy)하고 깨끗한 새 돌을 인스턴스화
    /// </summary>
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

    #region 범용 입력 처리

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
            if (!isCurrentlyHeld)
            {
                requireTouchRelease = false;
            }
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
