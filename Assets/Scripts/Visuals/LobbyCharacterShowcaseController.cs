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
        [Header("스테이징 기준점")]
        [SerializeField] private Transform stagingPosition; // Staging_Position 포함된 더미

        [Header("캐릭터 프리팹 목록 (자동 스캔)")]
        [SerializeField] private List<GameObject> characterPrefabs = new List<GameObject>();

        [Header("트랜지션 및 회전 설정")]
        [SerializeField] private float transitionDuration = 0.55f;
        [SerializeField] private float entryOffsetDistance = 3.5f; // 스테이징 기준 좌/우 스폰 거리
        [SerializeField] private float rotationSensitivity = 0.4f; // 드래그 회전 감도

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
                    if (characterPrefabs[i] != null && characterPrefabs[i].name.Equals(savedId, StringComparison.OrdinalIgnoreCase))
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
        /// 계층 구조 내 Staging_Position 자동 탐색 (대소문자/오타 무관하게 STAGING_POSITION 또는 STAGINGPOSITION 검색)
        /// </summary>
        public void FindStagingPosition()
        {
            if (stagingPosition != null) return;

            Transform[] allChildren = GetComponentsInChildren<Transform>(true);
            foreach (var t in allChildren)
            {
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
        /// StoneThrowerCharacter 스크립트를 보유하고 해금(isUnlocked == true)된 캐릭터 프리팹만 스캔
        /// </summary>
        public void ScanCharacterPrefabs()
        {
            characterPrefabs.Clear();

            var dm = GameDataManager.Instance;

#if UNITY_EDITOR
            string[] guids = UnityEditor.AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" });
            foreach (string guid in guids)
            {
                string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                GameObject prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab != null && prefab.GetComponentInChildren<StoneThrowerCharacter>(true) != null)
                {
                    // 카탈로그 언락 여부 체크
                    bool isUnlocked = true;
                    if (dm != null && dm.characterCatalog != null && dm.characterCatalog.Count > 0)
                    {
                        var info = dm.characterCatalog.Find(c => c.id.Equals(prefab.name, StringComparison.OrdinalIgnoreCase) || 
                                                                 prefab.name.Contains(c.id) ||
                                                                 (c.prefabPath != null && c.prefabPath.Contains(prefab.name)));
                        if (info != null)
                        {
                            isUnlocked = info.isUnlocked;
                        }
                    }

                    if (isUnlocked && !characterPrefabs.Contains(prefab))
                    {
                        characterPrefabs.Add(prefab);
                    }
                }
            }
#else
            var allPrefabs = Resources.LoadAll<GameObject>("");
            foreach (var p in allPrefabs)
            {
                if (p != null && p.GetComponentInChildren<StoneThrowerCharacter>(true) != null)
                {
                    bool isUnlocked = true;
                    if (dm != null && dm.characterCatalog != null && dm.characterCatalog.Count > 0)
                    {
                        var info = dm.characterCatalog.Find(c => c.id.Equals(p.name, StringComparison.OrdinalIgnoreCase) || p.name.Contains(c.id));
                        if (info != null) isUnlocked = info.isUnlocked;
                    }
                    if (isUnlocked) characterPrefabs.Add(p);
                }
            }
#endif
        }

        private void Update()
        {
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
                Camera cam = Camera.main;
                if (cam == null) cam = FindAnyObjectByType<Camera>();

                bool hitCharacter = false;
                if (cam != null && currentSpawnedCharacter != null)
                {
                    Ray ray = cam.ScreenPointToRay(pointerPos);
                    if (Physics.Raycast(ray, out RaycastHit hit, 100f))
                    {
                        if (hit.transform.IsChildOf(currentSpawnedCharacter.transform))
                        {
                            hitCharacter = true;
                        }
                    }
                }

                if (hitCharacter)
                {
                    isDragging = true;
                    lastPointerPos = pointerPos;
                }
            }
            else if (isDragging && !pointerUp)
            {
                float deltaX = pointerPos.x - lastPointerPos.x;
                lastPointerPos = pointerPos;

                currentModelRotationY -= deltaX * rotationSensitivity;
                if (currentSpawnedCharacter != null)
                {
                    Quaternion baseRot = stagingPosition != null ? stagingPosition.rotation : Quaternion.identity;
                    currentSpawnedCharacter.transform.rotation = baseRot * Quaternion.Euler(0f, currentModelRotationY, 0f);
                }
            }
            else if (pointerUp)
            {
                isDragging = false;
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
        /// 캐릭터 슬라이드/걸어나오기 트랜지션 코루틴
        /// direction: 1 = 다음(오른쪽으로 퇴장, 왼쪽에서 등장) / -1 = 이전
        /// </summary>
        private IEnumerator TransitionRoutine(int targetIndex, int direction)
        {
            isTransitioning = true;
            GameObject oldChr = currentSpawnedCharacter;

            Vector3 centerPos = stagingPosition != null ? stagingPosition.position : transform.position;
            Quaternion finalFrontRot = stagingPosition != null ? stagingPosition.rotation : transform.rotation;

            // 로비 룸 및 카메라 45도 쿼터뷰에 맞춘 화면상 좌우 횡이동 축 계산 (로비 로컬 Y+45도)
            Vector3 lobbyForward = transform.forward;
            Vector3 moveDir = (Quaternion.Euler(0f, 45f, 0f) * transform.right).normalized;

            Vector3 exitPos = centerPos + moveDir * (direction * entryOffsetDistance);
            Vector3 enterPos = centerPos - moveDir * (direction * entryOffsetDistance);

            // 진입 시 캐릭터가 바라볼 방향 (중앙을 향해 걸어오는 방향)
            Vector3 enterLookDir = (centerPos - enterPos).normalized;
            Quaternion enterWalkRot = enterLookDir != Vector3.zero ? Quaternion.LookRotation(enterLookDir, Vector3.up) : finalFrontRot;

            // 퇴장 시 캐릭터가 바라볼 방향 (화면 밖으로 나가는 방향)
            Vector3 exitLookDir = (exitPos - centerPos).normalized;
            Quaternion exitWalkRot = exitLookDir != Vector3.zero ? Quaternion.LookRotation(exitLookDir, Vector3.up) : finalFrontRot;

            // 새 캐릭터 스폰 (화면 밖, 걸어오는 방향을 바라보며 시작)
            GameObject newPrefab = characterPrefabs[targetIndex];
            GameObject newChr = Instantiate(newPrefab, enterPos, enterWalkRot);
            SetupCharacterInstance(newChr);

            // 걷기 모션이 있다면 트리거
            Animator newAnim = newChr.GetComponentInChildren<Animator>();
            if (newAnim != null) newAnim.SetBool("IsWalking", true);

            float elapsed = 0f;
            while (elapsed < transitionDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / transitionDuration);
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
                    
                    // 진입 시: 처음엔 걸어오는 방향 -> 중앙 도달(후반 50%~) 시 부드럽게 정면 카메라를 바라보도록 회전
                    float turnT = Mathf.Clamp01((t - 0.4f) / 0.6f);
                    float turnEase = Mathf.SmoothStep(0f, 1f, turnT);
                    newChr.transform.rotation = Quaternion.Slerp(enterWalkRot, finalFrontRot, turnEase);
                }

                yield return null;
            }

            if (oldChr != null) Destroy(oldChr);

            currentSpawnedCharacter = newChr;
            if (currentSpawnedCharacter != null)
            {
                currentSpawnedCharacter.transform.position = centerPos;
                currentSpawnedCharacter.transform.rotation = finalFrontRot;
            }
            currentModelRotationY = 0f;

            if (newAnim != null)
            {
                newAnim.SetBool("IsWalking", false);
                newAnim.SetTrigger("Idle");
            }

            currentCharacterIndex = targetIndex;
            NotifyCharacterChanged();

            isTransitioning = false;
        }

        private void SpawnCharacterInstant(int index)
        {
            if (currentSpawnedCharacter != null)
            {
                Destroy(currentSpawnedCharacter);
                currentSpawnedCharacter = null;
            }

            if (index < 0 || index >= characterPrefabs.Count) return;

            Vector3 centerPos = stagingPosition != null ? stagingPosition.position : transform.position;
            Quaternion centerRot = stagingPosition != null ? stagingPosition.rotation : Quaternion.identity;

            GameObject prefab = characterPrefabs[index];
            currentSpawnedCharacter = Instantiate(prefab, centerPos, centerRot);
            SetupCharacterInstance(currentSpawnedCharacter);

            currentModelRotationY = 0f;
            NotifyCharacterChanged();
        }

        private void SetupCharacterInstance(GameObject chrInstance)
        {
            if (chrInstance == null) return;
            chrInstance.name = $"[Showcase_Chr]_{chrInstance.name}";

            // 쇼케이스용으로 안전 처리: StoneThrowerCharacter의 인게임 자동 초기화가 에러나지 않도록 비활성화
            var thrower = chrInstance.GetComponentInChildren<StoneThrowerCharacter>();
            if (thrower != null)
            {
                thrower.enabled = false;
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
