using System;
using System.Text.RegularExpressions;
using UnityEngine;

namespace SkippingStones.Terrain
{
    /// <summary>
    /// [맵 지형 소켓 앵커(Socket Anchor) 스마트 탐색 및 동적 보완 헬퍼]
    /// - 3ds Max / Blender 등 모델링 툴에서 Export된 다양한 앵커 명칭(Ancher_S, Ancher_S001~004, Anchor_S, AnchorS, S_Anchor 등)을 스마트하게 자동 탐색
    /// - 앵커가 누락된 맵/프리팹의 경우 지형 메쉬(MeshCollider/MeshFilter/Terrain) 바운드(X=0, minZ / maxZ)를 기준으로 동적 가상 앵커 생성 및 반환
    /// </summary>
    public static class MapAnchorHelper
    {
        // 시작 앵커(S Anchor) 정규식 패턴 (대소문자 무관)
        // 매칭 예: Ancher_S, Ancher_S001, Anchor_S, AnchorS, S_Anchor, Start_Anchor, Anchor_Start 등
        private static readonly Regex StartAnchorRegex = new Regex(
            @"^(anch[e|a]r[_\-\s]*s(\d*)|s[_\-\s]*anch[e|a]r|start[_\-\s]*anch[e|a]r|anch[e|a]r[_\-\s]*start)$",
            RegexOptions.IgnoreCase | RegexOptions.Compiled
        );

        // 끝 앵커(E Anchor) 정규식 패턴 (대소문자 무관)
        // 매칭 예: Ancher_E, Ancher_E001, Anchor_E, AnchorE, E_Anchor, End_Anchor, Anchor_End 등
        private static readonly Regex EndAnchorRegex = new Regex(
            @"^(anch[e|a]r[_\-\s]*e(\d*)|e[_\-\s]*anch[e|a]r|end[_\-\s]*anch[e|a]r|anch[e|a]r[_\-\s]*end)$",
            RegexOptions.IgnoreCase | RegexOptions.Compiled
        );

        /// <summary>
        /// 대상 GameObject 계층 구조 내에서 시작 앵커(Anchor_S)를 스마트하게 탐색
        /// </summary>
        public static Transform FindStartAnchor(GameObject root)
        {
            if (root == null) return null;

            // 1. 직접 자식 우선 탐색
            for (int i = 0; i < root.transform.childCount; i++)
            {
                Transform child = root.transform.GetChild(i);
                if (IsStartAnchorName(child.name)) return child;
            }

            // 2. 전체 하위 계층 전수 탐색
            Transform[] allChildren = root.GetComponentsInChildren<Transform>(true);
            foreach (var t in allChildren)
            {
                if (t == root.transform) continue;
                if (IsStartAnchorName(t.name)) return t;
            }

            return null;
        }

        /// <summary>
        /// 대상 GameObject 계층 구조 내에서 끝 앵커(Anchor_E)를 스마트하게 탐색
        /// </summary>
        public static Transform FindEndAnchor(GameObject root)
        {
            if (root == null) return null;

            // 1. 직접 자식 우선 탐색
            for (int i = 0; i < root.transform.childCount; i++)
            {
                Transform child = root.transform.GetChild(i);
                if (IsEndAnchorName(child.name)) return child;
            }

            // 2. 전체 하위 계층 전수 탐색
            Transform[] allChildren = root.GetComponentsInChildren<Transform>(true);
            foreach (var t in allChildren)
            {
                if (t == root.transform) continue;
                if (IsEndAnchorName(t.name)) return t;
            }

            return null;
        }

