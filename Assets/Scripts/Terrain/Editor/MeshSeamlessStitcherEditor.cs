using UnityEngine;
using UnityEditor;
using SkippingStones.TerrainUtils;

namespace SkippingStones.Editor
{
    [CustomEditor(typeof(MeshSeamlessStitcher))]
    public class MeshSeamlessStitcherEditor : UnityEditor.Editor
    {
        private SerializedProperty stitchMode;
        private SerializedProperty alignmentAxis;
        private SerializedProperty targetDockingMeshObject;
        private SerializedProperty dockingBlendTarget;

        private SerializedProperty blendDistance;
        private SerializedProperty seamDetectTolerance;
        private SerializedProperty crossAxisSnapTolerance;
        private SerializedProperty blendCurve;

        private SerializedProperty stitchHeights;
        private SerializedProperty stitchNormals;
        private SerializedProperty stitchVertexColors;

        private SerializedProperty showGizmos;
        private SerializedProperty seamGizmoColor;
        private SerializedProperty blendGizmoColor;

        private void OnEnable()
        {
            stitchMode = serializedObject.FindProperty("stitchMode");
            alignmentAxis = serializedObject.FindProperty("alignmentAxis");
            targetDockingMeshObject = serializedObject.FindProperty("targetDockingMeshObject");
            dockingBlendTarget = serializedObject.FindProperty("dockingBlendTarget");

            blendDistance = serializedObject.FindProperty("blendDistance");
            seamDetectTolerance = serializedObject.FindProperty("seamDetectTolerance");
            crossAxisSnapTolerance = serializedObject.FindProperty("crossAxisSnapTolerance");
            blendCurve = serializedObject.FindProperty("blendCurve");

            stitchHeights = serializedObject.FindProperty("stitchHeights");
            stitchNormals = serializedObject.FindProperty("stitchNormals");
            stitchVertexColors = serializedObject.FindProperty("stitchVertexColors");

            showGizmos = serializedObject.FindProperty("showGizmos");
            seamGizmoColor = serializedObject.FindProperty("seamGizmoColor");
            blendGizmoColor = serializedObject.FindProperty("blendGizmoColor");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.Space(5);
            EditorGUILayout.HelpBox(
                "3D 메쉬 지형의 경계면(이음새) 정점과 노멀, 버텍스 컬러를 완벽히 일치시켜\n" +
                "무한 반복 스트리밍 시 틈(Gap)과 음영 끊김(Seam)을 완전히 제거합니다.",
                MessageType.Info);
            EditorGUILayout.Space(8);

            // 1. 모드 및 대상
            DrawSectionHeader("1. 스티칭 모드 및 축 설정 (Mode & Axis)");
            EditorGUILayout.PropertyField(stitchMode, new GUIContent("스티칭 모드 (Mode)", "단일 메쉬 자체 무한 루프 또는 두 메쉬 간 도킹 결합"));
            EditorGUILayout.PropertyField(alignmentAxis, new GUIContent("정렬 축 (Axis)", "Z축: 물길 진행 방향, X축: 횡방향"));

            if ((MeshSeamlessStitcher.StitchMode)stitchMode.enumValueIndex == MeshSeamlessStitcher.StitchMode.TwoMeshesDocking)
            {
                EditorGUILayout.Space(4);
                EditorGUILayout.PropertyField(targetDockingMeshObject, new GUIContent("도킹 대상 메쉬 (Target)", "맞닿아 연결할 반대편 메쉬 오브젝트"));
                EditorGUILayout.PropertyField(dockingBlendTarget, new GUIContent("결합 기준 (Blend Target)", "양쪽 평균값 또는 특정 메쉬 기준 정렬"));
            }
            EditorGUILayout.Space(10);

            // 2. 보간 및 정점 스냅
            DrawSectionHeader("2. 보간(Blend) 및 정점 스냅 설정");
            EditorGUILayout.PropertyField(blendDistance, new GUIContent("보간 거리 (Blend Dist, m)", "경계면에서 안쪽으로 자연스럽게 블렌딩될 거리 (권장: 15m~50m)"));
            EditorGUILayout.PropertyField(seamDetectTolerance, new GUIContent("경계면 감지 두께 (m)", "경계선 끝단 정점을 판별할 두께 허용치 (기본: 0.2m)"));
            EditorGUILayout.PropertyField(crossAxisSnapTolerance, new GUIContent("횡방향 매칭 오차 (m)", "반대편 정점과 X/Y 위치를 매칭할 허용 오차 (기본: 2.0m)"));
            EditorGUILayout.PropertyField(blendCurve, new GUIContent("보간 곡선 (Curve)", "부드러운 전이 방식 (SmoothStep 권장)"));
            EditorGUILayout.Space(10);

            // 3. 동기화 항목
            DrawSectionHeader("3. 동기화 항목 (Attributes to Stitch)");
            EditorGUILayout.PropertyField(stitchHeights, new GUIContent("지형 높이/형상 일치 (Heights)", "정점 높이(Y) 및 형상을 일치시킵니다."));
            EditorGUILayout.PropertyField(stitchNormals, new GUIContent("법선 노멀 일치 (Normals)", "라이팅/음영 끊김(검은 줄)을 제거합니다."));
            EditorGUILayout.PropertyField(stitchVertexColors, new GUIContent("버텍스 컬러 일치 (Colors)", "페인팅된 잔디/바위/모래 텍스처 가중치를 부드럽게 섞습니다."));
            EditorGUILayout.Space(10);

            // 4. 기즈모
            DrawSectionHeader("4. 씬 뷰 시각화 (Gizmos)");
            EditorGUILayout.PropertyField(showGizmos, new GUIContent("기즈모 표시 (Show Gizmos)"));
            if (showGizmos.boolValue)
            {
                EditorGUILayout.PropertyField(seamGizmoColor, new GUIContent("경계선 색상"));
                EditorGUILayout.PropertyField(blendGizmoColor, new GUIContent("보간 영역 색상"));
            }
            EditorGUILayout.Space(15);

            serializedObject.ApplyModifiedProperties();

            // Execute Button
            MeshSeamlessStitcher stitcher = (MeshSeamlessStitcher)target;

            GUI.backgroundColor = new Color(0.2f, 0.75f, 1f);
            if (GUILayout.Button("🔗 메쉬 이음새 완벽 동기화 (Apply Mesh Seamless) 🪄", GUILayout.Height(40)))
            {
                stitcher.ExecuteStitch();
            }

            EditorGUILayout.Space(5);
            GUI.backgroundColor = new Color(0.4f, 0.9f, 0.4f);
            if (GUILayout.Button("💾 수정된 메쉬 에셋으로 영구 저장 (Save as Asset)", GUILayout.Height(32)))
            {
                stitcher.SaveMeshAsAsset();
            }
            GUI.backgroundColor = Color.white;
            EditorGUILayout.Space(10);
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
