#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(LakeEnvironmentManager))]
public class LakeEnvironmentManagerEditor : Editor
{
    private SerializedProperty mapTitleProp;
    private SerializedProperty mapThumbnailProp;
    private SerializedProperty startMapPrefabProp;
    private SerializedProperty loopSlotsProp;
    private SerializedProperty endingMapPrefabProp;
    private SerializedProperty targetClearDistanceProp;
    private SerializedProperty autoChunkSizeProp;

    private void OnEnable()
    {
        mapTitleProp = serializedObject.FindProperty("mapTitle");
        mapThumbnailProp = serializedObject.FindProperty("mapThumbnail");
        startMapPrefabProp = serializedObject.FindProperty("startMapPrefab");
        loopSlotsProp = serializedObject.FindProperty("loopSlots");
        endingMapPrefabProp = serializedObject.FindProperty("endingMapPrefab");
        targetClearDistanceProp = serializedObject.FindProperty("targetClearDistance");
        autoChunkSizeProp = serializedObject.FindProperty("autoChunkSize");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        GUIStyle headerStyle = new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize = 12,
            alignment = TextAnchor.MiddleLeft
        };

        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("🏷️ 0. 맵 메타 정보", headerStyle);
        EditorGUILayout.PropertyField(mapTitleProp, new GUIContent("Map Title (맵 이름)", "3번 맵 선택 및 로비/인게임 UI에 표시될 맵 타이틀 텍스트"));
        EditorGUILayout.PropertyField(mapThumbnailProp, new GUIContent("Map Thumbnail (2D)", "3번 맵 선택 및 로비 UI에 표시될 2D 맵 썸네일 이미지"));
        EditorGUILayout.Space(6);

        EditorGUILayout.LabelField("🎬 1. 모듈러 스토리 시퀀스 설정", headerStyle);

        // 1. 시작 맵
        EditorGUILayout.PropertyField(startMapPrefabProp, new GUIContent("Start Map Prefab (SM)", "시작 전용 맵 프리팹 (비어있을 시 슬롯 1번 BaseMap 사용)"));
        EditorGUILayout.Space(6);

        // 2. 루프 슬롯 스핀박스 및 직관적 슬롯 에디터
        EditorGUILayout.LabelField("🔁 루프 슬롯 구성 (Loop System)", headerStyle);

        int currentCount = loopSlotsProp.arraySize;
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.PrefixLabel(new GUIContent("루프 맵 개수 (슬롯 수)", "순환할 루프 슬롯의 총 개수"));
        int newCount = EditorGUILayout.IntField(currentCount, GUILayout.Width(50));
        
        if (GUILayout.Button("▲", GUILayout.Width(24))) newCount++;
        if (GUILayout.Button("▼", GUILayout.Width(24))) newCount--;
        EditorGUILayout.EndHorizontal();

        if (newCount < 0) newCount = 0;
        if (newCount != currentCount)
        {
            loopSlotsProp.arraySize = newCount;
        }

        EditorGUI.indentLevel++;
        for (int i = 0; i < loopSlotsProp.arraySize; i++)
        {
            SerializedProperty slotProp = loopSlotsProp.GetArrayElementAtIndex(i);
            SerializedProperty baseMapProp = slotProp.FindPropertyRelative("baseMapPrefab");
            SerializedProperty useVarProp = slotProp.FindPropertyRelative("useVariations");
            SerializedProperty varListProp = slotProp.FindPropertyRelative("variationPrefabs");

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField($"📌 [ 슬롯 {i + 1} ]", EditorStyles.boldLabel);

            EditorGUILayout.PropertyField(baseMapProp, new GUIContent("Base Map Prefab", "이 슬롯의 기준 메인 프리팹 (필수)"));

            EditorGUILayout.PropertyField(useVarProp, new GUIContent("변주(랜덤) 맵 사용", "체크 시 등록된 변주 프리팹들 중 무작위 선택"));

            if (useVarProp.boolValue)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(varListProp, new GUIContent("추가 변주 프리팹 목록"), true);
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(2);
        }
        EditorGUI.indentLevel--;

        EditorGUILayout.Space(6);
        // 3. 엔딩 맵
        EditorGUILayout.LabelField("🏁 2. 피날레 엔딩 설정", headerStyle);
        EditorGUILayout.PropertyField(endingMapPrefabProp, new GUIContent("Ending Map Prefab (EM)", "목표 거리 도달 시 등장할 엔딩 맵 프리팹"));
        EditorGUILayout.PropertyField(targetClearDistanceProp, new GUIContent("목표 거리 (Target Clear)", "0 이하일 경우 엔딩 없이 무한 루프"));

        EditorGUILayout.Space(6);
        // 4. 청크 측정 정보
        EditorGUILayout.LabelField("📏 3. 스트리밍 정보", headerStyle);
        EditorGUILayout.PropertyField(autoChunkSizeProp, new GUIContent("Auto Chunk Size (Z)", "배경 1개 청크의 Z축 길이"));

        serializedObject.ApplyModifiedProperties();
    }
}
#endif
