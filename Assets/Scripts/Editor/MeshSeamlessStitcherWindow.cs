using UnityEngine;
using UnityEditor;
using SkippingStones.TerrainUtils;

namespace SkippingStones.Editor
{
    public class MeshSeamlessStitcherWindow : EditorWindow
    {
        [MenuItem("Tools/SkippingStones/🧩 3D Mesh Seamless Stitcher")]
        public static void OpenWindow()
        {
            var window = GetWindow<MeshSeamlessStitcherWindow>("Mesh Seamless");
            window.minSize = new Vector2(340, 480);
            window.Show();
        }

        public GameObject targetObject;
        public MeshSeamlessStitcher.StitchMode stitchMode = MeshSeamlessStitcher.StitchMode.SingleMeshSelfLoop;
        public MeshSeamlessStitcher.AlignmentAxis alignmentAxis = MeshSeamlessStitcher.AlignmentAxis.Z_Axis;
        public GameObject targetDockingMeshObject;
        public MeshSeamlessStitcher.DockingBlendTarget dockingBlendTarget = MeshSeamlessStitcher.DockingBlendTarget.AverageBoth;

        public float blendDistance = 30f;
        public float seamDetectTolerance = 0.2f;
        public float crossAxisSnapTolerance = 2.0f;
        public MeshSeamlessStitcher.BlendCurveType blendCurve = MeshSeamlessStitcher.BlendCurveType.SmoothStep;

        public bool stitchHeights = true;
        public bool stitchNormals = true;
        public bool stitchVertexColors = true;

        private Vector2 scrollPos;

        private void OnEnable()
        {
            AutoAssignSelection();
        }

        private void OnSelectionChange()
        {
            AutoAssignSelection();
            Repaint();
        }

        private void AutoAssignSelection()
        {
            if (Selection.activeGameObject != null && targetObject == null)
            {
                var mf = Selection.activeGameObject.GetComponent<MeshFilter>();
                if (mf != null)
                {
                    targetObject = Selection.activeGameObject;
                }
            }
        }

