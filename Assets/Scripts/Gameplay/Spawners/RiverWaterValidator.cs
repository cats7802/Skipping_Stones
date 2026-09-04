using UnityEngine;
using System.Collections.Generic;

namespace SkippingStones.Gameplay.Spawners
{
    /// <summary>
    /// 🌊 수면 레이캐스트 지형/수심 검증, 섬 분기 수로 검출 및 엔티티 간 겹침 방지를 전담하는 검증 헬퍼
    /// </summary>
    public static class RiverWaterValidator
    {
        /// <summary>
        /// 상공에서 수직 레이캐스트: MeshCollider 및 TerrainCollider를 검사하여 안전 수심(waterDepth >= 0.35m) 확보 검증
        /// </summary>
        public static bool IsValidWaterPosition(Vector3 pos, float currentWaterY, bool checkZBounds = false, float chunkStartZ = 0f, float chunkEndZ = float.MaxValue)
        {
            if (checkZBounds && chunkEndZ < float.MaxValue)
            {
                if (pos.z < chunkStartZ - 50f || pos.z > chunkEndZ + 50f) return false;
            }

            float curWater = pos.y;
            if (Mathf.Abs(curWater) < 0.001f) curWater = currentWaterY;

            float rayStart = curWater + 250f;
            Vector3 rayOrigin = new Vector3(pos.x, rayStart, pos.z);

            RaycastHit[] hits = Physics.RaycastAll(rayOrigin, Vector3.down, 400f, ~0, QueryTriggerInteraction.Collide);
            if (hits == null || hits.Length == 0)
            {
                return false;
            }

            bool hasWaterSurface = false;
            float groundY = float.MinValue;
            bool hasGround = false;

            foreach (var hit in hits)
            {
                if (hit.collider == null) continue;

                if (hit.collider.GetComponent<WaterSurface>() != null || hit.collider.name.ToLower().Contains("water"))
                {
                    hasWaterSurface = true;
                    curWater = hit.point.y;
                }
                else
                {
                    if (hit.point.y > groundY)
                    {
                        groundY = hit.point.y;
                        hasGround = true;
                    }
                }
            }

            if (!hasWaterSurface && !hasGround) return false;

            // 지형이 수면보다 높이 솟아 있는 육지 스폰 차단
            if (hasGround && groundY >= curWater - 0.15f)
            {
                return false;
            }

            // 수심이 너무 얕은 경우 스폰 차단
            if (hasGround && (curWater - groundY) < 0.35f)
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// 🏝️ 특정 단면(Distance)에서 섬으로 인해 분리된 다중 수면 채널(Water Channels) 중심점 검출
        /// </summary>
        public static List<Vector3> DetectSplitWaterChannels(Vector3 centerPos, Vector3 normal, float maxScanWidth, float waterY)
        {
            List<Vector3> channels = new List<Vector3>();
            float scanRange = Mathf.Max(maxScanWidth, 75f);
            float step = 2.5f;

            bool inWater = false;
            float segmentStartOffset = 0f;

            for (float offset = -scanRange; offset <= scanRange; offset += step)
            {
                Vector3 testPos = centerPos + normal * offset;
                testPos.y = waterY;

                bool isWater = IsValidWaterPosition(testPos, waterY, false);

                if (isWater && !inWater)
                {
                    inWater = true;
                    segmentStartOffset = offset;
                }
                else if (!isWater && inWater)
                {
                    inWater = false;
                    float segEndOffset = offset - step;
                    if (segEndOffset - segmentStartOffset >= 4.0f) // 폭 4m 이상의 유효 수로만 채널로 인정
                    {
                        float midOffset = (segmentStartOffset + segEndOffset) * 0.5f;
                        Vector3 chPos = centerPos + normal * midOffset;
                        chPos.y = waterY;
                        channels.Add(chPos);
                    }
                }
            }

            if (inWater)
            {
                float segEndOffset = scanRange;
                if (segEndOffset - segmentStartOffset >= 4.0f)
                {
                    float midOffset = (segmentStartOffset + segEndOffset) * 0.5f;
                    Vector3 chPos = centerPos + normal * midOffset;
                    chPos.y = waterY;
                    channels.Add(chPos);
                }
            }

            return channels;
        }

        /// <summary>
        /// 🌟 오브젝트 간 겹침 방지 (2D 수평 거리 검사)
        /// </summary>
        public static bool HasNearbySpawnedEntity(Transform parentTransform, Vector3 pos, float minRadius = 3.8f)
        {
            if (parentTransform == null) return false;
            float minSq = minRadius * minRadius;
            for (int i = 0; i < parentTransform.childCount; i++)
            {
                Transform child = parentTransform.GetChild(i);
                if (child == null) continue;
                Vector3 diff = child.position - pos;
                diff.y = 0f;
                if (diff.sqrMagnitude < minSq)
                {
                    return true;
                }
            }
            return false;
        }
    }
}
