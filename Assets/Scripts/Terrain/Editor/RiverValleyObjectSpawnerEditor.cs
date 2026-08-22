#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(RiverValleyObjectSpawner))]
public class RiverValleyObjectSpawnerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        RiverValleyObjectSpawner spawner = (RiverValleyObjectSpawner)target;

        EditorGUILayout.Space(5);
        EditorGUILayout.HelpBox("🌲 지형 생성기(`RiverValleyTerrainGenerator`)와 연동하여 나무, 바위, 덤불, 야생화를 지형 조건(높이, 경사도, 물길 거리, 군락 노이즈)에 맞춰 자동으로 자연스럽게 배치합니다.", MessageType.Info);
        EditorGUILayout.Space(8);

        // 상단 프리셋 및 실행 버튼 바
        EditorGUILayout.BeginHorizontal();
        GUI.backgroundColor = new Color(0.35f, 0.75f, 1f, 1f);
        if (GUILayout.Button("🔄 기본 프리셋 규칙 불러오기", GUILayout.Height(30)))
        {
            if (EditorUtility.DisplayDialog("기본 규칙 초기화", "기본 프랍 배치 규칙(소나무, 덤불, 꽃, 바위)을 다시 로드하시겠습니까?", "예", "아니오"))
            {
                Undo.RecordObject(spawner, "Reset Prop Rules");
                spawner.ResetToDefaultRules();
                EditorUtility.SetDirty(spawner);
            }
        }

        GUI.backgroundColor = new Color(1f, 0.85f, 0.35f, 1f);
        if (GUILayout.Button("🎲 랜덤 시드 변경", GUILayout.Height(30)))
        {
            Undo.RecordObject(spawner, "Change Prop Seed");
            spawner.seed = Random.Range(1, 999999);
            EditorUtility.SetDirty(spawner);
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(10);
        GUI.backgroundColor = Color.white;

        // 인스펙터 속성 그리기
        DrawDefaultInspector();

        EditorGUILayout.Space(15);

        // 핵심 실행 버튼
        GUI.backgroundColor = new Color(0.4f, 0.9f, 0.4f, 1f);
        if (GUILayout.Button("🌲 지형에 프랍 자동 배치하기 (Spawn Props) ✨", GUILayout.Height(42)))
        {
            spawner.SpawnAllProps();
        }

        EditorGUILayout.Space(5);
        GUI.backgroundColor = new Color(1f, 0.45f, 0.45f, 1f);
        if (GUILayout.Button("🧹 모든 프랍 지우기 (Clear All Props)", GUILayout.Height(28)))
        {
            if (EditorUtility.DisplayDialog("프랍 전체 삭제", "배치된 모든 프랍 오브젝트를 삭제하시겠습니까?", "삭제", "취소"))
            {
                spawner.ClearAllProps();
            }
        }

        GUI.backgroundColor = Color.white;
        EditorGUILayout.Space(10);

        serializedObject.ApplyModifiedProperties();
    }
}
#endif
