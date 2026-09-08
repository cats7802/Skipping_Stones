using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SkippingStones.Data;

namespace SkippingStones.Visuals
{
    /// <summary>
    /// 로비 3D 스톤 쇼케이스 회전 및 슬롯 캐러셀 컨트롤러
    /// - StoneDatabaseSO 기반의 단일 진실 공급원 직접 참조
    /// - 하단 다이얼(StoneSelector, 30도)과 상단 스탠드(Stone_Stand, 120도) 연동 회전
    /// - 3개 슬롯(Stage_01: 정면, Stage_02: +120° 정면진입, Stage_03: -120° 정면진입)
    /// - 회전 전 다음 정면 슬롯 돌 사전 스폰 보장 + 무한 링 버퍼 완벽 캐러셀
    /// </summary>
    public class LobbyStoneShowcaseController : MonoBehaviour
    {
        [Header("🪨 조약돌 데이터베이스 (직접 참조)")]
        [SerializeField] private StoneDatabaseSO stoneDatabase;

        [Header("하이어라키 참조 (자동 검색 또는 직접 연결)")]
        [SerializeField] private Camera targetCamera;          // 로비 뷰 카메라
        [SerializeField] private Transform dialTransform;      // 하단 다이얼 (StoneSelector)
        [SerializeField] private Transform stageTransform;     // 상단 3개 슬롯 회전대 (Stone_Stand)
        [SerializeField] private Transform[] stageSlots = new Transform[3]; // [0]=Stage_01, [1]=Stage_02, [2]=Stage_03

        [Header("돌 프리팹 목록 (해금 돌 목록)")]
        [SerializeField] private List<GameObject> unlockedStonePrefabs = new List<GameObject>();

        [Header("회전 및 인터랙션 설정")]
        [SerializeField] private float rotationDuration = 0.40f;
        [SerializeField] private float dialStepAngle = 30f;
        [SerializeField] private float stageStepAngle = 120f;
        [SerializeField] private float dragThresholdPixels = 35f;

        [Header("쇼케이스 돌 비주얼 설정")]
        [SerializeField] private Vector3 stoneLocalOffset = new Vector3(0f, 0.02f, 0f); // 접시 홈 위에 도톰하게 안착
        [SerializeField] private Vector3 stoneLocalScale = new Vector3(1.4f, 1.4f, 1.4f); // 접시 크기에 맞는 볼륨감

        [Header("현재 상태 모니터링")]
        [SerializeField] private int currentStoneIndex = 0;
        [SerializeField] private int currentFacingSlotIndex = 0;
        [SerializeField] private bool isRotating = false;

        private GameObject[] spawnedStones = new GameObject[3];
        private int[] slotStoneIndices = new int[3] { -1, -1, -1 };
        private int currentStep = 0;

        private Vector2 dragStartPos;
        private Vector2 currentPointerPos;
        private bool isDragging = false;

        public event Action<int, GameObject> OnSelectedStoneChanged;

        private void Awake()
        {
            AutoFindReferences();
            ScanUnlockedStones();
        }

        private void Start()
        {
            InitializeShowcase();
        }

