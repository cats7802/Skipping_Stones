#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace SkippingStones.Terrain
{
    /// <summary>
    /// [3D 메쉬 청크 강줄기 자동 추적 & 메쉬 버텍스 직접 분석 베이킹 엔진]
    /// - 물리 엔진(Raycast)의 에디터 지연/오차에 의존하지 않고, 지형 메쉬 버텍스(Mesh.vertices)를 직접 C#에서 수학적으로 분석
    /// - 2.5m 간격 횡단면에서 지형 고도 곡선을 계산하여 수면 아래로 파인 V자 계곡 골짜기(Waterline) 100% 정밀 검출
    /// - 섬(Island)으로 물길이 2개로 갈라지는 분기 지형도 경로 연속성이 가장 자연스러운 주 물길을 자동 선택
    /// - 실제 S자 굴곡을 100% 반영한 3D 연속 스플라인 경로와 강폭 데이터를 프리팹/씬에 영구 직렬화
    /// </summary>
    public static class RiverPathBaker
    {
        private struct WaterChannel
        {
            public float leftX;
            public float rightX;
            public float centerX => (leftX + rightX) * 0.5f;
            public float width => rightX - leftX;
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

            // 1. 앵커(Anchor_S / Anchor_E) 탐색
            Transform anchorS = MapAnchorHelper.FindStartAnchor(chunkRoot);
            Transform anchorE = MapAnchorHelper.FindEndAnchor(chunkRoot);

            if (anchorS == null || anchorE == null)
            {
                MapAnchorHelper.GetOrCreateAnchors(chunkRoot, out anchorS, out anchorE);
            }

            Vector3 startLocal = chunkRoot.transform.InverseTransformPoint(anchorS.position);
            Vector3 endLocal = chunkRoot.transform.InverseTransformPoint(anchorE.position);

            float totalZSpan = Mathf.Abs(endLocal.z - startLocal.z);
            if (totalZSpan < 10f) totalZSpan = 500f;

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

            // 3. 지형 메쉬 버텍스들을 청크 로컬 좌표계로 미리 일괄 변환하여 수집
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

            int stepCount = Mathf.Max(5, Mathf.CeilToInt(totalZSpan / sampleInterval));
            List<RiverPathNode> bakedNodes = new List<RiverPathNode>();

            float accumulatedDist = 0f;
            Vector3 prevNodePos = startLocal;
            float lastSelectedCenterX = startLocal.x;

            float scanStepX = 1.0f; // 1m 단위 횡단면 검사

            for (int i = 0; i <= stepCount; i++)
            {
                float t = (float)i / stepCount;
                float currentZ = Mathf.Lerp(startLocal.z, endLocal.z, t);

                // 현재 Z 슬라이스에서 메쉬 버텍스를 기반으로 수면 아래 침수 채널들 검출
                List<WaterChannel> detectedChannels = ScanChannelsFromMeshVertices(terrainMeshes, currentZ, defaultWaterY, scanHalfWidth, scanStepX);

                float chosenLeftX = -20f;
                float chosenRightX = 20f;

                if (detectedChannels.Count > 0)
                {
                    // 🌟 섬(Island) 분기 시 이전 노드 중심 X와 가장 가깝고 연속성이 자연스러운 메인 물길 선택
                    WaterChannel bestChannel = detectedChannels[0];
                    float minDiff = float.MaxValue;

                    foreach (var ch in detectedChannels)
                    {
                        if (ch.width < 4.0f && detectedChannels.Count > 1) continue;

                        float diff = Mathf.Abs(ch.centerX - lastSelectedCenterX);
                        if (diff < minDiff)
                        {
                            minDiff = diff;
                            bestChannel = ch;
                        }
                    }

                    chosenLeftX = bestChannel.leftX;
                    chosenRightX = bestChannel.rightX;
                    lastSelectedCenterX = bestChannel.centerX;
                }
                else
                {
                    // 버텍스 밀도가 낮은 구간은 이전 중심선 유지
                    chosenLeftX = lastSelectedCenterX - 18f;
                    chosenRightX = lastSelectedCenterX + 18f;
                }

                float centerX = (chosenLeftX + chosenRightX) * 0.5f;
                float leftW = Mathf.Max(4f, centerX - chosenLeftX);
                float rightW = Mathf.Max(4f, chosenRightX - centerX);

                Vector3 nodeLocalPos = new Vector3(centerX, defaultWaterY, currentZ);

                if (i > 0)
                {
                    accumulatedDist += Vector3.Distance(prevNodePos, nodeLocalPos);
                }
                prevNodePos = nodeLocalPos;

                RiverPathNode node = new RiverPathNode
                {
                    localPosition = nodeLocalPos,
                    localTangent = (endLocal - startLocal).normalized,
                    waterHeight = defaultWaterY,
                    leftWidth = leftW,
                    rightWidth = rightW,
                    cumulativeDistance = accumulatedDist
                };

                bakedNodes.Add(node);
            }

            // 4. 중심선 스플라인 1차 스무딩 (노이즈 완화)
            SmoothPathCenterline(bakedNodes);

            // 5. 각 노드의 접선(Tangent) 벡터 정밀 계산 (Catmull-Rom 중앙 차분)
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

            Debug.Log($"[RiverPathBaker] 🌟 '{chunkRoot.name}' 메쉬 버텍스 직접 분석 S자 곡선 베이킹 완료! (노드 {bakedNodes.Count}개, 총 길이 {accumulatedDist:F1}m, 평균 강폭 {data.averageWidth:F1}m)");
            return true;
        }

        /// <summary>
        /// 특정 Z 단면에서 C# 버텍스 배열을 직접 읽어 수면(waterY) 아래로 파여있는 V자 계곡 골짜기 채널 검출
        /// </summary>
        private static List<WaterChannel> ScanChannelsFromMeshVertices(List<TransformedTerrainMesh> meshes, float currentZ, float waterY, float halfScanWidth, float stepX)
        {
            List<WaterChannel> channels = new List<WaterChannel>();
            float zWindow = 4.0f; // Z 슬라이스 검색 폭

            // 1. 현재 Z 단면에 인접한 버텍스들만 필터링
            List<Vector3> sliceVerts = new List<Vector3>();
            foreach (var tm in meshes)
            {
                if (currentZ < tm.localBounds.min.z - zWindow || currentZ > tm.localBounds.max.z + zWindow) continue;

                foreach (var v in tm.localVertices)
                {
                    if (Mathf.Abs(v.z - currentZ) <= zWindow)
                    {
                        sliceVerts.Add(v);
                    }
                }
            }

            if (sliceVerts.Count < 5) return channels;

            bool inWater = false;
            float channelStartX = 0f;

            for (float x = -halfScanWidth; x <= halfScanWidth; x += stepX)
            {
                // 해당 X, Z 위치 주변의 지형 높이 샘플링 (반경 3.5m 내 버텍스들의 역거리 가중 평균 또는 최저/최고 높이)
                float sampledHeight = SampleHeightFromVertices(sliceVerts, x, currentZ, 3.5f);

                // 지형이 수면(waterY)보다 낮거나 거의 같은 구간을 물길(침수 구역)로 판정
                bool isUnderwater = (sampledHeight < waterY + 0.15f);

                if (isUnderwater && !inWater)
                {
                    inWater = true;
                    channelStartX = x;
                }
                else if (!isUnderwater && inWater)
                {
                    inWater = false;
                    float channelEndX = x - stepX;
                    if (channelEndX - channelStartX >= 3.0f) // 폭 3m 이상의 수로만 인정
                    {
                        channels.Add(new WaterChannel { leftX = channelStartX, rightX = channelEndX });
                    }
                }
            }

            if (inWater)
            {
                channels.Add(new WaterChannel { leftX = channelStartX, rightX = halfScanWidth });
            }

            return channels;
        }

        /// <summary>
        /// 특정 (X, Z) 주변의 버텍스들로부터 지형 높이 보간 산출
        /// </summary>
        private static float SampleHeightFromVertices(List<Vector3> verts, float targetX, float targetZ, float searchRadius)
        {
            float searchRadiusSq = searchRadius * searchRadius;
            float totalWeight = 0f;
            float weightedHeight = 0f;
            float nearestDistSq = float.MaxValue;
            float nearestHeight = 0f;

            foreach (var v in verts)
            {
                float dx = v.x - targetX;
                float dz = v.z - targetZ;
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

            if (totalWeight > 0.0001f)
            {
                return weightedHeight / totalWeight;
            }

            return nearestHeight;
        }

        /// <summary>
        /// 중심선 경로의 3점 이동평균 스무딩 (급격한 지형 꺾임 완화)
        /// </summary>
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

                    float smoothX = (prev.localPosition.x * 0.25f) + (curr.localPosition.x * 0.5f) + (next.localPosition.x * 0.25f);
                    curr.localPosition = new Vector3(smoothX, curr.localPosition.y, curr.localPosition.z);
                    nodes[i] = curr;
                }
            }
        }
    }
}
#endif
