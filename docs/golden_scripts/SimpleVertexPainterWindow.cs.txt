using UnityEngine;
using UnityEditor;
using System.IO;

namespace SkippingStones.Editor
{
    public class SimpleVertexPainterWindow : EditorWindow
    {
        public enum PaintChannel
        {
            Erase_BaseGrass = 0, // 검은색 (기본 잔디)
            Red_Dirt = 1,        // 빨간색 (흙/길)
            Green_Rock = 2,      // 초록색 (바위/절벽)
            Blue_Sand = 3,       // 파란색 (모래/해변)
            CustomColor = 4
        }

        [Header("브러시 설정")]
        public PaintChannel selectedChannel = PaintChannel.Red_Dirt;
        public Color customColor = Color.red;
        [Range(0.2f, 30f)] public float brushRadius = 3.5f;
        [Range(0.01f, 1f)] public float brushOpacity = 0.35f;
        [Range(0.01f, 1f)] public float brushFalloff = 0.5f;

        [Header("대상 메시")]
        public GameObject targetObject;
        private MeshFilter targetFilter;
        private Mesh clonedMesh;

        [MenuItem("Tools/SkippingStones/🖌️ Terrain Vertex Painter")]
        public static void OpenWindow()
        {
            var window = GetWindow<SimpleVertexPainterWindow>("Vertex Painter");
            window.minSize = new Vector2(320, 420);
            window.Show();
        }

        private void OnEnable()
        {
            SceneView.duringSceneGui += OnSceneGUI;
            AutoAssignSelection();
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui -= OnSceneGUI;
        }

        private void OnSelectionChange()
        {
            AutoAssignSelection();
            Repaint();
        }

        private void AutoAssignSelection()
        {
            if (Selection.activeGameObject != null)
            {
                var mf = Selection.activeGameObject.GetComponent<MeshFilter>();
                if (mf != null && mf.sharedMesh != null)
                {
                    targetObject = Selection.activeGameObject;
                    targetFilter = mf;
                }
            }
        }

        [Header("커스텀 레이어 이름")]
        public string layer0Name = "Layer 0 (Base Grass)";
        public string layer1Name = "Layer 1 (Dirt / Road)";
        public string layer2Name = "Layer 2 (Rock / Cliff)";
        public string layer3Name = "Layer 3 (Sand / Beach)";

