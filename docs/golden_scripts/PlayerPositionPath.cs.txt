using System.Collections.Generic;
using UnityEngine;

[ExecuteAlways]
public class PlayerPositionPath : MonoBehaviour
{
    public static PlayerPositionPath Instance { get; private set; }

    [Header("경로 설정")]
    [Tooltip("자식 오브젝트들을 점(Waypoint)으로 자동 감지할지 여부")]
    public bool autoDetectChildren = true;

    [Tooltip("씬 뷰에 표시할 기즈모 색상")]
    public Color gizmoLineColor = new Color(0.2f, 0.95f, 0.3f, 1f);
    public Color gizmoPointColor = new Color(1f, 0.8f, 0.1f, 1f);

    public List<Vector3> waypoints = new List<Vector3>();

    private void Awake()
    {
        Instance = this;
        RefreshPath();
    }

    private void OnEnable()
    {
        Instance = this;
        RefreshPath();
    }

    private void Update()
    {
        // 에디터 모드 및 런타임에서 실시간 위치 갱신
        if (!Application.isPlaying || autoDetectChildren)
        {
            RefreshPath();
        }
    }

    public void RefreshPath()
    {
        waypoints.Clear();

        List<Transform> pointTransforms = new List<Transform>();

        // 1. Player_Position 직속 자식 점들 수집
        if (autoDetectChildren && transform.childCount > 0)
        {
            for (int i = 0; i < transform.childCount; i++)
            {
                Transform child = transform.GetChild(i);
                if (child.gameObject.activeSelf)
                {
                    pointTransforms.Add(child);
                }
            }
        }

        // 2. 만약 직속 자식이 없으면 씬 전체에서 PP01 ~ PP29 이름의 오브젝트 탐색
        if (pointTransforms.Count == 0)
        {
            var activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (activeScene.isLoaded)
            {
                var roots = activeScene.GetRootGameObjects();
                foreach (var root in roots)
                {
                    var allTransforms = root.GetComponentsInChildren<Transform>(true);
                    foreach (var t in allTransforms)
                    {
                        if (t.name.StartsWith("PP", System.StringComparison.OrdinalIgnoreCase) && t.name.Length >= 3 && char.IsDigit(t.name[2]))
                        {
                            pointTransforms.Add(t);
                        }
                    }
                }
            }
        }

        // 3. PP01 ~ PP29 번호 순으로 자연 정렬 (Natural Numerical Sort)
        pointTransforms.Sort((a, b) =>
        {
            int numA = ExtractNumber(a.name);
            int numB = ExtractNumber(b.name);
            if (numA != numB) return numA.CompareTo(numB);
            return a.GetSiblingIndex().CompareTo(b.GetSiblingIndex());
        });

        // 4. 월드 좌표 리스트로 등록
        foreach (var t in pointTransforms)
        {
            waypoints.Add(t.position);
        }

        // 아무 점도 발견되지 않은 경우 자신의 위치를 단일 점으로 등록
        if (waypoints.Count == 0)
        {
            waypoints.Add(transform.position);
        }
    }

    private int ExtractNumber(string name)
    {
        var match = System.Text.RegularExpressions.Regex.Match(name, @"\d+");
        if (match.Success && int.TryParse(match.Value, out int result))
        {
            return result;
        }
        return 9999;
    }

    /// <summary>
    /// 경로 상에서 0.0 ~ 1.0 비율 또는 누적 거리(offset)에 해당하는 월드 좌표 및 전방 회전각 반환
    /// </summary>
    public Vector3 GetPositionAlongPath(float t01, out Vector3 forwardDir)
    {
        forwardDir = transform.forward;
        if (waypoints == null || waypoints.Count == 0)
        {
            return transform.position;
        }

        if (waypoints.Count == 1)
        {
            return waypoints[0];
        }

        t01 = Mathf.Clamp01(t01);

        float totalDist = 0f;
        float[] segDists = new float[waypoints.Count - 1];
        for (int i = 0; i < waypoints.Count - 1; i++)
        {
            segDists[i] = Vector3.Distance(waypoints[i], waypoints[i + 1]);
            totalDist += segDists[i];
        }

        if (totalDist <= 0.001f)
        {
            return waypoints[0];
        }

        float targetDist = t01 * totalDist;
        float accumulated = 0f;

        for (int i = 0; i < segDists.Length; i++)
        {
            if (targetDist <= accumulated + segDists[i] || i == segDists.Length - 1)
            {
                float segT = (segDists[i] > 0.0001f) ? (targetDist - accumulated) / segDists[i] : 0f;
                Vector3 pos = Vector3.Lerp(waypoints[i], waypoints[i + 1], segT);
                Vector3 tangent = (waypoints[i + 1] - waypoints[i]).normalized;
                if (tangent.sqrMagnitude > 0.001f) forwardDir = tangent;
                return pos;
            }
            accumulated += segDists[i];
        }

        return waypoints[waypoints.Count - 1];
    }

    /// <summary>
    /// 중앙(t=0.5)을 기준으로 -1.0(왼쪽 끝) ~ +1.0(오른쪽 끝) 범위의 오프셋으로 위치 계산
    /// </summary>
    public Vector3 GetPositionByCenterOffset(float normalizedOffset, out Vector3 tangentDir)
    {
        float t01 = (normalizedOffset + 1f) * 0.5f;
        return GetPositionAlongPath(t01, out tangentDir);
    }

    public int TotalPointsCount => (waypoints != null) ? waypoints.Count : 0;

    public Vector3 GetWaypoint(int index)
    {
        if (waypoints == null || waypoints.Count == 0) return transform.position;
        int clampedIdx = Mathf.Clamp(index, 0, waypoints.Count - 1);
        return waypoints[clampedIdx];
    }

    public List<Vector3> GetAllWaypoints()
    {
        if (waypoints == null || waypoints.Count == 0) RefreshPath();
        return waypoints;
    }

    public Vector3 GetCenterPosition()
    {
        Vector3 dummy;
        return GetPositionAlongPath(0.5f, out dummy);
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        RefreshPath();

        if (waypoints == null || waypoints.Count == 0) return;

        // 1. PP01 ~ PP29 점들 구체 및 번호 라벨 표시
        Gizmos.color = gizmoPointColor;
        for (int i = 0; i < waypoints.Count; i++)
        {
            Gizmos.DrawSphere(waypoints[i], 0.4f);
            UnityEditor.Handles.Label(waypoints[i] + Vector3.up * 0.7f, $"PP{i+1:D2}", new GUIStyle
            {
                normal = { textColor = Color.yellow },
                fontSize = 12,
                fontStyle = FontStyle.Bold
            });
        }

        // 2. 연결선 표시
        Gizmos.color = gizmoLineColor;
        for (int i = 0; i < waypoints.Count - 1; i++)
        {
            Gizmos.DrawLine(waypoints[i], waypoints[i + 1]);
            UnityEditor.Handles.DrawAAPolyLine(4f, waypoints[i], waypoints[i + 1]);
        }

        // 3. 중앙 시작점(PP15 / Center) 강조 표시
        if (waypoints.Count > 1)
        {
            Vector3 center = GetCenterPosition();
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireCube(center, Vector3.one * 0.8f);
            UnityEditor.Handles.Label(center + Vector3.up * 1.2f, "★ Character Start (Center) ★", new GUIStyle
            {
                normal = { textColor = Color.cyan },
                fontSize = 13,
                fontStyle = FontStyle.Bold
            });
        }
    }
#endif
}
