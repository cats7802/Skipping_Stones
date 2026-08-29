using UnityEngine;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
using System.IO;
#endif

namespace SkippingStones.TerrainUtils
{
    [ExecuteInEditMode]
    [RequireComponent(typeof(MeshFilter))]
    public class MeshSeamlessStitcher : MonoBehaviour
    {
        public enum StitchMode
        {
            SingleMeshSelfLoop, // 단일 청크 자체 무한 반복 (시작단 ↔ 끝단 일치)
            TwoMeshesDocking    // 두 메쉬 간 도킹 결합 (현재 메쉬 ➔ 타깃 메쉬 결합)
        }

        public enum AlignmentAxis
        {
            Z_Axis, // 앞/뒤 (Z축 - 강물 진행 방향)
            X_Axis  // 좌/우 (X축 - 횡방향)
        }

        public enum BlendCurveType
        {
            SmoothStep,   // 부드러운 S자 곡선 (기본 추천)
            SmootherStep, // 더 부드러운 5차 다항 곡선
            Linear        // 선형 보간
        }

        public enum DockingBlendTarget
        {
            AverageBoth,       // 양쪽 중간값으로 일치
            MatchToTargetMesh, // 타깃 메쉬의 경계에 맞춤 (현재 메쉬 변형)
            MatchToThisMesh    // 현재 메쉬의 경계에 맞춤 (타깃 메쉬 변형)
        }

        [Header("1. 스티칭 모드 및 대상")]
        [Tooltip("단일 메쉬 자체 무한 루프 또는 두 메쉬 간 도킹 결합")]
        public StitchMode stitchMode = StitchMode.SingleMeshSelfLoop;

        [Tooltip("연결할 축 (Z축: 물길 진행 방향, X축: 횡방향)")]
        public AlignmentAxis alignmentAxis = AlignmentAxis.Z_Axis;

        [Tooltip("두 메쉬 도킹 시 연결 대상이 될 반대편 메쉬 오브젝트")]
        public GameObject targetDockingMeshObject;

        [Tooltip("두 메쉬 도킹 시 어떤 형상을 기준으로 맞출지 선택")]
        public DockingBlendTarget dockingBlendTarget = DockingBlendTarget.AverageBoth;

        [Header("2. 보간(Blend) 및 스냅 설정")]
        [Tooltip("경계면에서 안쪽으로 보간(블렌딩)이 적용될 거리 (미터 단위)")]
        [Range(1f, 150f)]
        public float blendDistance = 30f;

        [Tooltip("경계면 정점을 감지하는 허용 오차 두께 (미터)")]
        [Range(0.01f, 5f)]
        public float seamDetectTolerance = 0.2f;

        [Tooltip("반대편 정점과 X/Y 위치를 매칭할 때 허용 오차 (미터)")]
        [Range(0.05f, 10f)]
        public float crossAxisSnapTolerance = 2.0f;

        [Tooltip("보간 곡선 함수")]
        public BlendCurveType blendCurve = BlendCurveType.SmoothStep;

        [Header("3. 동기화 항목")]
        [Tooltip("정점 높이(Y축) 및 형상을 동기화합니다.")]
        public bool stitchHeights = true;

        [Tooltip("경계면 노멀(법선 벡터)을 일치시켜 라이팅/음영 끊김을 제거합니다.")]
        public bool stitchNormals = true;

        [Tooltip("버텍스 컬러(페인팅된 잔디/바위/흙/모래 가중치)를 동기화합니다.")]
        public bool stitchVertexColors = true;

        [Header("4. 시각화 기즈모")]
        public bool showGizmos = true;
        public Color seamGizmoColor = new Color(0.2f, 0.8f, 1f, 0.6f);
        public Color blendGizmoColor = new Color(1f, 0.8f, 0.2f, 0.25f);

