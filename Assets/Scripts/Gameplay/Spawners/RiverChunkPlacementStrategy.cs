using UnityEngine;
using System.Collections.Generic;

namespace SkippingStones.Gameplay.Spawners
{
    /// <summary>
    /// 📐 강줄기 곡선 및 강폭(협곡, 중형, 대형) 적응형 지그재그 분산 배치 알고리즘
    /// </summary>
    public static class RiverChunkPlacementStrategy
    {
        public static void PlaceChunkEntities(
            Transform parent,
            RiverEntityFactory factory,
            float curveStartDist,
            float curveEndDist,
            bool hasRiverPath,
            float minX,
            float maxX,
            float curWaterY,
            bool isRhythmArcade)
        {
            // 1. 🚀 가속 부스트 패드 / 랜덤 링 (지그재그 분산 배치)
            for (float z = curveStartDist + 35f; z < curveEndDist - 35f; z += Random.Range(35f, 65f))
            {
                if (hasRiverPath && SkippingStones.Terrain.GlobalRiverPath.Instance.EvaluateAtDistance(z, out Vector3 cPos, out Vector3 tan, out float rWidth, out float wY))
                {
                    Vector3 normal = Vector3.Cross(Vector3.up, tan).normalized;
                    float halfW = Mathf.Clamp(rWidth * 0.45f, 4f, 35f);

                    List<Vector3> splitChannels = RiverWaterValidator.DetectSplitWaterChannels(cPos, normal, Mathf.Max(halfW * 1.6f, 30f), wY + 0.05f);

                    if (splitChannels.Count > 1)
                    {
                        for (int chIdx = 0; chIdx < splitChannels.Count; chIdx++)
                        {
                            float offsetZ = z + (chIdx % 2 == 0 ? Random.Range(-6f, 2f) : Random.Range(4f, 10f));
                            if (SkippingStones.Terrain.GlobalRiverPath.Instance.EvaluateAtDistance(offsetZ, out Vector3 chEvalPos, out Vector3 chEvalTan, out _, out float chWY))
                            {
                                Vector3 chEvalNormal = Vector3.Cross(Vector3.up, chEvalTan).normalized;
                                float lateralOffset = (chIdx == 0) ? -halfW * 0.5f : halfW * 0.5f;
                                Vector3 chPos = chEvalPos + chEvalNormal * lateralOffset;
                                chPos.y = chWY + 0.05f;
                                TrySpawnPadOrRing(parent, factory, chPos, chEvalTan, isRhythmArcade, curWaterY);
                            }
                            else
                            {
                                TrySpawnPadOrRing(parent, factory, splitChannels[chIdx], tan, isRhythmArcade, curWaterY);
                            }
                        }
                    }
                    else
                    {
                        float effectiveWidth = halfW * 2f;
                        if (effectiveWidth < 15f)
                        {
                            float offsetZ = z + Random.Range(-5f, 5f);
                            if (SkippingStones.Terrain.GlobalRiverPath.Instance.EvaluateAtDistance(offsetZ, out Vector3 evalPos, out Vector3 evalTan, out _, out float evalWY))
                            {
                                Vector3 evalNormal = Vector3.Cross(Vector3.up, evalTan).normalized;
                                Vector3 midPos = evalPos + evalNormal * Random.Range(-1.5f, 1.5f);
                                midPos.y = evalWY + 0.05f;
                                TrySpawnPadOrRing(parent, factory, midPos, evalTan, isRhythmArcade, curWaterY);
                            }
                            else
                            {
                                Vector3 midPos = cPos + normal * Random.Range(-1.5f, 1.5f);
                                midPos.y = wY + 0.05f;
                                TrySpawnPadOrRing(parent, factory, midPos, tan, isRhythmArcade, curWaterY);
                            }
                        }
                        else if (effectiveWidth < 25f)
                        {
                            float z1 = z - Random.Range(3f, 8f);
                            float z2 = z + Random.Range(3f, 8f);

                            if (SkippingStones.Terrain.GlobalRiverPath.Instance.EvaluateAtDistance(z1, out Vector3 evalPos1, out Vector3 evalTan1, out float rw1, out float wy1))
                            {
                                Vector3 n1 = Vector3.Cross(Vector3.up, evalTan1).normalized;
                                Vector3 p1 = evalPos1 - n1 * (rw1 * 0.45f * 0.5f);
                                p1.y = wy1 + 0.05f;
                                TrySpawnPadOrRing(parent, factory, p1, evalTan1, isRhythmArcade, curWaterY);
                            }

                            if (Random.value < 0.7f && SkippingStones.Terrain.GlobalRiverPath.Instance.EvaluateAtDistance(z2, out Vector3 evalPos2, out Vector3 evalTan2, out float rw2, out float wy2))
                            {
                                Vector3 n2 = Vector3.Cross(Vector3.up, evalTan2).normalized;
                                Vector3 p2 = evalPos2 + n2 * (rw2 * 0.45f * 0.5f);
                                p2.y = wy2 + 0.05f;
                                TrySpawnPadOrRing(parent, factory, p2, evalTan2, isRhythmArcade, curWaterY);
                            }
                        }
                        else
                        {
                            float zLeft = z - Random.Range(4f, 10f);
                            float zMid = z + Random.Range(-3f, 3f);
                            float zRight = z + Random.Range(4f, 10f);

                            if (SkippingStones.Terrain.GlobalRiverPath.Instance.EvaluateAtDistance(zLeft, out Vector3 leftPos, out Vector3 leftTan, out float rwL, out float wyL))
                            {
                                Vector3 nL = Vector3.Cross(Vector3.up, leftTan).normalized;
                                Vector3 posL = leftPos - nL * (rwL * 0.45f * 0.65f);
                                posL.y = wyL + 0.05f;
                                TrySpawnPadOrRing(parent, factory, posL, leftTan, isRhythmArcade, curWaterY);
                            }

                            if (SkippingStones.Terrain.GlobalRiverPath.Instance.EvaluateAtDistance(zMid, out Vector3 midPos, out Vector3 midTan, out float rwM, out float wyM))
                            {
                                Vector3 nM = Vector3.Cross(Vector3.up, midTan).normalized;
                                Vector3 posM = midPos + nM * Random.Range(-rwM * 0.45f * 0.2f, rwM * 0.45f * 0.2f);
                                posM.y = wyM + 0.05f;
                                TrySpawnPadOrRing(parent, factory, posM, midTan, isRhythmArcade, curWaterY);
                            }

                            if (SkippingStones.Terrain.GlobalRiverPath.Instance.EvaluateAtDistance(zRight, out Vector3 rightPos, out Vector3 rightTan, out float rwR, out float wyR))
                            {
                                Vector3 nR = Vector3.Cross(Vector3.up, rightTan).normalized;
                                Vector3 posR = rightPos + nR * (rwR * 0.45f * 0.65f);
                                posR.y = wyR + 0.05f;
                                TrySpawnPadOrRing(parent, factory, posR, rightTan, isRhythmArcade, curWaterY);
                            }
                        }
                    }
                }
                else
                {
                    float leftX = Random.Range(minX, Mathf.Lerp(minX, maxX, 0.35f));
                    float centerX = Random.Range(Mathf.Lerp(minX, maxX, 0.35f), Mathf.Lerp(minX, maxX, 0.65f));
                    float rightX = Random.Range(Mathf.Lerp(minX, maxX, 0.65f), maxX);

                    TrySpawnPadOrRing(parent, factory, new Vector3(leftX, curWaterY + 0.05f, z - Random.Range(4f, 8f)), Vector3.forward, isRhythmArcade, curWaterY);
                    TrySpawnPadOrRing(parent, factory, new Vector3(centerX, curWaterY + 0.05f, z + Random.Range(-3f, 3f)), Vector3.forward, isRhythmArcade, curWaterY);
                    TrySpawnPadOrRing(parent, factory, new Vector3(rightX, curWaterY + 0.05f, z + Random.Range(4f, 8f)), Vector3.forward, isRhythmArcade, curWaterY);
                }
            }

            // 2. 🪨 장애물 바위
            for (float z = curveStartDist + 40f; z < curveEndDist - 30f; z += Random.Range(25f, 42f))
            {
                if (hasRiverPath && SkippingStones.Terrain.GlobalRiverPath.Instance.EvaluateAtDistance(z, out Vector3 cPos, out Vector3 tan, out float rWidth, out float wY))
                {
                    Vector3 normal = Vector3.Cross(Vector3.up, tan).normalized;
                    float halfW = Mathf.Clamp(rWidth * 0.45f, 4f, 35f);

                    List<Vector3> splitChannels = RiverWaterValidator.DetectSplitWaterChannels(cPos, normal, Mathf.Max(halfW * 1.6f, 30f), wY);

                    if (splitChannels.Count > 1)
                    {
                        foreach (var chPos in splitChannels)
                        {
                            if (Random.value < 0.6f)
                            {
                                Vector3 rockPos = chPos + normal * (Random.value < 0.5f ? -2f : 2f);
                                rockPos.y = wY;
                                TrySpawnObstacleRock(parent, factory, rockPos, curWaterY);
                            }
                        }
                    }
                    else
                    {
                        float effectiveWidth = halfW * 2f;
                        if (effectiveWidth < 15f)
                        {
                            if (Random.value < 0.5f)
                            {
                                float side = (Random.value < 0.5f) ? -halfW * 0.75f : halfW * 0.75f;
                                Vector3 rockPos = cPos + normal * side;
                                rockPos.y = wY;
                                TrySpawnObstacleRock(parent, factory, rockPos, curWaterY);
                            }
                        }
                        else
                        {
                            float offset = Random.Range(-halfW * 0.75f, halfW * 0.75f);
                            Vector3 rockPos = cPos + normal * offset;
                            rockPos.y = wY;
                            TrySpawnObstacleRock(parent, factory, rockPos, curWaterY);
                        }
                    }
                }
                else
                {
                    float x = Random.Range(minX, maxX);
                    TrySpawnObstacleRock(parent, factory, new Vector3(x, curWaterY, z + Random.Range(-4f, 4f)), curWaterY);
                }
            }

            // 3. 🐟 물고기
            for (float z = curveStartDist + 25f; z < curveEndDist - 25f; z += Random.Range(20f, 42f))
            {
                if (hasRiverPath && SkippingStones.Terrain.GlobalRiverPath.Instance.EvaluateAtDistance(z, out Vector3 cPos, out Vector3 tan, out float rWidth, out float wY))
                {
                    Vector3 normal = Vector3.Cross(Vector3.up, tan).normalized;
                    float halfW = Mathf.Clamp(rWidth * 0.45f, 4f, 35f);

                    List<Vector3> splitChannels = RiverWaterValidator.DetectSplitWaterChannels(cPos, normal, Mathf.Max(halfW * 1.6f, 30f), wY);

                    if (splitChannels.Count > 1)
                    {
                        for (int chIdx = 0; chIdx < splitChannels.Count; chIdx++)
                        {
                            TrySpawnFish(parent, factory, splitChannels[chIdx], z + (chIdx * 6f), curWaterY);
                            if (Random.value < 0.55f)
                            {
                                Vector3 sidePos = splitChannels[chIdx] + normal * Random.Range(-2.5f, 2.5f);
                                TrySpawnFish(parent, factory, sidePos, z + (chIdx * 6f) + Random.Range(3f, 8f), curWaterY);
                            }
                        }
                    }
                    else
                    {
                        float effectiveWidth = halfW * 2f;
                        if (effectiveWidth < 15f)
                        {
                            Vector3 fPos = cPos + normal * Random.Range(-halfW * 0.5f, halfW * 0.5f);
                            fPos.y = wY;
                            TrySpawnFish(parent, factory, fPos, z, curWaterY);
                        }
                        else
                        {
                            Vector3 fPos1 = cPos - normal * (halfW * 0.65f);
                            Vector3 fPos2 = cPos + normal * Random.Range(-halfW * 0.25f, halfW * 0.25f);
                            Vector3 fPos3 = cPos + normal * (halfW * 0.65f);
                            fPos1.y = wY;
                            fPos2.y = wY;
                            fPos3.y = wY;

                            TrySpawnFish(parent, factory, fPos1, z, curWaterY);
                            TrySpawnFish(parent, factory, fPos2, z + Random.Range(4f, 10f), curWaterY);
                            TrySpawnFish(parent, factory, fPos3, z + Random.Range(8f, 16f), curWaterY);
                        }
                    }
                }
                else
                {
                    float x1 = Random.Range(minX, Mathf.Lerp(minX, maxX, 0.35f));
                    float x2 = Random.Range(Mathf.Lerp(minX, maxX, 0.35f), Mathf.Lerp(minX, maxX, 0.65f));
                    float x3 = Random.Range(Mathf.Lerp(minX, maxX, 0.65f), maxX);
                    TrySpawnFish(parent, factory, new Vector3(x1, curWaterY, z), z, curWaterY);
                    TrySpawnFish(parent, factory, new Vector3(x2, curWaterY, z + Random.Range(4f, 10f)), z, curWaterY);
                    TrySpawnFish(parent, factory, new Vector3(x3, curWaterY, z + Random.Range(8f, 16f)), z, curWaterY);
                }
            }

            // 4. 🪷 연잎 군락
            if (hasRiverPath)
            {
                for (float z = curveStartDist + 15f; z < curveEndDist - 15f; z += Random.Range(25f, 45f))
                {
                    if (SkippingStones.Terrain.GlobalRiverPath.Instance.EvaluateAtDistance(z, out Vector3 cPos, out Vector3 tan, out float rWidth, out float wY))
                    {
                        Vector3 normal = Vector3.Cross(Vector3.up, tan).normalized;
                        float halfW = Mathf.Clamp(rWidth * 0.45f, 4f, 35f);

                        float side = (Random.value < 0.5f) ? -halfW * Random.Range(0.65f, 0.9f) : halfW * Random.Range(0.65f, 0.9f);
                        Vector3 lilyPos = cPos + normal * side;
                        lilyPos.y = wY + 0.04f;

                        if (RiverWaterValidator.IsValidWaterPosition(lilyPos, curWaterY, false))
                        {
                            factory.SpawnSingleLilyCluster(parent, lilyPos);
                        }
                    }
                }
            }
        }

