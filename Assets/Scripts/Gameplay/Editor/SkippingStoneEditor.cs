#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(SkippingStone))]
public class SkippingStoneEditor : Editor
{
    private SerializedProperty customStonePrefab;
    private SerializedProperty forwardPower;
    private SerializedProperty initialUpwardForce;
    private SerializedProperty baseBounceUpForce;
    private SerializedProperty maxHorizontalSpeed;
    private SerializedProperty gravityScale;
    private SerializedProperty airDrag;
    private SerializedProperty inFlightVisualScale;

    private SerializedProperty timingWindowHeight;
    private SerializedProperty perfectDistance;
    private SerializedProperty greatDistance;
    private SerializedProperty goodDistance;

    private SerializedProperty minSkimSkips;
    private SerializedProperty maxSkimSkips;

    private SerializedProperty trail;
    private SerializedProperty trailCustomMaterial;
    private SerializedProperty stoneCustomMaterial;
    private SerializedProperty trailStartColor;
    private SerializedProperty trailEndColor;

    private void OnEnable()
    {
        customStonePrefab = serializedObject.FindProperty("customStonePrefab");
        forwardPower = serializedObject.FindProperty("forwardPower");
        initialUpwardForce = serializedObject.FindProperty("initialUpwardForce");
        baseBounceUpForce = serializedObject.FindProperty("baseBounceUpForce");
        maxHorizontalSpeed = serializedObject.FindProperty("maxHorizontalSpeed");
        gravityScale = serializedObject.FindProperty("gravityScale");
        airDrag = serializedObject.FindProperty("airDrag");
        inFlightVisualScale = serializedObject.FindProperty("inFlightVisualScale");

        timingWindowHeight = serializedObject.FindProperty("timingWindowHeight");
        perfectDistance = serializedObject.FindProperty("perfectDistance");
        greatDistance = serializedObject.FindProperty("greatDistance");
        goodDistance = serializedObject.FindProperty("goodDistance");

        minSkimSkips = serializedObject.FindProperty("minSkimSkips");
        maxSkimSkips = serializedObject.FindProperty("maxSkimSkips");

        trail = serializedObject.FindProperty("trail");
        trailCustomMaterial = serializedObject.FindProperty("trailCustomMaterial");
        stoneCustomMaterial = serializedObject.FindProperty("stoneCustomMaterial");
        trailStartColor = serializedObject.FindProperty("trailStartColor");
        trailEndColor = serializedObject.FindProperty("trailEndColor");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        SkippingStone stone = (SkippingStone)target;

        GUIStyle titleStyle = new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize = 13,
            alignment = TextAnchor.MiddleLeft
        };

        // 1. 프리팹 설정
        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("📦 3D 모델 및 프리팹", titleStyle);
        EditorGUILayout.PropertyField(customStonePrefab, new GUIContent("조약돌 프리팹 (Prefab)"));

        // 2. 물리 및 투척 속도 슬라이더
        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("🚀 물리 및 투척/바운스 속도", titleStyle);

        DrawSliderField(forwardPower, "전방 투척 속도 (Power)", 5f, 50f, "기본 투척 수평 전진 속도 (m/s)");
        DrawSliderField(initialUpwardForce, "초기 솟구침 상승력", 1f, 15f, "첫 투척 시 위로 뜨는 포물선 힘");
        DrawSliderField(baseBounceUpForce, "수면 바운스 반사력", 1f, 12f, "수면에 닿았을 때 위로 튀어오르는 높이");
        DrawSliderField(maxHorizontalSpeed, "최대 수평 속도 상한", 10f, 60f, "콤보 가속 시 도달할 수 있는 최고 속도");
        DrawSliderField(gravityScale, "중력 가속도 배율", 0.5f, 3.0f, "낙하 속도 및 체공 시간 제어");
        DrawSliderField(airDrag, "공기 저항 감쇠", 0.95f, 1.0f, "공기 중 전진 속도 보존율");

        // 3. 비행 시 시각 연출 (돌 크기)
        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("✨ 비행 시 비주얼 연출 (돌 크기)", titleStyle);
        DrawSliderField(inFlightVisualScale, "비행 중 돌 크기 배율", 0.5f, 5.0f, "1.0 = 원본 크기 유지 / 수치를 올리면 날아갈 때 돌이 시원하게 커집니다");

        // 4. 리듬 타이밍 판정 범위
        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("🎯 리듬 탭 판정 거리 (수면 위 m)", titleStyle);
        DrawSliderField(timingWindowHeight, "타이밍 윈도우 시작 높이", 1.0f, 5.0f, "판정 링이 표시되는 수면 위 높이");
        DrawSliderField(perfectDistance, "PERFECT 판정 거리", 0.1f, 2.0f, "퍼펙트 인정 착수 직전 높이");
        DrawSliderField(greatDistance, "GREAT 판정 거리", 0.2f, 3.0f, "그레이트 인정 높이");
        DrawSliderField(goodDistance, "GOOD 판정 거리", 0.5f, 4.0f, "굿 인정 높이");

