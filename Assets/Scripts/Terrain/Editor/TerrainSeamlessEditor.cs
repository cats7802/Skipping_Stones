using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(TerrainSeamless))]
public class TerrainSeamlessEditor : Editor
{
    private SerializedProperty seamlessX;
    private SerializedProperty seamlessZ;
    private SerializedProperty blendDistance;
    private SerializedProperty blendHeights;
    private SerializedProperty blendTextures;
    private SerializedProperty blendCurve;

    private void OnEnable()
    {
        seamlessX = serializedObject.FindProperty("seamlessX");
        seamlessZ = serializedObject.FindProperty("seamlessZ");
        blendDistance = serializedObject.FindProperty("blendDistance");
        blendHeights = serializedObject.FindProperty("blendHeights");
        blendTextures = serializedObject.FindProperty("blendTextures");
        blendCurve = serializedObject.FindProperty("blendCurve");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.Space(5);
        EditorGUILayout.HelpBox(
            "브러쉬로 지형을 수정한 후 [🔗 경계면 이음새 맞추기] 버튼을 누르면\n" +
            "선택한 축의 양쪽 끝 경계면을 매끄럽게 보간하여 무한 반복(Seamless) 지형으로 변환합니다.", 
            MessageType.Info);
        EditorGUILayout.Space(8);

        // 1. 이음새 동기화 대상 축
        DrawSectionHeader("1. 반복 타일링 축 선택 (Seamless Axes)");
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.PropertyField(seamlessX, new GUIContent("X축 연결 (좌 ↔ 우)", "체크 시 좌측과 우측 경계면을 매끄럽게 연결합니다."));
        EditorGUILayout.PropertyField(seamlessZ, new GUIContent("Z축 연결 (앞 ↔ 뒤)", "체크 시 앞쪽과 뒤쪽 경계면을 매끄럽게 연결합니다."));
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.Space(10);

        // 2. 보간 설정
        DrawSectionHeader("2. 보간(Blend) 설정");
        EditorGUILayout.PropertyField(blendDistance, new GUIContent("보간 거리 (미터)", "경계면에서 안쪽으로 자연스럽게 섞여 들어갈 거리 (권장: 40m~100m)"));
        EditorGUILayout.PropertyField(blendCurve, new GUIContent("보간 곡선 (Curve)", "경계면과 안쪽 지형을 이어줄 부드러움 방식 (SmoothStep 권장)"));
        EditorGUILayout.Space(3);
        EditorGUILayout.PropertyField(blendHeights, new GUIContent("지형 높이 보간 (Height)", "높낮이 단차를 없애고 매끄럽게 연결합니다."));
        EditorGUILayout.PropertyField(blendTextures, new GUIContent("텍스처 보간 (Textures)", "경계면의 칠해진 잔디/바위/모래 텍스처를 자연스럽게 섞어줍니다."));
        EditorGUILayout.Space(15);

        serializedObject.ApplyModifiedProperties();

        // Stitch Button
        GUI.backgroundColor = new Color(0.2f, 0.6f, 0.95f);
        if (GUILayout.Button("🔗 경계면 이음새 맞추기 (Apply Seamless Stitch) 🪄", GUILayout.Height(38)))
        {
            TerrainSeamless stitcher = (TerrainSeamless)target;
            stitcher.MakeSeamless();
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
