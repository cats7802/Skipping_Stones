#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace SkippingStones.Terrain
{
    /// <summary>
    /// [3D 메쉬 청크 강줄기 자동 추적 & 정밀 지형 횡단면 베이킹 엔진]
    /// - 청크의 실제 3D 지형 메쉬를 2.5m 간격으로 횡단면 슬라이스
    /// - 0.5m 단위로 지형 깊이를 정밀 스캔하여 실제 물이 고이는 V자 계곡 골짜기(Waterline) 감지
    /// - 섬(Island)으로 물길이 2개로 갈라지는 분기 지형도 경로 연속성이 가장 자연스러운 주 물길을 자동 선택
    /// - S자 굴곡을 100% 반영한 3D 연속 스플라인 경로와 실제 강폭 데이터를 RiverPathChunkData에 영구 저장
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

        public static bool BakeRiverPathForChunk(GameObject chunkRoot, float sampleInterval = 2.5f, float scanHalfWidth = 120f)
        {
            if (chunkRoot == null) return false;

            Transform anchorS = MapAnchorHelper.FindStartAnchor(chunkRoot);
            Transform anchorE = MapAnchorHelper.FindEndAnchor(chunkRoot);

            if (anchorS == null || anchorE == null)
            {
                Debug.LogWarning($"[RiverPathBaker] ⚠️ '{chunkRoot.name}'에서 앵커(Anchor_S / Anchor_E)를 찾을 수 없습니다. 지형 바운드 기준으로 자동 생성합니다.");
                MapAnchorHelper.GetOrCreateAnchors(chunkRoot, out anchorS, out anchorE);
            }

            Vector3 startLocal = chunkRoot.transform.InverseTransformPoint(anchorS.position);
            Vector3 endLocal = chunkRoot.transform.InverseTransformPoint(anchorE.position);

            float totalZSpan = Mathf.Abs(endLocal.z - startLocal.z);
            if (totalZSpan < 10f) totalZSpan = 500f;

            // 1. 수면 높이(waterY) 및 WaterSurface 수집
            WaterSurface ws = chunkRoot.GetComponentInChildren<WaterSurface>(true);
            BoxCollider waterBox = ws != null ? ws.GetComponent<BoxCollider>() : null;
            float defaultWaterY = 0f;
            if (waterBox != null)
            {
                Vector3 boxMaxWorld = waterBox.transform.TransformPoint(waterBox.center + new Vector3(0, waterBox.size.y * 0.5f, 0));
                defaultWaterY = chunkRoot.transform.InverseTransformPoint(boxMaxWorld).y;
            }

            // 2. 지형 메쉬 필터 및 콜라이더 수집
            MeshFilter[] terrainFilters = chunkRoot.GetComponentsInChildren<MeshFilter>(true);
            List<MeshFilter> validGroundFilters = new List<MeshFilter>();
            foreach (var mf in terrainFilters)
            {
                if (mf == null || mf.sharedMesh == null) continue;
                string lowerName = mf.gameObject.name.ToLowerInvariant();
                if (lowerName.Contains("water") || lowerName.Contains("pier") || lowerName.Contains("camera") || lowerName.Contains("ui")) continue;
                validGroundFilters.Add(mf);
            }

            int stepCount = Mathf.Max(5, Mathf.CeilToInt(totalZSpan / sampleInterval));
            List<RiverPathNode> bakedNodes = new List<RiverPathNode>();

            float accumulatedDist = 0f;
            Vector3 prevNodePos = startLocal;
            float lastSelectedCenterX = startLocal.x; // 이전 프레임 중심선 추적 (섬 분기 시 연속성 유지)

            float scanStepX = 0.5f; // 0.5m 단위 초정밀 횡단면 검사

            for (int i = 0; i <= stepCount; i++)
            {
                float t = (float)i / stepCount;
                float currentZ = Mathf.Lerp(startLocal.z, endLocal.z, t);

                // 현재 Z 슬라이스에서 물이 잠기는 채널(수변 구간들) 탐색
                List<WaterChannel> detectedChannels = ScanWaterChannelsAtZ(chunkRoot, validGroundFilters, currentZ, defaultWaterY, scanHalfWidth, scanStepX);

                float chosenLeftX = -20f;
                float chosenRightX = 20f;

                if (detectedChannels.Count > 0)
                {
                    // 🌟 섬(Island) 등으로 물길이 2개 이상으로 갈라질 때:
                    // 이전 노드의 중심 X좌표와 가장 가깝고 자연스럽게 이어지는 주 물길 채널 1개 선택!
                    WaterChannel bestChannel = detectedChannels[0];
                    float minDiff = float.MaxValue;

                    foreach (var ch in detectedChannels)
                    {
                        // 최소 유효 강폭(3m 이상) 필터링
                        if (ch.width < 3.0f && detectedChannels.Count > 1) continue;

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
                    // 지형 스캔이 잡히지 않는 특수 구간은 이전 중심선 유지
                    chosenLeftX = lastSelectedCenterX - 20f;
                    chosenRightX = lastSelectedCenterX + 20f;
                }

                float centerX = (chosenLeftX + chosenRightX) * 0.5f;
                float riverWidth = Mathf.Max(6f, chosenRightX - chosenLeftX);
                float halfWidth = riverWidth * 0.5f;

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
                    leftWidth = halfWidth,
                    rightWidth = halfWidth,
                    cumulativeDistance = accumulatedDist
                };

                bakedNodes.Add(node);
            }

            // 3. 중심선 스플라인 1차 스무딩 (노이즈 제거)
            SmoothPathCenterline(bakedNodes);

            // 4. 각 노드의 접선(Tangent) 벡터 정밀 계산 (Catmull-Rom 중앙 차분)
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

            // 5. RiverPathChunkData 컴포넌트에 주입 및 프리팹 영구 저장
            RiverPathChunkData data = chunkRoot.GetComponent<RiverPathChunkData>();
            if (data == null) data = chunkRoot.AddComponent<RiverPathChunkData>();

            data.nodes = bakedNodes;
            data.totalLength = accumulatedDist;
            data.averageWidth = (bakedNodes.Count > 0) ? bakedNodes[0].totalWidth : 50f;

            EditorUtility.SetDirty(chunkRoot);
            Debug.Log($"[RiverPathBaker] 🌟 '{chunkRoot.name}' 지형 정밀 S자 곡선 베이킹 완료! (노드 {bakedNodes.Count}개, 총 길이 {accumulatedDist:F1}m, 평균 강폭 {data.averageWidth:F1}m)");

            return true;
        }

        /// <summary>
        /// 특정 Z 단면에서 X축을 촘촘히 스캔하여 수면 아래로 잠기는 실제 물길 채널(Water Channels) 목록 산출
        /// </summary>
        private static List<WaterChannel> ScanWaterChannelsAtZ(GameObject chunkRoot, List<MeshFilter> groundFilters, float currentZ, float waterY, float halfScanWidth, float stepX)
        {
            List<WaterChannel> channels = new List<WaterChannel>();

            bool inWater = false;
            float channelStartX = 0f;

            for (float x = -halfScanWidth; x <= halfScanWidth; x += stepX)
            {
                Vector3 localPoint = new Vector3(x, 0f, currentZ);
                Vector3 worldPoint = chunkRoot.transform.TransformPoint(localPoint);

                // 초고도에서 아래로 지형 높이 측정
                float groundYLocal = SampleGroundHeightAtLocal(chunkRoot, groundFilters, localPoint);

                // 수면보다 0.05m 이상 아래로 파여있는 경우 물이 찬 강바닥으로 판정
                bool isUnderwater = (groundYLocal < waterY - 0.05f);

                if (isUnderwater && !inWater)
                {
                    inWater = true;
                    channelStartX = x;
                }
                else if (!isUnderwater && inWater)
                {
                    inWater = false;
                    float channelEndX = x - stepX;
                    if (channelEndX - channelStartX >= 2.0f) // 최소 2m 이상 파인 수로만 채널로 인정
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
        /// 청크 로컬 (X, Z) 지점에서 지형 메쉬의 높이를 정밀 샘플링
        /// </summary>
        private static float SampleGroundHeightAtLocal(GameObject chunkRoot, List<MeshFilter> filters, Vector3 localPos)
        {
            Vector3 worldPos = chunkRoot.transform.TransformPoint(new Vector3(localPos.x, 150f, localPos.z));
            Ray ray = new Ray(worldPos, Vector3.down);

            // 1. 씬에 MeshCollider가 활성화되어 있는 경우 물리 Raycast로 초고속 샘플링
            RaycastHit[] hits = Physics.RaycastAll(ray, 300f, ~0, QueryTriggerInteraction.Ignore);
            float highestGroundY = float.MinValue;
            bool foundHit = false;

            foreach (var h in hits)
            {
                if (h.collider == null) continue;
                string colName = h.collider.gameObject.name.ToLowerInvariant();
                if (colName.Contains("water") || colName.Contains("pier") || colName.Contains("ui")) continue;

                Vector3 hitLocal = chunkRoot.transform.InverseTransformPoint(h.point);
                if (hitLocal.y > highestGroundY)
                {
                    highestGroundY = hitLocal.y;
                    foundHit = true;
                }
            }

            if (foundHit) return highestGroundY;

            // 2. 물리 콜라이더가 없는 경우 지형 메쉬 버텍스 바운드 근사 보간
            foreach (var mf in filters)
            {
                if (mf == null || mf.sharedMesh == null) continue;
                Mesh m = mf.sharedMesh;
                Vector3 meshLocal = mf.transform.InverseTransformPoint(chunkRoot.transform.TransformPoint(localPos));

                Bounds b = m.bounds;
                if (meshLocal.x >= b.min.x && meshLocal.x <= b.max.x && meshLocal.z >= b.min.z && meshLocal.z <= b.max.z)
                {
                    // 버텍스 평균 표면 높이 추정
                    Vector3 chunkY = chunkRoot.transform.InverseTransformPoint(mf.transform.TransformPoint(new Vector3(0, b.min.y, 0)));
                    return chunkY.y;
                }
            }

            return localPos.y;
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
