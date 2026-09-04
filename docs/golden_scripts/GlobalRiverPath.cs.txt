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
                Destroy(this);
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
        /// (LakeEnvironmentManager의 실제 도킹 순서를 최우선 반영하여 곡선/U턴에서도 100% 완벽한 순서 보장)
        /// </summary>
        public void RebuildPath()
        {
            segments.Clear();
            totalRiverLength = 0f;

            List<RiverPathChunkData> orderedChunks = new List<RiverPathChunkData>();

            // 1. LakeEnvironmentManager의 실제 청크 도킹 순서(DynamicChunks) 최우선 수집
            if (LakeEnvironmentManager.Instance != null && LakeEnvironmentManager.Instance.DynamicChunks != null)
            {
                foreach (var chunkObj in LakeEnvironmentManager.Instance.DynamicChunks)
                {
                    if (chunkObj == null) continue;
                    var cData = chunkObj.GetComponentInChildren<RiverPathChunkData>(true);
                    if (cData != null && !orderedChunks.Contains(cData))
                    {
                        orderedChunks.Add(cData);
                    }
                }
            }

            // 2. 씬에 배치된 미등록 청크가 있다면 Z축 좌표 기준으로 보완 수집
            RiverPathChunkData[] allChunks = FindObjectsByType<RiverPathChunkData>(FindObjectsInactive.Exclude);
            if (allChunks != null && allChunks.Length > 0)
            {
                List<RiverPathChunkData> remaining = new List<RiverPathChunkData>();
                foreach (var c in allChunks)
                {
                    if (!orderedChunks.Contains(c)) remaining.Add(c);
                }
                remaining.Sort((a, b) => a.transform.position.z.CompareTo(b.transform.position.z));
                orderedChunks.AddRange(remaining);
            }

            if (orderedChunks.Count == 0) return;

            float accumulated = 0f;
            foreach (var chunk in orderedChunks)
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

            // 🌟 만약 요청 거리가 현재 생성된 강 총 길이를 초과했을 때(청크 생성 경계 순간)
            // 뒤로 묶지(Clamp) 않고 마지막 청크의 끝 접선 방향으로 직진 외삽(Extrapolate)하여 와리가리 유턴 방지
            if (totalDistance > totalRiverLength)
            {
                ChunkSegment lastSeg = segments[segments.Count - 1];
                float overDist = totalDistance - totalRiverLength;
                float localLastDist = lastSeg.length;
                if (lastSeg.chunkData != null && lastSeg.chunkData.EvaluateLocal(localLastDist, out Vector3 lEndPos, out Vector3 lEndTan, out width, out float lastWaterY))
                {
                    Vector3 worldEndPos = lastSeg.chunkTransform.TransformPoint(lEndPos);
                    worldTangent = lastSeg.chunkTransform.TransformDirection(lEndTan).normalized;
                    worldPos = worldEndPos + worldTangent * overDist;
                    waterY = worldEndPos.y;
                    return true;
                }
            }

            totalDistance = Mathf.Max(0f, totalDistance);

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

        /// <summary>
        /// 특정 청크 게임오브젝트의 곡선 시작/끝 거리 반환 (100% 정밀 매칭)
        /// </summary>
        public bool GetSegmentDistanceRange(GameObject chunkObj, out float startDist, out float endDist)
        {
            startDist = 0f;
            endDist = 500f;

            if (chunkObj == null) return false;
            if (segments.Count == 0) RebuildPath();
            if (segments.Count == 0) return false;

            foreach (var seg in segments)
            {
                if (seg.chunkTransform != null && (seg.chunkTransform.gameObject == chunkObj || seg.chunkTransform.IsChildOf(chunkObj.transform) || chunkObj.transform.IsChildOf(seg.chunkTransform)))
                {
                    startDist = seg.startDistance;
                    endDist = seg.endDistance;
                    return true;
                }
            }

            return GetSegmentDistanceRange(chunkObj.transform.position.z, out startDist, out endDist);
        }

        /// <summary>
        /// 특정 청크 인덱스의 곡선 시작/끝 거리 반환
        /// </summary>
        public bool GetSegmentDistanceRangeByIndex(int chunkIndex, out float startDist, out float endDist)
        {
            startDist = 0f;
            endDist = 500f;

            if (segments.Count == 0) RebuildPath();
            if (segments.Count == 0) return false;

            if (chunkIndex >= 0 && chunkIndex < segments.Count)
            {
                startDist = segments[chunkIndex].startDistance;
                endDist = segments[chunkIndex].endDistance;
                return true;
            }

            // 인덱스 초과 시 마지막 세그먼트 반환
            startDist = segments[segments.Count - 1].startDistance;
            endDist = segments[segments.Count - 1].endDistance;
            return true;
        }

        /// <summary>
        /// 특정 월드 Z좌표에 위치한 청크 세그먼트의 곡선 시작/끝 거리(startDist, endDist) 반환
        /// </summary>
        public bool GetSegmentDistanceRange(float worldZ, out float startDist, out float endDist)
        {
            startDist = 0f;
            endDist = 500f;

            if (segments.Count == 0) RebuildPath();
            if (segments.Count == 0) return false;

            // 월드 Z와 가장 가까운 청크 세그먼트 매칭
            ChunkSegment bestSeg = segments[0];
            float minDiff = float.MaxValue;

            foreach (var seg in segments)
            {
                if (seg.chunkTransform == null) continue;
                float diff = Mathf.Abs(seg.chunkTransform.position.z - worldZ);
                if (diff < minDiff)
                {
                    minDiff = diff;
                    bestSeg = seg;
                }
            }

            startDist = bestSeg.startDistance;
            endDist = bestSeg.endDistance;
            return true;
        }
    }
}
