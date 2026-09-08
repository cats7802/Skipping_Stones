#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(SkippingStone))]
public class SkippingStoneEditor : Editor
{
    private SerializedProperty forwardPower;
    private SerializedProperty initialUpwardForce;
    private SerializedProperty baseBounceUpForce;
    private SerializedProperty maxHorizontalSpeed;
    private SerializedProperty gravityScale;
    private SerializedProperty airDrag;
    private SerializedProperty inFlightVisualScale;

    private SerializedProperty timingWindowHeight;

    private SerializedProperty minSkimSkips;
    private SerializedProperty maxSkimSkips;

    private void OnEnable()
    {
        forwardPower = serializedObject.FindProperty("forwardPower");
        initialUpwardForce = serializedObject.FindProperty("initialUpwardForce");
        baseBounceUpForce = serializedObject.FindProperty("baseBounceUpForce");
        maxHorizontalSpeed = serializedObject.FindProperty("maxHorizontalSpeed");
        gravityScale = serializedObject.FindProperty("gravityScale");
        airDrag = serializedObject.FindProperty("airDrag");
        inFlightVisualScale = serializedObject.FindProperty("inFlightVisualScale");

        timingWindowHeight = serializedObject.FindProperty("timingWindowHeight");

        minSkimSkips = serializedObject.FindProperty("minSkimSkips");
        maxSkimSkips = serializedObject.FindProperty("maxSkimSkips");
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

        // 1. 물리 및 투척 속도 슬라이더
        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField("🚀 물리 및 투척/바운스 속도", titleStyle);

        DrawSliderField(forwardPower, "전방 투척 속도 (Power)", 5f, 50f, "기본 투척 수평 전진 속도 (m/s)");
        DrawSliderField(initialUpwardForce, "초기 솟구침 상승력", 1f, 15f, "첫 투척 시 위로 뜨는 포물선 힘");
        DrawSliderField(baseBounceUpForce, "수면 바운스 반사력", 1f, 12f, "수면에 닿았을 때 위로 튀어오르는 높이");
        DrawSliderField(maxHorizontalSpeed, "최대 수평 속도 상한", 10f, 60f, "콤보 가속 시 도달할 수 있는 최고 속도");
        DrawSliderField(gravityScale, "중력 가속도 배율", 0.5f, 3.0f, "낙하 속도 및 체공 시간 제어");
        DrawSliderField(airDrag, "공기 저항 감쇠", 0.95f, 1.0f, "공기 중 전진 속도 보존율");

        // 2. 비행 시 시각 연출 (돌 크기)
        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("✨ 비행 시 비주얼 연출 (돌 크기)", titleStyle);
        DrawSliderField(inFlightVisualScale, "비행 중 돌 크기 배율", 0.5f, 5.0f, "1.0 = 원본 크기 유지 / 수치를 올리면 날아갈 때 돌이 시원하게 커집니다");

        // 3. 리듬 타이밍 판정 범위
        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("🎯 리듬 탭 판정 거리 (수면 위 m)", titleStyle);
        DrawSliderField(timingWindowHeight, "타이밍 윈도우 시작 높이", 1.0f, 5.0f, "판정 링이 표시되는 수면 위 높이 (기본: 2.8m)");
        EditorGUILayout.HelpBox("💡 착수 판정 및 모멘텀 계산은 StoneTimingEvaluator에서 단일 진실 공급원으로 통합 처리됩니다.\n⭕ 리듬 링의 세부 비주얼(크기, 색상 등)은 하단의 RhythmRingIndicator 컴포넌트에서 조절하세요.", MessageType.None);

        // 4. 피니시 스키밍
        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("🌊 피니시 스키밍 설정", titleStyle);
        EditorGUILayout.PropertyField(minSkimSkips, new GUIContent("최소 스키밍 발동 스킵 수"));
        EditorGUILayout.PropertyField(maxSkimSkips, new GUIContent("최대 스키밍 효과 스킵 수"));

        // 5. 원클릭 물리 프리셋 버튼
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

        // 7. 런타임 상태 표시 (Play 모드 전용)
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
