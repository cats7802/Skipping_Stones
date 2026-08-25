using System;
using System.Collections.Generic;
using UnityEngine;

namespace SkippingStones.Data
{
    [Serializable]
    public class CharacterInfoData
    {
        public string id;
        public string name;
        public string title;
        public string description;
        public float powerBonus = 0f;      // 파워 보너스 (예: +0.05)
        public float angleAssist = 0f;      // 각도 보정 범위 (예: +3.0도)
        public float perfectTimingAssist = 0f; // 퍼펙트 타이밍 판정 폭 증가
        public int unlockGoldCost = 1000;
        public bool isUnlocked = false;
    }

    [Serializable]
    public class MapInfoData
    {
        public string id;
        public string name;
        public string description;
        public int difficultyStars = 1;
        public float courseLength = 1500f;
        public bool isUnlocked = true;
    }

    [Serializable]
    public class StoneInfoData
    {
        public string id;
        public string name;
        public string description;
        /// <summary>프리팹 경로 (Assets/prefab/Stone/xxx.prefab)</summary>
        public string prefabPath;
        public int unlockGoldCost = 0;
        public bool isUnlocked = false;
    }

    /// <summary>
    /// 메타 UI에서 인게임(GameController)으로 주입하는 일회성 매치 시작 파라미터 DTO
    /// </summary>
    [Serializable]
    public class MatchSessionData
    {
        public string characterId = "boy_default";
        public string stoneId = "default";
        public string mapId = "emerald_lake";
        public GameController.GameMode gameMode = GameController.GameMode.LongDistance;
        public GameObject characterPrefabOverride = null;
        public GameObject stonePrefabOverride = null;
        public GameObject mapPrefabOverride = null;
    }

    /// <summary>
    /// 인게임 종료 시 정산 및 UI 표출을 위해 프론트로 전달하는 결과 DTO
    /// </summary>
    [Serializable]
    public class InGameResultData
    {
        public float finalDistance = 0f;
        public int skipCount = 0;
        public int perfectTimingCount = 0;
        public int fishSnipeCount = 0;
        public int friendOvertakeCount = 0;
        public int boostPadCount = 0;
        public int earnedCoins = 0;
        public int totalScore = 0;
        public bool isNewRecord = false;
        public List<string> snipedFishSpecies = new List<string>();
    }

    /// <summary>
    /// 로컬 및 서버에 영구 보존되는 유저 통합 세이브 데이터
    /// </summary>
    [Serializable]
    public class UserPersistentData
    {
        public string userId = "local_guest";
        public string nickname = "조약돌 달인";
        public string authProvider = "Guest";
        public bool hasKakaoAccount = false;
        public string kakaoNickname = "";

        // 재화
        public int gold = 1200;
        public int diamonds = 50;
        public int stamina = 10;
        public int maxStamina = 10;
        public long lastStaminaTickTimestamp = 0;

        // 선택 상태
        public string selectedCharacterId = "boy_default";
        public string selectedStoneId = "default";
        public string selectedMapId = "emerald_lake";
        public GameController.GameMode selectedGameMode = GameController.GameMode.LongDistance;

        // 최고 기록
        public float bestDistance = 0f;
        public int bestTargetScore = 0;
        public int totalPlayCount = 0;

        // 해금 목록
        public List<string> unlockedCharacterIds = new List<string> { "boy_default" };
        public List<string> unlockedStoneIds = new List<string> { "default", "flat_slate" };
    }
}
