#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using SkippingStones.Terrain;

namespace SkippingStones.EditorTools
{
    /// <summary>
    /// 브룩 맵 5종의 전체 연결 구간 곡률(R값) 및 리플렉터(보조 반사판) 필수 설치 지점 정밀 분석 툴
    /// </summary>
    public static class BrookCurvatureAnalysisTool
    {
        [MenuItem("Tools/Skipping Stones/📐 브룩 맵 곡률(R값) & 리플렉터 필수 지점 분석", priority = 12)]
        public static void AnalyzeBrookCurvature()
        {
            string[] prefabPaths = new[]
            {
                "Assets/prefab/Brook_Start.prefab",
                "Assets/prefab/Brook_M_01.prefab",
                "Assets/prefab/Brook_M_02.prefab",
                "Assets/prefab/Brook_M_03.prefab",
                "Assets/prefab/Brook_M_04.prefab"
            };

            Debug.Log("=================================================================");
            Debug.Log("📐 [브룩 5종 맵 곡률(R값) & 꺾임 각도 & 리플렉터 필수 지점 정밀 분석 리포트]");
            Debug.Log("기준: 돌 최대 선회력 = 3.0° / 10m (안전 최소 곡률반경 R >= 191m)");
            Debug.Log("=================================================================");

            Vector3 currentWorldPos = Vector3.zero;
            Quaternion currentWorldRot = Quaternion.identity;

            List<Vector3> chainedWorldPoints = new List<Vector3>();
            List<string> pointLabels = new List<string>();

            for (int i = 0; i < prefabPaths.Length; i++)
            {
                string path = prefabPaths[i];
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null)
                {
                    Debug.LogError($"❌ 프리팹을 찾을 수 없습니다: {path}");
                    continue;
                }

                Transform anchorS = MapAnchorHelper.FindStartAnchor(prefab);
                Transform anchorE = MapAnchorHelper.FindEndAnchor(prefab);

                Vector3 sLocalPos = anchorS != null ? anchorS.localPosition : Vector3.zero;
                Quaternion sLocalRot = anchorS != null ? anchorS.localRotation : Quaternion.identity;
                Vector3 eLocalPos = anchorE != null ? anchorE.localPosition : new Vector3(0, 0, 500);
                Quaternion eLocalRot = anchorE != null ? anchorE.localRotation : Quaternion.identity;

                // 청크 배치 매트릭스 계산
                Quaternion chunkRot = (i == 0) ? Quaternion.identity : currentWorldRot * Quaternion.Inverse(sLocalRot);
                Vector3 chunkPos = (i == 0) ? Vector3.zero : currentWorldPos - (chunkRot * sLocalPos);

                Vector3 worldS = chunkPos + (chunkRot * sLocalPos);
                Vector3 worldE = chunkPos + (chunkRot * eLocalPos);
                Quaternion worldRotE = chunkRot * eLocalRot;

                // 베이킹 데이터가 있다면 노드들 수집
                RiverPathChunkData chunkData = prefab.GetComponent<RiverPathChunkData>();
                if (chunkData != null && chunkData.nodes != null && chunkData.nodes.Count > 0)
                {
                    for (int n = 0; n < chunkData.nodes.Count; n++)
                    {
                        Vector3 wPt = chunkPos + (chunkRot * chunkData.nodes[n].localPosition);
                        chainedWorldPoints.Add(wPt);
                        pointLabels.Add($"{prefab.name} Node_{n}");
                    }
                }
                else
                {
                    chainedWorldPoints.Add(worldS);
                    pointLabels.Add($"{prefab.name}_StartAnchor");
                    chainedWorldPoints.Add(worldE);
                    pointLabels.Add($"{prefab.name}_EndAnchor");
                }

                // 앵커 간 직선 각도 꺾임 분석
                Vector3 startForward = sLocalRot * Vector3.forward;
                Vector3 endForward = eLocalRot * Vector3.forward;
                float chunkTurnAngle = Vector3.SignedAngle(startForward, endForward, Vector3.up);
                float chunkSpanDist = Vector3.Distance(worldS, worldE);

                Debug.Log($"🔹 [{i + 1}] {prefab.name} : 구간 길이 {chunkSpanDist:F1}m | 자체 선회 각도 {chunkTurnAngle:F2}° (진입->진출)");

                currentWorldPos = worldE;
                currentWorldRot = worldRotE;
            }

            Debug.Log("-----------------------------------------------------------------");
            Debug.Log("🔍 [구간별 상세 곡률 및 리플렉터 권장 위치 분석]");

            int hotspotCount = 0;
            for (int i = 1; i < chainedWorldPoints.Count - 1; i++)
            {
                Vector3 p0 = chainedWorldPoints[i - 1];
                Vector3 p1 = chainedWorldPoints[i];
                Vector3 p2 = chainedWorldPoints[i + 1];

                Vector3 dir1 = (p1 - p0).normalized;
                Vector3 dir2 = (p2 - p1).normalized;

                float segLen = Vector3.Distance(p0, p1);
                if (segLen < 0.1f) continue;

                float angleDelta = Vector3.Angle(dir1, dir2);
                float anglePer10m = (angleDelta / segLen) * 10f;
                float curvatureR = (angleDelta > 0.001f) ? (segLen / (angleDelta * Mathf.Deg2Rad)) : 9999f;

                if (anglePer10m > 3.0f || curvatureR < 190f)
                {
                    hotspotCount++;
                    string turnDir = Vector3.Cross(dir1, dir2).y > 0 ? "우측(Right)" : "좌측(Left)";
                    Debug.LogWarning($"⚠️ [리플렉터 필수 지점 #{hotspotCount}] 위치: {pointLabels[i]} (월드 Z={p1.z:F1}m)\n" +
                                     $"   • 10m당 꺾임각: {anglePer10m:F2}° (허용 3.0° 초과!)\n" +
                                     $"   • 곡률 반경: R = {curvatureR:F1}m (안전 R >= 191m 미달)\n" +
                                     $"   • 회전 방향: {turnDir} 커브 ➔ {turnDir} 외곽 강둑에 '리플렉터/가속 패드' 설치 권장!");
                }
            }

            if (hotspotCount == 0)
            {
                Debug.Log("🎉 [분석 결과]: 전체 구간의 곡선이 R >= 191m (10m당 3° 이내)로 완벽하게 설계되어 있어, 돌이 튕겨 나갈 위험 없이 매우 안전합니다!");
            }
            else
            {
                Debug.Log($"💡 [분석 요약]: 총 {hotspotCount}개 구간에서 3도 이상의 급격한 꺾임이 감지되었습니다. 해당 지점의 외곽 물길에 리플렉터 기믹을 배치하면 아주 박진감 넘치는 연출이 가능합니다.");
            }
            Debug.Log("=================================================================");
        }
    }
}
#endif
