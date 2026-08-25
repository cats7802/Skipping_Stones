using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SkippingStones.Visuals
{
    /// <summary>
    /// 로비 스톤 쇼케이스 컨트롤러 (초간결 정석 구조)
    /// - 스탠드(Stone_Stand)와 다이얼(StoneSelector)을 이징(SmoothStep)으로 회전
    /// - 더미 3개(Stone_Stage_01~03)에 돌을 얹어두면 부모 회전에 따라 자연스럽게 동반 회전
    /// - 회전 완료 시 등 뒤 슬롯만 다음/이전 해금 돌로 조용히 갱신
    /// </summary>
    public class LobbyStoneShowcaseController : MonoBehaviour
    {
        [Header("--- 3D 회전 대상 파츠 ---")]
        [Tooltip("30도씩 회전할 하단 다이얼 (StoneSelector)")]
        [SerializeField] private Transform dialTransform;

        [Tooltip("120도씩 회전할 상단 스탠드 (Stone_Stand)")]
        [SerializeField] private Transform stageTransform;

        [Header("--- 스탠드 위 더미 3개 ---")]
        [Tooltip("맥스에서 배치된 더미 3종 (Stone_Stage_01, 02, 03)")]
        [SerializeField] private Transform[] stageSlots = new Transform[3];

        [Header("--- 회전 세팅 ---")]
        [SerializeField] private float dialStepAngle = 30f;
        [SerializeField] private float stageStepAngle = 120f;
        [SerializeField] private float rotationDuration = 0.55f;

        [Header("--- 드래그 감도 ---")]
        [SerializeField] private float dragThresholdPixels = 35f;
        [SerializeField] private bool inputEnabled = true;

        [Header("--- 해금된 돌 프리팹 목록 ---")]
        [SerializeField] private List<GameObject> unlockedStonePrefabs = new List<GameObject>();

        // 이벤트
        public event Action<int, GameObject> OnSelectedStoneChanged;

        private int currentStoneIndex = 0;
        private int currentSlotFacingIndex = 0; // 0, 1, 2
        private bool isRotating = false;

        private Vector2 dragStartPos;
        private bool isDragging = false;
        private GameObject[] spawnedStones = new GameObject[3];

        private void Awake()
        {
            // 1. 다이얼 자동 탐색
            if (dialTransform == null)
            {
                foreach (Transform t in GetComponentsInChildren<Transform>(true))
                {
                    if (t.name.Equals("StoneSelector", StringComparison.OrdinalIgnoreCase))
                    {
                        dialTransform = t;
                        break;
                    }
                }
            }

            // 2. 스탠드 자동 탐색
            if (stageTransform == null)
            {
                foreach (Transform t in GetComponentsInChildren<Transform>(true))
                {
                    if (t.name.Equals("Stone_Stand", StringComparison.OrdinalIgnoreCase))
                    {
                        stageTransform = t;
                        break;
                    }
                }
            }

            // 3. 더미 3개 자동 탐색
            bool hasAllSlots = stageSlots != null && stageSlots.Length == 3 && stageSlots[0] != null && stageSlots[1] != null && stageSlots[2] != null;
            if (!hasAllSlots)
            {
                stageSlots = new Transform[3];
                foreach (Transform t in GetComponentsInChildren<Transform>(true))
                {
                    string n = t.name.Trim();
                    if (n.Equals("Stone_Stage_01", StringComparison.OrdinalIgnoreCase)) stageSlots[0] = t;
                    else if (n.Equals("Stone_Stage_02", StringComparison.OrdinalIgnoreCase)) stageSlots[1] = t;
                    else if (n.Equals("Stone_Stage_03", StringComparison.OrdinalIgnoreCase)) stageSlots[2] = t;
                }
            }

            // 4. 프리팹 폴더 자동 스캔 (비어있을 시)
            if (unlockedStonePrefabs.Count == 0)
            {
#if UNITY_EDITOR
                string[] guids = UnityEditor.AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/prefab/Stone" });
                foreach (var guid in guids)
                {
                    string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                    GameObject prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    if (prefab != null && prefab.GetComponent<SkippingStone>() != null)
                    {
                        unlockedStonePrefabs.Add(prefab);
                    }
                }
#endif
                if (unlockedStonePrefabs.Count == 0)
                {
                    GameObject res = Resources.Load<GameObject>("Stone");
                    if (res != null) unlockedStonePrefabs.Add(res);
                }
            }
        }

        private void Start()
        {
            // 시작 시 3개 더미에 돌 3종류를 자식으로 생성
            RefreshAllSlots();
        }

        private void Update()
        {
            if (!inputEnabled || isRotating) return;
            HandleDragInput();
        }

        private void HandleDragInput()
        {
            Vector2 currentPointerPos = Vector2.zero;
            bool pointerDown = false;
            bool pointerUp = false;

#if ENABLE_INPUT_SYSTEM
            if (UnityEngine.InputSystem.Touchscreen.current != null && UnityEngine.InputSystem.Touchscreen.current.primaryTouch.press.isPressed)
            {
                currentPointerPos = UnityEngine.InputSystem.Touchscreen.current.primaryTouch.position.ReadValue();
                if (UnityEngine.InputSystem.Touchscreen.current.primaryTouch.press.wasPressedThisFrame) pointerDown = true;
            }
            else if (UnityEngine.InputSystem.Touchscreen.current != null && UnityEngine.InputSystem.Touchscreen.current.primaryTouch.press.wasReleasedThisFrame)
            {
                currentPointerPos = UnityEngine.InputSystem.Touchscreen.current.primaryTouch.position.ReadValue();
                pointerUp = true;
            }
            else if (UnityEngine.InputSystem.Mouse.current != null)
            {
                currentPointerPos = UnityEngine.InputSystem.Mouse.current.position.ReadValue();
                if (UnityEngine.InputSystem.Mouse.current.leftButton.wasPressedThisFrame) pointerDown = true;
                if (UnityEngine.InputSystem.Mouse.current.leftButton.wasReleasedThisFrame) pointerUp = true;
            }
#else
            if (Input.touchCount > 0)
            {
                Touch t = Input.GetTouch(0);
                currentPointerPos = t.position;
                if (t.phase == TouchPhase.Began) pointerDown = true;
                else if (t.phase == TouchPhase.Ended || t.phase == TouchPhase.Canceled) pointerUp = true;
            }
            else
            {
                currentPointerPos = Input.mousePosition;
                if (Input.GetMouseButtonDown(0)) pointerDown = true;
                if (Input.GetMouseButtonUp(0)) pointerUp = true;
            }
#endif

            if (pointerDown)
            {
                dragStartPos = currentPointerPos;
                isDragging = true;
            }
            else if (pointerUp && isDragging)
            {
                isDragging = false;
                Vector2 dragDelta = currentPointerPos - dragStartPos;

                if (Mathf.Abs(dragDelta.x) >= dragThresholdPixels)
                {
                    // 오른쪽 드래그(+) -> 이전 돌, 왼쪽 드래그(-) -> 다음 돌
                    if (dragDelta.x > 0) RotateShowcase(-1);
                    else RotateShowcase(1);
                }
            }
        }

        [ContextMenu("Rotate Next")]
        public void RotateNext() => RotateShowcase(1);

        [ContextMenu("Rotate Previous")]
        public void RotatePrevious() => RotateShowcase(-1);

        private void RotateShowcase(int direction)
        {
            if (isRotating || unlockedStonePrefabs.Count == 0) return;
            StartCoroutine(RotateRoutine(direction));
        }

        /// <summary>
        /// 이징(Slow in - Fast - Slow out)을 적용하여 순수한 로컬 Y축만 회전
        /// </summary>
        private IEnumerator RotateRoutine(int direction)
        {
            isRotating = true;

            int total = unlockedStonePrefabs.Count;
            currentStoneIndex = (currentStoneIndex + direction + total) % total;
            currentSlotFacingIndex = (currentSlotFacingIndex + direction + 3) % 3;

            // 순수 로컬 Y축 시작 각도 및 목표 각도
            float startDialY = dialTransform != null ? dialTransform.localEulerAngles.y : 0f;
            float targetDialY = startDialY + (direction * dialStepAngle);

            float startStageY = stageTransform != null ? stageTransform.localEulerAngles.y : 0f;
            float targetStageY = startStageY + (direction * stageStepAngle);

            float elapsed = 0f;
            while (elapsed < rotationDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / rotationDuration);
                float ease = Mathf.SmoothStep(0f, 1f, t); // 부드러운 가감속 이징

                if (dialTransform != null)
                {
                    float currentDial = Mathf.Lerp(startDialY, targetDialY, ease);
                    dialTransform.localEulerAngles = new Vector3(dialTransform.localEulerAngles.x, currentDial, dialTransform.localEulerAngles.z);
                }
                if (stageTransform != null)
                {
                    float currentStage = Mathf.Lerp(startStageY, targetStageY, ease);
                    stageTransform.localEulerAngles = new Vector3(stageTransform.localEulerAngles.x, currentStage, stageTransform.localEulerAngles.z);
                }
                yield return null;
            }

            if (dialTransform != null)
                dialTransform.localEulerAngles = new Vector3(dialTransform.localEulerAngles.x, targetDialY, dialTransform.localEulerAngles.z);

            if (stageTransform != null)
                stageTransform.localEulerAngles = new Vector3(stageTransform.localEulerAngles.x, targetStageY, stageTransform.localEulerAngles.z);

            // 회전이 끝난 후 등 뒤로 돌아간 슬롯만 다음/이전 돌로 조용히 갱신
            UpdateBehindSlot();

            GameObject currentPrefab = unlockedStonePrefabs.Count > 0 ? unlockedStonePrefabs[currentStoneIndex] : null;
            OnSelectedStoneChanged?.Invoke(currentStoneIndex, currentPrefab);

            isRotating = false;
        }

        private void RefreshAllSlots()
        {
            if (unlockedStonePrefabs == null || unlockedStonePrefabs.Count == 0) return;
            int total = unlockedStonePrefabs.Count;

            for (int i = 0; i < 3; i++)
            {
                int offset = (i - currentSlotFacingIndex + 3) % 3;
                if (offset == 2) offset = -1;
                int stoneIdx = (currentStoneIndex + offset + total) % total;
                SpawnStoneAtSlot(i, stoneIdx);
            }
        }

        private void UpdateBehindSlot()
        {
            if (unlockedStonePrefabs == null || unlockedStonePrefabs.Count == 0) return;
            int total = unlockedStonePrefabs.Count;

            for (int i = 0; i < 3; i++)
            {
                if (i == currentSlotFacingIndex) continue; // 정면 슬롯은 이미 회전해왔으므로 건드리지 않음

                int offset = (i - currentSlotFacingIndex + 3) % 3;
                if (offset == 2) offset = -1;
                int targetStoneIdx = (currentStoneIndex + offset + total) % total;
                SpawnStoneAtSlot(i, targetStoneIdx);
            }
        }

        private void SpawnStoneAtSlot(int slotIndex, int stoneIndex)
        {
            if (stageSlots == null || slotIndex >= stageSlots.Length || stageSlots[slotIndex] == null) return;
            Transform dummy = stageSlots[slotIndex];

            // 기존 돌 삭제
            if (spawnedStones[slotIndex] != null)
            {
                Destroy(spawnedStones[slotIndex]);
                spawnedStones[slotIndex] = null;
            }

            if (stoneIndex < 0 || stoneIndex >= unlockedStonePrefabs.Count) return;
            GameObject prefab = unlockedStonePrefabs[stoneIndex];
            if (prefab == null) return;

            // 더미 밑에 자식으로 얹어놓기 (원점, 평평한 회전)
            GameObject instance = Instantiate(prefab, dummy);
            instance.name = $"ShowcaseStone_Slot{slotIndex}_{prefab.name}";
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            instance.transform.localScale = Vector3.one;

            // 전시용 물리 비활성화
            Rigidbody rb = instance.GetComponent<Rigidbody>();
            if (rb != null) rb.isKinematic = true;

            SkippingStone ss = instance.GetComponent<SkippingStone>();
            if (ss != null) ss.enabled = false;

            spawnedStones[slotIndex] = instance;
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
    }
}
