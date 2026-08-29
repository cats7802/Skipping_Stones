using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using SkippingStones.Visuals;
using SkippingStones.Terrain;

namespace SkippingStones.EditorTools
{
    /// <summary>
    /// [물수제비 프리팹 무결성 검증 & 원클릭 자동 수리 툴]
    /// - 캐릭터, 돌, 수면, 로비, 맵 앵커 등 필수 게임오브젝트의 컴포넌트 누락 여부를 전수 검사
    /// - PrefabUtility를 사용해 누락된 필수 컴포넌트/앵커를 프리팹 파일 원본에 정식으로 자동 부착 & 저장(Auto-Fix)
    /// - 메뉴 1: Tools -> Skipping Stones -> 🔍 프리팹 무결성 검증 (Health Check)
    /// - 메뉴 2: Tools -> Skipping Stones -> 🛠️ 누락 컴포넌트 원클릭 자동 수리 (Auto-Fix)
    /// - 메뉴 4: Tools -> Skipping Stones -> ⚓ 맵 프리팹 앵커 검증 및 자동 부착 (Anchor Auto-Fix)
    /// </summary>
    public static class PrefabHealthCheckTool
    {
        [MenuItem("Tools/Skipping Stones/🔍 프리팹 무결성 검증 (Health Check)", priority = 1)]
        public static void RunFullHealthCheck()
        {
            Debug.Log("=========================================================");
            Debug.Log("🚀 [물수제비 프리팹 무결성 검증(Health Check) 시작] 🚀");
            Debug.Log("=========================================================");

            int totalChecked = 0;
            int errorCount = 0;
            int warningCount = 0;

            // 1. 캐릭터 프리팹 검증
            CheckCharacterPrefabs(ref totalChecked, ref errorCount, ref warningCount);

            // 2. 돌(스톤) 프리팹 검증
            CheckStonePrefabs(ref totalChecked, ref errorCount, ref warningCount);

            // 3. 수면/환경 프리팹 검증
            CheckWaterPrefabs(ref totalChecked, ref errorCount, ref warningCount);

            // 4. 로비 프리팹 검증
            CheckLobbyPrefabs(ref totalChecked, ref errorCount, ref warningCount);

            // 5. 맵 지형 소켓 앵커 검증
            CheckTerrainAnchors(ref totalChecked, ref errorCount, ref warningCount);

            Debug.Log("=========================================================");
            if (errorCount == 0 && warningCount == 0)
            {
                Debug.Log($"🎉 [검증 완료] 총 {totalChecked}개 프리팹 검사 통과! 모든 프리팹이 완벽합니다. (오류 0, 경고 0)");
                EditorUtility.DisplayDialog("프리팹 검증 완료", $"총 {totalChecked}개의 프리팹을 검사했습니다.\n모든 필수 컴포넌트가 완벽하게 세팅되어 있습니다!", "확인");
            }
            else
            {
                Debug.LogWarning($"⚠️ [검증 완료] 총 {totalChecked}개 프리팹 중 오류 {errorCount}개, 경고 {warningCount}개 발견! (위 콘솔 로그 확인)");
                bool autoFixNow = EditorUtility.DisplayDialog("프리팹 검증 경고", 
                    $"검사 결과 오류 {errorCount}개, 경고 {warningCount}개가 발견되었습니다.\n\n지금 누락된 컴포넌트들을 프리팹 파일에 '자동 수리(Auto-Fix)'하여 저장하시겠습니까?", 
                    "🛠️ 지금 자동 수리", "직접 확인");
                if (autoFixNow)
                {
                    RunAutoFix();
                }
            }
            Debug.Log("=========================================================");
        }