        public void InitializeShowcase()
        {
            AutoFindReferences();
            ScanUnlockedStones();

            int savedIndex = 0;
            var dm = GameDataManager.Instance;
            if (dm != null && dm.UserData != null && !string.IsNullOrEmpty(dm.UserData.selectedStoneId))
            {
                string targetId = dm.UserData.selectedStoneId;
                for (int i = 0; i < unlockedStonePrefabs.Count; i++)
                {
                    if (unlockedStonePrefabs[i] == null) continue;
                    string pName = unlockedStonePrefabs[i].name;

                    if (pName.Equals(targetId, StringComparison.OrdinalIgnoreCase))
                    {
                        savedIndex = i;
                        break;
                    }

                    if (stoneDatabase != null)
                    {
                        var sData = stoneDatabase.GetStoneById(targetId);
                        if (sData != null && sData.prefab != null && sData.prefab.name.Equals(pName, StringComparison.OrdinalIgnoreCase))
                        {
                            savedIndex = i;
                            break;
                        }
                    }
                }
            }

            currentStep = 0;
            currentStoneIndex = (unlockedStonePrefabs.Count > 0) ? Mathf.Clamp(savedIndex, 0, unlockedStonePrefabs.Count - 1) : 0;
            currentFacingSlotIndex = GetFacingSlotIndex(currentStep);

            if (stageTransform != null) stageTransform.localRotation = Quaternion.identity;
            if (dialTransform != null) dialTransform.localRotation = Quaternion.identity;

            ClearAllSpawnedStones();
            RefreshAllSlots();

            if (unlockedStonePrefabs.Count > 0 && currentStoneIndex < unlockedStonePrefabs.Count)
            {
                OnSelectedStoneChanged?.Invoke(currentStoneIndex, unlockedStonePrefabs[currentStoneIndex]);
            }
        }

        private int GetFacingSlotIndex(int step)
        {
            int mod = step % 3;
            return (mod + 3) % 3;
        }

        public void ClearAllSpawnedStones()
        {
            for (int i = 0; i < 3; i++)
            {
                ClearSlot(i);
            }
        }

        public void AutoFindReferences()
        {
            if (targetCamera == null)
            {
                Camera[] allCams = FindObjectsByType<Camera>(FindObjectsInactive.Exclude);
                foreach (var cam in allCams)
                {
                    if (cam.gameObject.activeInHierarchy && (cam.name.Contains("Camera001") || cam.name.Contains("Lobby") || cam.name.Contains("Select")))
                    {
                        targetCamera = cam;
                        break;
                    }
                }
                if (targetCamera == null) targetCamera = Camera.main;
                if (targetCamera == null && allCams.Length > 0) targetCamera = allCams[0];
            }

            if (dialTransform == null) dialTransform = FindDeepChild(transform, "StoneSelector");
            if (stageTransform == null) stageTransform = FindDeepChild(transform, "Stone_Stand");

            if (stageSlots == null || stageSlots.Length != 3) stageSlots = new Transform[3];
            if (stageSlots[0] == null) stageSlots[0] = FindDeepChild(transform, "Stone_Stage_01");
            if (stageSlots[1] == null) stageSlots[1] = FindDeepChild(transform, "Stone_Stage_02");
            if (stageSlots[2] == null) stageSlots[2] = FindDeepChild(transform, "Stone_Stage_03");

            if (stoneDatabase == null)
            {
                var dm = GameDataManager.Instance;
                if (dm != null && dm.StoneDatabase != null)
                {
                    stoneDatabase = dm.StoneDatabase;
                }
                else
                {
                    stoneDatabase = Resources.Load<StoneDatabaseSO>("Data/StoneDatabase");
                }
            }
        }

        public void ScanUnlockedStones()
        {
            var dm = GameDataManager.Instance;
            var scannedList = new List<GameObject>();

            if (stoneDatabase == null)
            {
                stoneDatabase = (dm != null && dm.StoneDatabase != null) ? dm.StoneDatabase : Resources.Load<StoneDatabaseSO>("Data/StoneDatabase");
            }

            if (stoneDatabase != null && stoneDatabase.Count > 0)
            {
                foreach (var s in stoneDatabase.Stones)
                {
                    if (s == null || s.prefab == null) continue;

                    bool isUnlocked = s.isDefaultUnlocked;
                    if (dm != null && dm.UserData != null && dm.UserData.unlockedStoneIds != null)
                    {
                        if (dm.UserData.unlockedStoneIds.Contains(s.id))
                        {
                            isUnlocked = true;
                        }
                    }

                    if (isUnlocked && !scannedList.Contains(s.prefab))
                    {
                        scannedList.Add(s.prefab);
                    }
                }
            }

            // 폴백: 스캔 결과가 없을 때 기본 4종 프리팹 로드
            if (scannedList.Count == 0)
            {
                string[] pNames = { "Stone", "Stone_Blue", "Stone_Green", "Stone_red" };
                foreach (string pName in pNames)
                {
                    GameObject p = Resources.Load<GameObject>($"Stone/{pName}") ?? Resources.Load<GameObject>(pName);
#if UNITY_EDITOR
                    if (p == null) p = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Resources/Stone/{pName}.prefab");
                    if (p == null) p = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/prefab/Stone/{pName}.prefab");
#endif
                    if (p != null && !scannedList.Contains(p)) scannedList.Add(p);
                }
            }

            unlockedStonePrefabs = scannedList;
        }

