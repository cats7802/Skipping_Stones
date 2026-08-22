#if UNITY_EDITOR
using System.IO;
using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(RiverValleyTerrainGenerator))]
public class RiverValleyTerrainGeneratorEditor : Editor
{
    private string newPresetName = "My_Custom_Preset";

    public override void OnInspectorGUI()
    {
        RiverValleyTerrainGenerator gen = (RiverValleyTerrainGenerator)target;

        GUIStyle titleStyle = new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize = 13,
            alignment = TextAnchor.MiddleLeft
        };

        // 🌟 1. 원클릭 3대 지형 프리셋 버튼
        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField("🏞️ 1-Click 지형 스케일 프리셋", titleStyle);
        EditorGUILayout.HelpBox("버튼을 누르면 해당 분위기의 최적 수치가 즉시 세팅됩니다. 변경 후 맨 아래 [지형 생성하기]를 누르세요!", MessageType.Info);

        EditorGUILayout.BeginHorizontal();

        GUI.backgroundColor = new Color(0.7f, 1.0f, 0.7f); // 연초록
        if (GUILayout.Button("🍃 아기자기한 개울가\n(레퍼런스 추천)", GUILayout.Height(42)))
        {
            Undo.RecordObject(gen, "Apply Cozy Stream Preset");
            gen.ApplyPresetCozyStream();
            EditorUtility.SetDirty(gen);
        }

        GUI.backgroundColor = new Color(0.75f, 0.95f, 1.0f); // 연하늘
        if (GUILayout.Button("🌾 정겨운 시골 하천\n(중형 스케일)", GUILayout.Height(42)))
        {
            Undo.RecordObject(gen, "Apply Rural River Preset");
            gen.ApplyPresetRuralRiver();
            EditorUtility.SetDirty(gen);
        }

        GUI.backgroundColor = new Color(1.0f, 0.85f, 0.7f); // 연주황
        if (GUILayout.Button("🌊 시원한 넓은 강\n(대형 스케일)", GUILayout.Height(42)))
        {
            Undo.RecordObject(gen, "Apply Grand River Preset");
            gen.ApplyPresetGrandRiver();
            EditorUtility.SetDirty(gen);
        }

        GUI.backgroundColor = Color.white;
        EditorGUILayout.EndHorizontal();

        // 🌟 2. 커스텀 프리셋 저장 및 로드
        EditorGUILayout.Space(12);
        EditorGUILayout.LabelField("💾 내 프리셋 저장 / 불러오기", titleStyle);

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        // 프리셋 슬롯
        EditorGUILayout.BeginHorizontal();
        gen.activePreset = (RiverValleyTerrainPreset)EditorGUILayout.ObjectField("프리셋 에셋", gen.activePreset, typeof(RiverValleyTerrainPreset), false);
        if (gen.activePreset != null)
        {
            GUI.backgroundColor = new Color(0.8f, 1.0f, 0.9f);
            if (GUILayout.Button("📂 적용 (Load)", GUILayout.Width(90), GUILayout.Height(20)))
            {
                Undo.RecordObject(gen, "Load Terrain Preset");
                gen.activePreset.ApplyToGenerator(gen);
                EditorUtility.SetDirty(gen);
            }
            GUI.backgroundColor = Color.white;
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(4);

        // 현재 수치를 새 프리셋으로 저장
        EditorGUILayout.BeginHorizontal();
        newPresetName = EditorGUILayout.TextField("새 프리셋 이름", newPresetName);
        GUI.backgroundColor = new Color(1.0f, 0.9f, 0.5f);
        if (GUILayout.Button("💾 현재 수치 저장", GUILayout.Width(110)))
        {
            SaveCurrentAsPreset(gen, newPresetName);
        }
        GUI.backgroundColor = Color.white;
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.EndVertical();

        EditorGUILayout.Space(10);

        // 🌟 3. 기본 인스펙터 프로퍼티 (슬라이더 및 세부 파라미터)
        DrawDefaultInspector();

        // 🌟 4. 최종 지형 생성 버튼
        EditorGUILayout.Space(16);
        GUI.backgroundColor = new Color(0.2f, 0.8f, 0.4f);
        if (GUILayout.Button("🚀 지형 및 수면 생성하기 (Generate Terrain)", GUILayout.Height(40)))
        {
            gen.Generate();
        }
        GUI.backgroundColor = Color.white;
        EditorGUILayout.Space(10);
    }

    private void SaveCurrentAsPreset(RiverValleyTerrainGenerator gen, string presetName)
    {
        if (string.IsNullOrWhiteSpace(presetName)) presetName = "Custom_Terrain_Preset";

        string folderPath = "Assets/TerrainData/Presets";
        if (!AssetDatabase.IsValidFolder("Assets/TerrainData"))
        {
            AssetDatabase.CreateFolder("Assets", "TerrainData");
        }
        if (!AssetDatabase.IsValidFolder(folderPath))
        {
            AssetDatabase.CreateFolder("Assets/TerrainData", "Presets");
        }

        string assetPath = $"{folderPath}/{presetName}.asset";
        RiverValleyTerrainPreset preset = AssetDatabase.LoadAssetAtPath<RiverValleyTerrainPreset>(assetPath);

        if (preset == null)
        {
            preset = ScriptableObject.CreateInstance<RiverValleyTerrainPreset>();
            preset.presetName = presetName;
            preset.CopyFromGenerator(gen);
            AssetDatabase.CreateAsset(preset, assetPath);
        }
        else
        {
            Undo.RecordObject(preset, "Overwrite Terrain Preset");
            preset.CopyFromGenerator(gen);
            EditorUtility.SetDirty(preset);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        gen.activePreset = preset;
        EditorUtility.SetDirty(gen);

        EditorUtility.DisplayDialog("프리셋 저장 완료", $"현재 지형 설정이 성공적으로 저장되었습니다!\n\n경로: {assetPath}", "확인");
    }
}
#endif