        [MenuItem("Tools/Skipping Stones/🛠️ 누락 컴포넌트 원클릭 자동 수리 (Auto-Fix)", priority = 2)]
        public static void RunAutoFix()
        {
            Debug.Log("=========================================================");
            Debug.Log("🔧 [물수제비 프리팹 원클릭 자동 수리(Auto-Fix) 시작] 🔧");
            Debug.Log("=========================================================");

            int fixedCount = 0;

            // 1. 캐릭터 프리팹 자동 수리
            FixCharacterPrefabs(ref fixedCount);

            // 2. 돌 프리팹 자동 수리
            FixStonePrefabs(ref fixedCount);

            // 4. 로비 프리팹 자동 수리
            FixLobbyPrefabs(ref fixedCount);

            // 5. 맵 지형 앵커 자동 수리
            FixTerrainAnchors(ref fixedCount);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"🎉 [수리 완료] 총 {fixedCount}개 프리팹에 누락된 컴포넌트/앵커가 정식 부착 및 저장되었습니다!");
            EditorUtility.DisplayDialog("자동 수리 완료", $"총 {fixedCount}개 프리팹의 필수 컴포넌트/앵커가 정식으로 추가 및 저장되었습니다!\n\n이제 검증(Health Check)을 실행해보세요.", "확인");
            Debug.Log("=========================================================");

            // 수리 후 즉시 재검증
            RunFullHealthCheck();
        }

        [MenuItem("Tools/Skipping Stones/⚓ 맵 프리팹 앵커 검증 및 자동 부착 (Anchor Auto-Fix)", priority = 4)]
        public static void RunMapAnchorCheckAndFix()
        {
            Debug.Log("=========================================================");
            Debug.Log("⚓ [맵 프리팹 소켓 앵커 검증 및 자동 부착 시작] ⚓");
            Debug.Log("=========================================================");

            int totalChecked = 0;
            int errorCount = 0;
            int warningCount = 0;

            CheckTerrainAnchors(ref totalChecked, ref errorCount, ref warningCount);

            if (warningCount == 0 && errorCount == 0)
            {
                Debug.Log($"🎉 [검증 완료] 총 {totalChecked}개 맵 프리팹의 앵커가 완벽합니다!");
                EditorUtility.DisplayDialog("맵 앵커 검증 완료", $"총 {totalChecked}개의 맵 프리팹을 검사했습니다.\n모든 맵에 Start/End 앵커가 정상 배치되어 있습니다!", "확인");
            }
            else
            {
                bool fixNow = EditorUtility.DisplayDialog("맵 앵커 누락 발견",
                    $"총 {totalChecked}개 맵 중 {warningCount + errorCount}개에서 앵커가 누락되었습니다.\n\n지형 바운드(X=0, minZ/maxZ) 기준으로 Anchor_S, Anchor_E를 프리팹에 자동 생성 및 저장하시겠습니까?",
                    "⚓ 지금 앵커 자동 부착", "취소");
                if (fixNow)
                {
                    int fixedCount = 0;
                    FixTerrainAnchors(ref fixedCount);
                    AssetDatabase.SaveAssets();
                    AssetDatabase.Refresh();
                    Debug.Log($"🎉 [수리 완료] 총 {fixedCount}개 맵 프리팹에 앵커가 정식 부착 및 저장되었습니다!");
                    EditorUtility.DisplayDialog("앵커 부착 완료", $"총 {fixedCount}개 맵 프리팹에 앵커(Anchor_S, Anchor_E)를 자동 부착 및 저장했습니다!", "확인");
                }
            }
            Debug.Log("=========================================================");
        }

