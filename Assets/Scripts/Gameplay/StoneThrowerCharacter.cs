using UnityEngine;
using System.Collections;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class StoneThrowerCharacter : MonoBehaviour
{
    [Header("모델 & 애니메이터")]
    public Animator animator;
    [Tooltip("오른손 본 (Bip001 R Hand)")]
    public Transform rightHandBone;
    [Tooltip("돌이 부착될 더미 소켓 (Dummy001 / Dummy01)")]
    public Transform dummy01Socket;

    [Header("55프레임 릴리즈 설정")]
    [Tooltip("돌이 손에서 떨어져 나가는 목표 프레임 (기본: 55프레임)")]
    public float releaseFrame = 55f;
    [Tooltip("애니메이션 FPS (기본: 30fps)")]
    public float animationFps = 30f;
    [Tooltip("Dummy001 기준 조약돌 위치 오프셋")]
    public Vector3 stoneOffset = Vector3.zero;
    [Tooltip("Dummy001 기준 조약돌 로컬 회전 (더미 Z축과 돌 Y축 정렬)")]
    public Vector3 stoneDummyRotationEuler = new Vector3(90f, 0f, 0f);

    [Header("위치 및 이동 설정")]
    public Vector3 basePosition = new Vector3(0f, 0.9f, -3.8f);
    public float moveSpeed = 10f;

    [Header("실시간 상태")]
    public bool isThrowing = false;
    public bool isStoneReleased = false;

    public Vector3 currentPosition;
    public Quaternion baseRotation;
    private SkippingStone attachedStone;
    private AnimationClip throwClip;

    private void Awake()
    {
        InitializeCharacter();
    }

    [Header("PP(Player_Position) 강변 가이드 리본")]
    public Transform playerPositionMeshObj;
    [Tooltip("지형 표면과 발바닥 사이의 추가 높이 오프셋")]
    public float groundFootOffset = 0.0f;
    public Quaternion currentAimRotation = Quaternion.Euler(0f, 90f, 0f);
    public float currentAimAngle = 0f;
    private List<Vector3> ppCenterStations = new List<Vector3>();

    public void RefreshPlayerPositionGuide()
    {
        if (playerPositionMeshObj == null)
        {
            // 1. PlayerPositionPath 컴포넌트 우선 탐색
            var pathComp = FindAnyObjectByType<PlayerPositionPath>();
            if (pathComp != null)
            {
                playerPositionMeshObj = pathComp.transform;
            }
            else
            {
                // 2. BG_01 외부의 standalone Player_Position 오브젝트 우선 탐색
                var activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
                if (activeScene.isLoaded)
                {
                    var roots = activeScene.GetRootGameObjects();
                    // 먼저 루트 레벨에서 탐색
                    foreach (var root in roots)
                    {
                        if (root.name.Equals("Player_Position", System.StringComparison.OrdinalIgnoreCase))
                        {
                            playerPositionMeshObj = root.transform;
                            break;
                        }
                    }
                    // 루트에 없으면 BG_01이 아닌 하위 계층에서 탐색
                    if (playerPositionMeshObj == null)
                    {
                        foreach (var root in roots)
                        {
                            if (root.name.StartsWith("BG_01", System.StringComparison.OrdinalIgnoreCase)) continue;
                            var transforms = root.GetComponentsInChildren<Transform>(true);
                            foreach (var t in transforms)
                            {
                                if (t.name.Equals("Player_Position", System.StringComparison.OrdinalIgnoreCase))
                                {
                                    playerPositionMeshObj = t;
                                    break;
                                }
                            }
                            if (playerPositionMeshObj != null) break;
                        }
                    }
                }
            }
        }

        ppCenterStations.Clear();

        if (playerPositionMeshObj != null)
        {
            // PlayerPositionPath 컴포넌트가 있으면 자식 점들(Waypoints) 자동 등록
            PlayerPositionPath ppPath = playerPositionMeshObj.GetComponent<PlayerPositionPath>();
            if (ppPath == null) ppPath = playerPositionMeshObj.gameObject.AddComponent<PlayerPositionPath>();
            ppPath.RefreshPath();

            if (ppPath.waypoints != null && ppPath.waypoints.Count > 0)
            {
                if (ppPath.waypoints.Count == 1)
                {
                    ppCenterStations.Add(ppPath.waypoints[0]);
                }
                else
                {
                    // 32개 부드러운 스테이션으로 보간 샘플링
                    int sampleCount = 32;
                    for (int i = 0; i < sampleCount; i++)
                    {
                        float t01 = i / (float)(sampleCount - 1);
                        Vector3 dummyDir;
                        ppCenterStations.Add(ppPath.GetPositionAlongPath(t01, out dummyDir));
                    }
                }
            }
            else
            {
                ppCenterStations.Add(playerPositionMeshObj.position);
            }
        }
    }

    /// <summary>
    /// 지정된 (X, Z) 지점에서 Ground 메쉬 콜라이더의 실제 표면 Y 높이를 100% 정밀 감지
    /// </summary>
    public float GetGroundHeightAt(float x, float z, float fallbackY)
    {
        Vector3 origin = new Vector3(x, 200f, z);
        RaycastHit[] hits = Physics.RaycastAll(origin, Vector3.down, 400f, ~0, QueryTriggerInteraction.Ignore);

        float bestGroundY = float.MinValue;
        bool foundGround = false;

        foreach (var h in hits)
        {
            if (h.collider != null && h.collider.gameObject != gameObject && !h.collider.isTrigger)
            {
                // Ground 이름이 포함된 콜라이더 표면 최우선 적용
                if (h.collider.name.IndexOf("ground", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return h.point.y + groundFootOffset;
                }
                if (h.point.y > bestGroundY)
                {
                    bestGroundY = h.point.y;
                    foundGround = true;
                }
            }
        }

        if (foundGround)
        {
            return bestGroundY + groundFootOffset;
        }

        return fallbackY + groundFootOffset;
    }

    /// <summary>
    /// PP 리본 곡선 경로를 따라 중심점(Station 16, offset=0)을 기준으로 좌우 이동한 위치와 수면(+X 강물 전방 방향, Euler Y=90도) 회전각 반환
    /// </summary>
    public Vector3 GetPPCenterPositionAlongPath(float pathOffset, out Quaternion outRotation)
    {
        if (ppCenterStations == null || ppCenterStations.Count == 0)
        {
            RefreshPlayerPositionGuide();
        }

        if (ppCenterStations.Count < 2)
        {
            outRotation = Quaternion.Euler(0f, 90f, 0f);
            return basePosition;
        }

        int centerIdx = 16;
        if (centerIdx >= ppCenterStations.Count) centerIdx = ppCenterStations.Count / 2;

        float[] stationDist = new float[ppCenterStations.Count];
        stationDist[centerIdx] = 0f;
        for (int i = centerIdx - 1; i >= 0; i--)
        {
            stationDist[i] = stationDist[i + 1] - Vector3.Distance(ppCenterStations[i], ppCenterStations[i + 1]);
        }
        for (int i = centerIdx + 1; i < ppCenterStations.Count; i++)
        {
            stationDist[i] = stationDist[i - 1] + Vector3.Distance(ppCenterStations[i - 1], ppCenterStations[i]);
        }

        float minS = stationDist[0];
        float maxS = stationDist[stationDist.Length - 1];
        float targetS = Mathf.Clamp(pathOffset, minS, maxS);

        Vector3 rawPos = ppCenterStations[centerIdx];

        for (int i = 0; i < ppCenterStations.Count - 1; i++)
        {
            if (targetS >= stationDist[i] && targetS <= stationDist[i + 1])
            {
                float t = Mathf.InverseLerp(stationDist[i], stationDist[i + 1], targetS);
                rawPos = Vector3.Lerp(ppCenterStations[i], ppCenterStations[i + 1], t);
                break;
            }
        }

        float groundY = GetGroundHeightAt(rawPos.x, rawPos.z, rawPos.y);

        // 투구 방향은 항상 강물이 흐르는 월드 +X 방향(Euler Y = 90도)
        outRotation = Quaternion.Euler(0f, 90f, 0f);
        return new Vector3(rawPos.x, groundY, rawPos.z);
    }

    public void GetPathDistanceRange(out float minOffset, out float maxOffset)
    {
        if (ppCenterStations == null || ppCenterStations.Count == 0)
        {
            RefreshPlayerPositionGuide();
        }

        if (ppCenterStations == null || ppCenterStations.Count < 2)
        {
            minOffset = -200f;
            maxOffset = 200f;
            return;
        }

        int centerIdx = ppCenterStations.Count / 2;
        float minS = 0f;
        for (int i = centerIdx - 1; i >= 0; i--)
        {
            minS -= Vector3.Distance(ppCenterStations[i], ppCenterStations[i + 1]);
        }
        float maxS = 0f;
        for (int i = centerIdx + 1; i < ppCenterStations.Count; i++)
        {
            maxS += Vector3.Distance(ppCenterStations[i - 1], ppCenterStations[i]);
        }

        minOffset = minS;
        maxOffset = maxS;
    }

    public Vector3 GetPPCenterPositionAlongPath(float pathOffset)
    {
        Quaternion dummyRot;
        return GetPPCenterPositionAlongPath(pathOffset, out dummyRot);
    }

    public Vector3 GetPPCenterOfTotalLength()
    {
        return GetPPCenterPositionAlongPath(0f);
    }

    [Header("포인트 단위 이동 상태")]
    public int currentWaypointIndex = 14;

    public int GetTotalWaypointsCount()
    {
        if (playerPositionMeshObj != null)
        {
            var ppPath = playerPositionMeshObj.GetComponent<PlayerPositionPath>();
            if (ppPath != null && ppPath.TotalPointsCount > 0) return ppPath.TotalPointsCount;
        }
        return (ppCenterStations != null && ppCenterStations.Count > 0) ? ppCenterStations.Count : 1;
    }

    public int GetCurrentWaypointIndex()
    {
        return currentWaypointIndex;
    }

    public Vector3 GetWaypointWorldPos(int idx)
    {
        if (playerPositionMeshObj != null)
        {
            var ppPath = playerPositionMeshObj.GetComponent<PlayerPositionPath>();
            if (ppPath != null && ppPath.TotalPointsCount > 0)
            {
                Vector3 raw = ppPath.GetWaypoint(idx);
                float gy = GetGroundHeightAt(raw.x, raw.z, raw.y);
                return new Vector3(raw.x, gy, raw.z);
            }
        }
        if (ppCenterStations != null && ppCenterStations.Count > 0)
        {
            int cIdx = Mathf.Clamp(idx, 0, ppCenterStations.Count - 1);
            Vector3 raw = ppCenterStations[cIdx];
            float gy = GetGroundHeightAt(raw.x, raw.z, raw.y);
            return new Vector3(raw.x, gy, raw.z);
        }
        return transform.position;
    }

    public void MoveToPreviousWaypoint()
    {
        int total = GetTotalWaypointsCount();
        if (total <= 1) return;
        currentWaypointIndex = Mathf.Max(0, currentWaypointIndex - 1);
        ApplyCurrentWaypointTarget();
    }

    public void MoveToNextWaypoint()
    {
        int total = GetTotalWaypointsCount();
        if (total <= 1) return;
        currentWaypointIndex = Mathf.Min(total - 1, currentWaypointIndex + 1);
        ApplyCurrentWaypointTarget();
    }

    public void SetWaypointIndex(int idx)
    {
        int total = GetTotalWaypointsCount();
        if (total <= 0) return;
        currentWaypointIndex = Mathf.Clamp(idx, 0, total - 1);
        ApplyCurrentWaypointTarget();
    }

    public void ApplyCurrentWaypointTarget()
    {
        Vector3 targetPos = GetWaypointWorldPos(currentWaypointIndex);
        currentPosition = targetPos;
        baseRotation = Quaternion.Euler(0f, 90f, 0f);
        currentAimRotation = baseRotation;
        currentAimAngle = 0f;
    }

    public void InitializeCharacter()
    {
        FindHandAndDummySocket(true);
        EnsureHandDummyStone();
        DetectAnimationClipFps();
        HoldZeroFramePose();
    }

    public void SetHandStonePrefab(GameObject stonePrefab)
    {
        if (dummy01Socket == null) FindHandAndDummySocket(true);
        Transform parentSocket = (dummy01Socket != null) ? dummy01Socket : rightHandBone;
        if (parentSocket == null) return;

        // 🌟 기존에 소켓 밑에 남아있던 모든 HandDummyStone 오브젝트들을 즉시 완전 삭제 (중복 생성 방지)
        for (int i = parentSocket.childCount - 1; i >= 0; i--)
        {
            Transform child = parentSocket.GetChild(i);
            if (child.name.StartsWith("HandDummyStone") || child.name.Contains("Stone"))
            {
                DestroyImmediate(child.gameObject);
            }
        }

        GameObject prefabToUse = stonePrefab;
        if (prefabToUse == null) prefabToUse = Resources.Load<GameObject>("Stone");
#if UNITY_EDITOR
        if (prefabToUse == null)
        {
            prefabToUse = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/3D/prefab/Stone.prefab");
        }
#endif
        if (prefabToUse != null)
        {
            GameObject dummyObj = Instantiate(prefabToUse, parentSocket);
            dummyObj.name = "HandDummyStone";
            dummyObj.transform.localPosition = stoneOffset;
            dummyObj.transform.localRotation = Quaternion.Euler(stoneDummyRotationEuler);
            dummyObj.transform.localScale = Vector3.one;

            // 1. 커스텀 스크립트 먼저 제거하여 의존성 해제
            var customScripts = dummyObj.GetComponentsInChildren<MonoBehaviour>(true);
            foreach (var s in customScripts)
            {
                if (s == null) continue;
                if (Application.isPlaying) DestroyImmediate(s);
            }

            // 2. 콜라이더 제거
            var cols = dummyObj.GetComponentsInChildren<Collider>(true);
            foreach (var col in cols)
            {
                if (col == null) continue;
                if (Application.isPlaying) DestroyImmediate(col);
            }

            // 3. 리지드바디 마지막 제거
            var rbs = dummyObj.GetComponentsInChildren<Rigidbody>(true);
            foreach (var rb in rbs)
            {
                if (rb == null) continue;
                if (Application.isPlaying) DestroyImmediate(rb);
            }
        }
    }

    private void EnsureHandDummyStone()
    {
        Transform parentSocket = (dummy01Socket != null) ? dummy01Socket : rightHandBone;
        if (parentSocket != null && parentSocket.Find("HandDummyStone") == null)
        {
            var gc = FindAnyObjectByType<GameController>();
            GameObject prefab = (gc != null && gc.defaultStonePrefab != null) ? gc.defaultStonePrefab : null;
            SetHandStonePrefab(prefab);
        }
    }

    private void Update()
    {
        if (!isThrowing)
        {
            HoldZeroFramePose();
        }
    }

    private void EnsureAnimator()
    {
        if (animator == null) animator = GetComponent<Animator>();
        if (animator == null) animator = GetComponentInChildren<Animator>();
        if (animator == null) animator = gameObject.AddComponent<Animator>();

        if (animator != null)
        {
            animator.enabled = true;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        }

        if (animator != null && animator.runtimeAnimatorController == null)
        {
            var ctrl = Resources.Load<RuntimeAnimatorController>("Test_Chr_CTRL");
#if UNITY_EDITOR
            if (ctrl == null)
            {
                ctrl = UnityEditor.AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/3D/Character/Test_Chr_CTRL.controller");
            }
#endif
            if (ctrl != null)
            {
                animator.runtimeAnimatorController = ctrl;
                Debug.Log($"🎮 [StoneThrowerCharacter] '{gameObject.name}'에 Test_Chr_CTRL.controller 자동 연결 완료!");
            }
        }

        if (animator != null && animator.layerCount > 0)
        {
            animator.SetLayerWeight(0, 1f);
        }
    }

    /// <summary>
    /// Bip001 R Hand 및 하위에 연결된 Dummy001을 정밀 탐색하여 매핑
    /// </summary>
    public void FindHandAndDummySocket(bool forceRefresh = false)
    {
        if (forceRefresh)
        {
            rightHandBone = null;
            dummy01Socket = null;
        }

        Transform[] allChildren = GetComponentsInChildren<Transform>(true);

        // 1. Bip001 R Hand 본 탐색
        if (rightHandBone == null)
        {
            foreach (Transform t in allChildren)
            {
                string tName = t.name.Trim();
                if (tName.Equals("Bip001 R Hand", System.StringComparison.OrdinalIgnoreCase) ||
                    tName.Equals("Bip01 R Hand", System.StringComparison.OrdinalIgnoreCase) ||
                    tName.Equals("Bip001_R_Hand", System.StringComparison.OrdinalIgnoreCase) ||
                    tName.Equals("Bip01_R_Hand", System.StringComparison.OrdinalIgnoreCase))
                {
                    rightHandBone = t;
                    break;
                }
            }

            if (rightHandBone == null)
            {
                foreach (Transform t in allChildren)
                {
                    string lower = t.name.ToLower();
                    if (!lower.Contains("foot") && !lower.Contains("eye") && (lower.Contains("r hand") || lower.Contains("r_hand") || lower.Contains("hand.r") || lower.Contains("righthand") || lower.Contains("hand_r")))
                    {
                        rightHandBone = t;
                        break;
                    }
                }
            }
        }

        // 2. Dummy001 소켓 탐색 (Bip001 R Hand 하위 우선 탐색, Eye_Dummy001 등 제외)
        if (rightHandBone != null)
        {
            Transform[] handChildren = rightHandBone.GetComponentsInChildren<Transform>(true);
            foreach (Transform t in handChildren)
            {
                string tName = t.name.Trim();
                string lower = tName.ToLower();
                if (lower.Contains("eye")) continue;

                if (t != rightHandBone && (
                    tName.Equals("Dummy001", System.StringComparison.OrdinalIgnoreCase) ||
                    tName.Equals("Dummy01", System.StringComparison.OrdinalIgnoreCase) ||
                    tName.Equals("Dummy_001", System.StringComparison.OrdinalIgnoreCase) ||
                    tName.Equals("Dummy_01", System.StringComparison.OrdinalIgnoreCase) ||
                    lower.Contains("dummy")))
                {
                    dummy01Socket = t;
                    Debug.Log($"✅ [StoneThrowerCharacter] Bip001 R Hand 하위의 '{t.name}' 소켓 매핑 완료! (Path: {GetHierarchyPath(t)})");
                    return;
                }
            }
        }

        // 3. 전체 하위 계층에서 Dummy001 탐색 (Eye 제외)
        foreach (Transform t in allChildren)
        {
            string tName = t.name.Trim();
            string lower = tName.ToLower();
            if (lower.Contains("eye")) continue;

            if (tName.Equals("Dummy001", System.StringComparison.OrdinalIgnoreCase) ||
                tName.Equals("Dummy01", System.StringComparison.OrdinalIgnoreCase) ||
                tName.Equals("Dummy_001", System.StringComparison.OrdinalIgnoreCase) ||
                tName.Equals("Dummy_01", System.StringComparison.OrdinalIgnoreCase))
            {
                dummy01Socket = t;
                Debug.Log($"✅ [StoneThrowerCharacter] 캐릭터 계층에서 '{t.name}' 소켓 매핑 완료! (Path: {GetHierarchyPath(t)})");
                return;
            }
        }

        // 4. Dummy001이 없을 경우 Bip001 R Hand 사용
        if (dummy01Socket == null)
        {
            dummy01Socket = rightHandBone;
            if (dummy01Socket == null)
            {
                GameObject virtualSocket = new GameObject("Dummy001 (Virtual)");
                virtualSocket.transform.SetParent(transform);
                virtualSocket.transform.localPosition = new Vector3(0.35f, 1.1f, 0.45f);
                dummy01Socket = virtualSocket.transform;
            }
            Debug.LogWarning($"⚠️ [StoneThrowerCharacter] Dummy001을 찾지 못하여 '{dummy01Socket.name}'을 소켓으로 지정했습니다.");
        }
    }

    private string GetHierarchyPath(Transform t)
    {
        if (t == null) return "";
        string path = t.name;
        Transform curr = t.parent;
        while (curr != null && curr != transform.parent)
        {
            path = curr.name + "/" + path;
            curr = curr.parent;
        }
        return path;
    }

    private void DetectAnimationClipFps()
    {
        if (animator != null && animator.runtimeAnimatorController != null)
        {
            var clips = animator.runtimeAnimatorController.animationClips;
            if (clips != null && clips.Length > 0)
            {
                foreach (var clip in clips)
                {
                    if (clip.name.ToLower().Contains("throw") || clip.name.ToLower().Contains("take") || clip.name.ToLower().Contains("shot") || clip.name.ToLower().Contains("attack"))
                    {
                        throwClip = clip;
                        if (clip.frameRate > 0) animationFps = clip.frameRate;
                        break;
                    }
                }

                if (throwClip == null && clips.Length > 0)
                {
                    throwClip = clips[0];
                    if (throwClip.frameRate > 0) animationFps = throwClip.frameRate;
                }
            }
        }
    }

    /// <summary>
    /// Dummy001 소켓의 현재 월드 위치 반환 (돌 매칭 위치)
    /// </summary>
    public Vector3 GetHandPosition()
    {
        if (dummy01Socket != null)
        {
            return dummy01Socket.TransformPoint(stoneOffset);
        }
        if (rightHandBone != null)
        {
            return rightHandBone.TransformPoint(stoneOffset);
        }
        return transform.position + new Vector3(0.35f, 1.1f, 0.45f);
    }

    /// <summary>
    /// Dummy001 소켓의 현재 월드 회전 반환
    /// </summary>
    public Quaternion GetHandRotation()
    {
        if (dummy01Socket != null) return dummy01Socket.rotation;
        if (rightHandBone != null) return rightHandBone.rotation;
        return transform.rotation;
    }

    /// <summary>
    /// 조약돌을 Dummy001에 직접 계층 링크(Parenting)하여 0~54프레임 동안 완전히 고정
    /// </summary>
    public void AttachStone(SkippingStone stone)
    {
        attachedStone = stone;
        isStoneReleased = false;
        if (attachedStone != null)
        {
            Rigidbody rb = attachedStone.GetComponent<Rigidbody>();
            if (rb != null)
            {
                if (!rb.isKinematic)
                {
                    rb.linearVelocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                }
                rb.useGravity = false;
                rb.isKinematic = true;
            }

            Transform parentSocket = (dummy01Socket != null) ? dummy01Socket : rightHandBone;
            if (parentSocket != null)
            {
                attachedStone.transform.SetParent(parentSocket, false);
                attachedStone.transform.localPosition = stoneOffset;
                attachedStone.transform.localRotation = Quaternion.Euler(stoneDummyRotationEuler);
                attachedStone.transform.localScale = Vector3.one;
            }
            else
            {
                attachedStone.transform.position = GetHandPosition();
            }
        }
    }

    /// <summary>
    /// 캐릭터를 0프레임 상태(투구 시작 대기 포즈)로 고정 (0~2단계 파워 선택 완료 전까지 유지)
    /// </summary>
    public void HoldZeroFramePose()
    {
        if (isThrowing) return;

        if (animator != null && animator.runtimeAnimatorController != null && animator.layerCount > 0)
        {
            animator.applyRootMotion = false;
            animator.speed = 0f;
            if (HasState(animator, "ReadyPose"))
            {
                animator.Play("ReadyPose", 0, 0f);
            }
            else if (HasState(animator, "Throw"))
            {
                animator.Play("Throw", 0, 0f);
            }
            animator.Update(0f);
        }
    }

    private void LateUpdate()
    {
        // 55프레임 이전(스윙 및 준비 포즈 중)에는 손 소켓 본에 100% 찰싹 달라붙도록 매 프레임 위치/회전 강제 유지
        if (!isStoneReleased && attachedStone != null)
        {
            Transform parentSocket = (dummy01Socket != null) ? dummy01Socket : rightHandBone;
            if (parentSocket != null)
            {
                if (attachedStone.transform.parent != parentSocket)
                {
                    attachedStone.transform.SetParent(parentSocket, false);
                }
                attachedStone.transform.localPosition = stoneOffset;
                attachedStone.transform.localRotation = Quaternion.Euler(stoneDummyRotationEuler);
                attachedStone.transform.localScale = Vector3.one;
            }
        }
    }

    /// <summary>
    /// 0단계: 위치 선정 (장거리 모드: 발판 위 +Z축 물줄기 응시 / 타겟 모드: PP 포인트 강 건너편 +X축 응시)
    /// </summary>
    public void UpdatePositioning(float offset, float inputH)
    {
        if (isThrowing) return;

        var gc = FindAnyObjectByType<GameController>();
        if (gc != null && (gc.currentMode == GameController.GameMode.LongDistance || gc.currentMode == GameController.GameMode.RhythmArcade))
        {
            // 🏆 장거리 및 아케이드 모드: 발판 위에서 좌우 X 오프셋(offset) 반영 및 월드 +Z축 물줄기 방향(Euler 0, 0, 0) 정면 응시
            baseRotation = Quaternion.Euler(0f, 0f, 0f);
            Vector3 targetPierPos = new Vector3(basePosition.x + offset, basePosition.y, basePosition.z);
            currentPosition = Vector3.Lerp(currentPosition, targetPierPos, Time.deltaTime * 15f);
        }
        else
        {
            // 🎯 타겟 맞추기 모드: 선택된 PP 포인트 위치에서 강 건너편(+X / Euler 0, 90, 0) 응시
            Vector3 targetPos = GetWaypointWorldPos(currentWaypointIndex);
            currentPosition = Vector3.Lerp(currentPosition, targetPos, Time.deltaTime * 12f);
            baseRotation = Quaternion.Euler(0f, 90f, 0f);
        }

        transform.position = currentPosition;
        currentAimRotation = baseRotation;
        currentAimAngle = 0f;
        transform.rotation = baseRotation;

        HoldZeroFramePose();
    }

    /// <summary>
    /// 1단계: 방향 조준 (기본 시선 -Z 방향 baseRotation을 기준으로 1단계 게이지 좌우 편각 회전)
    /// </summary>
    public void UpdateAiming(float aimGauge)
    {
        if (isThrowing) return;

        float maxAngle = 25f;
        currentAimAngle = aimGauge * maxAngle;
        currentAimRotation = baseRotation * Quaternion.Euler(0f, currentAimAngle, 0f);
        transform.rotation = Quaternion.Slerp(transform.rotation, currentAimRotation, Time.deltaTime * 12f);

        HoldZeroFramePose();
    }

    /// <summary>
    /// 2단계: 파워 충전 (1단계에서 선택된 조준 방향 유지)
    /// </summary>
    public void UpdateWindup(float powerGauge)
    {
        if (isThrowing) return;

        transform.rotation = currentAimRotation;
        HoldZeroFramePose();
    }

    /// <summary>
    /// 3단계: 방향 및 파워 선택 완료 -> 캐릭터 투구 애니메이션 실행 -> 30~55프레임 페이드아웃 -> 45프레임 55f 발사 앵커 기준 카메라 선행 가속 -> 55프레임 발사
    /// </summary>
    public void PlayThrowAnimation(System.Action<Vector3, Vector3> onCameraLeadInCallback = null, System.Action onReleaseCallback = null, float speedMultiplier = 1f)
    {
        StartCoroutine(ThrowRoutine(onCameraLeadInCallback, onReleaseCallback, speedMultiplier));
    }

    private IEnumerator ThrowRoutine(System.Action<Vector3, Vector3> onCameraLeadInCallback, System.Action onReleaseCallback, float speedMultiplier = 1f)
    {
        isThrowing = true;
        isStoneReleased = false;
        transform.rotation = currentAimRotation;
        RestoreVisibility();

        // 1. 0프레임 정지 해제 및 투구 애니메이션 0프레임부터 재생 시작 (BPM 속도 연동)
        if (animator != null && animator.runtimeAnimatorController != null)
        {
            animator.enabled = true;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            animator.speed = Mathf.Max(0.1f, speedMultiplier);
            if (animator.layerCount > 0) animator.SetLayerWeight(0, 1f);

            if (HasState(animator, "Throw"))
            {
                animator.Play("Throw", 0, 0f);
            }
            else
            {
                animator.Play(0, 0, 0f);
            }
            animator.Update(0f);
        }

        float fps = (animationFps > 0f) ? animationFps : 30f;
        float actualSpeed = Mathf.Max(0.1f, speedMultiplier);
        float frame30Time = (30f / fps) / actualSpeed;
        float frame45Time = (45f / fps) / actualSpeed;
        float frame55Time = (55f / fps) / actualSpeed;

        bool cameraLeadInTriggered = false;
        var charRenderers = GetComponentsInChildren<Renderer>(true);
        Transform platform = GameController.FindPlatformInScene();
        Renderer pierRenderer = (platform != null && platform.gameObject.activeInHierarchy) ? platform.GetComponent<Renderer>() : null;

        float elapsed = 0f;
        while (elapsed < frame55Time)
        {
            elapsed += Time.deltaTime;

            // 🌟 45프레임 도달: 55프레임 발사 예정 앵커 위치를 기준으로 카메라 완만 가속 시작!
            if (elapsed >= frame45Time && !cameraLeadInTriggered)
            {
                cameraLeadInTriggered = true;
                Vector3 launchAnchorPos = transform.position + (currentAimRotation * new Vector3(0.35f, 1.2f, 0.8f));
                Vector3 launchForwardDir = currentAimRotation * Vector3.forward;
                onCameraLeadInCallback?.Invoke(launchAnchorPos, launchForwardDir);
            }

            // 손 소켓 동기화
            if (attachedStone != null && !isStoneReleased)
            {
                Transform parentSocket = (dummy01Socket != null) ? dummy01Socket : rightHandBone;
                if (parentSocket != null)
                {
                    if (attachedStone.transform.parent != parentSocket)
                    {
                        attachedStone.transform.SetParent(parentSocket, false);
                    }
                    attachedStone.transform.localPosition = stoneOffset;
                    attachedStone.transform.localRotation = Quaternion.Euler(stoneDummyRotationEuler);
                    attachedStone.transform.localScale = Vector3.one;
                }
            }
            yield return null;
        }

        // 🌟 55프레임 도달: 캐릭터 및 손의 더미 조약돌 동시 비활성화 (포탄 투사체 방식)
        foreach (var r in charRenderers)
        {
            if (r != null)
            {
                r.enabled = false;
            }
        }
        // 🌟 Lakeside_WoodenPier(나무 발판)는 숨기지 않고 항상 화면에 선명하게 유지!
        if (pierRenderer != null)
        {
            pierRenderer.enabled = true;
        }

        isStoneReleased = true;
        Debug.Log($"🚀 [StoneThrowerCharacter] 55프레임 도달! 캐릭터 & 손 돌 숨김 완료 -> 독립 조약돌 인스턴스 발사!");
        onReleaseCallback?.Invoke();

        isThrowing = false;
        HoldZeroFramePose();
    }

    public void RestoreVisibility()
    {
        var charRenderers = GetComponentsInChildren<Renderer>(true);
        foreach (var r in charRenderers)
        {
            if (r != null)
            {
                r.enabled = true;
            }
        }

        Transform platform = GameController.FindPlatformInScene();
        if (platform != null)
        {
            var pr = platform.GetComponent<Renderer>();
            if (pr != null)
            {
                pr.enabled = true;
            }
        }
    }

    /// <summary>
    /// 게임 재시작 시 Dummy001에 다시 링크하고 0프레임 준비 자세로 복귀
    /// </summary>
    public void ResetCharacter(float initialX = 0f)
    {
        StopAllCoroutines();
        RestoreVisibility();
        isThrowing = false;
        isStoneReleased = false;
        currentPosition = new Vector3(initialX, basePosition.y, basePosition.z);
        transform.position = currentPosition;
        transform.rotation = baseRotation;

        if (attachedStone != null)
        {
            AttachStone(attachedStone);
        }

        HoldZeroFramePose();
    }

    private bool HasState(Animator anim, string stateName)
    {
        if (anim == null || anim.runtimeAnimatorController == null) return false;
        int hash = Animator.StringToHash(stateName);
        return anim.HasState(0, hash);
    }
}
