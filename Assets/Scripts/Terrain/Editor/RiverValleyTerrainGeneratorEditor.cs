using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(RiverValleyTerrainGenerator))]
public class RiverValleyTerrainGeneratorEditor : Editor
{
    private SerializedProperty sizeX;
    private SerializedProperty sizeY;
    private SerializedProperty sizeZ;
    private SerializedProperty heightmapResolution;
    private SerializedProperty alphamapResolution;

    private SerializedProperty randomSeed;

    private SerializedProperty riverWidthMin;
    private SerializedProperty riverWidthMax;
    private SerializedProperty waterHeight;
    private SerializedProperty waterMeshWidth;
    private SerializedProperty riverBedDepth;
    private SerializedProperty meanderPrimaryAmp;
    private SerializedProperty meanderSecondaryAmp;
    private SerializedProperty meanderTertiaryAmp;
    private SerializedProperty applyTertiaryToRiver;

    private SerializedProperty valleyBaseHeight;
    private SerializedProperty leftValleyWidthMin;
    private SerializedProperty leftValleyWidthMax;
    private SerializedProperty rightValleyWidthMin;
    private SerializedProperty rightValleyWidthMax;
    private SerializedProperty mountainFootTertiaryAmp;
    private SerializedProperty mountainFootNoiseAmp;
    private SerializedProperty mountainMaxHeightMin;
    private SerializedProperty mountainMaxHeightMax;
    private SerializedProperty mountainTransitionWidthMin;
    private SerializedProperty mountainTransitionWidthMax;

    private SerializedProperty grassLayer;
    private SerializedProperty rockLayer;
    private SerializedProperty sandLayer;
    private SerializedProperty snowLayer;
    private SerializedProperty waterMaterial;

