using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SkippingStones.Data;

namespace SkippingStones.Visuals
{
    /// <summary>
    /// 로비 3D 캐릭터 선택 및 쇼케이스 컨트롤러
    /// - StoneThrowerCharacter 스크립트가 붙은 프리팹을 자동으로 스캔/등록
    /// - Staging_Position 더미를 중심으로 좌/우 화면 밖 ↔ 중앙 진입/퇴장 (Walk/Slide 보간)
    /// - 중앙 안착 시 마우스/터치 드래그로 360도 자유 회전 감상
    /// - GameDataManager 캐릭터 카탈로그 및 유저 선택 캐릭터 ID 동기화
    /// </summary>
    public class LobbyCharacterShowcaseController : MonoBehaviour
    {
        [Header("하이어라키 참조 (카메라 & 스테이징)")]
        [SerializeField] private Camera targetCamera;          // 로비 뷰 카메라 (직접 할당 또는 자동 검색)
        [SerializeField] private Transform stagingPosition; // Staging_Position 포함된 더미

        [Header("캐릭터 프리팹 목록 (자동 스캔)")]
        [SerializeField] private List<GameObject> characterPrefabs = new List<GameObject>();

        [Header("트랜지션 및 회전 설정 (캐주얼 슬라이드 & 발판)")]
        [SerializeField] private float slideExitDuration = 0.22f;   // 퇴장 슬라이드 시간 (0.22초)
        [SerializeField] private float slideEnterDuration = 0.28f;  // 등장 슬라이드 시간 (0.28초)
        [SerializeField] private float slideDistance = 3.2f;        // 화면 밖 슬라이드 이동 거리 (3.2m)
        [SerializeField] private float entryAngleOffset = 55f;      // 진입/퇴장 각도 보정 (55도)
        [SerializeField] private float rotationSensitivity = 0.4f;  // 드래그 회전 감도

        [Header("상태 모니터링")]
        [SerializeField] private int currentCharacterIndex = 0;
        [SerializeField] private bool isTransitioning = false;

        private GameObject currentSpawnedRoot; // 캐릭터 + 스탠드를 담고 있는 컨테이너 또는 루트
        private GameObject currentSpawnedCharacter;
        private Animator currentAnimator;

        // 드래그 조작
        private Vector2 lastPointerPos;
        private bool isDragging = false;
        private float currentModelRotationY = 0f;

        // 이벤트: UI(스탯창/이름) 갱신용
        public event Action<int, GameObject, CharacterInfoData> OnCharacterChanged;

        private void Awake()
        {
            FindStagingPosition();
            ScanCharacterPrefabs();
        }

        private void Start()
        {
            InitializeShowcase();
        }

        private void OnEnable()
        {
            if (characterPrefabs.Count > 0 && currentSpawnedCharacter == null)
            {
                InitializeShowcase();
            }
        }

        public void InitializeShowcase()
        {
            FindStagingPosition();
            ScanCharacterPrefabs();

            if (characterPrefabs.Count == 0) return;

            // 유저가 이전에 선택해둔 캐릭터(selectedCharacterId) 동기화
            int targetIndex = 0;
            var dm = GameDataManager.Instance;
            if (dm != null && dm.UserData != null && !string.IsNullOrEmpty(dm.UserData.selectedCharacterId))
            {
                string savedId = dm.UserData.selectedCharacterId;
                for (int i = 0; i < characterPrefabs.Count; i++)
                {
                    if (characterPrefabs[i] == null) continue;
                    string pName = characterPrefabs[i].name;
                    if (pName.Equals(savedId, StringComparison.OrdinalIgnoreCase))
                    {
                        targetIndex = i;
                        break;
                    }

                    // 카탈로그 ID 또는 프리팹 경로와의 일치 검사
                    var catItem = dm.characterCatalog?.Find(c => c.id.Equals(savedId, StringComparison.OrdinalIgnoreCase));
                    if (catItem != null && !string.IsNullOrEmpty(catItem.prefabPath) && catItem.prefabPath.Contains(pName))
                    {
                        targetIndex = i;
                        break;
                    }
                }
            }

            currentCharacterIndex = targetIndex;
            SpawnCharacterInstant(currentCharacterIndex);
        }

