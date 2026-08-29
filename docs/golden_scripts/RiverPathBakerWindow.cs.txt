#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using SkippingStones.Terrain;

namespace SkippingStones.EditorTools
{
    /// <summary>
    /// [강줄기 곡선 & 강폭 자동 베이킹 에디터 윈도우]
    /// - 상단 메뉴: Tools -> Skipping Stones -> 🌊 강줄기 곡선 & 폭 자동 베이킹 툴
    /// - LakeEnvironmentManager 프리팹(TestEnvMgr 등)을 등록받아 그 안에 세팅된 모든 맵(Start, Loop, Var, Ending)을 원클릭 일괄 베이킹
    /// </summary>
    public class RiverPathBakerWindow : EditorWindow
    {
        [MenuItem("Tools/Skipping Stones/🌊 강줄기 곡선 & 폭 자동 베이킹 툴", priority = 10)]
        public static void OpenWindow()
        {
            var win = GetWindow<RiverPathBakerWindow>("River Path Baker");
            win.minSize = new Vector2(440, 420);
            win.Show();
        }

        private GameObject targetEnvMgrPrefab;
        private float sampleInterval = 5f;

        private void OnEnable()
        {
            // 기본 환경 매니저 프리팹 자동 탐색
            if (targetEnvMgrPrefab == null)
            {
                targetEnvMgrPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/prefab/BG_Env/TestEnvMgr.prefab")
                                  ?? AssetDatabase.LoadAssetAtPath<GameObject>("Assets/prefab/BG_Env/New_TestEnvMgr.prefab");
            }
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("🌊 3D 메쉬 강줄기 곡선 & 강폭 자동 베이킹 툴", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "환경 매니저 프리팹(TestEnvMgr 등)에 등록된 모든 맵(시작/루프/변주/엔딩)을 스캔하여,\n" +
                "물길 중심선(Centerline)과 유효 강폭(Width)을 계산하고 RiverPathChunkData로 원본 프리팹에 베이킹합니다.\n" +
                "베이킹된 데이터는 갓모드 완주 곡선 비행 및 RiverSpawner 안전 플로팅에 사용됩니다.",
                MessageType.Info);

            EditorGUILayout.Space(6);
            sampleInterval = EditorGUILayout.Slider("샘플링 간격 (Sample Step, m)", sampleInterval, 2f, 20f);

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("🎯 1. 환경 매니저(Env Manager) 프리팹 기준 일괄 베이킹", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            targetEnvMgrPrefab = (GameObject)EditorGUILayout.ObjectField("대상 환경 프리팹 (EnvMgr)", targetEnvMgrPrefab, typeof(GameObject), false);

            GUI.backgroundColor = new Color(0.35f, 0.85f, 0.45f);
            if (GUILayout.Button("🚀 등록된 모든 맵 프리팹 원클릭 일괄 베이킹 (Bake All)", GUILayout.Height(36)))
            {
                if (targetEnvMgrPrefab != null)
                {
                    BakeAllFromEnvManager(targetEnvMgrPrefab);
                }
                else
                {
                    EditorUtility.DisplayDialog("알림", "환경 매니저 프리팹(TestEnvMgr 등)을 먼저 등록해 주세요.", "확인");
                }
            }
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("📍 2. 현재 씬의 선택된 단일 오브젝트 베이킹", EditorStyles.boldLabel);
            if (GUILayout.Button("선택한 오브젝트 베이킹 (Bake Selected)", GUILayout.Height(28)))
            {
                if (Selection.activeGameObject != null)
                {
                    Undo.RegisterFullObjectHierarchyUndo(Selection.activeGameObject, "Bake River Path");
                    RiverPathBaker.BakeRiverPathForChunk(Selection.activeGameObject, sampleInterval);
                    SceneView.RepaintAll();
                }
                else
                {
                    EditorUtility.DisplayDialog("알림", "베이킹할 맵 청크 오브젝트를 먼저 선택해 주세요.", "확인");
                }
            }

            EditorGUILayout.Space(10);
            if (GUILayout.Button("🔄 씬 내 활성화된 청크 글로벌 경로 재구성 (Rebuild Global Path)", GUILayout.Height(28)))
            {
                GlobalRiverPath.Instance.RebuildPath();
                SceneView.RepaintAll();
                Debug.Log($"[RiverPathBakerWindow] 🔄 글로벌 강줄기 경로 재구성 완료! (총 길이: {GlobalRiverPath.Instance.totalRiverLength:F1}m)");
            }
        }

        public static void BakeAllFromEnvManager(GameObject envMgrPrefab, float sampleStep = 5f)
        {
            if (envMgrPrefab == null) return;

            LakeEnvironmentManager mgr = envMgrPrefab.GetComponent<LakeEnvironmentManager>();
            if (mgr == null)
            {
                EditorUtility.DisplayDialog("오류", "선택된 프리팹에 LakeEnvironmentManager 컴포넌트가 없습니다.", "확인");
                return;
            }

            // 고유 맵 프리팹 목록 수집
            HashSet<GameObject> targetPrefabs = new HashSet<GameObject>();
            if (mgr.startMapPrefab != null) targetPrefabs.Add(mgr.startMapPrefab);
            if (mgr.endingMapPrefab != null) targetPrefabs.Add(mgr.endingMapPrefab);

            if (mgr.loopSlots != null)
            {
                foreach (var slot in mgr.loopSlots)
                {
                    if (slot == null) continue;
                    if (slot.baseMapPrefab != null) targetPrefabs.Add(slot.baseMapPrefab);
                    if (slot.variationPrefabs != null)
                    {
                        foreach (var v in slot.variationPrefabs)
                        {
                            if (v != null) targetPrefabs.Add(v);
                        }
                    }
                }
            }

            if (targetPrefabs.Count == 0)
            {
                EditorUtility.DisplayDialog("알림", "환경 매니저에 등록된 맵 프리팹이 없습니다.", "확인");
                return;
            }

            int bakedCount = 0;
            int total = targetPrefabs.Count;
            int current = 0;

            try
            {
                foreach (var prefab in targetPrefabs)
                {
                    current++;
                    string path = AssetDatabase.GetAssetPath(prefab);
                    if (string.IsNullOrEmpty(path)) continue;

                    EditorUtility.DisplayProgressBar("강줄기 곡선 베이킹 중...", $"{prefab.name} ({current}/{total})", (float)current / total);

                    GameObject prefabRoot = PrefabUtility.LoadPrefabContents(path);
                    if (prefabRoot != null)
                    {
                        bool ok = RiverPathBaker.BakeRiverPathForChunk(prefabRoot, sampleStep);
                        if (ok)
                        {
                            PrefabUtility.SaveAsPrefabAsset(prefabRoot, path);
                            bakedCount++;
                        }
                        PrefabUtility.UnloadPrefabContents(prefabRoot);
                    }
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }

            EditorUtility.DisplayDialog("베이킹 완료", $"'{envMgrPrefab.name}'에 등록된 총 {bakedCount}개의 맵 프리팹에 강줄기 곡선 데이터가 성공적으로 베이킹되었습니다!", "확인");
        }
    }
}
#endif
