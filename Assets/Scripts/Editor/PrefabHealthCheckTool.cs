using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using SkippingStones.Visuals;

namespace SkippingStones.EditorTools
{
    /// <summary>
    /// [물수제비 프리팹 무결성 검증 툴]
    /// - 캐릭터, 돌, 수면, 로비 등 필수 게임오브젝트의 컴포넌트 누락 여부를 원클릭 전수 검사
    /// - 메뉴: Tools -> Skipping Stones -> 🔍 프리팹 무결성 검증 (Health Check)
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
                EditorUtility.DisplayDialog("프리팹 검증 경고", $"검사 결과 오류 {errorCount}개, 경고 {warningCount}개가 발견되었습니다.\n콘솔(Console) 창의 상세 안내를 확인해주세요!", "확인");
            }
            Debug.Log("=========================================================");
        }

        #region 1. 캐릭터 프리팹 검증 (StoneThrowerCharacter)
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

                    // 1-1. Animator 체크
                    var anim = prefab.GetComponentInChildren<Animator>(true);
                    if (anim == null) issues.Add("❌ [필수] Animator 컴포넌트 누락");
                    else if (anim.runtimeAnimatorController == null) issues.Add("⚠️ [권장] Animator에 Controller가 비어있음");

                    // 1-2. 콜라이더 체크 (터치 감지용)
                    var col = prefab.GetComponentInChildren<Collider>(true);
                    if (col == null) issues.Add("⚠️ [권장] 터치 및 피직스 감지용 Collider(CapsuleCollider 등) 누락");

                    // 1-3. 오른손 본 및 Dummy001 소켓 체크
                    if (thrower.rightHandBone == null) issues.Add("⚠️ [세팅] StoneThrowerCharacter의 rightHandBone(Bip001 R Hand) 연결 누락");
                    if (thrower.dummy01Socket == null) issues.Add("⚠️ [세팅] StoneThrowerCharacter의 dummy01Socket(Dummy001) 연결 누락");

                    ReportResult("👤 [캐릭터]", prefab.name, path, issues, ref errors, ref warnings);
                }
            }
        }
        #endregion

        #region 2. 돌(스톤) 프리팹 검증 (SkippingStone)
        private static void CheckStonePrefabs(ref int total, ref int errors, ref int warnings)
        {
            string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" });
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null) continue;

                var stone = prefab.GetComponentInChildren<SkippingStone>(true);
                if (stone != null)
                {
                    total++;
                    List<string> issues = new List<string>();

                    // 2-1. Rigidbody 체크
                    var rb = prefab.GetComponentInChildren<Rigidbody>(true);
                    if (rb == null)
                    {
                        issues.Add("❌ [필수] Rigidbody 컴포넌트 누락! (인게임 물리 필수)");
                    }
                    else
                    {
                        if (rb.useGravity) issues.Add("⚠️ [설정] Rigidbody의 UseGravity가 켜져있습니다 (물수제비는 false 권장)");
                    }

                    // 2-2. 콜라이더 체크
                    var col = prefab.GetComponentInChildren<Collider>(true);
                    if (col == null) issues.Add("❌ [필수] Collider(SphereCollider 등) 누락");

                    // 2-3. TrailRenderer 체크
                    var trail = prefab.GetComponentInChildren<TrailRenderer>(true);
                    if (trail == null) issues.Add("⚠️ [권장] 궤적 연출용 TrailRenderer 누락");

                    // 2-4. RhythmRingIndicator 체크
                    var ring = prefab.GetComponentInChildren<RhythmRingIndicator>(true);
                    if (ring == null) issues.Add("⚠️ [권장] 리듬 링 판정 표시용 RhythmRingIndicator 누락");

                    ReportResult("🪨 [돌/스톤]", prefab.name, path, issues, ref errors, ref warnings);
                }
            }
        }
        #endregion

        #region 3. 수면/환경 프리팹 검증 (WaterSurface)
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

        #region 4. 로비 프리팹 검증 (Lobby.prefab)
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