        /// <summary>
        /// 이름이 시작 앵커 규칙에 부합하는지 판별
        /// </summary>
        public static bool IsStartAnchorName(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            string clean = name.Trim();
            if (StartAnchorRegex.IsMatch(clean)) return true;

            // 부분 매칭 완화 규칙
            string lower = clean.ToLowerInvariant();
            if ((lower.Contains("anch") || lower.Contains("socket")) && (lower.Contains("_s") || lower.EndsWith("s") || lower.Contains("start")))
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// 이름이 끝 앵커 규칙에 부합하는지 판별
        /// </summary>
        public static bool IsEndAnchorName(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            string clean = name.Trim();
            if (EndAnchorRegex.IsMatch(clean)) return true;

            // 부분 매칭 완화 규칙
            string lower = clean.ToLowerInvariant();
            if ((lower.Contains("anch") || lower.Contains("socket")) && (lower.Contains("_e") || lower.EndsWith("e") || lower.Contains("end")))
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// 대상 청크 인스턴스에서 S/E 앵커를 탐색하고, 누락된 경우 지형 바운드(X=0, minZ/maxZ) 기준으로 동적 가상 앵커를 생성하여 반환
        /// </summary>
        public static bool GetOrCreateAnchors(GameObject chunkInstance, out Transform anchorS, out Transform anchorE)
        {
            anchorS = null;
            anchorE = null;

            if (chunkInstance == null) return false;

            anchorS = FindStartAnchor(chunkInstance);
            anchorE = FindEndAnchor(chunkInstance);

            // 둘 다 정상 탐색된 경우
            if (anchorS != null && anchorE != null)
            {
                return true;
            }

            // 앵커가 1개 이상 누락된 경우: 지형 바운드 실측 후 동적 보완
            Bounds terrainBounds;
            bool hasBounds = TryGetTerrainLocalBounds(chunkInstance, out terrainBounds);

            if (!hasBounds)
            {
                // 지형 바운드 실측 실패 시 기본 500m 바운드로 폴백
                terrainBounds = new Bounds(Vector3.zero, new Vector3(100f, 10f, 500f));
            }

            if (anchorS == null)
            {
                Debug.LogWarning($"[MapAnchorHelper] ⚠️ 청크 '{chunkInstance.name}'에서 Start Anchor를 찾지 못해 지형 기준 (X=0, minZ={terrainBounds.min.z:F1})에 동적 앵커(Anchor_S)를 생성합니다.");
                GameObject sObj = new GameObject("Anchor_S");
                sObj.transform.SetParent(chunkInstance.transform, false);
                sObj.transform.localPosition = new Vector3(0f, terrainBounds.min.y, terrainBounds.min.z);
                sObj.transform.localRotation = Quaternion.identity;
                sObj.transform.localScale = Vector3.one;
                anchorS = sObj.transform;
            }

            if (anchorE == null)
            {
                Debug.LogWarning($"[MapAnchorHelper] ⚠️ 청크 '{chunkInstance.name}'에서 End Anchor를 찾지 못해 지형 기준 (X=0, maxZ={terrainBounds.max.z:F1})에 동적 앵커(Anchor_E)를 생성합니다.");
                GameObject eObj = new GameObject("Anchor_E");
                eObj.transform.SetParent(chunkInstance.transform, false);
                eObj.transform.localPosition = new Vector3(0f, terrainBounds.min.y, terrainBounds.max.z);
                eObj.transform.localRotation = Quaternion.identity;
                eObj.transform.localScale = Vector3.one;
                anchorE = eObj.transform;
            }

            return true;
        }

        /// <summary>
        /// 지형 메쉬(MeshFilter / MeshCollider / Terrain)의 로컬 바운드 측정 (X=0 정렬 기준)
        /// </summary>
        public static bool TryGetTerrainLocalBounds(GameObject root, out Bounds localBounds)
        {
            localBounds = new Bounds(Vector3.zero, Vector3.zero);
            if (root == null) return false;

            bool found = false;

            // 1. MeshFilter 우선 실측
            MeshFilter[] filters = root.GetComponentsInChildren<MeshFilter>(true);
            foreach (var mf in filters)
            {
                if (mf == null || mf.sharedMesh == null) continue;
                // 수면/발판/캐릭터/이펙트 제외
                string goName = mf.gameObject.name.ToLowerInvariant();
                if (goName.Contains("water") || goName.Contains("platform") || goName.Contains("pier") || goName.Contains("player")) continue;

                Bounds b = mf.sharedMesh.bounds;
                // 청크 루트 기준 로컬 변환 바운드 계산
                Vector3 center = root.transform.InverseTransformPoint(mf.transform.TransformPoint(b.center));
                Vector3 size = Vector3.Scale(b.size, mf.transform.lossyScale);

                if (!found)
                {
                    localBounds = new Bounds(center, size);
                    found = true;
                }
                else
                {
                    localBounds.Encapsulate(new Bounds(center, size));
                }
            }

            if (found && localBounds.size.z > 1f) return true;

            // 2. MeshCollider 실측
            MeshCollider[] cols = root.GetComponentsInChildren<MeshCollider>(true);
            foreach (var mc in cols)
            {
                if (mc == null || mc.sharedMesh == null) continue;
                string goName = mc.gameObject.name.ToLowerInvariant();
                if (goName.Contains("water") || goName.Contains("platform") || goName.Contains("pier")) continue;

                Bounds b = mc.sharedMesh.bounds;
                Vector3 center = root.transform.InverseTransformPoint(mc.transform.TransformPoint(b.center));
                Vector3 size = Vector3.Scale(b.size, mc.transform.lossyScale);

                if (!found)
                {
                    localBounds = new Bounds(center, size);
                    found = true;
                }
                else
                {
                    localBounds.Encapsulate(new Bounds(center, size));
                }
            }

            if (found && localBounds.size.z > 1f) return true;

            // 3. Unity Terrain 실측
            UnityEngine.Terrain terrain = root.GetComponentInChildren<UnityEngine.Terrain>(true);
            if (terrain != null && terrain.terrainData != null)
            {
                Vector3 tSize = terrain.terrainData.size;
                localBounds = new Bounds(new Vector3(0f, tSize.y * 0.5f, tSize.z * 0.5f), tSize);
                return true;
            }

            // 4. Renderer 실측
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            foreach (var r in renderers)
            {
                if (r == null || r is ParticleSystemRenderer || r is TrailRenderer) continue;
                string goName = r.gameObject.name.ToLowerInvariant();
                if (goName.Contains("water") || goName.Contains("platform") || goName.Contains("pier")) continue;

                Bounds b = r.bounds;
                Vector3 center = root.transform.InverseTransformPoint(b.center);
                Vector3 size = b.size;

                if (!found)
                {
                    localBounds = new Bounds(center, size);
                    found = true;
                }
                else
                {
                    localBounds.Encapsulate(new Bounds(center, size));
                }
            }

            return found && localBounds.size.z > 1f;
        }
    }
}