        /// <summary>
        /// 계층 구조 내 Staging_Position 및 로비 뷰 카메라 자동 탐색
        /// </summary>
        public void FindStagingPosition()
        {
            if (targetCamera == null || !targetCamera.gameObject.activeInHierarchy)
            {
                Camera[] allCams = FindObjectsByType<Camera>(FindObjectsInactive.Exclude);
                foreach (var cam in allCams)
                {
                    if (cam.gameObject.activeInHierarchy && (cam.name.Contains("Lobby") || cam.name.Contains("Camera001") || cam.name.Contains("Select")))
                    {
                        targetCamera = cam;
                        break;
                    }
                }
                if (targetCamera == null) targetCamera = Camera.main;
            }

            if (stagingPosition != null && stagingPosition.gameObject.scene.name != null) return;

            Transform[] allChildren = GetComponentsInChildren<Transform>(true);
            foreach (var t in allChildren)
            {
                if (t == null) continue;
                string nameUpper = t.name.ToUpperInvariant();
                if (nameUpper.Contains("STAGING_POSITION") || nameUpper.Contains("STAGINGPOSITION") || nameUpper.Contains("CHARACTER_STAGE"))
                {
                    stagingPosition = t;
                    break;
                }
            }

            // 폴백: 못 찾으면 로비 루트 위치 사용
            if (stagingPosition == null)
            {
                stagingPosition = transform;
            }
        }

        /// <summary>
        /// 로비 뷰 카메라(targetCamera)를 정면으로 바라보는 기준 회전 반환
        /// </summary>
        public Quaternion GetCameraFacingRotation(Vector3 centerPos)
        {
            FindStagingPosition();
            if (targetCamera != null)
            {
                Vector3 toCam = (targetCamera.transform.position - centerPos);
                toCam.y = 0f;
                if (toCam.sqrMagnitude > 0.001f)
                {
                    return Quaternion.LookRotation(toCam.normalized, Vector3.up);
                }
            }
            return stagingPosition != null ? stagingPosition.rotation : transform.rotation;
        }

        /// <summary>
        /// GameDataManager의 characterCatalog에서 해금(isUnlocked == true)된 캐릭터 프리팹을 정식 로드
        /// </summary>
        public void ScanCharacterPrefabs()
        {
            characterPrefabs.Clear();

            var dm = GameDataManager.Instance;
            if (dm != null && dm.characterCatalog != null && dm.characterCatalog.Count > 0)
            {
                foreach (var info in dm.characterCatalog)
                {
                    // 해금 여부 체크 (UserData or Catalog isUnlocked)
                    bool isUnlocked = info.isUnlocked;
                    if (dm.UserData != null && dm.UserData.unlockedCharacterIds != null)
                    {
                        if (dm.UserData.unlockedCharacterIds.Contains(info.id))
                        {
                            isUnlocked = true;
                        }
                    }

                    if (!isUnlocked) continue;

                    GameObject loadedPrefab = null;
#if UNITY_EDITOR
                    if (!string.IsNullOrEmpty(info.prefabPath))
                    {
                        loadedPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(info.prefabPath);
                    }

                    // 경로로 못 찾았을 경우 에셋 전체에서 검색 폴백
                    if (loadedPrefab == null)
                    {
                        string[] guids = UnityEditor.AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/prefab/Character", "Assets" });
                        foreach (string guid in guids)
                        {
                            string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                            var p = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path);
                            if (p != null && p.GetComponentInChildren<StoneThrowerCharacter>(true) != null)
                            {
                                if (p.name.Equals(info.id, StringComparison.OrdinalIgnoreCase) ||
                                    (!string.IsNullOrEmpty(info.prefabPath) && info.prefabPath.EndsWith(p.name + ".prefab", StringComparison.OrdinalIgnoreCase)))
                                {
                                    loadedPrefab = p;
                                    break;
                                }
                            }
                        }
                    }
#else
                    if (!string.IsNullOrEmpty(info.prefabPath))
                    {
                        string rPath = info.prefabPath;
                        if (rPath.StartsWith("Assets/prefab/")) rPath = rPath.Substring("Assets/prefab/".Length);
                        if (rPath.EndsWith(".prefab")) rPath = rPath.Substring(0, rPath.Length - ".prefab".Length);
                        loadedPrefab = Resources.Load<GameObject>(rPath);

                        if (loadedPrefab == null)
                        {
                            string fileName = System.IO.Path.GetFileNameWithoutExtension(info.prefabPath);
                            loadedPrefab = Resources.Load<GameObject>(fileName) ?? Resources.Load<GameObject>($"Character/{fileName}");
                        }
                    }
#endif
                    if (loadedPrefab != null && !characterPrefabs.Contains(loadedPrefab))
                    {
                        characterPrefabs.Add(loadedPrefab);
                    }
                }
            }