        private void Update()
        {
            HandleInput();
        }

        private void HandleInput()
        {
            if (isRotating || unlockedStonePrefabs == null || unlockedStonePrefabs.Count < 2) return;

            bool pointerDown = false;
            bool pointerUp = false;

#if ENABLE_INPUT_SYSTEM
            if (UnityEngine.InputSystem.Touchscreen.current != null)
            {
                var touch = UnityEngine.InputSystem.Touchscreen.current.primaryTouch;
                currentPointerPos = touch.position.ReadValue();
                if (touch.press.wasPressedThisFrame) pointerDown = true;
                else if (touch.press.wasReleasedThisFrame) pointerUp = true;
            }
            else if (UnityEngine.InputSystem.Mouse.current != null)
            {
                currentPointerPos = UnityEngine.InputSystem.Mouse.current.position.ReadValue();
                if (UnityEngine.InputSystem.Mouse.current.leftButton.wasPressedThisFrame) pointerDown = true;
                if (UnityEngine.InputSystem.Mouse.current.leftButton.wasReleasedThisFrame) pointerUp = true;
            }
#endif

            if (pointerDown)
            {
                Camera cam = targetCamera;
                if (cam == null || !cam.gameObject.activeInHierarchy)
                {
                    AutoFindReferences();
                    cam = targetCamera;
                }

                bool hitDial = false;
                if (cam != null)
                {
                    Ray ray = cam.ScreenPointToRay(currentPointerPos);
                    if (Physics.Raycast(ray, out RaycastHit hit, 100f))
                    {
                        if (dialTransform != null && (hit.transform == dialTransform || hit.transform.IsChildOf(dialTransform))) hitDial = true;
                        else if (stageTransform != null && (hit.transform == stageTransform || hit.transform.IsChildOf(stageTransform))) hitDial = true;
                    }
                }

                if (hitDial)
                {
                    dragStartPos = currentPointerPos;
                    isDragging = true;
                }
            }
            else if (pointerUp && isDragging)
            {
                isDragging = false;
                Vector2 dragDelta = currentPointerPos - dragStartPos;

                if (Mathf.Abs(dragDelta.x) >= dragThresholdPixels)
                {
                    if (dragDelta.x < 0) RotateShowcase(1);
                    else RotateShowcase(-1);
                }
            }
        }

        [ContextMenu("Rotate Next (다음 돌)")]
        public void RotateNext() => RotateShowcase(1);

        [ContextMenu("Rotate Previous (이전 돌)")]
        public void RotatePrevious() => RotateShowcase(-1);

        private void RotateShowcase(int direction)
        {
            if (isRotating || unlockedStonePrefabs.Count <= 1) return;
            StartCoroutine(RotateRoutine(direction));
        }

