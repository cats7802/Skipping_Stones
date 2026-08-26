using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using SkippingStones.Visuals;

namespace SkippingStones.EditorTools
{
    /// <summary>
    /// [물수제비 프리팹 무결성 검증 & 원클릭 자동 수리 툴]
    /// - 캐릭터, 돌, 수면, 로비 등 필수 게임오브젝트의 컴포넌트 누락 여부를 전수 검사
    /// - PrefabUtility를 사용해 누락된 필수 컴포넌트를 프리팹 파일 원본에 정식으로 자동 부착 & 저장(Auto-Fix)
    /// - 메뉴 1: Tools -> Skipping Stones -> 🔍 프리팹 무결성 검증 (Health Check)
    /// - 메뉴 2: Tools -> Skipping Stones -> 🛠️ 누락 컴포넌트 원클릭 자동 수리 (Auto-Fix)
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

            // 3. 로비 프리팹 자동 수리
            FixLobbyPrefabs(ref fixedCount);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"🎉 [수리 완료] 총 {fixedCount}개 프리팹에 누락된 컴포넌트가 정식 부착 및 저장되었습니다!");
            EditorUtility.DisplayDialog("자동 수리 완료", $"총 {fixedCount}개 프리팹의 필수 컴포넌트가 정식으로 추가 및 저장되었습니다!\n\n이제 검증(Health Check)을 실행해보세요.", "확인");
            Debug.Log("=========================================================");

            // 수리 후 즉시 재검증
            RunFullHealthCheck();
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
