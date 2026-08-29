using System.Collections.Generic;
using UnityEngine;

namespace SkippingStones.Terrain
{
    /// <summary>
    /// [글로벌 강줄기 연속 경로 매니저 (GlobalRiverPath)]
    /// - 소켓 앵커로 도킹된 청크들의 RiverPathChunkData를 런타임에 단일 연속 스플라인으로 체인 결합
    /// - 갓모드 곡선 비행, 카메라 접선 회전, RiverSpawner 안전 수면 플로팅 스폰 지원
    /// </summary>
    public class GlobalRiverPath : MonoBehaviour
    {
        private static GlobalRiverPath _instance;
        public static GlobalRiverPath Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindAnyObjectByType<GlobalRiverPath>();
                    if (_instance == null)
                    {
                        GameObject go = new GameObject("[GlobalRiverPath]");
                        _instance = go.AddComponent<GlobalRiverPath>();
                    }
                }
                return _instance;
            }
        }

        public struct ChunkSegment
        {
            public RiverPathChunkData chunkData;
            public Transform chunkTransform;
            public float startDistance;
            public float endDistance;
            public float length => endDistance - startDistance;
        }

        private readonly List<ChunkSegment> segments = new List<ChunkSegment>();
        public float totalRiverLength { get; private set; } = 0f;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
        }

        private void Start()
        {
            RebuildPath();
        }

        /// <summary>
        /// 활성화된 청크들의 RiverPathChunkData를 검색하여 연속된 글로벌 스플라인 체인 재구성
        /// </summary>
        public void RebuildPath()
        {
            segments.Clear();
            totalRiverLength = 0f;

            // 씬 내의 모든 RiverPathChunkData 탐색 후 Z축 / 누적 위치 기준 정렬
            RiverPathChunkData[] allChunks = FindObjectsByType<RiverPathChunkData>(FindObjectsInactive.Exclude);
            if (allChunks == null || allChunks.Length == 0) return;

            // 청크들을 월드 Z축 좌표 기준으로 오름차순 정렬
            System.Array.Sort(allChunks, (a, b) => a.transform.position.z.CompareTo(b.transform.position.z));

            float accumulated = 0f;
            foreach (var chunk in allChunks)
            {
                if (chunk.nodes == null || chunk.nodes.Count == 0) continue;

                float len = chunk.totalLength > 0f ? chunk.totalLength : 500f;
                segments.Add(new ChunkSegment
                {
                    chunkData = chunk,
                    chunkTransform = chunk.transform,
                    startDistance = accumulated,
                    endDistance = accumulated + len
                });

                accumulated += len;
            }

            totalRiverLength = accumulated;
        }

        /// <summary>
        /// 시작점으로부터 totalDistance(m) 떨어진 지점의 월드 중심선 좌표, 진행 접선 방향, 강폭, 수면 Y 높이 반환
        /// </summary>
        public bool EvaluateAtDistance(float totalDistance, out Vector3 worldPos, out Vector3 worldTangent, out float width, out float waterY)
        {
            worldPos = Vector3.zero;
            worldTangent = Vector3.forward;
            width = 30f;
            waterY = 0f;

            if (segments.Count == 0) RebuildPath();
            if (segments.Count == 0) return false;

            totalDistance = Mathf.Clamp(totalDistance, 0f, totalRiverLength);

            // 해당 거리를 포함하는 청크 세그먼트 탐색
            ChunkSegment targetSeg = segments[0];
            for (int i = 0; i < segments.Count; i++)
            {
                if (totalDistance >= segments[i].startDistance && totalDistance <= segments[i].endDistance)
                {
                    targetSeg = segments[i];
                    break;
                }
            }

            float localDist = totalDistance - targetSeg.startDistance;
            if (targetSeg.chunkData != null && targetSeg.chunkData.EvaluateLocal(localDist, out Vector3 lPos, out Vector3 lTan, out width, out float lWaterY))
            {
                worldPos = targetSeg.chunkTransform.TransformPoint(lPos);
                worldTangent = targetSeg.chunkTransform.TransformDirection(lTan).normalized;
                waterY = worldPos.y; // 로컬 좌표가 이미 월드로 변환됨
                return true;
            }

            return false;
        }

        /// <summary>
        /// 특정 월드 좌표에서 가장 가까운 강 중심선 상의 점과 접선 및 오프셋 반환
        /// </summary>
        public bool GetClosestPointOnRiver(Vector3 searchWorldPos, out Vector3 closestWorldPos, out Vector3 tangent, out float distanceAlongRiver)
        {
            closestWorldPos = searchWorldPos;
            tangent = Vector3.forward;
            distanceAlongRiver = 0f;

            if (segments.Count == 0) RebuildPath();
            if (segments.Count == 0) return false;

            float minSqDist = float.MaxValue;
            float bestDist = 0f;

            // 10m 단위 조밀 샘플링 후 최적값 탐색
            float step = 5f;
            for (float d = 0f; d <= totalRiverLength; d += step)
            {
                if (EvaluateAtDistance(d, out Vector3 p, out Vector3 t, out _, out _))
                {
                    float sqDist = (p - searchWorldPos).sqrMagnitude;
                    if (sqDist < minSqDist)
                    {
                        minSqDist = sqDist;
                        bestDist = d;
                        closestWorldPos = p;
                        tangent = t;
                    }
                }
            }

            distanceAlongRiver = bestDist;
            return true;
        }
    }
}