        private IEnumerator RotateRoutine(int direction)
        {
            isRotating = true;

            int total = unlockedStonePrefabs.Count;
            int nextStoneIndex = (currentStoneIndex + direction + total) % total;
            int nextStep = currentStep + direction;
            int targetFacingSlot = GetFacingSlotIndex(nextStep);

            // 🌟 회전 전 다음 정면 슬롯 돌 사전 스폰
            SpawnStoneAtSlot(targetFacingSlot, nextStoneIndex);

            float startDialY = currentStep * dialStepAngle;
            float startStageY = currentStep * stageStepAngle;

            currentStep = nextStep;
            currentStoneIndex = nextStoneIndex;
            currentFacingSlotIndex = targetFacingSlot;

            float targetDialY = currentStep * dialStepAngle;
            float targetStageY = currentStep * stageStepAngle;

            float elapsed = 0f;
            while (elapsed < rotationDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / rotationDuration);
                float ease = Mathf.SmoothStep(0f, 1f, t);

                if (dialTransform != null)
                {
                    float currentDial = Mathf.Lerp(startDialY, targetDialY, ease);
                    dialTransform.localRotation = Quaternion.Euler(0f, currentDial, 0f);
                }
                if (stageTransform != null)
                {
                    float currentStage = Mathf.Lerp(startStageY, targetStageY, ease);
                    stageTransform.localRotation = Quaternion.Euler(0f, currentStage, 0f);
                }
                yield return null;
            }

            if (dialTransform != null)
                dialTransform.localRotation = Quaternion.Euler(0f, targetDialY, 0f);

            if (stageTransform != null)
                stageTransform.localRotation = Quaternion.Euler(0f, targetStageY, 0f);

            UpdateBehindSlots();

            GameObject currentPrefab = unlockedStonePrefabs.Count > 0 ? unlockedStonePrefabs[currentStoneIndex] : null;
            OnSelectedStoneChanged?.Invoke(currentStoneIndex, currentPrefab);

            isRotating = false;
        }

        private void RefreshAllSlots()
        {
            if (unlockedStonePrefabs == null || unlockedStonePrefabs.Count == 0) return;
            int total = unlockedStonePrefabs.Count;

            int facingSlot = currentFacingSlotIndex;
            int nextSlot = (facingSlot + 1) % 3;
            int prevSlot = (facingSlot + 2) % 3;

            SpawnStoneAtSlot(facingSlot, currentStoneIndex);

            if (total == 1)
            {
                ClearSlot(nextSlot);
                ClearSlot(prevSlot);
            }
            else if (total == 2)
            {
                int otherStone = (currentStoneIndex + 1) % total;
                SpawnStoneAtSlot(nextSlot, otherStone);
                SpawnStoneAtSlot(prevSlot, otherStone);
            }
            else
            {
                int nextStone = (currentStoneIndex + 1) % total;
                int prevStone = (currentStoneIndex - 1 + total) % total;
                SpawnStoneAtSlot(nextSlot, nextStone);
                SpawnStoneAtSlot(prevSlot, prevStone);
            }
        }

        private void UpdateBehindSlots()
        {
            if (unlockedStonePrefabs == null || unlockedStonePrefabs.Count == 0) return;
            int total = unlockedStonePrefabs.Count;

            int facingSlot = currentFacingSlotIndex;
            int nextSlot = (facingSlot + 1) % 3;
            int prevSlot = (facingSlot + 2) % 3;

            if (total == 1)
            {
                ClearSlot(nextSlot);
                ClearSlot(prevSlot);
            }
            else if (total == 2)
            {
                int otherStone = (currentStoneIndex + 1) % total;
                SpawnStoneAtSlot(nextSlot, otherStone);
                SpawnStoneAtSlot(prevSlot, otherStone);
            }
            else
            {
                int nextStone = (currentStoneIndex + 1) % total;
                int prevStone = (currentStoneIndex - 1 + total) % total;
                SpawnStoneAtSlot(nextSlot, nextStone);
                SpawnStoneAtSlot(prevSlot, prevStone);
            }
        }

        private void ClearSlot(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= 3) return;

            if (spawnedStones[slotIndex] != null)
            {
                if (Application.isPlaying) Destroy(spawnedStones[slotIndex]);
                else DestroyImmediate(spawnedStones[slotIndex]);
                spawnedStones[slotIndex] = null;
            }
            slotStoneIndices[slotIndex] = -1;