        private void OnGUI()
        {
            scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

            GUILayout.Label("🧩 3D 메쉬 심리스 스티처 (Mesh Stitcher)", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            EditorGUILayout.HelpBox(
                "3D 메쉬 청크의 경계면 정점/노멀/버텍스 컬러를 일치시켜\n" +
                "무한 반복 스트리밍 시 틈(Gap)과 음영 끊김을 완벽히 제거합니다.",
                MessageType.Info);
            EditorGUILayout.Space(8);

            // 1. 대상 메쉬
            DrawSectionHeader("1. 대상 메쉬 오브젝트");
            targetObject = (GameObject)EditorGUILayout.ObjectField("대상 메쉬", targetObject, typeof(GameObject), true);

            if (targetObject != null)
            {
                var mf = targetObject.GetComponent<MeshFilter>();
                if (mf != null && mf.sharedMesh != null)
                {
                    EditorGUILayout.LabelField($"메시: {mf.sharedMesh.name} (정점: {mf.sharedMesh.vertexCount}개)");
                }
                else
                {
                    EditorGUILayout.HelpBox("선택된 오브젝트에 MeshFilter 또는 Mesh가 없습니다!", MessageType.Warning);
                }
            }
            EditorGUILayout.Space(8);

            // 2. 모드 및 축 설정
            DrawSectionHeader("2. 모드 및 축 설정");
            stitchMode = (MeshSeamlessStitcher.StitchMode)EditorGUILayout.EnumPopup("스티칭 모드", stitchMode);
            alignmentAxis = (MeshSeamlessStitcher.AlignmentAxis)EditorGUILayout.EnumPopup("정렬 축", alignmentAxis);

            if (stitchMode == MeshSeamlessStitcher.StitchMode.TwoMeshesDocking)
            {
                EditorGUILayout.Space(3);
                targetDockingMeshObject = (GameObject)EditorGUILayout.ObjectField("도킹 대상 메쉬", targetDockingMeshObject, typeof(GameObject), true);
                dockingBlendTarget = (MeshSeamlessStitcher.DockingBlendTarget)EditorGUILayout.EnumPopup("결합 기준", dockingBlendTarget);
            }
            EditorGUILayout.Space(8);

            // 3. 보간 및 정점 스냅
            DrawSectionHeader("3. 보간(Blend) 및 스냅 설정");
            blendDistance = EditorGUILayout.Slider("보간 거리 (m)", blendDistance, 1f, 150f);
            seamDetectTolerance = EditorGUILayout.Slider("경계면 감지 두께 (m)", seamDetectTolerance, 0.01f, 5f);
            crossAxisSnapTolerance = EditorGUILayout.Slider("횡방향 매칭 오차 (m)", crossAxisSnapTolerance, 0.05f, 10f);
            blendCurve = (MeshSeamlessStitcher.BlendCurveType)EditorGUILayout.EnumPopup("보간 곡선", blendCurve);
            EditorGUILayout.Space(8);

            // 4. 동기화 항목
            DrawSectionHeader("4. 동기화 항목");
            stitchHeights = EditorGUILayout.Toggle("지형 높이/형상 일치", stitchHeights);
            stitchNormals = EditorGUILayout.Toggle("법선 노멀 일치", stitchNormals);
            stitchVertexColors = EditorGUILayout.Toggle("버텍스 컬러 일치", stitchVertexColors);
            EditorGUILayout.Space(15);

            // Actions
            GUI.backgroundColor = new Color(0.2f, 0.75f, 1f);
            if (GUILayout.Button("🔗 메쉬 이음새 완벽 동기화 실행 🪄", GUILayout.Height(40)))
            {
                ExecuteFromWindow();
            }

            EditorGUILayout.Space(5);
            GUI.backgroundColor = new Color(0.4f, 0.9f, 0.4f);
            if (GUILayout.Button("💾 수정된 메쉬 에셋으로 영구 저장 (Save as Asset)", GUILayout.Height(32)))
            {
                SaveAssetFromWindow();
            }
            GUI.backgroundColor = Color.white;

            EditorGUILayout.EndScrollView();
        }

        private void ExecuteFromWindow()
        {
            if (targetObject == null)
            {
                EditorUtility.DisplayDialog("경고", "대상 오브젝트를 먼저 지정해주세요.", "확인");
                return;
            }

            var stitcher = targetObject.GetComponent<MeshSeamlessStitcher>();
            if (stitcher == null)
            {
                stitcher = Undo.AddComponent<MeshSeamlessStitcher>(targetObject);
            }

            stitcher.stitchMode = stitchMode;
            stitcher.alignmentAxis = alignmentAxis;
            stitcher.targetDockingMeshObject = targetDockingMeshObject;
            stitcher.dockingBlendTarget = dockingBlendTarget;
            stitcher.blendDistance = blendDistance;
            stitcher.seamDetectTolerance = seamDetectTolerance;
            stitcher.crossAxisSnapTolerance = crossAxisSnapTolerance;
            stitcher.blendCurve = blendCurve;
            stitcher.stitchHeights = stitchHeights;
            stitcher.stitchNormals = stitchNormals;
            stitcher.stitchVertexColors = stitchVertexColors;

            stitcher.ExecuteStitch();
        }

        private void SaveAssetFromWindow()
        {
            if (targetObject == null)
            {
                EditorUtility.DisplayDialog("경고", "대상 오브젝트를 먼저 지정해주세요.", "확인");
                return;
            }

            var stitcher = targetObject.GetComponent<MeshSeamlessStitcher>();
            if (stitcher != null)
            {
                stitcher.SaveMeshAsAsset();
            }
            else
            {
                EditorUtility.DisplayDialog("경고", "먼저 동기화를 실행한 후 저장해주세요.", "확인");
            }
        }

        private void DrawSectionHeader(string title)
        {
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
            Rect rect = EditorGUILayout.GetControlRect(false, 1);
            EditorGUI.DrawRect(rect, new Color(0.5f, 0.5f, 0.5f, 0.5f));
            EditorGUILayout.Space(3);
        }
    }
}