    private void OnEnable()
    {
        sizeX = serializedObject.FindProperty("sizeX");
        sizeY = serializedObject.FindProperty("sizeY");
        sizeZ = serializedObject.FindProperty("sizeZ");
        heightmapResolution = serializedObject.FindProperty("heightmapResolution");
        alphamapResolution = serializedObject.FindProperty("alphamapResolution");

        randomSeed = serializedObject.FindProperty("randomSeed");

        riverWidthMin = serializedObject.FindProperty("riverWidthMin");
        riverWidthMax = serializedObject.FindProperty("riverWidthMax");
        waterHeight = serializedObject.FindProperty("waterHeight");
        waterMeshWidth = serializedObject.FindProperty("waterMeshWidth");
        riverBedDepth = serializedObject.FindProperty("riverBedDepth");
        meanderPrimaryAmp = serializedObject.FindProperty("meanderPrimaryAmp");
        meanderSecondaryAmp = serializedObject.FindProperty("meanderSecondaryAmp");
        meanderTertiaryAmp = serializedObject.FindProperty("meanderTertiaryAmp");
        applyTertiaryToRiver = serializedObject.FindProperty("applyTertiaryToRiver");

        valleyBaseHeight = serializedObject.FindProperty("valleyBaseHeight");
        leftValleyWidthMin = serializedObject.FindProperty("leftValleyWidthMin");
        leftValleyWidthMax = serializedObject.FindProperty("leftValleyWidthMax");
        rightValleyWidthMin = serializedObject.FindProperty("rightValleyWidthMin");
        rightValleyWidthMax = serializedObject.FindProperty("rightValleyWidthMax");
        mountainFootTertiaryAmp = serializedObject.FindProperty("mountainFootTertiaryAmp");
        mountainFootNoiseAmp = serializedObject.FindProperty("mountainFootNoiseAmp");
        mountainMaxHeightMin = serializedObject.FindProperty("mountainMaxHeightMin");
        mountainMaxHeightMax = serializedObject.FindProperty("mountainMaxHeightMax");
        mountainTransitionWidthMin = serializedObject.FindProperty("mountainTransitionWidthMin");
        mountainTransitionWidthMax = serializedObject.FindProperty("mountainTransitionWidthMax");

        grassLayer = serializedObject.FindProperty("grassLayer");
        rockLayer = serializedObject.FindProperty("rockLayer");
        sandLayer = serializedObject.FindProperty("sandLayer");
        snowLayer = serializedObject.FindProperty("snowLayer");
        waterMaterial = serializedObject.FindProperty("waterMaterial");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.Space(5);
        EditorGUILayout.HelpBox("수치를 변경한 후 아래의 [🌲 지형 생성하기] 버튼을 누르면 즉시 지형에 반영됩니다.\n강과 평야가 1~3차 굴곡을 공유하며, 산맥 기슭의 불규칙 굴곡이 자연스러운 산자락을 만듭니다.", MessageType.Info);
        EditorGUILayout.Space(8);

        // 1. 지형 기본 크기
        DrawSectionHeader("1. 지형 크기 및 해상도");
        EditorGUILayout.PropertyField(sizeX, new GUIContent("가로 크기 (X축)", "지형의 전체 가로 폭 (미터)"));
        EditorGUILayout.PropertyField(sizeY, new GUIContent("최대 높이 범위 (Y축)", "지형 높이의 최대 스케일 (미터)"));
        EditorGUILayout.PropertyField(sizeZ, new GUIContent("세로 길이 (Z축 반복 주기)", "지형 세로 길이이자 무한 반복되는 타일 주기 (미터)"));
        EditorGUILayout.PropertyField(heightmapResolution, new GUIContent("높이맵 해상도", "기본 513 권장"));
        EditorGUILayout.PropertyField(alphamapResolution, new GUIContent("텍스처 해상도", "기본 512 권장"));
        EditorGUILayout.Space(10);

        // 2. 랜덤 시드
        DrawSectionHeader("2. 랜덤 시드 (Random Seed)");
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.PropertyField(randomSeed, new GUIContent("시드 번호 (Seed)", "랜덤 지형 생성을 위한 고유 시드 번호"));
        if (GUILayout.Button("🎲 랜덤 시드 변경", GUILayout.Width(120)))
        {
            randomSeed.intValue = Random.Range(1, 99999);
        }
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.Space(10);

        // 3. 강 및 수면 설정
        DrawSectionHeader("3. 강 및 수면 설정 (River & Water)");
        EditorGUILayout.LabelField("강폭 범위 (River Width Min / Max)", EditorStyles.miniBoldLabel);
        EditorGUILayout.PropertyField(riverWidthMin, new GUIContent("  강폭 Min (미터)", "강의 최소 수면 폭"));
        EditorGUILayout.PropertyField(riverWidthMax, new GUIContent("  강폭 Max (미터)", "강의 최대 수면 폭"));

        EditorGUILayout.Space(3);
        EditorGUILayout.PropertyField(waterHeight, new GUIContent("수면 높이 Y (미터)", "물 표면의 월드 Y 높이"));
        EditorGUILayout.PropertyField(waterMeshWidth, new GUIContent("수면 메쉬 폭 (미터)", "수면이 지형 안쪽으로 파고들 수 있도록 지형보다 넓게 설정 (권장 100m)"));
        EditorGUILayout.PropertyField(riverBedDepth, new GUIContent("강바닥 기준 깊이 Y (미터)", "강 중심 바닥면의 기준 Y 높이 (수심 약 5~7m 형성)"));
        EditorGUILayout.PropertyField(meanderPrimaryAmp, new GUIContent("강 및 평야 굽이침 1차 진폭", "강 물길과 평야의 큰 S자 굴곡 폭"));
        EditorGUILayout.PropertyField(meanderSecondaryAmp, new GUIContent("강 및 평야 굽이침 2차 세부 진폭", "강 물길과 평야의 2차 세부 굴곡 폭"));
        EditorGUILayout.PropertyField(applyTertiaryToRiver, new GUIContent("강 물길에도 3차 진폭 적용", "체크 시 강 물길(수로)에도 3차 미세 굽이침을 함께 적용합니다 (체크 해제 시 평야/산맥 경계에만 적용)"));
        if (applyTertiaryToRiver.boolValue)
        {
            EditorGUILayout.PropertyField(meanderTertiaryAmp, new GUIContent("  └ 강 3차 미세 진폭 (미터)", "강 물길의 3차 미세 굴곡 폭"));
        }
        EditorGUILayout.Space(10);

        // 4. 산맥 및 계곡 평야 설정
        DrawSectionHeader("4. 산맥 및 계곡 평야 설정 (Mountains & Valley)");
        EditorGUILayout.PropertyField(valleyBaseHeight, new GUIContent("계곡 바닥 높이 Y (미터)", "중앙 평야 지대의 기본 바닥 Y 높이"));

        EditorGUILayout.Space(3);
        EditorGUILayout.LabelField("좌측 평야 폭 범위 (강 중심 ~ 좌측 산맥)", EditorStyles.miniBoldLabel);
        EditorGUILayout.PropertyField(leftValleyWidthMin, new GUIContent("  좌측 평야 폭 Min (미터)", "강 중심에서 좌측 산맥 시작점까지의 최소 평야 폭"));
        EditorGUILayout.PropertyField(leftValleyWidthMax, new GUIContent("  좌측 평야 폭 Max (미터)", "강 중심에서 좌측 산맥 시작점까지의 최대 평야 폭"));

        EditorGUILayout.Space(3);
        EditorGUILayout.LabelField("우측 평야 폭 범위 (강 중심 ~ 우측 산맥)", EditorStyles.miniBoldLabel);
        EditorGUILayout.PropertyField(rightValleyWidthMin, new GUIContent("  우측 평야 폭 Min (미터)", "강 중심에서 우측 산맥 시작점까지의 최소 평야 폭"));
        EditorGUILayout.PropertyField(rightValleyWidthMax, new GUIContent("  우측 평야 폭 Max (미터)", "강 중심에서 우측 산맥 시작점까지의 최대 평야 폭"));

        EditorGUILayout.Space(3);
        EditorGUILayout.LabelField("산맥 기슭(평야 경계선) 3차 진폭 및 랜덤 굴곡", EditorStyles.miniBoldLabel);
        EditorGUILayout.PropertyField(mountainFootTertiaryAmp, new GUIContent("  산맥 기슭 3차 굴곡 진폭 (미터)", "평야 끝에서 산맥이 시작되는 경계선의 3차 주기적 굽이침 진폭"));
        EditorGUILayout.PropertyField(mountainFootNoiseAmp, new GUIContent("  산맥 기슭 불규칙 노이즈 진폭 (미터)", "산자락이 평야 쪽으로 불규칙하게 뻗어 나오거나 물러나는 크기"));

        EditorGUILayout.Space(3);
        EditorGUILayout.LabelField("산맥 최고봉 높이 범위 (랜덤)", EditorStyles.miniBoldLabel);
        EditorGUILayout.PropertyField(mountainMaxHeightMin, new GUIContent("  산맥 최고 높이 Min (미터)", "양쪽 산맥의 최고봉 최소 높이"));
        EditorGUILayout.PropertyField(mountainMaxHeightMax, new GUIContent("  산맥 최고 높이 Max (미터)", "양쪽 산맥의 최고봉 최대 높이"));

        EditorGUILayout.Space(3);
        EditorGUILayout.LabelField("산맥 경사면 폭 범위 (랜덤)", EditorStyles.miniBoldLabel);
        EditorGUILayout.PropertyField(mountainTransitionWidthMin, new GUIContent("  산맥 경사면 폭 Min (미터)", "평야에서 산맥 정상으로 이어지는 경사 구간의 최소 폭"));
        EditorGUILayout.PropertyField(mountainTransitionWidthMax, new GUIContent("  산맥 경사면 폭 Max (미터)", "평야에서 산맥 정상으로 이어지는 경사 구간의 최대 폭"));
        EditorGUILayout.Space(10);

        // 5. 에셋 레이어
        DrawSectionHeader("5. 텍스처 레이어 및 물 머티리얼");
        EditorGUILayout.PropertyField(grassLayer, new GUIContent("잔디 레이어 (Grass)", "평야와 완만한 경사면에 칠해지는 잔디 텍스처"));
        EditorGUILayout.PropertyField(rockLayer, new GUIContent("바위/암벽 레이어 (Rock)", "가파른 절벽과 고지대에 칠해지는 바위 텍스처"));
        EditorGUILayout.PropertyField(sandLayer, new GUIContent("모래/강변 레이어 (Sand)", "물속 강바닥과 물가 강둑에 칠해지는 모래/자갈 텍스처"));
        EditorGUILayout.PropertyField(snowLayer, new GUIContent("설경 레이어 (Snow)", "산 정상에 칠해지는 만년설 텍스처"));
        EditorGUILayout.PropertyField(waterMaterial, new GUIContent("강물 머티리얼 (Water Material)", "수면 메쉬에 적용되는 워터 셰이더 머티리얼"));
        EditorGUILayout.Space(15);

        serializedObject.ApplyModifiedProperties();

        // Big Generate Button
        GUI.backgroundColor = new Color(0.25f, 0.75f, 0.4f);
        if (GUILayout.Button("🌲 지형 생성하기 (Generate Terrain) 🌊", GUILayout.Height(40)))
        {
            RiverValleyTerrainGenerator generator = (RiverValleyTerrainGenerator)target;
            Undo.RegisterFullObjectHierarchyUndo(generator.gameObject, "Generate Terrain");
            generator.Generate();
            EditorUtility.SetDirty(generator);
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