            if (stageSlots != null && slotIndex < stageSlots.Length && stageSlots[slotIndex] != null)
            {
                var dummy = stageSlots[slotIndex];
                for (int c = dummy.childCount - 1; c >= 0; c--)
                {
                    var child = dummy.GetChild(c);
                    if (Application.isPlaying) Destroy(child.gameObject);
                    else DestroyImmediate(child.gameObject);
                }
            }
        }

        private void SpawnStoneAtSlot(int slotIndex, int stoneIndex)
        {
            if (stageSlots == null || slotIndex >= stageSlots.Length || stageSlots[slotIndex] == null) return;
            Transform dummy = stageSlots[slotIndex];

            if (slotStoneIndices[slotIndex] == stoneIndex && spawnedStones[slotIndex] != null)
            {
                return;
            }

            ClearSlot(slotIndex);

            if (stoneIndex < 0 || stoneIndex >= unlockedStonePrefabs.Count) return;
            GameObject prefab = unlockedStonePrefabs[stoneIndex];
            if (prefab == null) return;

            GameObject instance = Instantiate(prefab, dummy);
            instance.name = $"ShowcaseStone_Slot{slotIndex}_{prefab.name}";

            instance.transform.localPosition = stoneLocalOffset;
            instance.transform.localRotation = prefab.transform.localRotation;
            instance.transform.localScale = stoneLocalScale;

            // 물리 및 인게임 컴포넌트 제거
            var customScripts = instance.GetComponentsInChildren<MonoBehaviour>(true);
            foreach (var script in customScripts)
            {
                if (script == null) continue;
                if (Application.isPlaying) Destroy(script);
                else DestroyImmediate(script);
            }

            var cols = instance.GetComponentsInChildren<Collider>(true);
            foreach (var col in cols)
            {
                if (col == null) continue;
                if (Application.isPlaying) Destroy(col);
                else DestroyImmediate(col);
            }

            var rbs = instance.GetComponentsInChildren<Rigidbody>(true);
            foreach (var rb in rbs)
            {
                if (rb == null) continue;
                if (Application.isPlaying) Destroy(rb);
                else DestroyImmediate(rb);
            }

            // 기본 조약돌 가시성 보정
            var renderers = instance.GetComponentsInChildren<MeshRenderer>(true);
            foreach (var mr in renderers)
            {
                if (mr == null) continue;
                if (prefab.name.Equals("Stone", StringComparison.OrdinalIgnoreCase) ||
                    (mr.sharedMaterial != null && mr.sharedMaterial.name.Contains("Pebble")))
                {
                    var mpb = new MaterialPropertyBlock();
                    mr.GetPropertyBlock(mpb);
                    mpb.SetColor("_BaseColor", new Color(0.52f, 0.56f, 0.60f, 1f));
                    mr.SetPropertyBlock(mpb);
                }
            }

            spawnedStones[slotIndex] = instance;
            slotStoneIndices[slotIndex] = stoneIndex;
        }

        public void SetUnlockedStones(List<GameObject> prefabs, int initialIndex = 0)
        {
            unlockedStonePrefabs = prefabs ?? new List<GameObject>();
            currentStoneIndex = Mathf.Clamp(initialIndex, 0, Mathf.Max(0, unlockedStonePrefabs.Count - 1));
            RefreshAllSlots();
        }

        public GameObject GetCurrentSelectedStonePrefab()
        {
            if (unlockedStonePrefabs.Count == 0 || currentStoneIndex < 0 || currentStoneIndex >= unlockedStonePrefabs.Count)
                return null;
            return unlockedStonePrefabs[currentStoneIndex];
        }

        private static Transform FindDeepChild(Transform parent, string childName)
        {
            foreach (Transform child in parent)
            {
                if (child.name == childName) return child;
                Transform found = FindDeepChild(child, childName);
                if (found != null) return found;
            }
            return null;
        }
    }
}