        private void OnGUI()
        {
            GUILayout.Label("🖌️ 지형 텍스처 버텍스 페인터 (Unity 6 호환)", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            targetObject = (GameObject)EditorGUILayout.ObjectField("대상 오브젝트", targetObject, typeof(GameObject), true);
            
            Material currentMat = null;
            if (targetObject != null)
            {
                targetFilter = targetObject.GetComponent<MeshFilter>();
                var rend = targetObject.GetComponent<MeshRenderer>();
                if (rend != null) currentMat = rend.sharedMaterial;

                if (targetFilter != null && targetFilter.sharedMesh != null)
                {
                    EditorGUILayout.LabelField($"메시: {targetFilter.sharedMesh.name} (버텍스: {targetFilter.sharedMesh.vertexCount}개)");
                }
                else
                {
                    EditorGUILayout.HelpBox("선택된 오브젝트에 MeshFilter가 없습니다!", MessageType.Warning);
                }
            }
            else
            {
                EditorGUILayout.HelpBox("씬에서 칠할 지형(메시) 오브젝트를 선택해주세요.", MessageType.Info);
            }
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(6);
            GUILayout.Label("🎨 텍스처 레이어 선택 & 커스텀 라벨", EditorStyles.boldLabel);

            // 머티리얼 텍스처 자동 감지
            Texture tex0 = currentMat != null && currentMat.HasProperty("_BaseTex") ? currentMat.GetTexture("_BaseTex") : null;
            Texture tex1 = currentMat != null && currentMat.HasProperty("_DirtTex") ? currentMat.GetTexture("_DirtTex") : null;
            Texture tex2 = currentMat != null && currentMat.HasProperty("_RockTex") ? currentMat.GetTexture("_RockTex") : null;
            Texture tex3 = currentMat != null && currentMat.HasProperty("_SandTex") ? currentMat.GetTexture("_SandTex") : null;

            DrawLayerSelectorRow(PaintChannel.Erase_BaseGrass, "🌿 Base", ref layer0Name, tex0, Color.black);
            DrawLayerSelectorRow(PaintChannel.Red_Dirt, "🟫 Layer 1 (R)", ref layer1Name, tex1, Color.red);
            DrawLayerSelectorRow(PaintChannel.Green_Rock, "🪨 Layer 2 (G)", ref layer2Name, tex2, Color.green);
            DrawLayerSelectorRow(PaintChannel.Blue_Sand, "🏖️ Layer 3 (B)", ref layer3Name, tex3, Color.blue);

            EditorGUILayout.Space(6);
            if (selectedChannel == PaintChannel.CustomColor)
            {
                customColor = EditorGUILayout.ColorField("커스텀 색상", customColor);
            }

            EditorGUILayout.Space(6);
            GUILayout.Label("🖌️ 브러시 조절", EditorStyles.boldLabel);
            brushRadius = EditorGUILayout.Slider("반경 (Radius)", brushRadius, 0.2f, 30f);
            brushOpacity = EditorGUILayout.Slider("강도 (Opacity)", brushOpacity, 0.01f, 1f);
            brushFalloff = EditorGUILayout.Slider("외곽 부드러움 (Falloff)", brushFalloff, 0.01f, 1f);

            EditorGUILayout.Space(10);

            if (GUILayout.Button("🔄 전체 잔디(기본)로 초기화", GUILayout.Height(28)))
            {
                ClearAllColors();
            }

            EditorGUILayout.Space(4);

            if (GUILayout.Button("💾 칠해진 메시를 에셋으로 저장 (Save Mesh Asset)", GUILayout.Height(32)))
            {
                SaveMeshAsset();
            }

            EditorGUILayout.Space(6);
            EditorGUILayout.HelpBox("💡 사용법:\n1. 씬 뷰에서 대상을 마우스로 드래그하면 칠해집니다.\n2. Shift를 누르면 기본 잔디(지우개)로 칠해집니다.", MessageType.Info);
        }

        private void DrawLayerSelectorRow(PaintChannel channel, string defaultPrefix, ref string layerName, Texture tex, Color channelColor)
        {
            bool isSelected = (selectedChannel == channel);
            EditorGUILayout.BeginHorizontal(isSelected ? EditorStyles.selectionRect : EditorStyles.helpBox);

            GUI.backgroundColor = isSelected ? new Color(0.4f, 0.85f, 1f, 1f) : Color.white;
            if (GUILayout.Button(isSelected ? "● 선택됨" : "선택", GUILayout.Width(65), GUILayout.Height(26)))
            {
                selectedChannel = channel;
            }
            GUI.backgroundColor = Color.white;

            EditorGUILayout.LabelField(defaultPrefix, GUILayout.Width(85));

            layerName = EditorGUILayout.TextField(layerName);

            string texName = tex != null ? tex.name : "(None)";
            EditorGUILayout.LabelField($"[{texName}]", EditorStyles.miniLabel, GUILayout.Width(90));

            EditorGUILayout.EndHorizontal();
        }

        private Color GetActivePaintColor()
        {
            switch (selectedChannel)
            {
                case PaintChannel.Erase_BaseGrass: return new Color(0, 0, 0, 1);
                case PaintChannel.Red_Dirt: return new Color(1, 0, 0, 1);
                case PaintChannel.Green_Rock: return new Color(0, 1, 0, 1);
                case PaintChannel.Blue_Sand: return new Color(0, 0, 1, 1);
                case PaintChannel.CustomColor: return customColor;
                default: return Color.black;
            }
        }

        private void OnSceneGUI(SceneView sceneView)
        {
            if (targetObject == null || targetFilter == null || targetFilter.sharedMesh == null) return;

            Event e = Event.current;
            int controlID = GUIUtility.GetControlID(FocusType.Passive);
            HandleUtility.AddDefaultControl(controlID);

            Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
            bool hitFound = false;
            Vector3 hitPoint = Vector3.zero;
            Vector3 hitNormal = Vector3.up;

            // 1. 타깃 오브젝트의 MeshCollider 또는 실제 메시 삼각형 정밀 레이캐스트
            EnsureMeshInstance();
            if (clonedMesh != null)
            {
                hitFound = RaycastTargetMesh(ray, out hitPoint, out hitNormal);
            }

            // 2. 메시 레이캐스트 실패 시 가장 가까운 버텍스 탐색
            if (!hitFound && clonedMesh != null)
            {
                hitFound = FindClosestVertexToRay(ray, out hitPoint, out hitNormal);
            }

            if (hitFound)
            {
                // 브러시 서클 렌더링 (지형 표면 법선 방향으로 원형 가이드)
                Handles.color = new Color(1f, 0.85f, 0.1f, 0.90f);
                Handles.DrawWireDisc(hitPoint, hitNormal, brushRadius);
                Handles.color = new Color(1f, 0.85f, 0.1f, 0.18f);
                Handles.DrawSolidDisc(hitPoint, hitNormal, brushRadius);

                EventType eventType = e.GetTypeForControl(controlID);

                if ((eventType == EventType.MouseDrag || eventType == EventType.MouseDown) && e.button == 0 && !e.alt)
                {
                    bool isShift = e.shift;
                    Color paintColor = isShift ? new Color(0, 0, 0, 1) : GetActivePaintColor();

                    PaintVertices(hitPoint, paintColor);
                    e.Use();
                    GUI.changed = true;
                }
            }

            if (e.type == EventType.MouseMove)
            {
                sceneView.Repaint();
            }
        }

        private bool RaycastTargetMesh(Ray worldRay, out Vector3 hitPoint, out Vector3 hitNormal)
        {
            hitPoint = Vector3.zero;
            hitNormal = Vector3.up;

            Transform t = targetFilter.transform;
            Ray localRay = new Ray(t.InverseTransformPoint(worldRay.origin), t.InverseTransformDirection(worldRay.direction));

            Vector3[] verts = clonedMesh.vertices;
            int[] tris = clonedMesh.triangles;
            Vector3[] normals = clonedMesh.normals;

            float minDistance = float.MaxValue;
            bool hitFound = false;
            Vector3 bestLocalHit = Vector3.zero;
            Vector3 bestLocalNormal = Vector3.up;

            for (int i = 0; i < tris.Length; i += 3)
            {
                Vector3 v0 = verts[tris[i]];
                Vector3 v1 = verts[tris[i + 1]];
                Vector3 v2 = verts[tris[i + 2]];

                if (IntersectRayTriangle(localRay, v0, v1, v2, out float dist, out Vector3 localHit))
                {
                    if (dist < minDistance)
                    {
                        minDistance = dist;
                        bestLocalHit = localHit;
                        bestLocalNormal = Vector3.Cross(v1 - v0, v2 - v0).normalized;
                        hitFound = true;
                    }
                }
            }

            if (hitFound)
            {
                hitPoint = t.TransformPoint(bestLocalHit);
                hitNormal = t.TransformDirection(bestLocalNormal).normalized;
                return true;
            }

            return false;
        }

        private bool FindClosestVertexToRay(Ray worldRay, out Vector3 hitPoint, out Vector3 hitNormal)
        {
            hitPoint = Vector3.zero;
            hitNormal = Vector3.up;
            if (clonedMesh == null) return false;

            Transform t = targetFilter.transform;
            Vector3[] verts = clonedMesh.vertices;
            float bestDistSq = float.MaxValue;
            Vector3 bestVertWorld = Vector3.zero;
            bool found = false;

            for (int i = 0; i < verts.Length; i++)
            {
                Vector3 wPos = t.TransformPoint(verts[i]);
                float distSq = Vector3.Cross(worldRay.direction, wPos - worldRay.origin).sqrMagnitude;
                if (distSq < (brushRadius * brushRadius) && distSq < bestDistSq)
                {
                    bestDistSq = distSq;
                    bestVertWorld = wPos;
                    found = true;
                }
            }

            if (found)
            {
                hitPoint = bestVertWorld;
                hitNormal = t.up;
                return true;
            }
            return false;
        }

        private bool IntersectRayTriangle(Ray ray, Vector3 v0, Vector3 v1, Vector3 v2, out float distance, out Vector3 hitPoint)
        {
            distance = 0f;
            hitPoint = Vector3.zero;

            Vector3 edge1 = v1 - v0;
            Vector3 edge2 = v2 - v0;
            Vector3 h = Vector3.Cross(ray.direction, edge2);
            float a = Vector3.Dot(edge1, h);

            if (Mathf.Abs(a) < 0.00001f) return false;

            float f = 1.0f / a;
            Vector3 s = ray.origin - v0;
            float u = f * Vector3.Dot(s, h);
            if (u < 0.0f || u > 1.0f) return false;

            Vector3 q = Vector3.Cross(s, edge1);
            float v = f * Vector3.Dot(ray.direction, q);
            if (v < 0.0f || u + v > 1.0f) return false;

            float t = f * Vector3.Dot(edge2, q);
            if (t > 0.0001f)
            {
                distance = t;
                hitPoint = ray.origin + ray.direction * t;
                return true;
            }

            return false;
        }

        private void EnsureMeshInstance()
        {
            if (targetFilter == null || targetFilter.sharedMesh == null) return;

            // 원본 메시 보호 및 즉시 수정을 위한 인스턴스화
            if (clonedMesh == null || targetFilter.sharedMesh != clonedMesh)
            {
                clonedMesh = Instantiate(targetFilter.sharedMesh);
                clonedMesh.name = $"{targetFilter.sharedMesh.name}_Painted";
                Undo.RegisterCompleteObjectUndo(targetFilter, "Instantiate Painted Mesh");
                targetFilter.sharedMesh = clonedMesh;
            }
        }

        private void PaintVertices(Vector3 worldHitPos, Color targetColor)
        {
            EnsureMeshInstance();
            if (clonedMesh == null) return;

            Vector3[] vertices = clonedMesh.vertices;
            Color[] colors = clonedMesh.colors;

            if (colors == null || colors.Length != vertices.Length)
            {
                colors = new Color[vertices.Length];
                for (int i = 0; i < colors.Length; i++) colors[i] = new Color(0, 0, 0, 1);
            }

            Transform t = targetFilter.transform;
            bool modified = false;

            // 1. 브러시 범위 내 정점들의 가중치(blend) 계산
            float[] blendFactors = new float[vertices.Length];
            Vector3[] worldVerts = new Vector3[vertices.Length];

            for (int i = 0; i < vertices.Length; i++)
            {
                worldVerts[i] = t.TransformPoint(vertices[i]);
                float dist = Vector3.Distance(worldVerts[i], worldHitPos);

                if (dist <= brushRadius)
                {
                    float factor = 1.0f - (dist / brushRadius);
                    factor = Mathf.Pow(factor, 1.0f / Mathf.Max(brushFalloff, 0.01f));
                    blendFactors[i] = factor * brushOpacity;
                    modified = true;
                }
            }

            if (!modified) return;

            // 2. 같은 위치에 겹쳐 있는 하드 엣지/심 정점들(Coincident Vertices)에 동일한 최대 가중치 전파
            for (int i = 0; i < vertices.Length; i++)
            {
                if (blendFactors[i] > 0f)
                {
                    for (int j = 0; j < vertices.Length; j++)
                    {
                        if (i != j && (worldVerts[i] - worldVerts[j]).sqrMagnitude < 0.0001f) // 동일 위치 정점
                        {
                            blendFactors[j] = Mathf.Max(blendFactors[j], blendFactors[i]);
                        }
                    }
                }
            }

            // 3. 부드럽고 깨끗한 채널 블렌딩 적용 (원하는 채널은 올리고, 기존 다른 채널은 부드럽게 감소)
            for (int i = 0; i < vertices.Length; i++)
            {
                float blend = blendFactors[i];
                if (blend > 0f)
                {
                    if (selectedChannel == PaintChannel.Erase_BaseGrass)
                    {
                        // 기본 잔디로 지우기: RGB 채널 전체를 0(Black)으로 부드럽게 감쇄
                        colors[i].r = Mathf.Lerp(colors[i].r, 0f, blend);
                        colors[i].g = Mathf.Lerp(colors[i].g, 0f, blend);
                        colors[i].b = Mathf.Lerp(colors[i].b, 0f, blend);
                    }
                    else
                    {
                        // 선택된 채널(R, G, B)에 따라 독립 가중치 전이
                        Color c = colors[i];
                        c = Color.Lerp(c, targetColor, blend);
                        colors[i] = c;
                    }
                }
            }

            Undo.RecordObject(clonedMesh, "Paint Vertex Color");
            clonedMesh.colors = colors;
        }

        private void ClearAllColors()
        {
            EnsureMeshInstance();
            if (clonedMesh == null) return;

            Color[] colors = new Color[clonedMesh.vertexCount];
            for (int i = 0; i < colors.Length; i++) colors[i] = new Color(0, 0, 0, 1);

            Undo.RecordObject(clonedMesh, "Clear Vertex Colors");
            clonedMesh.colors = colors;
            Debug.Log("🌿 [VertexPainter] 전체 버텍스 컬러를 기본 잔디(Black)로 초기화했습니다.");
        }

        private void SaveMeshAsset()
        {
            if (clonedMesh == null)
            {
                Debug.LogWarning("⚠️ 저장할 수정된 메시가 없습니다.");
                return;
            }

            string dir = "Assets/_Project/Meshes/Painted";
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

            string path = $"{dir}/{clonedMesh.name}.asset";
            path = AssetDatabase.GenerateUniqueAssetPath(path);

            AssetDatabase.CreateAsset(Instantiate(clonedMesh), path);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"✅ [VertexPainter] 페인팅된 메시가 에셋으로 영구 저장되었습니다: {path}");
        }
    }
}