            // 만약 아무것도 못 찾았을 경우 에셋 폴더 내 기본 캐릭터 폴백 등록
            if (characterPrefabs.Count == 0)
            {
#if UNITY_EDITOR
                string[] guids = UnityEditor.AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/prefab/Character", "Assets/Resources/Character" });
                foreach (string guid in guids)
                {
                    string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                    var p = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    if (p != null && p.GetComponentInChildren<StoneThrowerCharacter>(true) != null)
                    {
                        if (!characterPrefabs.Contains(p)) characterPrefabs.Add(p);
                    }
                }
#endif
            }
        }

        private void Update()
        {
            // 인게임 및 타이틀 화면에서는 쇼케이스 캐릭터를 안전하게 숨김 처리 (로비 또는 모달 상태에서만 표시)
            if (SkippingStones.UI.MetaUIManager.Instance != null)
            {
                var screen = SkippingStones.UI.MetaUIManager.Instance.currentScreen;
                bool shouldShow = (screen == SkippingStones.UI.MetaScreen.Lobby);

                if (!shouldShow && currentSpawnedRoot != null && currentSpawnedRoot.activeSelf)
                {
                    currentSpawnedRoot.SetActive(false);
                }
                else if (shouldShow && currentSpawnedRoot != null && !currentSpawnedRoot.activeSelf)
                {
                    currentSpawnedRoot.SetActive(true);
                }
            }

            HandleDragRotation();
        }

        /// <summary>
        /// 캐릭터 360도 드래그 회전 처리 (캐릭터 또는 발판 콜라이더를 터치했을 때 활성화)
        /// </summary>
        private void HandleDragRotation()
        {
            if (isTransitioning || currentSpawnedRoot == null) return;

            Vector2 pointerPos = Vector2.zero;
            bool pointerDown = false;
            bool pointerUp = false;

#if ENABLE_INPUT_SYSTEM
            var mouse = UnityEngine.InputSystem.Mouse.current;
            var touch = UnityEngine.InputSystem.Touchscreen.current;
            if (mouse != null)
            {
                if (mouse.leftButton.wasPressedThisFrame) { pointerDown = true; pointerPos = mouse.position.ReadValue(); }
                else if (mouse.leftButton.isPressed) { pointerPos = mouse.position.ReadValue(); }
                else if (mouse.leftButton.wasReleasedThisFrame) { pointerUp = true; pointerPos = mouse.position.ReadValue(); }
            }
            if (!pointerDown && touch != null && touch.primaryTouch.press.isPressed)
            {
                pointerPos = touch.primaryTouch.position.ReadValue();
                if (touch.primaryTouch.press.wasPressedThisFrame) pointerDown = true;
            }
#else
            if (Input.GetMouseButtonDown(0)) { pointerDown = true; pointerPos = Input.mousePosition; }
            else if (Input.GetMouseButton(0)) { pointerPos = Input.mousePosition; }
            else if (Input.GetMouseButtonUp(0)) { pointerUp = true; pointerPos = Input.mousePosition; }
#endif

            if (pointerDown)
            {
                // Raycast로 캐릭터 또는 발판 콜라이더를 터치했는지 확인
                Camera cam = targetCamera;
                if (cam == null || !cam.gameObject.activeInHierarchy)
                {
                    cam = Camera.main;
                }

                if (cam != null)
                {
                    Ray ray = cam.ScreenPointToRay(pointerPos);
                    if (Physics.Raycast(ray, out RaycastHit hit, 50f))
                    {
                        if (hit.collider != null && (hit.collider.transform.IsChildOf(currentSpawnedRoot.transform)))
                        {
                            isDragging = true;
                            lastPointerPos = pointerPos;
                        }
                    }
                }
            }
            else if (pointerUp)
            {
                isDragging = false;
            }

            if (isDragging)
            {
                float deltaX = pointerPos.x - lastPointerPos.x;
                currentModelRotationY -= deltaX * rotationSensitivity;
                lastPointerPos = pointerPos;

                if (currentSpawnedRoot != null)
                {
                    Vector3 centerPos = stagingPosition != null ? stagingPosition.position : transform.position;
                    Quaternion baseRot = GetCameraFacingRotation(centerPos);
                    currentSpawnedRoot.transform.rotation = baseRot * Quaternion.Euler(0f, currentModelRotationY, 0f);
                }
            }
        }

