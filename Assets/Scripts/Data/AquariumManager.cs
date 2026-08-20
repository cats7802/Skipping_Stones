using UnityEngine;
using System;
using System.Collections.Generic;

[System.Serializable]
public class FishSpecies
{
    public string id;
    public string name;
    public string icon;
    public string description;
    public int spawnStartDistance;
    public int spawnEndDistance;
    public int caughtCount = 0;
    public int rewardCoins = 100;
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

        fishSpeciesList.Add(new FishSpecies
        {
            id = "minnow",
            name = "피라미 (Minnow)",
            icon = "🐟",
            description = "강변 여울에서 흔히 볼 수 있는 작고 날렵한 민물고기",
            spawnStartDistance = 30,
            spawnEndDistance = 250,
            rewardCoins = 100
        });

        fishSpeciesList.Add(new FishSpecies
        {
            id = "carp",
            name = "비단 잉어 (Golden Carp)",
            icon = "🐠",
            description = "물살을 가르며 높이 뛰어오르는 황금빛 비늘의 고급 어종",
            spawnStartDistance = 250,
            spawnEndDistance = 600,
            rewardCoins = 300
        });

        fishSpeciesList.Add(new FishSpecies
        {
            id = "flying_fish",
            name = "날치 (Flying Fish)",
            icon = "🦅",
            description = "수면 위를 장거리 활강하는 전설의 초고속 어종",
            spawnStartDistance = 600,
            spawnEndDistance = 1500,
            rewardCoins = 800
        });
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
