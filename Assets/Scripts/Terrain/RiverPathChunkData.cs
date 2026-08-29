using System;
using System.Collections.Generic;
using UnityEngine;

namespace SkippingStones.Terrain
{
    /// <summary>
    /// 강줄기 스플라인 상의 단일 베이킹 노드
    /// </summary>
    [Serializable]
    public struct RiverPathNode
    {
        [Tooltip("청크 기준 로컬 중심선 좌표")]
        public Vector3 localPosition;

        [Tooltip("물길 진행 방향 접선 단위 벡터")]
        public Vector3 localTangent;

        [Tooltip("노드 위치에서의 로컬 수면 높이(Y)")]
        public float waterHeight;

        [Tooltip("좌측 강둑까지의 유효 거리(m)")]
        public float leftWidth;

        [Tooltip("우측 강둑까지의 유효 거리(m)")]
        public float rightWidth;

        [Tooltip("전체 유효 강폭(m)")]
        public float totalWidth => leftWidth + rightWidth;

        [Tooltip("Anchor_S로부터의 누적 곡선 거리(m)")]
        public float cumulativeDistance;
    }

    /// <summary>
    /// [청크별 강줄기 곡선 & 강폭 베이킹 데이터 컴포넌트]
    /// - 3D 메쉬 맵 청크(Brook_Start, Brook_M_01~04 등)에 부착되어 사전 계산된 물길 중심선 및 강폭 정보 보관
    /// - 런타임 레이캐스트 부하 0%로 갓모드 곡선 비행 및 RiverSpawner 안전 수면 플로팅 스폰 지원
    /// </summary>
    [DisallowMultipleComponent]
    public class RiverPathChunkData : MonoBehaviour
    {
        [Header("🌊 베이킹된 강줄기 노드 데이터")]
        [SerializeField]
        public List<RiverPathNode> nodes = new List<RiverPathNode>();

        [Tooltip("청크 내 강줄기 총 곡선 길이(m)")]
        public float totalLength = 500f;

        [Tooltip("평균 강폭(m)")]
        public float averageWidth = 30f;

        [Header("기즈모 시각화 설정")]
        public bool showGizmos = true;
        public Color centerlineColor = new Color(0f, 0.9f, 1f, 0.9f);
        public Color bankBoundaryColor = new Color(1f, 0.85f, 0.2f, 0.45f);

        /// <summary>
        /// 청크 내 로컬 곡선 거리(localDistance)에 해당하는 중심선 위치, 진행 접선, 강폭, 수면높이 보간 평가
        /// </summary>
        public bool EvaluateLocal(float localDistance, out Vector3 localPos, out Vector3 localTangent, out float width, out float waterY)
        {
            localPos = Vector3.zero;
            localTangent = Vector3.forward;
            width = averageWidth;
            waterY = 0f;

            if (nodes == null || nodes.Count == 0) return false;
            if (nodes.Count == 1)
            {
                localPos = nodes[0].localPosition;
                localTangent = nodes[0].localTangent;
                width = nodes[0].totalWidth;
                waterY = nodes[0].waterHeight;
                return true;
            }

            localDistance = Mathf.Clamp(localDistance, 0f, totalLength);

            // 누적 거리에 따른 인접 2개 노드 탐색
            int idx = 0;
            for (int i = 0; i < nodes.Count - 1; i++)
            {
                if (localDistance >= nodes[i].cumulativeDistance && localDistance <= nodes[i + 1].cumulativeDistance)
                {
                    idx = i;
                    break;
                }
            }

            RiverPathNode n0 = nodes[idx];
            RiverPathNode n1 = nodes[Mathf.Min(idx + 1, nodes.Count - 1)];

            float segLen = n1.cumulativeDistance - n0.cumulativeDistance;
            float t = (segLen > 0.0001f) ? (localDistance - n0.cumulativeDistance) / segLen : 0f;
            t = Mathf.Clamp01(t);

            // 선형 및 큐빅 스플라인 보간
            localPos = Vector3.Lerp(n0.localPosition, n1.localPosition, t);
            localTangent = Vector3.Slerp(n0.localTangent, n1.localTangent, t).normalized;
            width = Mathf.Lerp(n0.totalWidth, n1.totalWidth, t);
            waterY = Mathf.Lerp(n0.waterHeight, n1.waterHeight, t);

            return true;
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (!showGizmos || nodes == null || nodes.Count < 2) return;

            // 1. 강줄기 중심선 (청록색)
            Gizmos.color = centerlineColor;
            for (int i = 0; i < nodes.Count - 1; i++)
            {
                Vector3 p0 = transform.TransformPoint(nodes[i].localPosition);
                Vector3 p1 = transform.TransformPoint(nodes[i + 1].localPosition);
                Gizmos.DrawLine(p0, p1);
                Gizmos.DrawSphere(p0, 0.4f);
            }
            if (nodes.Count > 0)
            {
                Gizmos.DrawSphere(transform.TransformPoint(nodes[nodes.Count - 1].localPosition), 0.4f);
            }

            // 2. 좌우 강폭 경계 리본 (노란색)
            Gizmos.color = bankBoundaryColor;
            for (int i = 0; i < nodes.Count; i++)
            {
                Vector3 center = transform.TransformPoint(nodes[i].localPosition);
                Vector3 right = transform.TransformDirection(Vector3.Cross(Vector3.up, nodes[i].localTangent).normalized);

                Vector3 leftPt = center - right * nodes[i].leftWidth;
                Vector3 rightPt = center + right * nodes[i].rightWidth;

                Gizmos.DrawLine(leftPt, rightPt);

                if (i < nodes.Count - 1)
                {
                    Vector3 nextCenter = transform.TransformPoint(nodes[i + 1].localPosition);
                    Vector3 nextRight = transform.TransformDirection(Vector3.Cross(Vector3.up, nodes[i + 1].localTangent).normalized);

                    Vector3 nextLeftPt = nextCenter - nextRight * nodes[i + 1].leftWidth;
                    Vector3 nextRightPt = nextCenter + nextRight * nodes[i + 1].rightWidth;

                    Gizmos.DrawLine(leftPt, nextLeftPt);
                    Gizmos.DrawLine(rightPt, nextRightPt);
                }
            }
        }
#endif
    }
}
