using System;
using System.Collections.Generic;
using UnityEngine;
using SkippingStones.Auth;

namespace SkippingStones.Data
{
    public class GameDataManager : MonoBehaviour
    {
        public static GameDataManager Instance { get; private set; }

        private const string SAVE_KEY = "USER_SAVE_DATA_V1";
        private const int STAMINA_RECOVERY_SECONDS = 600; // 10분당 1 스태미나

        public UserPersistentData UserData { get; private set; } = new UserPersistentData();

        [Header("🛠️ 개발자 테스트 설정")]
        [Tooltip("에디터 플레이 시 모든 캐릭터/돌/맵을 항상 ALL 해금 상태로 시작합니다.")]
        [SerializeField] private bool devUnlockAllByDefault = true;

        [Header("마스터 카탈로그")]
        public List<CharacterInfoData> characterCatalog = new List<CharacterInfoData>();
        public List<MapInfoData> mapCatalog = new List<MapInfoData>();
        public List<StoneInfoData> stoneCatalog = new List<StoneInfoData>();

        public event Action<UserPersistentData> OnUserDataChanged;
        public event Action<int, int> OnStaminaChanged; // current, max

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void AutoInitialize()
        {
            if (Instance == null)
            {
                GameObject go = new GameObject("[GameDataManager]");
                go.AddComponent<GameDataManager>();
                DontDestroyOnLoad(go);
            }
        }

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
                InitializeCatalog();
                LoadUserData();
            }
            else if (Instance != this)
            {
                Destroy(gameObject);
            }
        }

        private void OnValidate()
        {
            if (Application.isPlaying && devUnlockAllByDefault)
            {
                DevUnlockAll();
            }
        }

        private void Update()
        {
            UpdateStaminaRegeneration();
        }

        private void InitializeCatalog()
        {
            if (characterCatalog.Count == 0)
            {
                characterCatalog.Add(new CharacterInfoData
                {
                    id = "boy_default",
                    name = "민우",
                    title = "호숫가 소년",
                    description = "기본기가 탄탄하며 안정적인 투구 각도를 구사합니다.",
                    prefabPath = "Assets/prefab/Character/Thrower_Minwoo.prefab",
                    powerBonus = 0f,
                    angleAssist = 2.0f,
                    perfectTimingAssist = 0.05f,
                    isUnlocked = true
                });

                characterCatalog.Add(new CharacterInfoData
                {
                    id = "girl_athlete",
                    name = "수아",
                    title = "투수 지망생",
                    description = "강력한 어깨로 초속과 파워를 극대화합니다.",
                    prefabPath = "Assets/prefab/Character/Thrower_001.prefab",
                    powerBonus = 0.12f,
                    angleAssist = 0f,
                    perfectTimingAssist = 0.08f,
                    unlockGoldCost = 3000,
                    isUnlocked = true // 테스트 편의를 위해 기본 해금
                });

                characterCatalog.Add(new CharacterInfoData
                {
                    id = "master_old",
                    name = "도사님",
                    title = "물수제비 은둔 고수",
                    description = "바람과 수면의 흐름을 읽어 모든 바운스에 보너스를 받습니다.",
                    prefabPath = "Assets/prefab/Character/Thrower_Test.prefab",
                    powerBonus = 0.20f,
                    angleAssist = 5.0f,
                    perfectTimingAssist = 0.15f,
                    unlockGoldCost = 10000,
                    isUnlocked = true // 테스트 편의를 위해 기본 해금
                });
            }

            if (mapCatalog.Count == 0)
            {
                mapCatalog.Add(new MapInfoData
                {
                    id = "emerald_lake",
                    name = "에메랄드 호숫가 (기본)",
                    description = "물결이 잔잔하여 물수제비를 던지기에 가장 이상적인 호수",
                    difficultyStars = 1,
                    courseLength = 1500f,
                    isUnlocked = true
                });

                mapCatalog.Add(new MapInfoData
                {
                    id = "sunset_river",
                    name = "노을빛 굽이치는 강",
                    description = "바람과 유속이 있어 정밀한 각도 조절이 요구되는 중급 코스",
                    difficultyStars = 3,
                    courseLength = 2500f,
                    isUnlocked = true
                });

                mapCatalog.Add(new MapInfoData
                {
                    id = "misty_valley",
                    name = "안개 낀 비경 계곡",
                    description = "수많은 기암괴석과 장애물이 도사리는 상급자 전용 계곡",
                    difficultyStars = 5,
                    courseLength = 4000f,
                    isUnlocked = false
                });
            }

            if (stoneCatalog.Count == 0)
            {
                stoneCatalog = StoneCatalogManager.LoadMasterCatalog();
            }
        }

        public void LoadUserData()
        {
            string json = PlayerPrefs.GetString(SAVE_KEY, string.Empty);
            if (!string.IsNullOrEmpty(json))
            {
                try
                {
                    UserData = JsonUtility.FromJson<UserPersistentData>(json);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[GameDataManager] 세이브 로드 실패, 기본값 사용: {ex.Message}");
                    UserData = CreateDefaultUserData();
                }
            }
            else
            {
                UserData = CreateDefaultUserData();
            }

            // 카탈로그 동기화: 유저의 unlocked 목록에 맞춰 카탈로그 isUnlocked 일치
            SyncCatalogWithUserData();

            // 스태미나 시간 정산
            UpdateStaminaRegeneration();
            OnUserDataChanged?.Invoke(UserData);
        }

        /// <summary>
        /// 기본 신규 유저 세이브 데이터 생성 (에디터 테스트 환경에서는 모든 해금 상태 지원)
        /// </summary>
        private UserPersistentData CreateDefaultUserData()
        {
            var data = new UserPersistentData();

#if UNITY_EDITOR
            // 🌟 에디터/개발 PC 테스트 모드: 모든 캐릭터/돌/맵 ALL 해금 + 테스트 골드 지급
            data.userId = "dev_tester_local";
            data.nickname = "개발자(QA)";
            data.gold = 999999;
            data.diamonds = 9999;
            data.unlockedCharacterIds = new List<string> { "boy_default", "girl_athlete", "master_old" };
            data.unlockedStoneIds = new List<string> { "default", "flat_slate", "emerald_pebble", "crimson_flint" };
            Debug.Log("🛠️ [GameDataManager] 에디터 테스트 모드 활성화: 모든 캐릭터/돌/맵 ALL 해금 프로필이 로드되었습니다.");
#else
            // 상용 배포: 순정 신규 유저 기본값
            data.userId = "local_guest";
            data.nickname = "조약돌 달인";
            data.gold = 1200;
            data.diamonds = 50;
            data.unlockedCharacterIds = new List<string> { "boy_default" };
            data.unlockedStoneIds = new List<string> { "default", "flat_slate" };
#endif
            return data;
        }

        /// <summary>
        /// 유저 세이브 데이터의 해금 목록을 마스터 카탈로그에 동기화
        /// </summary>
        public void SyncCatalogWithUserData()
        {
            if (UserData == null) return;

            if (characterCatalog != null && UserData.unlockedCharacterIds != null)
            {
                foreach (var c in characterCatalog)
                {
                    c.isUnlocked = UserData.unlockedCharacterIds.Contains(c.id);
                }
            }

            if (stoneCatalog != null && UserData.unlockedStoneIds != null)
            {
                foreach (var s in stoneCatalog)
                {
                    s.isUnlocked = UserData.unlockedStoneIds.Contains(s.id);
                }
            }
        }

        [ContextMenu("🛠️ [DEV] 모든 캐릭터/돌 해금 (All Unlock)")]
        public void DevUnlockAll()
        {
            if (UserData == null) UserData = new UserPersistentData();
            UserData.unlockedCharacterIds = new List<string> { "boy_default", "girl_athlete", "master_old" };
            UserData.unlockedStoneIds = new List<string> { "default", "flat_slate", "emerald_pebble", "crimson_flint" };
            UserData.gold = 999999;
            UserData.diamonds = 9999;
            SyncCatalogWithUserData();
            SaveUserData();
            Debug.Log("✅ [DEV] 모든 캐릭터/돌 해금 완료!");
        }

        [ContextMenu("🔄 [DEV] 신규 유저 순정 상태로 리셋 (Clean Reset)")]
        public void DevResetToCleanUser()
        {
            PlayerPrefs.DeleteKey(SAVE_KEY);
            UserData = new UserPersistentData
            {
                userId = "local_guest",
                nickname = "조약돌 달인",
                gold = 1200,
                diamonds = 50,
                unlockedCharacterIds = new List<string> { "boy_default" },
                unlockedStoneIds = new List<string> { "default", "flat_slate" }
            };
            SyncCatalogWithUserData();
            SaveUserData();
            Debug.Log("🔄 [DEV] 신규 유저 순정 상태로 초기화 완료!");
        }

        public void SaveUserData()
        {
            if (UserData == null) return;
            string json = JsonUtility.ToJson(UserData);
            PlayerPrefs.SetString(SAVE_KEY, json);
            PlayerPrefs.Save();
            OnUserDataChanged?.Invoke(UserData);
        }

        private void UpdateStaminaRegeneration()
        {
            if (UserData.stamina >= UserData.maxStamina)
            {
                UserData.lastStaminaTickTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                return;
            }

            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            if (UserData.lastStaminaTickTimestamp <= 0)
            {
                UserData.lastStaminaTickTimestamp = now;
                return;
            }

            long elapsed = now - UserData.lastStaminaTickTimestamp;
            if (elapsed >= STAMINA_RECOVERY_SECONDS)
            {
                int recoverAmount = (int)(elapsed / STAMINA_RECOVERY_SECONDS);
                UserData.stamina = Mathf.Min(UserData.maxStamina, UserData.stamina + recoverAmount);
                UserData.lastStaminaTickTimestamp += (recoverAmount * STAMINA_RECOVERY_SECONDS);
                OnStaminaChanged?.Invoke(UserData.stamina, UserData.maxStamina);
                SaveUserData();
            }
        }

        public bool ConsumeStamina(int amount = 1)
        {
            if (UserData.stamina >= amount)
            {
                UserData.stamina -= amount;
                if (UserData.stamina < UserData.maxStamina && UserData.lastStaminaTickTimestamp <= 0)
                {
                    UserData.lastStaminaTickTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                }
                OnStaminaChanged?.Invoke(UserData.stamina, UserData.maxStamina);
                SaveUserData();
                return true;
            }
            return false;
        }

        public void AddStamina(int amount)
        {
            UserData.stamina = Mathf.Min(UserData.maxStamina * 2, UserData.stamina + amount);
            OnStaminaChanged?.Invoke(UserData.stamina, UserData.maxStamina);
            SaveUserData();
        }

        public void AddCurrency(int goldAmount, int diaAmount)
        {
            UserData.gold += goldAmount;
            UserData.diamonds += diaAmount;
            SaveUserData();
        }

        public MatchSessionData CreateCurrentMatchSession()
        {
            return new MatchSessionData
            {
                characterId = UserData.selectedCharacterId,
                stoneId = UserData.selectedStoneId,
                mapId = UserData.selectedMapId,
                gameMode = UserData.selectedGameMode
            };
        }

        public void ProcessMatchResult(InGameResultData result)
        {
            if (result == null) return;

            UserData.totalPlayCount++;
            UserData.gold += result.earnedCoins;

            if (result.finalDistance > UserData.bestDistance)
            {
                UserData.bestDistance = result.finalDistance;
                result.isNewRecord = true;
            }

            SaveUserData();
        }
    }
}