        private float EvaluateBlend(float t)
        {
            t = Mathf.Clamp01(t);
            switch (blendCurve)
            {
                case BlendCurveType.Linear:
                    return t;
                case BlendCurveType.SmootherStep:
                    return t * t * t * (t * (t * 6f - 15f) + 10f);
                case BlendCurveType.SmoothStep:
                default:
                    return t * t * (3f - 2f * t);
            }
        }

#if UNITY_EDITOR
        [ContextMenu("🔗 메쉬 이음새 완벽 동기화 (Execute Mesh Seamless Stitch)")]
        public void ExecuteStitch()
        {
            MeshFilter mf = GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null)
            {
                Debug.LogError("[MeshSeamlessStitcher] 대상 오브젝트에 MeshFilter 또는 Mesh가 없습니다.");
                return;
            }

            if (stitchMode == StitchMode.SingleMeshSelfLoop)
            {
                StitchSingleMeshSelfLoop(mf);
            }
            else
            {
                StitchTwoMeshesDocking(mf);
            }
        }

        /// <summary>
        /// 단일 메쉬의 시작 경계면(Min)과 끝 경계면(Max)을 완벽히 일치시키고 안쪽으로 블렌딩합니다.
        /// </summary>
        private void StitchSingleMeshSelfLoop(MeshFilter mf)
        {
            Mesh mesh = Instantiate(mf.sharedMesh);
            mesh.name = $"{mf.sharedMesh.name}_Seamless";
            Undo.RegisterCompleteObjectUndo(mf, "Apply Mesh Seamless Stitch");

            Vector3[] vertices = mesh.vertices;
            Vector3[] normals = mesh.normals;
            Color[] colors = mesh.colors;

            if (normals == null || normals.Length != vertices.Length)
            {
                mesh.RecalculateNormals();
                normals = mesh.normals;
            }

            bool hasColors = (colors != null && colors.Length == vertices.Length);
            if (!hasColors && stitchVertexColors)
            {
                colors = new Color[vertices.Length];
                for (int i = 0; i < colors.Length; i++) colors[i] = Color.black;
                hasColors = true;
            }

            // 1. 축에 따른 바운즈 계산
            Bounds bounds = mesh.bounds;
            float minVal = (alignmentAxis == AlignmentAxis.Z_Axis) ? bounds.min.z : bounds.min.x;
            float maxVal = (alignmentAxis == AlignmentAxis.Z_Axis) ? bounds.max.z : bounds.max.x;
            float totalLength = maxVal - minVal;

            if (totalLength <= 0.001f)
            {
                Debug.LogError("[MeshSeamlessStitcher] 메쉬 크기가 유효하지 않습니다.");
                return;
            }

            // 2. 시작단(Start)과 끝단(End) 정점 인덱스 수집
            List<int> startVertIndices = new List<int>();
            List<int> endVertIndices = new List<int>();

            for (int i = 0; i < vertices.Length; i++)
            {
                float val = (alignmentAxis == AlignmentAxis.Z_Axis) ? vertices[i].z : vertices[i].x;
                if (Mathf.Abs(val - minVal) <= seamDetectTolerance)
                {
                    startVertIndices.Add(i);
                }
                else if (Mathf.Abs(val - maxVal) <= seamDetectTolerance)
                {
                    endVertIndices.Add(i);
                }
            }

            if (startVertIndices.Count == 0 || endVertIndices.Count == 0)
            {
                Debug.LogWarning($"[MeshSeamlessStitcher] 경계면 정점을 충분히 찾지 못했습니다. (Start: {startVertIndices.Count}, End: {endVertIndices.Count}). Seam Detect Tolerance({seamDetectTolerance}m)를 늘려보세요.");
                return;
            }

            // 3. 시작단 ↔ 끝단 정점 1:1 / N:N 횡방향(Cross-Axis) 매칭
            // Z축 기준일 때는 X 위치, X축 기준일 때는 Z 위치를 기준으로 가장 가까운 정점 쌍 매칭
            Dictionary<int, List<int>> startToEndMatches = new Dictionary<int, List<int>>();
            Dictionary<int, List<int>> endToStartMatches = new Dictionary<int, List<int>>();

            foreach (int sIdx in startVertIndices)
            {
                float sCross = (alignmentAxis == AlignmentAxis.Z_Axis) ? vertices[sIdx].x : vertices[sIdx].z;
                startToEndMatches[sIdx] = new List<int>();

                foreach (int eIdx in endVertIndices)
                {
                    float eCross = (alignmentAxis == AlignmentAxis.Z_Axis) ? vertices[eIdx].x : vertices[eIdx].z;
                    if (Mathf.Abs(sCross - eCross) <= crossAxisSnapTolerance)
                    {
                        startToEndMatches[sIdx].Add(eIdx);
                    }
                }
            }

            foreach (int eIdx in endVertIndices)
            {
                float eCross = (alignmentAxis == AlignmentAxis.Z_Axis) ? vertices[eIdx].x : vertices[eIdx].z;
                endToStartMatches[eIdx] = new List<int>();

                foreach (int sIdx in startVertIndices)
                {
                    float sCross = (alignmentAxis == AlignmentAxis.Z_Axis) ? vertices[sIdx].x : vertices[sIdx].z;
                    if (Mathf.Abs(eCross - sCross) <= crossAxisSnapTolerance)
                    {
                        endToStartMatches[eIdx].Add(sIdx);
                    }
                }
            }

            // 4. 경계면 정점 평균치 산출 및 동기화
            // 각 시작 정점에 대해 매칭된 끝 정점들과의 평균 높이/노멀/컬러 산출
            Dictionary<int, Vector3> targetStartPositions = new Dictionary<int, Vector3>();
            Dictionary<int, Vector3> targetEndPositions = new Dictionary<int, Vector3>();
            Dictionary<int, Vector3> targetStartNormals = new Dictionary<int, Vector3>();
            Dictionary<int, Vector3> targetEndNormals = new Dictionary<int, Vector3>();
            Dictionary<int, Color> targetStartColors = new Dictionary<int, Color>();
            Dictionary<int, Color> targetEndColors = new Dictionary<int, Color>();

            foreach (int sIdx in startVertIndices)
            {
                List<int> matchedEnds = startToEndMatches[sIdx];
                if (matchedEnds.Count > 0)
                {
                    float sumY = vertices[sIdx].y;
                    Vector3 sumNormal = normals[sIdx];
                    Color sumColor = hasColors ? colors[sIdx] : Color.black;

                    foreach (int eIdx in matchedEnds)
                    {
                        sumY += vertices[eIdx].y;
                        sumNormal += normals[eIdx];
                        if (hasColors) sumColor += colors[eIdx];
                    }

                    int totalCount = matchedEnds.Count + 1;
                    float avgY = sumY / totalCount;
                    Vector3 avgNormal = (sumNormal / totalCount).normalized;
                    Color avgColor = sumColor / totalCount;

                    Vector3 newStartPos = vertices[sIdx];
                    newStartPos.y = avgY;
                    if (alignmentAxis == AlignmentAxis.Z_Axis) newStartPos.z = minVal;
                    else newStartPos.x = minVal;

                    targetStartPositions[sIdx] = newStartPos;
                    targetStartNormals[sIdx] = avgNormal;
                    if (hasColors) targetStartColors[sIdx] = avgColor;

                    foreach (int eIdx in matchedEnds)
                    {
                        Vector3 newEndPos = vertices[eIdx];
                        newEndPos.y = avgY;
                        if (alignmentAxis == AlignmentAxis.Z_Axis) newEndPos.z = maxVal;
                        else newEndPos.x = maxVal;

                        targetEndPositions[eIdx] = newEndPos;
                        targetEndNormals[eIdx] = avgNormal;
                        if (hasColors) targetEndColors[eIdx] = avgColor;
                    }
                }
            }

            // 5. 경계 정점 값 덮어쓰기
            foreach (var kvp in targetStartPositions)
            {
                if (stitchHeights) vertices[kvp.Key] = kvp.Value;
                if (stitchNormals && targetStartNormals.ContainsKey(kvp.Key)) normals[kvp.Key] = targetStartNormals[kvp.Key];
                if (stitchVertexColors && hasColors && targetStartColors.ContainsKey(kvp.Key)) colors[kvp.Key] = targetStartColors[kvp.Key];
            }

            foreach (var kvp in targetEndPositions)
            {
                if (stitchHeights) vertices[kvp.Key] = kvp.Value;
                if (stitchNormals && targetEndNormals.ContainsKey(kvp.Key)) normals[kvp.Key] = targetEndNormals[kvp.Key];
                if (stitchVertexColors && hasColors && targetEndColors.ContainsKey(kvp.Key)) colors[kvp.Key] = targetEndColors[kvp.Key];
            }

            // 6. 안쪽으로 부드러운 S-Curve 블렌딩 (Internal Blend)
            float maxBlendDist = Mathf.Min(blendDistance, totalLength * 0.48f);

            for (int i = 0; i < vertices.Length; i++)
            {
                // 이미 완벽 스냅된 경계 정점은 제외
                if (startVertIndices.Contains(i) || endVertIndices.Contains(i)) continue;

                float val = (alignmentAxis == AlignmentAxis.Z_Axis) ? vertices[i].z : vertices[i].x;
                float distToStart = Mathf.Abs(val - minVal);
                float distToEnd = Mathf.Abs(val - maxVal);

                if (distToStart < maxBlendDist)
                {
                    // 시작단 근처 정점 ➔ 시작단 정점들의 평균 타깃값 쪽으로 보간
                    float weight = EvaluateBlend(distToStart / maxBlendDist); // 0 at seam, 1 at interior
                    float nearestAvgY = FindNearestTargetY(vertices[i], targetStartPositions);
                    if (stitchHeights) vertices[i].y = Mathf.Lerp(nearestAvgY, vertices[i].y, weight);
                }
                else if (distToEnd < maxBlendDist)
                {
                    // 끝단 근처 정점 ➔ 끝단 정점들의 평균 타깃값 쪽으로 보간
                    float weight = EvaluateBlend(distToEnd / maxBlendDist);
                    float nearestAvgY = FindNearestTargetY(vertices[i], targetEndPositions);
                    if (stitchHeights) vertices[i].y = Mathf.Lerp(nearestAvgY, vertices[i].y, weight);
                }
            }

            // 7. 메쉬 버퍼 적용 및 갱신
            mesh.vertices = vertices;
            mesh.normals = normals;
            if (hasColors) mesh.colors = colors;

            mesh.RecalculateTangents();
            mesh.RecalculateBounds();

            // 콜라이더가 있으면 동기화
            MeshCollider mc = GetComponent<MeshCollider>();
            if (mc != null)
            {
                Undo.RegisterCompleteObjectUndo(mc, "Update MeshCollider");
                mc.sharedMesh = null;
                mc.sharedMesh = mesh;
            }

            mf.sharedMesh = mesh;
            EditorUtility.SetDirty(gameObject);

            Debug.Log($"✅ [MeshSeamlessStitcher] '{gameObject.name}' 단일 메쉬 자체 무한 루프 이음새 동기화 완료! (시작정점: {startVertIndices.Count}개, 끝정점: {endVertIndices.Count}개, 보간거리: {maxBlendDist:F1}m)");
        }

