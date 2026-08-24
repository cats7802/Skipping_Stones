using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public class StoneItem
{
    public string id;
    public string name;
    public string description;
    public Color color;
    public Color trailColor;
    public float bounceMultiplier = 1.0f;
    public float forwardPowerMultiplier = 1.0f;
    public bool isUnlocked = false;
}

public class StoneInventory : MonoBehaviour
{
    public static StoneInventory Instance { get; private set; }

    public List<StoneItem> stones = new List<StoneItem>();
    public int currentStoneIndex = 0;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            InitDefaultStones();
        }
    }

    private void InitDefaultStones()
    {
        if (stones.Count > 0) return;

        stones.Add(new StoneItem
        {
            id = "default",
            name = "기본 강변 조약돌",
            description = "표면이 매끄러워 물수제비에 최적화된 기본 돌",
            color = new Color(0.25f, 0.30f, 0.35f),
            trailColor = new Color(0.25f, 0.85f, 1.0f, 0.40f),
            bounceMultiplier = 1.0f,
            forwardPowerMultiplier = 1.0f,
            isUnlocked = true
        });

        stones.Add(new StoneItem
        {
            id = "flat_slate",
            name = "납작 청석판",
            description = "아주 얇고 넓어 완벽한 수면 반사력을 자랑하는 돌",
            color = new Color(0.2f, 0.45f, 0.55f),
            trailColor = new Color(0.1f, 1f, 0.8f, 0.85f),
            bounceMultiplier = 1.25f,
            forwardPowerMultiplier = 1.1f,
            isUnlocked = true
        });

        stones.Add(new StoneItem
        {
            id = "golden_hidden",
            name = "✨ 전설의 황금 조약돌 (히든)",
            description = "물고기 도감 100% 수집 달성자에게 주어지는 한정판 전설의 돌",
            color = new Color(1f, 0.85f, 0.1f),
            trailColor = new Color(1f, 0.75f, 0.05f, 0.95f),
            bounceMultiplier = 1.45f,
            forwardPowerMultiplier = 1.35f,
            isUnlocked = false
        });
    }

    public StoneItem GetCurrentStone()
    {
        if (stones.Count == 0) InitDefaultStones();
        return stones[currentStoneIndex];
    }

    public void SelectStone(int index)
    {
        if (index >= 0 && index < stones.Count && stones[index].isUnlocked)
        {
            currentStoneIndex = index;
        }
    }

    public void UnlockGoldenStone()
    {
        foreach (var s in stones)
        {
            if (s.id == "golden_hidden")
            {
                s.isUnlocked = true;
                break;
            }
        }
    }
}
