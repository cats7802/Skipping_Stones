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

        [Header("트랜지션 및 회전 설정 (보폭 23.2cm 물리 싱크)")]
        [SerializeField] private float transitionDuration = 1.4f;   // 3걸음 진입 시간 (1.4초)
        [SerializeField] private float entryOffsetDistance = 0.70f; // 보폭 기준 3걸음 실제 물리 이동 거리 (0.70m)
        [SerializeField] private float entryAngleOffset = 55f;      // 진입/퇴장 각도 보정 (55도)
        [SerializeField] private float rotationSensitivity = 0.4f;  // 드래그 회전 감도

        [Header("상태 모니터링")]
        [SerializeField] private int currentCharacterIndex = 0;
        [SerializeField] private bool isTransitioning = false;

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

                if (!shouldShow && currentSpawnedCharacter != null && currentSpawnedCharacter.activeSelf)
                {
                    currentSpawnedCharacter.SetActive(false);
                }
                else if (shouldShow && currentSpawnedCharacter != null && !currentSpawnedCharacter.activeSelf)
                {
                    currentSpawnedCharacter.SetActive(true);
                }
            }

            HandleDragRotation();
        }

        /// <summary>
        /// 캐릭터 360도 드래그 회전 처리 (캐릭터 콜라이더를 직접 터치했을 때만 활성화)
        /// </summary>
        private void HandleDragRotation()
        {
            if (isTransitioning || currentSpawnedCharacter == null) return;

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
                // Raycast로 캐릭터 본체 콜라이더를 터치했는지 확인
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
                        if (hit.collider != null && hit.collider.transform.IsChildOf(currentSpawnedCharacter.transform))
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

                if (currentSpawnedCharacter != null)
                {
                    Vector3 centerPos = stagingPosition != null ? stagingPosition.position : transform.position;
                    Quaternion baseRot = GetCameraFacingRotation(centerPos);
                    currentSpawnedCharacter.transform.rotation = baseRot * Quaternion.Euler(0f, currentModelRotationY, 0f);
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
        /// 캐릭터 슬라이드/걸어나오기 트랜지션 코루틴 (캐릭터별 독립 보폭/속도 동적 적용)
        /// direction: 1 = 다음(오른쪽으로 퇴장, 왼쪽에서 등장) / -1 = 이전
        /// </summary>
        private IEnumerator TransitionRoutine(int targetIndex, int direction)
        {
            isTransitioning = true;
            GameObject oldChr = currentSpawnedCharacter;

            FindStagingPosition();
            Vector3 centerPos = stagingPosition != null ? stagingPosition.position : transform.position;
            Quaternion finalFrontRot = GetCameraFacingRotation(centerPos);

            // 🌟 새 캐릭터 프리팹에서 캐릭터별 고유 쇼케이스 세팅 읽기
            GameObject newPrefab = characterPrefabs[targetIndex];
            var newThrowerSetting = newPrefab.GetComponentInChildren<StoneThrowerCharacter>(true);

            float curDuration = (newThrowerSetting != null) ? newThrowerSetting.showcaseDuration : transitionDuration;
            float curDistance = (newThrowerSetting != null) ? newThrowerSetting.showcaseDistance : entryOffsetDistance;
            float curWalkSpeed = (newThrowerSetting != null) ? newThrowerSetting.showcaseWalkSpeed : 1.45f;
            bool curHasWalk = (newThrowerSetting != null) ? newThrowerSetting.hasWalkAnimation : true;

            // 로비 룸 및 카메라 쿼터뷰에 맞춘 화면상 좌우 횡이동 축 계산 (기본 55도 적용)
            Vector3 moveDir = (Quaternion.Euler(0f, entryAngleOffset, 0f) * transform.right).normalized;

            Vector3 exitPos = centerPos + moveDir * (direction * curDistance);
            Vector3 enterPos = centerPos - moveDir * (direction * curDistance);

            // 진입 시 캐릭터가 바라볼 방향 (중앙을 향해 걸어오는 방향)
            Vector3 enterLookDir = (centerPos - enterPos).normalized;
            Quaternion enterWalkRot = enterLookDir != Vector3.zero ? Quaternion.LookRotation(enterLookDir, Vector3.up) : finalFrontRot;

            // 퇴장 시 캐릭터가 바라볼 방향 (화면 밖으로 나가는 방향)
            Vector3 exitLookDir = (exitPos - centerPos).normalized;
            Quaternion exitWalkRot = exitLookDir != Vector3.zero ? Quaternion.LookRotation(exitLookDir, Vector3.up) : finalFrontRot;

            // 새 캐릭터 스폰 (화면 밖, 걸어오는 방향을 바라보며 시작)
            GameObject newChr = Instantiate(newPrefab, enterPos, enterWalkRot, stagingPosition != null ? stagingPosition : transform);
            SetupCharacterInstance(newChr);

            // 걷기 모션이 있는 캐릭터는 걷기 모션 활성화 및 보폭 싱크 속도 적용
            Animator newAnim = newChr.GetComponentInChildren<Animator>();
            if (newAnim != null)
            {
                newAnim.enabled = true;
                newAnim.speed = curWalkSpeed;
                if (curHasWalk && HasParameter(newAnim, "IsWalking"))
                {
                    newAnim.SetBool("IsWalking", true);
                }
            }

            // 퇴장 캐릭터도 걷기 모션 지원 시 활성화
            Animator oldAnim = oldChr != null ? oldChr.GetComponentInChildren<Animator>() : null;
            if (oldAnim != null)
            {
                oldAnim.enabled = true;
                oldAnim.speed = curWalkSpeed;
                if (curHasWalk && HasParameter(oldAnim, "IsWalking"))
                {
                    oldAnim.SetBool("IsWalking", true);
                }
            }

            float elapsed = 0f;
            while (elapsed < curDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / curDuration);
                float ease = Mathf.SmoothStep(0f, 1f, t);

                if (oldChr != null)
                {
                    oldChr.transform.position = Vector3.Lerp(centerPos, exitPos, ease);
                    // 퇴장 시 나가는 방향으로 회전하며 이동
                    oldChr.transform.rotation = Quaternion.Slerp(finalFrontRot, exitWalkRot, Mathf.Clamp01(t * 2f));
                }
                if (newChr != null)
                {
                    newChr.transform.position = Vector3.Lerp(enterPos, centerPos, ease);
                    
                    // 진입 시: 처음엔 걸어오는 방향 -> 중앙 도달(후반 40%~) 시 부드럽게 정면 카메라를 바라보도록 회전
                    float turnT = Mathf.Clamp01((t - 0.4f) / 0.6f);
                    float turnEase = Mathf.SmoothStep(0f, 1f, turnT);
                    newChr.transform.rotation = Quaternion.Slerp(enterWalkRot, finalFrontRot, turnEase);
                }

                yield return null;
            }

            if (oldChr != null)
            {
                if (Application.isPlaying) Destroy(oldChr);
                else DestroyImmediate(oldChr);
            }

            currentSpawnedCharacter = newChr;
            if (currentSpawnedCharacter != null)
            {
                currentSpawnedCharacter.transform.position = centerPos;
                currentSpawnedCharacter.transform.rotation = finalFrontRot;
            }
            currentModelRotationY = 0f;

            if (newAnim != null)
            {
                newAnim.speed = 1f;
                if (HasParameter(newAnim, "IsWalking")) newAnim.SetBool("IsWalking", false);
                if (HasParameter(newAnim, "Idle")) newAnim.SetTrigger("Idle");
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
            currentSpawnedCharacter = Instantiate(prefab, centerPos, centerRot, stagingPosition != null ? stagingPosition : transform);
            SetupCharacterInstance(currentSpawnedCharacter);

            currentModelRotationY = 0f;
            NotifyCharacterChanged();
        }

        public void ClearAllShowcaseCharacters()
        {
            if (currentSpawnedCharacter != null)
            {
                if (Application.isPlaying) Destroy(currentSpawnedCharacter);
                else DestroyImmediate(currentSpawnedCharacter);
                currentSpawnedCharacter = null;
            }

            // 하위의 모든 [Showcase_Chr] 오브젝트 일괄 정리
            var allShowcase = GetComponentsInChildren<Transform>(true);
            foreach (var t in allShowcase)
            {
                if (t != null && t.gameObject.name.StartsWith("[Showcase_Chr]"))
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
