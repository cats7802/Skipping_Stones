#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace SkippingStones.Terrain
{
    /// <summary>
    /// [3D 메쉬 청크 강줄기 자동 추적 & 곡선 법선 단면 베이킹 엔진]
    /// - 직선 맵뿐만 아니라 90도 급커브 및 복합 곡선 맵도 완벽 지원
    /// - Anchor_S ➔ Anchor_E 베지어 베이스라인을 따라 진행 방향의 수직 법선(Normal) 단면을 회전 스캔
    /// - 지형 메쉬 버텍스(Mesh.vertices)를 직접 C#에서 수학적으로 분석하여 V자 골짜기 중심선과 실제 강폭 산출
    /// - 실제 S자/커브 굴곡을 100% 반영한 3D 연속 스플라인 경로와 강폭 데이터를 프리팹/씬에 영구 직렬화
    /// </summary>
    public static class RiverPathBaker
    {
        private struct WaterChannel
        {
            public float leftOffset;
            public float rightOffset;
            public float centerOffset => (leftOffset + rightOffset) * 0.5f;
            public float width => rightOffset - leftOffset;
        }

        private struct TransformedTerrainMesh
        {
            public Vector3[] localVertices;
            public Bounds localBounds;
        }

        public static bool BakeRiverPathForChunk(GameObject chunkRoot, float sampleInterval = 2.5f, float scanHalfWidth = 150f)
        {
            if (chunkRoot == null) return false;

            bool isPrefabAsset = PrefabUtility.IsPartOfPrefabAsset(chunkRoot);

            // 1. 앵커(Anchor_S / Anchor_E) 탐색 및 방향 산출
            Transform anchorS = MapAnchorHelper.FindStartAnchor(chunkRoot);
            Transform anchorE = MapAnchorHelper.FindEndAnchor(chunkRoot);

            if (anchorS == null || anchorE == null)
            {
                MapAnchorHelper.GetOrCreateAnchors(chunkRoot, out anchorS, out anchorE);
            }

            Vector3 startLocal = chunkRoot.transform.InverseTransformPoint(anchorS.position);
            Vector3 endLocal = chunkRoot.transform.InverseTransformPoint(anchorE.position);

            Vector3 startFwd = chunkRoot.transform.InverseTransformDirection(anchorS.forward).normalized;
            Vector3 endFwd = chunkRoot.transform.InverseTransformDirection(anchorE.forward).normalized;

            if (startFwd == Vector3.zero) startFwd = Vector3.forward;
            if (endFwd == Vector3.zero) endFwd = (endLocal - startLocal).normalized;

            float chordDist = Vector3.Distance(startLocal, endLocal);
            if (chordDist < 10f) chordDist = 500f;

            // 베지어 제어점 생성 (커브 맵의 회전 궤적 추종)
            Vector3 p0 = startLocal;
            Vector3 p1 = startLocal + startFwd * (chordDist * 0.4f);
            Vector3 p2 = endLocal - endFwd * (chordDist * 0.4f);
            Vector3 p3 = endLocal;

            // 2. 수면 높이(waterY) 취득
            WaterSurface ws = chunkRoot.GetComponentInChildren<WaterSurface>(true);
            BoxCollider waterBox = ws != null ? ws.GetComponent<BoxCollider>() : null;
            float defaultWaterY = 0f;
            if (waterBox != null)
            {
                Transform wt = waterBox.transform;
                Vector3 boxCenterLocal = chunkRoot.transform.InverseTransformPoint(wt.TransformPoint(waterBox.center));
                Vector3 boxScale = wt.lossyScale;
                defaultWaterY = boxCenterLocal.y + (waterBox.size.y * boxScale.y * 0.5f);
            }

            // 3. 지형 메쉬 버텍스 수집
            MeshFilter[] terrainFilters = chunkRoot.GetComponentsInChildren<MeshFilter>(true);
            List<TransformedTerrainMesh> terrainMeshes = new List<TransformedTerrainMesh>();

            foreach (var mf in terrainFilters)
            {
                if (mf == null || mf.sharedMesh == null) continue;
                string lowerName = mf.gameObject.name.ToLowerInvariant();
                if (lowerName.Contains("water") || lowerName.Contains("pier") || lowerName.Contains("camera") || lowerName.Contains("ui")) continue;

                Mesh m = mf.sharedMesh;
                Vector3[] rawVerts = m.vertices;
                Vector3[] localVerts = new Vector3[rawVerts.Length];
                Transform t = mf.transform;

                Vector3 bMin = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
                Vector3 bMax = new Vector3(float.MinValue, float.MinValue, float.MinValue);

                for (int v = 0; v < rawVerts.Length; v++)
                {
                    Vector3 worldV = t.TransformPoint(rawVerts[v]);
                    Vector3 chunkLocalV = chunkRoot.transform.InverseTransformPoint(worldV);
                    localVerts[v] = chunkLocalV;

                    bMin = Vector3.Min(bMin, chunkLocalV);
                    bMax = Vector3.Max(bMax, chunkLocalV);
                }

                Bounds localB = new Bounds((bMin + bMax) * 0.5f, bMax - bMin);
                terrainMeshes.Add(new TransformedTerrainMesh { localVertices = localVerts, localBounds = localB });
            }

            // 대략적인 총 호(Arc) 길이 계산
            float estimatedArcLength = 0f;
            Vector3 prevEval = p0;
            for (int i = 1; i <= 20; i++)
            {
                Vector3 currEval = EvaluateCubicBezier(p0, p1, p2, p3, i / 20f);
                estimatedArcLength += Vector3.Distance(prevEval, currEval);
                prevEval = currEval;
            }
            if (estimatedArcLength < 10f) estimatedArcLength = chordDist;

            int stepCount = Mathf.Max(8, Mathf.CeilToInt(estimatedArcLength / sampleInterval));
            List<RiverPathNode> bakedNodes = new List<RiverPathNode>();

            float accumulatedDist = 0f;
            Vector3 prevNodePos = startLocal;
            float lastSelectedOffset = 0f;

            float scanStepOffset = 1.0f; // 1m 단위 횡단면 오프셋 검사

            for (int i = 0; i <= stepCount; i++)
            {
                float t = (float)i / stepCount;
                Vector3 basePos = EvaluateCubicBezier(p0, p1, p2, p3, t);
                Vector3 tangent = EvaluateCubicBezierTangent(p0, p1, p2, p3, t);
                Vector3 normal = Vector3.Cross(Vector3.up, tangent).normalized;
                if (normal == Vector3.zero) normal = Vector3.right;

                // 🌟 진행 방향(Tangent)의 수직 법선(Normal) 단면을 회전 스캔하여 물길 검출
                List<WaterChannel> detectedChannels = ScanChannelsAlongNormal(terrainMeshes, basePos, normal, defaultWaterY, scanHalfWidth, scanStepOffset);

                float chosenLeftOffset = -18f;
                float chosenRightOffset = 18f;

                if (detectedChannels.Count > 0)
                {
                    WaterChannel bestChannel = detectedChannels[0];
                    float minDiff = float.MaxValue;

                    foreach (var ch in detectedChannels)
                    {
                        if (ch.width < 4.0f && detectedChannels.Count > 1) continue;

                        float diff = Mathf.Abs(ch.centerOffset - lastSelectedOffset);
                        if (diff < minDiff)
                        {
                            minDiff = diff;
                            bestChannel = ch;
                        }
                    }

                    chosenLeftOffset = bestChannel.leftOffset;
                    chosenRightOffset = bestChannel.rightOffset;
                    lastSelectedOffset = bestChannel.centerOffset;
                }
                else
                {
                    chosenLeftOffset = lastSelectedOffset - 16f;
                    chosenRightOffset = lastSelectedOffset + 16f;
                }

                float midOffset = (chosenLeftOffset + chosenRightOffset) * 0.5f;
                float leftW = Mathf.Max(4f, midOffset - chosenLeftOffset);
                float rightW = Mathf.Max(4f, chosenRightOffset - midOffset);

                Vector3 nodeLocalPos = basePos + normal * midOffset;
                nodeLocalPos.y = defaultWaterY;

                if (i > 0)
                {
                    accumulatedDist += Vector3.Distance(prevNodePos, nodeLocalPos);
                }
                prevNodePos = nodeLocalPos;

                RiverPathNode node = new RiverPathNode
                {
                    localPosition = nodeLocalPos,
                    localTangent = tangent,
                    waterHeight = defaultWaterY,
                    leftWidth = leftW,
                    rightWidth = rightW,
                    cumulativeDistance = accumulatedDist
                };

                bakedNodes.Add(node);
            }

            // 4. 중심선 스플라인 스무딩
            SmoothPathCenterline(bakedNodes);

            // 5. 각 노드의 접선(Tangent) 벡터 정밀 갱신
            for (int i = 0; i < bakedNodes.Count; i++)
            {
                Vector3 pPrev = (i > 0) ? bakedNodes[i - 1].localPosition : bakedNodes[0].localPosition;
                Vector3 pNext = (i < bakedNodes.Count - 1) ? bakedNodes[i + 1].localPosition : bakedNodes[bakedNodes.Count - 1].localPosition;

                Vector3 tan = (pNext - pPrev).normalized;
                if (tan == Vector3.zero) tan = bakedNodes[i].localTangent;

                RiverPathNode n = bakedNodes[i];
                n.localTangent = tan;
                bakedNodes[i] = n;
            }

            // 6. RiverPathChunkData 컴포넌트에 주입 및 프리팹/씬 영구 저장
            RiverPathChunkData data = chunkRoot.GetComponent<RiverPathChunkData>();
            if (data == null) data = chunkRoot.AddComponent<RiverPathChunkData>();

            data.nodes = bakedNodes;
            data.totalLength = accumulatedDist;
            data.averageWidth = (bakedNodes.Count > 0) ? bakedNodes[0].totalWidth : 50f;

            EditorUtility.SetDirty(chunkRoot);
            if (isPrefabAsset)
            {
                PrefabUtility.SavePrefabAsset(chunkRoot);
            }

            Debug.Log($"[RiverPathBaker] 🌟 '{chunkRoot.name}' 커브 적응형 S자 곡선 베이킹 완료! (노드 {bakedNodes.Count}개, 총 길이 {accumulatedDist:F1}m, 평균 강폭 {data.averageWidth:F1}m)");
            return true;
        }

        private static Vector3 EvaluateCubicBezier(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
        {
            t = Mathf.Clamp01(t);
            float u = 1f - t;
            return (u * u * u * p0) + (3f * u * u * t * p1) + (3f * u * t * t * p2) + (t * t * t * p3);
        }

        private static Vector3 EvaluateCubicBezierTangent(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
        {
            t = Mathf.Clamp01(t);
            float u = 1f - t;
            Vector3 tan = 3f * u * u * (p1 - p0) + 6f * u * t * (p2 - p1) + 3f * t * t * (p3 - p2);
            return (tan == Vector3.zero) ? Vector3.forward : tan.normalized;
        }

        /// <summary>
        /// 진행 법선(Normal) 단면을 따라 수면(waterY) 아래로 파여있는 V자 골짜기 침수 구간 검출
        /// </summary>
        private static List<WaterChannel> ScanChannelsAlongNormal(List<TransformedTerrainMesh> meshes, Vector3 basePos, Vector3 normal, float waterY, float halfScanWidth, float stepOffset)
        {
            List<WaterChannel> channels = new List<WaterChannel>();
            float sampleRadius = 4.0f;

            bool inWater = false;
            float channelStartOffset = 0f;

            for (float offset = -halfScanWidth; offset <= halfScanWidth; offset += stepOffset)
            {
                Vector3 samplePos = basePos + normal * offset;

                // 해당 위치의 지형 버텍스 높이 샘플링
                float sampledHeight = SampleHeightFromAllMeshes(meshes, samplePos, sampleRadius);

                bool isUnderwater = (sampledHeight < waterY + 0.15f);

                if (isUnderwater && !inWater)
                {
                    inWater = true;
                    channelStartOffset = offset;
                }
                else if (!isUnderwater && inWater)
                {
                    inWater = false;
                    float channelEndOffset = offset - stepOffset;
                    if (channelEndOffset - channelStartOffset >= 3.0f)
                    {
                        channels.Add(new WaterChannel { leftOffset = channelStartOffset, rightOffset = channelEndOffset });
                    }
                }
            }

            if (inWater)
            {
                channels.Add(new WaterChannel { leftOffset = channelStartOffset, rightOffset = halfScanWidth });
            }

            return channels;
        }

        private static float SampleHeightFromAllMeshes(List<TransformedTerrainMesh> meshes, Vector3 targetPos, float searchRadius)
        {
            float searchRadiusSq = searchRadius * searchRadius;
            float totalWeight = 0f;
            float weightedHeight = 0f;
            float nearestDistSq = float.MaxValue;
            float nearestHeight = 0f;

            foreach (var tm in meshes)
            {
                if (targetPos.x < tm.localBounds.min.x - searchRadius || targetPos.x > tm.localBounds.max.x + searchRadius) continue;
                if (targetPos.z < tm.localBounds.min.z - searchRadius || targetPos.z > tm.localBounds.max.z + searchRadius) continue;

                foreach (var v in tm.localVertices)
                {
                    float dx = v.x - targetPos.x;
                    float dz = v.z - targetPos.z;
                    float distSq = dx * dx + dz * dz;

                    if (distSq < nearestDistSq)
                    {
                        nearestDistSq = distSq;
                        nearestHeight = v.y;
                    }

                    if (distSq <= searchRadiusSq)
                    {
                        float weight = 1f / (Mathf.Sqrt(distSq) + 0.1f);
                        weightedHeight += v.y * weight;
                        totalWeight += weight;
                    }
                }
            }

            if (totalWeight > 0.0001f)
            {
                return weightedHeight / totalWeight;
            }

            return nearestHeight;
        }

        private static void SmoothPathCenterline(List<RiverPathNode> nodes)
        {
            if (nodes.Count < 3) return;

            for (int iter = 0; iter < 2; iter++)
            {
                for (int i = 1; i < nodes.Count - 1; i++)
                {
                    RiverPathNode prev = nodes[i - 1];
                    RiverPathNode curr = nodes[i];
                    RiverPathNode next = nodes[i + 1];

                    Vector3 smoothPos = (prev.localPosition * 0.25f) + (curr.localPosition * 0.5f) + (next.localPosition * 0.25f);
                    curr.localPosition = smoothPos;
                    nodes[i] = curr;
                }
            }
        }
    }
}
#endif