        /// <summary>
        /// 다음 캐릭터로 전환 (+1)
        /// </summary>
        public void NextCharacter()
        {
            if (isTransitioning || characterPrefabs.Count <= 1) return;
            int nextIndex = (currentCharacterIndex + 1) % characterPrefabs.Count;
            StartCoroutine(TransitionRoutine(nextIndex, 1));
        }

        /// <summary>
        /// 이전 캐릭터로 전환 (-1)
        /// </summary>
        public void PreviousCharacter()
        {
            if (isTransitioning || characterPrefabs.Count <= 1) return;
            int prevIndex = (currentCharacterIndex - 1 + characterPrefabs.Count) % characterPrefabs.Count;
            StartCoroutine(TransitionRoutine(prevIndex, -1));
        }

        /// <summary>
        /// 캐릭터 선택/확인 시 환호(Select) 애니메이션 1회 재생
        /// </summary>
        public void PlaySelectAnimation()
        {
            if (currentSpawnedCharacter != null)
            {
                var anim = currentSpawnedCharacter.GetComponentInChildren<Animator>();
                if (anim != null)
                {
                    anim.SetTrigger("Select");
                }
            }
        }

        /// <summary>
        /// 캐릭터 + 발판 캐주얼 슬라이드 트랜지션 코루틴
        /// - Phase 1: 기존 캐릭터+발판이 화면 밖으로 슉 미끄러져 나감 (EaseInQuad)
        /// - Phase 2: 새 캐릭터+발판이 화면 밖에서 중앙으로 스윽 미끄러져 들어와 안착 (EaseOutCubic)
        /// - 안착 즉시 Select 트리거를 발동하여 환호 포즈를 0.5초 정지 취한 뒤 자연스럽게 Idle 복귀
        /// direction: 1 = 다음(우측으로 퇴장, 좌측에서 등장) / -1 = 이전(좌측으로 퇴장, 우측에서 등장)
        /// </summary>
        private IEnumerator TransitionRoutine(int targetIndex, int direction)
        {
            isTransitioning = true;
            GameObject oldRoot = currentSpawnedRoot;

            FindStagingPosition();
            Vector3 centerPos = stagingPosition != null ? stagingPosition.position : transform.position;
            Quaternion finalFrontRot = GetCameraFacingRotation(centerPos);

            // 로비 룸 및 카메라 쿼터뷰에 맞춘 화면상 좌우 횡이동 축 계산 (55도)
            Vector3 moveDir = (Quaternion.Euler(0f, entryAngleOffset, 0f) * transform.right).normalized;

            // ==========================================
            // [Phase 1] 기존 캐릭터+발판 퇴장 (화면 밖으로 가속 슬라이드)
            // ==========================================
            if (oldRoot != null)
            {
                Vector3 exitPos = centerPos + moveDir * (direction * slideDistance);
                float elapsedExit = 0f;

                while (elapsedExit < slideExitDuration)
                {
                    elapsedExit += Time.deltaTime;
                    float t = Mathf.Clamp01(elapsedExit / slideExitDuration);
                    // EaseInQuad: 부드럽게 출발해 빠르게 밖으로 휙 빠짐
                    float ease = t * t;

                    if (oldRoot != null)
                    {
                        oldRoot.transform.position = Vector3.Lerp(centerPos, exitPos, ease);
                    }
                    yield return null;
                }

                if (oldRoot != null)
                {
                    if (Application.isPlaying) Destroy(oldRoot);
                    else DestroyImmediate(oldRoot);
                    oldRoot = null;
                }
            }

            // ==========================================
            // [Phase 2] 새 캐릭터+발판 등장 (화면 밖에서 중앙으로 감속 슬라이드)
            // ==========================================
            GameObject newPrefab = characterPrefabs[targetIndex];
            Vector3 enterPos = centerPos - moveDir * (direction * slideDistance);

            // 새 쇼케이스 유닛(루트 + 스탠드 + 캐릭터) 생성
            GameObject newRoot = CreateShowcaseUnit(newPrefab, enterPos, finalFrontRot);
            GameObject newChr = newRoot.GetComponentInChildren<StoneThrowerCharacter>(true)?.gameObject;
            if (newChr == null)
            {
                var animRef = newRoot.GetComponentInChildren<Animator>(true);
                newChr = animRef != null ? animRef.gameObject : newRoot;
            }

            Animator newAnim = newRoot.GetComponentInChildren<Animator>();

            float elapsedEnter = 0f;
            while (elapsedEnter < slideEnterDuration)
            {
                elapsedEnter += Time.deltaTime;
                float t = Mathf.Clamp01(elapsedEnter / slideEnterDuration);
                // EaseOutCubic: 빠르게 들어와서 중앙에 착- 감기듯 멈춤
                float ease = 1f - Mathf.Pow(1f - t, 3f);

                if (newRoot != null)
                {
                    newRoot.transform.position = Vector3.Lerp(enterPos, centerPos, ease);
                }

                yield return null;
            }

            currentSpawnedRoot = newRoot;
            currentSpawnedCharacter = newChr;
            if (currentSpawnedRoot != null)
            {
                currentSpawnedRoot.transform.position = centerPos;
                currentSpawnedRoot.transform.rotation = finalFrontRot;
            }
            currentModelRotationY = 0f;

            // 중앙 도착 직후 시그니처 환호(Select) 애니메이션 1회 재생 (0.5초 포즈 유지 후 Idle 자동 전환)
            if (newAnim != null)
            {
                newAnim.speed = 1f;
                if (HasParameter(newAnim, "IsWalking")) newAnim.SetBool("IsWalking", false);
                if (HasParameter(newAnim, "Select"))
                {
                    newAnim.SetTrigger("Select");
                }
                else if (HasParameter(newAnim, "Idle"))
                {
                    newAnim.SetTrigger("Idle");
                }
            }

            currentCharacterIndex = targetIndex;
            NotifyCharacterChanged();

            isTransitioning = false;
        }