        [MenuItem("Tools/Skipping Stones/🧹 전체 프리팹 Missing Script 일괄 삭제", priority = 5)]
        public static void RemoveAllMissingScripts()
        {
            Debug.Log("=========================================================");
            Debug.Log("🧹 [전체 프리팹 Missing Script 일괄 삭제 시작] 🧹");
            Debug.Log("=========================================================");

            string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" });
            int cleanedPrefabs = 0;
            int totalComponentsRemoved = 0;

            try
            {
                for (int i = 0; i < guids.Length; i++)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                    EditorUtility.DisplayProgressBar("Missing Script 청소 중...", path, (float)i / guids.Length);

                    GameObject prefabRoot = PrefabUtility.LoadPrefabContents(path);
                    if (prefabRoot == null) continue;

                    int removedInThisPrefab = 0;
                    var allTransforms = prefabRoot.GetComponentsInChildren<Transform>(true);

                    foreach (var t in allTransforms)
                    {
                        int removed = GameObjectUtility.RemoveMonoBehavioursWithMissingScript(t.gameObject);
                        if (removed > 0)
                        {
                            removedInThisPrefab += removed;
                        }
                    }

                    if (removedInThisPrefab > 0)
                    {
                        PrefabUtility.SaveAsPrefabAsset(prefabRoot, path);
                        cleanedPrefabs++;
                        totalComponentsRemoved += removedInThisPrefab;
                        Debug.Log($"🧹 [{prefabRoot.name}] ({path}): 유실된 스크립트 {removedInThisPrefab}개 제거 완료");
                    }

                    PrefabUtility.UnloadPrefabContents(prefabRoot);
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }

            Debug.Log($"🎉 [청소 완료] 총 {cleanedPrefabs}개 프리팹에서 {totalComponentsRemoved}개의 유실된 컴포넌트(Missing Script)를 삭제 및 저장했습니다!");
            Debug.Log("=========================================================");

            EditorUtility.DisplayDialog("Missing Script 일괄 청소 완료", 
                $"총 {cleanedPrefabs}개의 프리팹에서\n{totalComponentsRemoved}개의 Missing Script를 성공적으로 제거하고 저장했습니다!", "확인");
        }

        [MenuItem("Tools/Skipping Stones/🌲 소나무 프리팹 LODGroup 제거 및 LOD0 단일화 최적화", priority = 4)]
        public static void OptimizePineLODPrefabs()
        {
            if (!EditorUtility.DisplayDialog("소나무 프리팹 LOD 최적화", 
                "소나무 프리팹(P_Pine*)의 가짜 LODGroup 컴포넌트를 제거하고,\nLOD0 단일 메쉬 구조로 최적화하시겠습니까?\n(LOD1, LOD2 등 중복 메쉬 자식 삭제)", "예, 최적화 진행", "취소"))
            {
                return;
            }

            Debug.Log("=========================================================");
            Debug.Log("🌲 [소나무 프리팹 LODGroup 제거 및 LOD0 단일화 시작] 🌲");
            Debug.Log("=========================================================");

            string pineFolder = "Assets/Design_sources/3D/Environments/SoStylized/Environment/Trees/Pine";
            string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { pineFolder });
            int optimizedCount = 0;

