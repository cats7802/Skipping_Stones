using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace SkippingStones.Data
{
    /// <summary>
    /// 조약돌 도감(카탈로그) 관리 컴포넌트
    /// - 빈 오브젝트나 매니저 오브젝트에 붙여 인스펙터에서 시각적으로 돌 도감을 관리
    /// - Resources/Data/StoneCatalogData.json과 양방향 자동 동기화
    /// </summary>
    [AddComponentMenu("Skipping Stones/Stone Catalog Manager")]
    [ExecuteInEditMode]
    public class StoneCatalogManager : MonoBehaviour
    {
        public const string RESOURCE_PATH = "Data/StoneCatalogData";
        public const string RELATIVE_JSON_PATH = "Assets/Resources/Data/StoneCatalogData.json";

        [Header("🪨 조약돌 도감 목록")]
        public List<StoneInfoData> catalog = new List<StoneInfoData>();

        [Serializable]
        public class CatalogWrapper
        {
            public List<StoneInfoData> stones = new List<StoneInfoData>();
        }

        private void OnEnable()
        {
            LoadFromDisk();
        }

        private void Reset()
        {
            LoadFromDisk();
        }

        /// <summary>
        /// JSON 파일 또는 Resources로부터 도감 데이터 로드
        /// </summary>
        public void LoadFromDisk()
        {
#if UNITY_EDITOR
            string fullPath = Path.Combine(Application.dataPath, "Resources/Data/StoneCatalogData.json");
            if (File.Exists(fullPath))
            {
                try
                {
                    string json = File.ReadAllText(fullPath);
                    var wrapper = JsonUtility.FromJson<CatalogWrapper>(json);
                    if (wrapper != null && wrapper.stones != null && wrapper.stones.Count > 0)
                    {
                        catalog = new List<StoneInfoData>(wrapper.stones);
                        return;
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[StoneCatalogManager] JSON 파싱 실패: {ex.Message}");
                }
            }
#endif
            // Resources 런타임 폴백 로드
            TextAsset textAsset = Resources.Load<TextAsset>(RESOURCE_PATH);
            if (textAsset != null && !string.IsNullOrEmpty(textAsset.text))
            {
                try
                {
                    var wrapper = JsonUtility.FromJson<CatalogWrapper>(textAsset.text);
                    if (wrapper != null && wrapper.stones != null && wrapper.stones.Count > 0)
                    {
                        catalog = new List<StoneInfoData>(wrapper.stones);
                        return;
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[StoneCatalogManager] Resources 로드 실패: {ex.Message}");
                }
            }

            // 파일이 없는 경우 기본 4종 시드 생성
            if (catalog == null || catalog.Count == 0)
            {
                SeedDefaultCatalog();
            }
        }

        /// <summary>
        /// 현재 도감 목록을 JSON 파일로 저장
        /// </summary>
        public void SaveToDisk()
        {
            var wrapper = new CatalogWrapper { stones = catalog ?? new List<StoneInfoData>() };
            string json = JsonUtility.ToJson(wrapper, true);

#if UNITY_EDITOR
            string dirPath = Path.Combine(Application.dataPath, "Resources/Data");
            if (!Directory.Exists(dirPath))
            {
                Directory.CreateDirectory(dirPath);
            }

            string fullPath = Path.Combine(dirPath, "StoneCatalogData.json");
            File.WriteAllText(fullPath, json);
            UnityEditor.AssetDatabase.Refresh();
            Debug.Log($"💾 [StoneCatalogManager] 조약돌 도감 데이터가 성공적으로 저장되었습니다. (총 {wrapper.stones.Count}종)");
#endif
        }

        /// <summary>
        /// 기본 4종 조약돌 시드 데이터 세팅
        /// </summary>
        public void SeedDefaultCatalog()
        {
            catalog = new List<StoneInfoData>
            {
                new StoneInfoData
                {
                    id = "default",
                    name = "기본 조약돌",
                    description = "표면이 매끄러워 물수제비에 최적화된 기본 회색 돌",
                    prefabPath = "Assets/prefab/Stone/Stone.prefab",
                    unlockGoldCost = 0,
                    isUnlocked = true
                },
                new StoneInfoData
                {
                    id = "flat_slate",
                    name = "납작 청석판",
                    description = "아주 얇고 넓어 완벽한 수면 반사력을 자랑하는 파란 돌",
                    prefabPath = "Assets/prefab/Stone/Stone_Blue.prefab",
                    unlockGoldCost = 0,
                    isUnlocked = true
                },
                new StoneInfoData
                {
                    id = "emerald_pebble",
                    name = "에메랄드 자갈",
                    description = "호숫가 깊은 곳에서 발견되는 초록빛 행운의 돌",
                    prefabPath = "Assets/prefab/Stone/Stone_Green.prefab",
                    unlockGoldCost = 2000,
                    isUnlocked = false
                },
                new StoneInfoData
                {
                    id = "crimson_flint",
                    name = "붉은 부싯돌",
                    description = "화산 지대에서 채집된 뜨거운 붉은 돌. 강력한 바운스를 자랑한다.",
                    prefabPath = "Assets/prefab/Stone/Stone_red.prefab",
                    unlockGoldCost = 5000,
                    isUnlocked = false
                }
            };
        }

        /// <summary>
        /// GameDataManager 또는 외부 시스템에서 최신 돌 도감 목록을 가져오는 정적 헬퍼
        /// </summary>
        public static List<StoneInfoData> LoadMasterCatalog()
        {
            TextAsset textAsset = Resources.Load<TextAsset>(RESOURCE_PATH);
            if (textAsset != null && !string.IsNullOrEmpty(textAsset.text))
            {
                try
                {
                    var wrapper = JsonUtility.FromJson<CatalogWrapper>(textAsset.text);
                    if (wrapper != null && wrapper.stones != null && wrapper.stones.Count > 0)
                    {
                        return wrapper.stones;
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[StoneCatalogManager] 마스터 도감 로드 실패: {ex.Message}");
                }
            }

            // 폴백: 기본 4종 반환
            var fallback = new StoneCatalogManager();
            fallback.SeedDefaultCatalog();
            return fallback.catalog;
        }
    }
}
