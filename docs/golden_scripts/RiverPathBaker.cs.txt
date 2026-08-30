#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace SkippingStones.Terrain
{
    /// <summary>
    /// [3D 메쉬 청크 강줄기 자동 추적 & 곡선 베이킹 엔진]
    /// - 청크의 Anchor_S ➔ Anchor_E 구간을 지정된 간격(기본 5m)으로 횡단면 슬라이스
    /// - WaterSurface 및 지형 메쉬(MeshCollider/MeshFilter)를 스캔하여 좌/우 강둑 경계점과 중심선(Centerline) 및 강폭(Width) 산출
    /// - 부드러운 스플라인으로 가공하여 RiverPathChunkData에 영구 저장
    /// </summary>
    public static class RiverPathBaker
    {
        public static bool BakeRiverPathForChunk(GameObject chunkRoot, float sampleInterval = 5f, float maxScanWidth = 200f)
        {
            if (chunkRoot == null) return false;

            Transform anchorS = MapAnchorHelper.FindStartAnchor(chunkRoot);
            Transform anchorE = MapAnchorHelper.FindEndAnchor(chunkRoot);

            if (anchorS == null || anchorE == null)
            {
                Debug.LogWarning($"[RiverPathBaker] ⚠️ '{chunkRoot.name}'에서 앵커(Anchor_S / Anchor_E)를 찾을 수 없습니다. 지형 바운드 기준으로 임시 생성 후 베이킹합니다.");
                MapAnchorHelper.GetOrCreateAnchors(chunkRoot, out anchorS, out anchorE);
            }

            Vector3 startLocal = chunkRoot.transform.InverseTransformPoint(anchorS.position);
            Vector3 endLocal = chunkRoot.transform.InverseTransformPoint(anchorE.position);

            float totalZSpan = endLocal.z - startLocal.z;
            float totalDistEstimate = Vector3.Distance(startLocal, endLocal);
            if (totalDistEstimate < 5f)
            {
                totalDistEstimate = 500f;
                totalZSpan = 500f;
            }

            int stepCount = Mathf.Max(3, Mathf.CeilToInt(totalDistEstimate / sampleInterval));
            List<RiverPathNode> bakedNodes = new List<RiverPathNode>();

            // WaterSurface 박스 콜라이더 및 메쉬 콜라이더 수집
            WaterSurface ws = chunkRoot.GetComponentInChildren<WaterSurface>(true);
            BoxCollider waterBox = ws != null ? ws.GetComponent<BoxCollider>() : null;
            float defaultWaterY = waterBox != null ? waterBox.center.y + (waterBox.size.y * 0.5f) : startLocal.y;

            // 지형 MeshCollider들 수집
            MeshCollider[] terrainCols = chunkRoot.GetComponentsInChildren<MeshCollider>(true);

            float accumulatedDist = 0f;
            Vector3 prevNodePos = startLocal;

            for (int i = 0; i <= stepCount; i++)
            {
                float t = (float)i / stepCount;
                // 기본 보간 지점
                Vector3 basePos = Vector3.Lerp(startLocal, endLocal, t);

                float leftX = -25f;
                float rightX = 25f;
                float nodeWaterY = defaultWaterY;

                // 1. WaterSurface BoxCollider가 존재할 경우 박스 내부 영역 정밀 측정
                if (waterBox != null)
                {
                    Transform wt = waterBox.transform;
                    Vector3 boxCenterLocalToChunk = chunkRoot.transform.InverseTransformPoint(wt.TransformPoint(waterBox.center));
                    Vector3 boxSize = Vector3.Scale(waterBox.size, wt.lossyScale);

                    leftX = boxCenterLocalToChunk.x - (boxSize.x * 0.5f);
                    rightX = boxCenterLocalToChunk.x + (boxSize.x * 0.5f);
                    nodeWaterY = boxCenterLocalToChunk.y + (boxSize.y * 0.5f);
                }

                // 2. 지형 메쉬 버텍스 횡단면 스캔을 통한 실제 강둑(Ground) 경계점 정밀 보정
                if (terrainCols != null && terrainCols.Length > 0)
                {
                    float detectedLeft = float.MinValue;
                    float detectedRight = float.MaxValue;

                    foreach (var mc in terrainCols)
                    {
                        if (mc == null || mc.sharedMesh == null) continue;
                        if (mc.gameObject.name.ToLowerInvariant().Contains("water")) continue;

                        Bounds b = mc.sharedMesh.bounds;
                        // 청크 로컬 기준 바운드
                        Vector3 colMin = chunkRoot.transform.InverseTransformPoint(mc.transform.TransformPoint(b.min));
                        Vector3 colMax = chunkRoot.transform.InverseTransformPoint(mc.transform.TransformPoint(b.max));

                        // Z 슬라이스 범위 내에 포함되는 경우
                        if (basePos.z >= Mathf.Min(colMin.z, colMax.z) - 5f && basePos.z <= Mathf.Max(colMin.z, colMax.z) + 5f)
                        {
                            // 중심점 좌/우 지형의 유효 하한/상한 보정
                            if (colMin.x < basePos.x && colMin.x > detectedLeft) detectedLeft = colMin.x;
                            if (colMax.x > basePos.x && colMax.x < detectedRight) detectedRight = colMax.x;
                        }
                    }

                    if (detectedLeft > float.MinValue && detectedLeft < basePos.x) leftX = detectedLeft;
                    if (detectedRight < float.MaxValue && detectedRight > basePos.x) rightX = detectedRight;
                }

                // 중심점 및 강폭 계산
                float centerX = (leftX + rightX) * 0.5f;
                float halfWidth = Mathf.Max(5f, (rightX - leftX) * 0.5f);

                Vector3 nodeLocalPos = new Vector3(centerX, nodeWaterY, basePos.z);

                if (i > 0)
                {
                    accumulatedDist += Vector3.Distance(prevNodePos, nodeLocalPos);
                }
                prevNodePos = nodeLocalPos;

                RiverPathNode node = new RiverPathNode
                {
                    localPosition = nodeLocalPos,
                    localTangent = (endLocal - startLocal).normalized,
                    waterHeight = nodeWaterY,
                    leftWidth = halfWidth,
                    rightWidth = halfWidth,
                    cumulativeDistance = accumulatedDist
                };

                bakedNodes.Add(node);
            }

            // 각 노드의 접선(Tangent) 벡터 정밀 계산 (Catmull-Rom 중앙 차분)
            for (int i = 0; i < bakedNodes.Count; i++)
            {
                Vector3 pPrev = (i > 0) ? bakedNodes[i - 1].localPosition : bakedNodes[0].localPosition;
                Vector3 pNext = (i < bakedNodes.Count - 1) ? bakedNodes[i + 1].localPosition : bakedNodes[bakedNodes.Count - 1].localPosition;

                Vector3 tangent = (pNext - pPrev).normalized;
                if (tangent == Vector3.zero) tangent = Vector3.forward;

                RiverPathNode n = bakedNodes[i];
                n.localTangent = tangent;
                bakedNodes[i] = n;
            }

            // RiverPathChunkData 컴포넌트에 주입
            RiverPathChunkData data = chunkRoot.GetComponent<RiverPathChunkData>();
            if (data == null) data = chunkRoot.AddComponent<RiverPathChunkData>();

            data.nodes = bakedNodes;
            data.totalLength = accumulatedDist;
            data.averageWidth = (bakedNodes.Count > 0) ? bakedNodes[0].totalWidth : 50f;

            EditorUtility.SetDirty(chunkRoot);
            Debug.Log($"[RiverPathBaker] ✅ '{chunkRoot.name}' 강줄기 베이킹 완료! (노드 {bakedNodes.Count}개, 총 길이 {accumulatedDist:F1}m, 평균 강폭 {data.averageWidth:F1}m)");

            return true;
        }
    }
}
#endif