        /// <summary>
        /// 두 메쉬(현재 메쉬 ↔ 타깃 메쉬) 간의 도킹 경계면을 결합합니다.
        /// </summary>
        private void StitchTwoMeshesDocking(MeshFilter mf)
        {
            if (targetDockingMeshObject == null)
            {
                Debug.LogError("[MeshSeamlessStitcher] 도킹 대상 'Target Docking Mesh Object'가 지정되지 않았습니다.");
                return;
            }

            MeshFilter targetMf = targetDockingMeshObject.GetComponent<MeshFilter>();
            if (targetMf == null || targetMf.sharedMesh == null)
            {
                Debug.LogError("[MeshSeamlessStitcher] 타깃 오브젝트에 MeshFilter가 없습니다.");
                return;
            }

            Mesh thisMesh = Instantiate(mf.sharedMesh);
            thisMesh.name = $"{mf.sharedMesh.name}_Stitched";
            Undo.RegisterCompleteObjectUndo(mf, "Stitch Two Meshes Docking");

            Mesh targetMesh = Instantiate(targetMf.sharedMesh);
            targetMesh.name = $"{targetMf.sharedMesh.name}_Stitched";
            Undo.RegisterCompleteObjectUndo(targetMf, "Stitch Two Meshes Docking Target");

            Vector3[] thisVerts = thisMesh.vertices;
            Vector3[] thisNormals = thisMesh.normals;
            Color[] thisColors = thisMesh.colors;

            Vector3[] targetVerts = targetMesh.vertices;
            Vector3[] targetNormals = targetMesh.normals;
            Color[] targetColors = targetMesh.colors;

            Transform thisT = transform;
            Transform targetT = targetDockingMeshObject.transform;

            // 월드 좌표계 변환을 통해 인접한 경계 정점 쌍 탐색
            int stitchedCount = 0;

            for (int i = 0; i < thisVerts.Length; i++)
            {
                Vector3 thisWorldPos = thisT.TransformPoint(thisVerts[i]);

                int bestTargetIdx = -1;
                float bestDistSqr = float.MaxValue;
                float snapDistThresholdSqr = seamDetectTolerance * seamDetectTolerance * 4f;

                for (int j = 0; j < targetVerts.Length; j++)
                {
                    Vector3 targetWorldPos = targetT.TransformPoint(targetVerts[j]);
                    float dSqr = (thisWorldPos - targetWorldPos).sqrMagnitude;
                    if (dSqr < snapDistThresholdSqr && dSqr < bestDistSqr)
                    {
                        bestDistSqr = dSqr;
                        bestTargetIdx = j;
                    }
                }

                if (bestTargetIdx >= 0)
                {
                    stitchedCount++;
                    Vector3 targetWorldPos = targetT.TransformPoint(targetVerts[bestTargetIdx]);

                    // 결합 목표 월드 위치 산출
                    Vector3 blendedWorldPos = thisWorldPos;
                    Vector3 blendedWorldNormal = thisT.TransformDirection(thisNormals[i]);

                    switch (dockingBlendTarget)
                    {
                        case DockingBlendTarget.AverageBoth:
                            blendedWorldPos = (thisWorldPos + targetWorldPos) * 0.5f;
                            blendedWorldNormal = ((thisT.TransformDirection(thisNormals[i]) + targetT.TransformDirection(targetNormals[bestTargetIdx])) * 0.5f).normalized;
                            break;
                        case DockingBlendTarget.MatchToTargetMesh:
                            blendedWorldPos = targetWorldPos;
                            blendedWorldNormal = targetT.TransformDirection(targetNormals[bestTargetIdx]);
                            break;
                        case DockingBlendTarget.MatchToThisMesh:
                            blendedWorldPos = thisWorldPos;
                            blendedWorldNormal = thisT.TransformDirection(thisNormals[i]);
                            break;
                    }

                    // 로컬 위치로 역변환 적용
                    if (stitchHeights)
                    {
                        thisVerts[i] = thisT.InverseTransformPoint(blendedWorldPos);
                        targetVerts[bestTargetIdx] = targetT.InverseTransformPoint(blendedWorldPos);
                    }

                    if (stitchNormals)
                    {
                        thisNormals[i] = thisT.InverseTransformDirection(blendedWorldNormal);
                        targetNormals[bestTargetIdx] = targetT.InverseTransformDirection(blendedWorldNormal);
                    }
                }
            }

            thisMesh.vertices = thisVerts;
            thisMesh.normals = thisNormals;
            thisMesh.RecalculateTangents();
            thisMesh.RecalculateBounds();

            targetMesh.vertices = targetVerts;
            targetMesh.normals = targetNormals;
            targetMesh.RecalculateTangents();
            targetMesh.RecalculateBounds();

            mf.sharedMesh = thisMesh;
            targetMf.sharedMesh = targetMesh;

            MeshCollider mc1 = GetComponent<MeshCollider>();
            if (mc1 != null) { mc1.sharedMesh = null; mc1.sharedMesh = thisMesh; }

            MeshCollider mc2 = targetDockingMeshObject.GetComponent<MeshCollider>();
            if (mc2 != null) { mc2.sharedMesh = null; mc2.sharedMesh = targetMesh; }

            EditorUtility.SetDirty(gameObject);
            EditorUtility.SetDirty(targetDockingMeshObject);

            Debug.Log($"✅ [MeshSeamlessStitcher] 두 메쉬 간 도킹 결합 완료! ({stitchedCount}개 경계 정점 완벽 스냅)");
        }