        // 5. 리듬 링 비주얼 세부 설정 (통합 슬라이더)
        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("⭕ 리듬 링 비주얼 및 두께 설정", titleStyle);
        SerializedProperty ringLineWidth = serializedObject.FindProperty("ringLineWidth");
        SerializedProperty ringTargetRadius = serializedObject.FindProperty("ringTargetRadius");
        SerializedProperty ringMaxMultiplier = serializedObject.FindProperty("ringMaxMultiplier");
        SerializedProperty dropLineWidth = serializedObject.FindProperty("dropLineWidth");

        if (ringLineWidth != null) DrawSliderField(ringLineWidth, "수면 링 선 두께", 0.005f, 0.15f, "수면 위에 렌더링되는 링의 굵기");
        if (ringTargetRadius != null) DrawSliderField(ringTargetRadius, "퍼펙트 타깃 링 반경", 0.1f, 1.5f, "중앙 퍼펙트 링의 크기");
        if (ringMaxMultiplier != null) DrawSliderField(ringMaxMultiplier, "바깥 링 시작 수축 배율", 1.5f, 10.0f, "수축 시작 시 바깥 링의 최대 크기 배율");
        if (dropLineWidth != null) DrawSliderField(dropLineWidth, "수직 드롭 가이드 선 두께", 0.001f, 0.04f, "돌에서 수면까지 잇는 수직 레이저 선 두께");

        // 6. 피니시 스키밍
        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("🌊 피니시 스키밍 설정", titleStyle);
        EditorGUILayout.PropertyField(minSkimSkips, new GUIContent("최소 스키밍 발동 스킵 수"));
        EditorGUILayout.PropertyField(maxSkimSkips, new GUIContent("최대 스키밍 효과 스킵 수"));

        // 7. 트레일 및 이펙트
        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("🌈 트레일 및 머티리얼", titleStyle);
        EditorGUILayout.PropertyField(trail, new GUIContent("트레일 렌더러"));
        EditorGUILayout.PropertyField(trailCustomMaterial, new GUIContent("트레일 머티리얼"));
        EditorGUILayout.PropertyField(stoneCustomMaterial, new GUIContent("조약돌 전용 머티리얼"));
        EditorGUILayout.PropertyField(trailStartColor, new GUIContent("트레일 시작 색상"));
        EditorGUILayout.PropertyField(trailEndColor, new GUIContent("트레일 끝 색상"));

        // 8. 원클릭 물리 프리셋 버튼
        EditorGUILayout.Space(12);
        EditorGUILayout.LabelField("⚙️ 원클릭 물리 밸런스 프리셋", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();

        GUI.backgroundColor = new Color(0.7f, 1.0f, 0.7f);
        if (GUILayout.Button("🍃 아늑한 힐링 호수 (추천)", GUILayout.Height(28)))
        {
            Undo.RecordObject(stone, "Apply Cozy Lake Preset");
            stone.forwardPower = 13.0f;
            stone.initialUpwardForce = 4.2f;
            stone.baseBounceUpForce = 4.0f;
            stone.maxHorizontalSpeed = 18.0f;
            stone.gravityScale = 1.45f;
            stone.inFlightVisualScale = 1.0f;
            EditorUtility.SetDirty(stone);
        }

        GUI.backgroundColor = new Color(0.7f, 0.9f, 1.0f);
        if (GUILayout.Button("⚡ 시원한 롱디스턴스", GUILayout.Height(28)))
        {
            Undo.RecordObject(stone, "Apply Long Distance Preset");
            stone.forwardPower = 23.0f;
            stone.initialUpwardForce = 5.5f;
            stone.baseBounceUpForce = 5.2f;
            stone.maxHorizontalSpeed = 36.0f;
            stone.gravityScale = 1.35f;
            stone.inFlightVisualScale = 1.0f;
            EditorUtility.SetDirty(stone);
        }
        GUI.backgroundColor = Color.white;
        EditorGUILayout.EndHorizontal();

        // 8. 런타임 상태 표시 (Play 모드 전용)
        if (Application.isPlaying)
        {
            EditorGUILayout.Space(10);
            EditorGUILayout.HelpBox($"[실시간 상태]\n스킵 횟수: {stone.skipCount}회\n비행 중: {stone.isThrown} | 침수: {stone.isSunk} | 스키밍: {stone.isSkimming}", MessageType.Info);
        }

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawSliderField(SerializedProperty prop, string label, float min, float max, string tooltip)
    {
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.PrefixLabel(new GUIContent(label, tooltip));
        prop.floatValue = EditorGUILayout.Slider(prop.floatValue, min, max);
        EditorGUILayout.EndHorizontal();
    }
}
#endif