            try
            {
                for (int i = 0; i < guids.Length; i++)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                    EditorUtility.DisplayProgressBar("소나무 LOD 최적화 중...", path, (float)i / guids.Length);

                    GameObject prefabRoot = PrefabUtility.LoadPrefabContents(path);
                    if (prefabRoot == null) continue;

                    bool modified = false;

                    // 1. LODGroup 컴포넌트 제거
                    LODGroup[] lodGroups = prefabRoot.GetComponentsInChildren<LODGroup>(true);
                    foreach (var lodGroup in lodGroups)
                    {
                        if (lodGroup != null)
                        {
                            Component.DestroyImmediate(lodGroup);
                            modified = true;
                        }
                    }

                    // 2. LOD1, LOD2, LOD3 등 중복 자식 게임오브젝트 제거 대상 수집
                    var toDelete = new List<GameObject>();
                    var allTransforms = prefabRoot.GetComponentsInChildren<Transform>(true);
                    foreach (var t in allTransforms)
                    {
                        if (t == null || t == prefabRoot.transform) continue;

                        string tName = t.name.ToUpperInvariant();
                        if (tName.Contains("LOD1") || tName.Contains("LOD2") || tName.Contains("LOD3") || tName.Contains("_LOD_1") || tName.Contains("_LOD_2"))
                        {
                            if (!toDelete.Contains(t.gameObject))
                            {
                                toDelete.Add(t.gameObject);
                            }
                        }
                        else if (tName.Contains("LOD0") || tName.Contains("_LOD_0"))
                        {
                            t.gameObject.SetActive(true);
                        }
                    }

                    foreach (var go in toDelete)
                    {
                        if (go != null)
                        {
                            Debug.Log($"   🗑️ [{prefabRoot.name}] 불필요한 하위 LOD 오브젝트 제거: {go.name}");
                            GameObject.DestroyImmediate(go);
                            modified = true;
                        }
                    }

                    // 3. 유실된 스크립트가 남아있을 경우 함께 제거 (삭제 후 새로 자식 검색)
                    var remainingTransforms = prefabRoot.GetComponentsInChildren<Transform>(true);
                    foreach (var t in remainingTransforms)
                    {
                        if (t != null && t.gameObject != null)
                        {
                            if (GameObjectUtility.RemoveMonoBehavioursWithMissingScript(t.gameObject) > 0)
                            {
                                modified = true;
                            }
                        }
                    }

                    if (modified)
                    {
                        PrefabUtility.SaveAsPrefabAsset(prefabRoot, path);
                        optimizedCount++;
                        Debug.Log($"🌲 [{prefabRoot.name}] LOD0 단일화 및 최적화 완료!");
                    }

                    PrefabUtility.UnloadPrefabContents(prefabRoot);
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }

            Debug.Log($"🎉 [소나무 최적화 완료] 총 {optimizedCount}개 소나무 프리팹을 LOD0 단일 메쉬 구조로 최적화했습니다!");
            Debug.Log("=========================================================");