        private float FindNearestTargetY(Vector3 vert, Dictionary<int, Vector3> targets)
        {
            float nearestY = vert.y;
            float minCrossDist = float.MaxValue;

            foreach (var kvp in targets)
            {
                float crossDist = (alignmentAxis == AlignmentAxis.Z_Axis) 
                    ? Mathf.Abs(vert.x - kvp.Value.x) 
                    : Mathf.Abs(vert.z - kvp.Value.z);

                if (crossDist < minCrossDist)
                {
                    minCrossDist = crossDist;
                    nearestY = kvp.Value.y;
                }
            }

            return nearestY;
        }

        /// <summary>
        /// 수정된 메쉬를 영구적인 .asset 파일로 프로젝트에 저장합니다.
        /// </summary>
        public void SaveMeshAsAsset()
        {
            MeshFilter mf = GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null)
            {
                Debug.LogWarning("[MeshSeamlessStitcher] 저장할 메쉬가 없습니다.");
                return;
            }

            string dir = "Assets/_Project/Meshes/Seamless";
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

            string path = $"{dir}/{mf.sharedMesh.name}.asset";
            path = AssetDatabase.GenerateUniqueAssetPath(path);

            Mesh meshToSave = Instantiate(mf.sharedMesh);
            AssetDatabase.CreateAsset(meshToSave, path);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            mf.sharedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);

            MeshCollider mc = GetComponent<MeshCollider>();
            if (mc != null) mc.sharedMesh = mf.sharedMesh;

            Debug.Log($"💾 [MeshSeamlessStitcher] 심리스 메쉬 에셋이 영구 저장되었습니다: {path}");
        }

        private void OnDrawGizmosSelected()
        {
            if (!showGizmos) return;

            MeshFilter mf = GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) return;

            Bounds bounds = mf.sharedMesh.bounds;
            Transform t = transform;

            Gizmos.matrix = t.localToWorldMatrix;

            // 경계면 표시
            Gizmos.color = seamGizmoColor;
            if (alignmentAxis == AlignmentAxis.Z_Axis)
            {
                Vector3 startCenter = new Vector3(bounds.center.x, bounds.center.y, bounds.min.z);
                Vector3 endCenter = new Vector3(bounds.center.x, bounds.center.y, bounds.max.z);
                Vector3 seamSize = new Vector3(bounds.size.x, bounds.size.y, seamDetectTolerance * 2f);

                Gizmos.DrawWireCube(startCenter, seamSize);
                Gizmos.DrawWireCube(endCenter, seamSize);

                // 블렌드 영역 표시
                Gizmos.color = blendGizmoColor;
                Vector3 blendSize = new Vector3(bounds.size.x, bounds.size.y, blendDistance);
                Gizmos.DrawCube(new Vector3(bounds.center.x, bounds.center.y, bounds.min.z + blendDistance * 0.5f), blendSize);
                Gizmos.DrawCube(new Vector3(bounds.center.x, bounds.center.y, bounds.max.z - blendDistance * 0.5f), blendSize);
            }
            else
            {
                Vector3 startCenter = new Vector3(bounds.min.x, bounds.center.y, bounds.center.z);
                Vector3 endCenter = new Vector3(bounds.max.x, bounds.center.y, bounds.center.z);
                Vector3 seamSize = new Vector3(seamDetectTolerance * 2f, bounds.size.y, bounds.size.z);

                Gizmos.DrawWireCube(startCenter, seamSize);
                Gizmos.DrawWireCube(endCenter, seamSize);

                Gizmos.color = blendGizmoColor;
                Vector3 blendSize = new Vector3(blendDistance, bounds.size.y, bounds.size.z);
                Gizmos.DrawCube(new Vector3(bounds.min.x + blendDistance * 0.5f, bounds.center.y, bounds.center.z), blendSize);
                Gizmos.DrawCube(new Vector3(bounds.max.x - blendDistance * 0.5f, bounds.center.y, bounds.center.z), blendSize);
            }
        }
#endif
    }
}