        private static void TrySpawnPadOrRing(Transform parent, RiverEntityFactory factory, Vector3 pos, Vector3 tangent, bool isRhythmArcade, float curWaterY)
        {
            if (!RiverWaterValidator.IsValidWaterPosition(pos, curWaterY, false)) return;
            if (RiverWaterValidator.HasNearbySpawnedEntity(parent, pos, 3.8f)) return;

            if (isRhythmArcade)
            {
                Quaternion rot = (tangent.sqrMagnitude > 0.01f) ? Quaternion.LookRotation(tangent, Vector3.up) : Quaternion.identity;
                factory.CreateRandomRing(parent, pos, rot);
            }
            else
            {
                factory.CreateBoostPad(parent, pos, Quaternion.identity);
            }
        }

        private static void TrySpawnObstacleRock(Transform parent, RiverEntityFactory factory, Vector3 pos, float curWaterY)
        {
            if (!RiverWaterValidator.IsValidWaterPosition(pos, curWaterY, false)) return;
            if (RiverWaterValidator.HasNearbySpawnedEntity(parent, pos, 4.2f)) return;
            factory.CreateObstacleRock(parent, pos);
        }

        private static void TrySpawnFish(Transform parent, RiverEntityFactory factory, Vector3 pos, float dist, float curWaterY)
        {
            if (!RiverWaterValidator.IsValidWaterPosition(pos, curWaterY, false)) return;
            if (RiverWaterValidator.HasNearbySpawnedEntity(parent, pos, 2.0f)) return;
            factory.SpawnSingleFish(parent, pos, dist);
        }
    }
}