            EditorUtility.DisplayDialog("소나무 LOD 최적화 완료", 
                $"총 {optimizedCount}개의 소나무 프리팹에서\nLODGroup과 중복 LOD 자식들을 제거하고 LOD0 단일 메쉬로 최적화했습니다!", "확인");
        }

        #region 1. 캐릭터 프리팹 검증 & 수리
        private static void CheckCharacterPrefabs(ref int total, ref int errors, ref int warnings)
        {
            string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" });
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null) continue;

                var thrower = prefab.GetComponentInChildren<StoneThrowerCharacter>(true);
                if (thrower != null)
                {
                    total++;
                    List<string> issues = new List<string>();

                    var anim = prefab.GetComponentInChildren<Animator>(true);
                    if (anim == null) issues.Add("❌ [필수] Animator 컴포넌트 누락");
                    else if (anim.runtimeAnimatorController == null) issues.Add("⚠️ [권장] Animator에 Controller가 비어있음");

                    var col = prefab.GetComponentInChildren<Collider>(true);
                    if (col == null) issues.Add("⚠️ [권장] 터치 및 피직스 감지용 Collider(CapsuleCollider 등) 누락");

                    if (thrower.rightHandBone == null) issues.Add("⚠️ [세팅] StoneThrowerCharacter의 rightHandBone(Bip001 R Hand) 연결 누락");
                    if (thrower.dummy01Socket == null) issues.Add("⚠️ [세팅] StoneThrowerCharacter의 dummy01Socket(Dummy001) 연결 누락");

                    ReportResult("👤 [캐릭터]", prefab.name, path, issues, ref errors, ref warnings);
                }
            }
        }

        private static void FixCharacterPrefabs(ref int fixedCount)
        {
            string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" });
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                GameObject prefabRoot = PrefabUtility.LoadPrefabContents(path);
                if (prefabRoot == null) continue;

                bool modified = false;
                var thrower = prefabRoot.GetComponentInChildren<StoneThrowerCharacter>(true);
                if (thrower != null)
                {
                    // 1. 콜라이더 없으면 CapsuleCollider 정식 추가
                    var col = prefabRoot.GetComponentInChildren<Collider>(true);
                    if (col == null)
                    {
                        var cap = thrower.gameObject.AddComponent<CapsuleCollider>();
                        cap.center = new Vector3(0f, 0.9f, 0f);
                        cap.radius = 0.35f;
                        cap.height = 1.8f;
                        modified = true;
                        Debug.Log($"🛠️ [{prefabRoot.name}] CapsuleCollider 정식 추가 완료!");
                    }

                    // 2. 오른손 본 & 더미 소켓 연결 자동 복구
                    if (thrower.rightHandBone == null)
                    {
                        thrower.rightHandBone = FindDeepChild(prefabRoot.transform, "Bip001 R Hand");
                        if (thrower.rightHandBone != null) modified = true;
                    }
                    if (thrower.dummy01Socket == null)
                    {
                        thrower.dummy01Socket = FindDeepChild(prefabRoot.transform, "Dummy001") ?? FindDeepChild(prefabRoot.transform, "Dummy01");
                        if (thrower.dummy01Socket != null) modified = true;
                    }

                    if (modified)
                    {
                        PrefabUtility.SaveAsPrefabAsset(prefabRoot, path);
                        fixedCount++;
                    }
                }
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }
        }
        #endregion

        #region 2. 돌(스톤) 프리팹 검증 & 수리
        private static void CheckStonePrefabs(ref int total, ref int errors, ref int warnings)
        {
            string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/prefab/Stone", "Assets/prefab" });
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null) continue;

                var stone = prefab.GetComponentInChildren<SkippingStone>(true);
                bool isStonePrefab = stone != null || path.Replace("\\", "/").Contains("prefab/Stone/");

                if (isStonePrefab)
                {
                    total++;
                    List<string> issues = new List<string>();

                    if (stone == null)
                    {
                        issues.Add("❌ [필수] SkippingStone 컴포넌트 누락!");
                    }

                    var rb = prefab.GetComponentInChildren<Rigidbody>(true);
                    if (rb == null)
                    {
                        issues.Add("❌ [필수] Rigidbody 컴포넌트 누락! (인게임 물리 필수)");
                    }
                    else
                    {
                        if (rb.useGravity) issues.Add("⚠️ [설정] Rigidbody의 UseGravity가 켜져있습니다 (물수제비는 false 권장)");
                    }

                    var col = prefab.GetComponentInChildren<Collider>(true);
                    if (col == null) issues.Add("❌ [필수] Collider(SphereCollider 등) 누락");

                    var trail = prefab.GetComponentInChildren<TrailRenderer>(true);
                    if (trail == null) issues.Add("⚠️ [권장] 궤적 연출용 TrailRenderer 누락");

                    var ring = prefab.GetComponentInChildren<RhythmRingIndicator>(true);
                    if (ring == null) issues.Add("⚠️ [권장] 리듬 링 판정 표시용 RhythmRingIndicator 누락");

                    ReportResult("🪨 [돌/스톤]", prefab.name, path, issues, ref errors, ref warnings);
                }
            }
        }

        private static void FixStonePrefabs(ref int fixedCount)
        {
            string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/prefab/Stone", "Assets/prefab" });
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                GameObject prefabRoot = PrefabUtility.LoadPrefabContents(path);
                if (prefabRoot == null) continue;

                var stone = prefabRoot.GetComponentInChildren<SkippingStone>(true);
                bool isStonePrefab = stone != null || path.Replace("\\", "/").Contains("prefab/Stone/");

                if (isStonePrefab)
                {
                    bool modified = false;

                    // 0. SkippingStone 컴포넌트 없으면 추가
                    if (stone == null)
                    {
                        stone = prefabRoot.AddComponent<SkippingStone>();
                        modified = true;
                        Debug.Log($"🛠️ [{prefabRoot.name}] SkippingStone 컴포넌트 정식 추가 완료!");
                    }

                    // 1. Rigidbody 없으면 추가 및 세팅
                    var rb = prefabRoot.GetComponentInChildren<Rigidbody>(true);
                    if (rb == null)
                    {
                        rb = stone.gameObject.AddComponent<Rigidbody>();
                        rb.useGravity = false;
                        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                        rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
                        rb.interpolation = RigidbodyInterpolation.Interpolate;
                        modified = true;
                        Debug.Log($"🛠️ [{prefabRoot.name}] Rigidbody 정식 추가 완료!");
                    }
                    else if (rb.useGravity)
                    {
                        rb.useGravity = false;
                        modified = true;
                    }

                    // 2. SphereCollider 없으면 추가
                    var col = prefabRoot.GetComponentInChildren<Collider>(true);
                    if (col == null)
                    {
                        var sc = stone.gameObject.AddComponent<SphereCollider>();
                        sc.radius = 0.12f;
                        modified = true;
                        Debug.Log($"🛠️ [{prefabRoot.name}] SphereCollider 정식 추가 완료!");
                    }

                    // 3. TrailRenderer 없으면 추가
                    var trail = prefabRoot.GetComponentInChildren<TrailRenderer>(true);
                    if (trail == null)
                    {
                        trail = stone.gameObject.AddComponent<TrailRenderer>();
                        trail.time = 0.38f;
                        trail.startWidth = 0.045f;
                        trail.endWidth = 0.002f;
                        trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                        stone.trail = trail;
                        modified = true;
                        Debug.Log($"🛠️ [{prefabRoot.name}] TrailRenderer 정식 추가 완료!");
                    }

                    // 4. RhythmRingIndicator 없으면 추가
                    var ring = prefabRoot.GetComponentInChildren<RhythmRingIndicator>(true);
                    if (ring == null)
                    {
                        ring = stone.gameObject.AddComponent<RhythmRingIndicator>();
                        ring.stone = stone;
                        modified = true;
                        Debug.Log($"🛠️ [{prefabRoot.name}] RhythmRingIndicator 정식 추가 완료!");
                    }

                    if (modified)
                    {
                        PrefabUtility.SaveAsPrefabAsset(prefabRoot, path);
                        fixedCount++;
                    }
                }
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }
        }
        #endregion

        #region 3. 수면/환경 프리팹 검증
        private static void CheckWaterPrefabs(ref int total, ref int errors, ref int warnings)
        {
            string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" });
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null) continue;

                var water = prefab.GetComponentInChildren<WaterSurface>(true);
                if (water != null)
                {
                    total++;
                    List<string> issues = new List<string>();

                    var boxCol = prefab.GetComponentInChildren<BoxCollider>(true);
                    if (boxCol == null) issues.Add("❌ [필수] 수면 감지용 BoxCollider 누락");
                    else if (!boxCol.isTrigger) issues.Add("⚠️ [설정] 수면 BoxCollider의 IsTrigger가 꺼져있습니다 (true 권장)");

                    ReportResult("🌊 [수면]", prefab.name, path, issues, ref errors, ref warnings);
                }
            }
        }
        #endregion

        #region 4. 로비 프리팹 검증 & 수리
        private static void CheckLobbyPrefabs(ref int total, ref int errors, ref int warnings)
        {
            string[] guids = AssetDatabase.FindAssets("Lobby t:Prefab", new[] { "Assets/prefab" });
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null) continue;

                total++;
                List<string> issues = new List<string>();

                // 필수 컨트롤러 컴포넌트 검증
                var stoneCtrl = prefab.GetComponentInChildren<LobbyStoneShowcaseController>(true);
                if (stoneCtrl == null) issues.Add("❌ [필수] 로비 돌 선택 컨트롤러 'LobbyStoneShowcaseController' 컴포넌트 누락");

                var charCtrl = prefab.GetComponentInChildren<LobbyCharacterShowcaseController>(true);
                if (charCtrl == null) issues.Add("❌ [필수] 로비 캐릭터 쇼케이스 컨트롤러 'LobbyCharacterShowcaseController' 컴포넌트 누락");

                // 필수 더미 및 하위 오브젝트 검색
                var staging = FindDeepChild(prefab.transform, "STAGING_POSITION");
                if (staging == null) issues.Add("⚠️ [로비] 캐릭터 등장 기준점 'Staging_Position' 더미 누락");

                var dial = FindDeepChild(prefab.transform, "StoneSelector");
                if (dial == null) issues.Add("❌ [로비] 돌 조작 다이얼 'StoneSelector' 오브젝트 누락");
                else if (dial.GetComponent<Collider>() == null) issues.Add("⚠️ [로비] 다이얼 터치 감지용 Collider 누락");

                var stand = FindDeepChild(prefab.transform, "Stone_Stand");
                if (stand == null) issues.Add("❌ [로비] 3개 접시 스탠드 'Stone_Stand' 오브젝트 누락");

                ReportResult("🏛️ [로비]", prefab.name, path, issues, ref errors, ref warnings);
            }
        }

        private static void FixLobbyPrefabs(ref int fixedCount)
        {
            string[] guids = AssetDatabase.FindAssets("Lobby t:Prefab", new[] { "Assets/prefab" });
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                GameObject prefabRoot = PrefabUtility.LoadPrefabContents(path);
                if (prefabRoot == null) continue;

                bool modified = false;

                // 1. LobbyStoneShowcaseController 없으면 추가
                var stoneCtrl = prefabRoot.GetComponentInChildren<LobbyStoneShowcaseController>(true);
                if (stoneCtrl == null)
                {
                    prefabRoot.AddComponent<LobbyStoneShowcaseController>();
                    modified = true;
                    Debug.Log($"🛠️ [{prefabRoot.name}] LobbyStoneShowcaseController 정식 추가 완료!");
                }

                // 2. LobbyCharacterShowcaseController 없으면 추가
                var charCtrl = prefabRoot.GetComponentInChildren<LobbyCharacterShowcaseController>(true);
                if (charCtrl == null)
                {
                    prefabRoot.AddComponent<LobbyCharacterShowcaseController>();
                    modified = true;
                    Debug.Log($"🛠️ [{prefabRoot.name}] LobbyCharacterShowcaseController 정식 추가 완료!");
                }

                // 3. 다이얼 콜라이더 없으면 추가
                var dial = FindDeepChild(prefabRoot.transform, "StoneSelector");
                if (dial != null && dial.GetComponent<Collider>() == null)
                {
                    var mc = dial.gameObject.AddComponent<MeshCollider>();
                    mc.convex = true;
                    modified = true;
                    Debug.Log($"🛠️ [{prefabRoot.name}] StoneSelector 다이얼 MeshCollider 정식 추가 완료!");
                }

                if (modified)
                {
                    PrefabUtility.SaveAsPrefabAsset(prefabRoot, path);
                    fixedCount++;
                }
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }
        }
        #endregion

        #region 5. 맵/지형 앵커 검증 & 수리
        private static void CheckTerrainAnchors(ref int total, ref int errors, ref int warnings)
        {
            string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/prefab", "Assets/_Project/Prefabs" });
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null) continue;

                // 캐릭터, 스톤, UI, 로비, 발판 등 명확한 비-맵 프리팹 제외
                if (prefab.GetComponentInChildren<StoneThrowerCharacter>(true) != null ||
                    prefab.GetComponentInChildren<SkippingStone>(true) != null ||
                    prefab.name.Contains("Lobby") || prefab.name.Contains("UGUI") || prefab.name.Contains("Pier"))
                {
                    continue;
                }

                // 맵/지형 관련 프리팹 여부 판별 (Brook, BG_, River_, Terrain, WaterSurface, RiverSpawner 등)
                bool isMapPrefab = prefab.name.StartsWith("Brook", StringComparison.OrdinalIgnoreCase) ||
                                   prefab.name.StartsWith("BG_", StringComparison.OrdinalIgnoreCase) ||
                                   prefab.name.StartsWith("River_", StringComparison.OrdinalIgnoreCase) ||
                                   prefab.GetComponentInChildren<WaterSurface>(true) != null ||
                                   prefab.GetComponentInChildren<RiverSpawner>(true) != null;

                if (isMapPrefab)
                {
                    total++;
                    List<string> issues = new List<string>();

                    Transform anchorS = MapAnchorHelper.FindStartAnchor(prefab);
                    Transform anchorE = MapAnchorHelper.FindEndAnchor(prefab);

                    if (anchorS == null) issues.Add("⚠️ [앵커] 시작 앵커(Anchor_S / Ancher_S) 누락 (소켓 도킹 스트리밍 시작점)");
                    if (anchorE == null) issues.Add("⚠️ [앵커] 끝 앵커(Anchor_E / Ancher_E) 누락 (다음 청크 도킹 연결점)");

                    ReportResult("🗺️ [맵 앵커]", prefab.name, path, issues, ref errors, ref warnings);
                }
            }
        }

        private static void FixTerrainAnchors(ref int fixedCount)
        {
            string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/prefab", "Assets/_Project/Prefabs" });
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null) continue;

                // 캐릭터, 스톤, UI, 로비, 발판 제외
                if (prefab.GetComponentInChildren<StoneThrowerCharacter>(true) != null ||
                    prefab.GetComponentInChildren<SkippingStone>(true) != null ||
                    prefab.name.Contains("Lobby") || prefab.name.Contains("UGUI") || prefab.name.Contains("Pier"))
                {
                    continue;
                }

                bool isMapPrefab = prefab.name.StartsWith("Brook", StringComparison.OrdinalIgnoreCase) ||
                                   prefab.name.StartsWith("BG_", StringComparison.OrdinalIgnoreCase) ||
                                   prefab.name.StartsWith("River_", StringComparison.OrdinalIgnoreCase) ||
                                   prefab.GetComponentInChildren<WaterSurface>(true) != null ||
                                   prefab.GetComponentInChildren<RiverSpawner>(true) != null;

                if (!isMapPrefab) continue;

                Transform existingS = MapAnchorHelper.FindStartAnchor(prefab);
                Transform existingE = MapAnchorHelper.FindEndAnchor(prefab);

                if (existingS != null && existingE != null) continue; // 이미 둘 다 존재하면 스킵

                GameObject prefabRoot = PrefabUtility.LoadPrefabContents(path);
                if (prefabRoot == null) continue;

                bool modified = MapAnchorHelper.GetOrCreateAnchors(prefabRoot, out Transform s, out Transform e);
                if (modified)
                {
                    PrefabUtility.SaveAsPrefabAsset(prefabRoot, path);
                    fixedCount++;
                    Debug.Log($"🛠️ [{prefabRoot.name}] 맵 앵커(Anchor_S, Anchor_E) 자동 생성 및 영구 저장 완료!");
                }

                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }
        }
        #endregion

        #region 유틸리티
        private static Transform FindDeepChild(Transform parent, string keyword)
        {
            foreach (Transform t in parent.GetComponentsInChildren<Transform>(true))
            {
                if (t.name.ToUpperInvariant().Contains(keyword.ToUpperInvariant())) return t;
            }
            return null;
        }

        private static void ReportResult(string category, string prefabName, string path, List<string> issues, ref int errors, ref int warnings)
        {
            if (issues.Count == 0)
            {
                Debug.Log($"✅ {category} '{prefabName}' : 모든 컴포넌트 정상");
            }
            else
            {
                string msg = $"⚠️ {category} '{prefabName}' ({path}):\n" + string.Join("\n", issues);
                Debug.LogWarning(msg);

                foreach (var iss in issues)
                {
                    if (iss.StartsWith("❌")) errors++;
                    else warnings++;
                }
            }
        }
        #endregion
    }
}
