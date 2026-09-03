using UnityEngine;
using System;
using System.Collections.Generic;

[System.Serializable]
public class FishSpecies
{
    public string id;
    public string name;
    public string scientificName;
    public string icon;
    public string description;
    public int spawnStartDistance;
    public int spawnEndDistance;
    public int caughtCount = 0;
    public int rewardCoins = 100;
    public Sprite bookSprite;
}

public class AquariumManager : MonoBehaviour
{
    public static AquariumManager Instance { get; private set; }

    [Header("물고기 도감 목록")]
    public List<FishSpecies> fishSpeciesList = new List<FishSpecies>();
    public int totalCoins = 500;

    public event Action<FishSpecies> OnFishCaught;
    public event Action OnAquariumCompleted;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            InitSpecies();
        }
    }

    private void InitSpecies()
    {
        if (fishSpeciesList.Count > 0) return;

        // 10종 어종 프리셋 데이터베이스에서 동적 초기화
        foreach (var preset in FishPresetDatabase.Presets)
        {
            fishSpeciesList.Add(new FishSpecies
            {
                id = preset.id,
                name = $"{preset.nameKor} ({preset.nameEng})",
                scientificName = preset.scientificName,
                icon = "🐟",
                description = $"[{preset.lengthRange}] {preset.behaviorDesc}",
                spawnStartDistance = (preset.index - 1) * 60,
                spawnEndDistance = preset.index * 150 + 200,
                rewardCoins = preset.rewardCoins,
                bookSprite = preset.bookSprite
            });
        }
    }

    public void RegisterCaughtFish(string speciesId)
    {
        FishSpecies target = fishSpeciesList.Find(f => f.id == speciesId);
        if (target != null)
        {
            bool isFirstCatch = (target.caughtCount == 0);
            target.caughtCount++;
            totalCoins += target.rewardCoins;

            OnFishCaught?.Invoke(target);

            if (CheckAllCompleted())
            {
                OnAquariumCompleted?.Invoke();
                if (StoneInventory.Instance != null)
                {
                    StoneInventory.Instance.UnlockGoldenStone();
                }
            }
        }
    }

    public bool CheckAllCompleted()
    {
        if (fishSpeciesList.Count == 0) return false;
        foreach (var f in fishSpeciesList)
        {
            if (f.caughtCount == 0) return false;
        }
        return true;
    }

    public void AddCoins(int amount)
    {
        totalCoins += amount;
    }

    public float GetCompletionPercentage()
    {
        if (fishSpeciesList.Count == 0) return 0f;
        int caughtTypes = 0;
        foreach (var f in fishSpeciesList)
        {
            if (f.caughtCount > 0) caughtTypes++;
        }
        return (float)caughtTypes / fishSpeciesList.Count * 100f;
    }
}