        private bool HasParameter(Animator anim, string paramName)
        {
            if (anim == null || anim.runtimeAnimatorController == null) return false;
            foreach (var p in anim.parameters)
            {
                if (p.name.Equals(paramName, StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }

        private void SpawnCharacterInstant(int index)
        {
            ClearAllShowcaseCharacters();

            if (index < 0 || index >= characterPrefabs.Count) return;

            FindStagingPosition();
            Vector3 centerPos = stagingPosition != null ? stagingPosition.position : transform.position;
            Quaternion centerRot = GetCameraFacingRotation(centerPos);

            GameObject prefab = characterPrefabs[index];
            currentSpawnedRoot = CreateShowcaseUnit(prefab, centerPos, centerRot);
            currentSpawnedCharacter = currentSpawnedRoot.GetComponentInChildren<Animator>()?.gameObject ?? currentSpawnedRoot;

            currentModelRotationY = 0f;
            NotifyCharacterChanged();
        }

        /// <summary>
        /// [Showcase_Unit] 컨테이너에 캐릭터 프리팹과 해당 캐릭터의 lobbyStandPrefab 발판을 결합하여 생성
        /// </summary>
        private GameObject CreateShowcaseUnit(GameObject chrPrefab, Vector3 worldPos, Quaternion worldRot)
        {
            GameObject unitRoot = new GameObject($"[Showcase_Unit]_{chrPrefab.name}");
            unitRoot.transform.SetParent(stagingPosition != null ? stagingPosition : transform);
            unitRoot.transform.position = worldPos;
            unitRoot.transform.rotation = worldRot;

            Transform targetAttachParent = unitRoot.transform;

            // 1. 캐릭터의 고유 발판(lobbyStandPrefab) 확인 및 생성
            var throwerSetting = chrPrefab.GetComponentInChildren<StoneThrowerCharacter>(true);
            GameObject standPrefabToSpawn = throwerSetting != null ? throwerSetting.lobbyStandPrefab : null;

            if (standPrefabToSpawn != null)
            {
                GameObject standInstance = Instantiate(standPrefabToSpawn, unitRoot.transform);
                standInstance.name = "Character_Stand";
                standInstance.transform.localPosition = Vector3.zero;
                standInstance.transform.localRotation = Quaternion.identity;

                // 발판 하위에 더미(Dummy, Point, Socket 등)가 있으면 바로 그 더미 노드를 부모로 지정
                Transform[] standChildren = standInstance.GetComponentsInChildren<Transform>(true);
                foreach (var child in standChildren)
                {
                    if (child == null || child == standInstance.transform) continue;
                    string nUpper = child.name.ToUpperInvariant();
                    if (nUpper.Contains("DUMMY") || nUpper.Contains("POINT") || nUpper.Contains("SOCKET") || nUpper.Contains("POS") || nUpper.Contains("CHAR"))
                    {
                        targetAttachParent = child;
                        break;
                    }
                }

                // 발판 콜라이더가 있다면 트리거로 설정하여 드래그 터치 영역 지원
                var standCols = standInstance.GetComponentsInChildren<Collider>();
                foreach (var col in standCols) col.isTrigger = true;
            }

            // 2. 캐릭터 생성 (발판 더미 노드 또는 유닛 루트에 결합)
            GameObject chrInstance = Instantiate(chrPrefab, targetAttachParent);
            chrInstance.name = chrPrefab.name;
            chrInstance.transform.localPosition = Vector3.zero;
            chrInstance.transform.localRotation = Quaternion.identity;

            SetupCharacterInstance(chrInstance);

            return unitRoot;
        }

        public void ClearAllShowcaseCharacters()
        {
            if (currentSpawnedRoot != null)
            {
                if (Application.isPlaying) Destroy(currentSpawnedRoot);
                else DestroyImmediate(currentSpawnedRoot);
                currentSpawnedRoot = null;
                currentSpawnedCharacter = null;
            }

            // 하위의 모든 [Showcase_Unit] 및 [Showcase_Chr] 오브젝트 일괄 정리
            var allChildren = GetComponentsInChildren<Transform>(true);
            foreach (var t in allChildren)
            {
                if (t != null && (t.gameObject.name.StartsWith("[Showcase_Unit]") || t.gameObject.name.StartsWith("[Showcase_Chr]")))
                {
                    if (Application.isPlaying) Destroy(t.gameObject);
                    else DestroyImmediate(t.gameObject);
                }
            }
        }

        private void SetupCharacterInstance(GameObject chrInstance)
        {
            if (chrInstance == null) return;
            chrInstance.name = $"[Showcase_Chr]_{chrInstance.name}";

            // 쇼케이스용으로 안전 처리: StoneThrowerCharacter 컴포넌트를 즉시 제거
            var throwers = chrInstance.GetComponentsInChildren<StoneThrowerCharacter>(true);
            foreach (var th in throwers)
            {
                if (th != null)
                {
                    if (Application.isPlaying) Destroy(th);
                    else DestroyImmediate(th);
                }
            }

            // 애니메이터 정상화
            var anim = chrInstance.GetComponentInChildren<Animator>(true);
            if (anim != null)
            {
                anim.enabled = true;
                anim.speed = 1f;
                anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            }

            // 물리 연산(밀침/충돌)은 끄되, 레이캐스트 터치 감지를 위해 콜라이더는 Trigger로 유지
            var colliders = chrInstance.GetComponentsInChildren<Collider>();
            foreach (var col in colliders)
            {
                col.isTrigger = true;
                col.enabled = true;
            }

            var rbs = chrInstance.GetComponentsInChildren<Rigidbody>();
            foreach (var rb in rbs) rb.isKinematic = true;
        }

        private void NotifyCharacterChanged()
        {
            if (currentCharacterIndex < 0 || currentCharacterIndex >= characterPrefabs.Count) return;

            GameObject currentPrefab = characterPrefabs[currentCharacterIndex];
            CharacterInfoData catalogData = null;

            var dm = GameDataManager.Instance;
            if (dm != null)
            {
                // 유저 세이브 데이터 동기화
                dm.UserData.selectedCharacterId = currentPrefab.name;
                dm.SaveUserData();

                // 카탈로그 데이터 검색
                if (dm.characterCatalog != null)
                {
                    catalogData = dm.characterCatalog.Find(c => c.id.Equals(currentPrefab.name, StringComparison.OrdinalIgnoreCase) || currentPrefab.name.Contains(c.id));
                }
            }

            OnCharacterChanged?.Invoke(currentCharacterIndex, currentPrefab, catalogData);
        }
    }
}
