using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public class FishSpeciesData
{
    public int index;              // 1 ~ 10
    public string id;              // minnow, pale_chub, etc.
    public string nameKor;         // 버들치
    public string nameEng;         // Chinese Minnow
    public string scientificName;  // 학명/영문 병기
    public string lengthRange;     // 8~12cm
    public string behaviorDesc;    // 사냥 특징 및 행동 설명
    public float scaleFactor;      // 3D 모델 크기 배율 (0.7f ~ 1.8f)
    public float minJumpHeight;    // 최소 도약 높이
    public float maxJumpHeight;    // 최대 도약 높이
    public float jumpDuration;     // 공중 체공 시간
    public int rewardCoins;        // 도감 등록 및 저격 보상 코인
    public Sprite bookSprite;      // 2D 도감 스프라이트
}

public static class FishPresetDatabase
{
    public static readonly List<FishSpeciesData> Presets = new List<FishSpeciesData>
    {
        new FishSpeciesData
        {
            index = 1,
            id = "chinese_minnow",
            nameKor = "버들치",
            nameEng = "Chinese Minnow",
            scientificName = "Rhynchocypris oxycephalus",
            lengthRange = "8 ~ 12 cm",
            behaviorDesc = "최상류 맑은 계곡에 살며 낙하하는 개미, 날파리를 낚아채려 뽈록뽈록 솟구침",
            scaleFactor = 0.75f,
            minJumpHeight = 1.2f,
            maxJumpHeight = 1.6f,
            jumpDuration = 0.9f,
            rewardCoins = 100
        },
        new FishSpeciesData
        {
            index = 2,
            id = "pale_chub",
            nameKor = "피라미",
            nameEng = "Pale Chub",
            scientificName = "Zacco platypus",
            lengthRange = "10 ~ 15 cm",
            behaviorDesc = "여울에서 해질녘 하루살이·날도래를 향해 쉼 없이 튀어 오르는 '라이즈'의 대명사",
            scaleFactor = 0.85f,
            minJumpHeight = 1.4f,
            maxJumpHeight = 1.9f,
            jumpDuration = 1.0f,
            rewardCoins = 120
        },
        new FishSpeciesData
        {
            index = 3,
            id = "dark_chub",
            nameKor = "갈겨니",
            nameEng = "Dark Chub",
            scientificName = "Zacco temminckii",
            lengthRange = "12 ~ 16 cm",
            behaviorDesc = "피라미보다 상류에 살며 육식성이 강해 수면 위 곤충을 민첩하게 점프 사냥",
            scaleFactor = 0.95f,
            minJumpHeight = 1.6f,
            maxJumpHeight = 2.1f,
            jumpDuration = 1.05f,
            rewardCoins = 150
        },
        new FishSpeciesData
        {
            index = 4,
            id = "ayu",
            nameKor = "은어",
            nameEng = "Ayu",
            scientificName = "Plecoglossus altivelis",
            lengthRange = "15 ~ 25 cm",
            behaviorDesc = "주식은 돌 이끼지만 활성도가 높을 때 수면 날파리를 힘차게 튀어오르며 포식",
            scaleFactor = 1.05f,
            minJumpHeight = 1.9f,
            maxJumpHeight = 2.4f,
            jumpDuration = 1.1f,
            rewardCoins = 200
        },
        new FishSpeciesData
        {
            index = 5,
            id = "masu_trout",
            nameKor = "산천어",
            nameEng = "Masu Trout",
            scientificName = "Oncorhynchus masou",
            lengthRange = "20 ~ 30 cm",
            behaviorDesc = "계곡의 맹수. 시각이 뛰어나 수면을 스치는 나방, 잠자리를 향해 전광석화 점프",
            scaleFactor = 1.2f,
            minJumpHeight = 2.2f,
            maxJumpHeight = 2.8f,
            jumpDuration = 1.15f,
            rewardCoins = 300
        },
        new FishSpeciesData
        {
            index = 6,
            id = "korean_chub",
            nameKor = "끄리",
            nameEng = "Korean Chub",
            scientificName = "Opsariichthys uncirostris",
            lengthRange = "25 ~ 35 cm",
            behaviorDesc = "갈매기 모양 입으로 유명한 토종 포식어. 잠자리나 날벌레를 향해 격렬하게 도약",
            scaleFactor = 1.3f,
            minJumpHeight = 2.4f,
            maxJumpHeight = 3.0f,
            jumpDuration = 1.2f,
            rewardCoins = 400
        },
        new FishSpeciesData
        {
            index = 7,
            id = "mandarin_fish",
            nameKor = "쏘가리",
            nameEng = "Mandarin Fish",
            scientificName = "Siniperca scherzeri",
            lengthRange = "30 ~ 40 cm",
            behaviorDesc = "바닥에 주로 머물지만 해질녘 여울목 수면의 큰 매미나 풍뎅이류를 덮치며 솟구침",
            scaleFactor = 1.4f,
            minJumpHeight = 2.6f,
            maxJumpHeight = 3.2f,
            jumpDuration = 1.25f,
            rewardCoins = 550
        },
        new FishSpeciesData
        {
            index = 8,
            id = "largemouth_bass",
            nameKor = "큰입배스",
            nameEng = "Largemouth Bass",
            scientificName = "Micropterus salmoides",
            lengthRange = "30 ~ 50 cm",
            behaviorDesc = "물가 갈대밭에 앉은 잠자리, 개구리, 매미를 온몸을 날려 통째로 집어삼킴",
            scaleFactor = 1.55f,
            minJumpHeight = 2.8f,
            maxJumpHeight = 3.5f,
            jumpDuration = 1.3f,
            rewardCoins = 700
        },
        new FishSpeciesData
        {
            index = 9,
            id = "rainbow_trout",
            nameKor = "무지개송어",
            nameEng = "Rainbow Trout",
            scientificName = "Oncorhynchus mykiss",
            lengthRange = "40 ~ 60 cm",
            behaviorDesc = "양어장 탈출 및 방류로 정착. 온몸이 공중으로 솟구쳐 공중 곤충을 낚아채는 도약력 발군",
            scaleFactor = 1.7f,
            minJumpHeight = 3.2f,
            maxJumpHeight = 4.2f,
            jumpDuration = 1.4f,
            rewardCoins = 850
        },
        new FishSpeciesData
        {
            index = 10,
            id = "predaceous_carp",
            nameKor = "강준치",
            nameEng = "Predaceous Carp",
            scientificName = "Chanodichthys erythropterus",
            lengthRange = "50 ~ 80 cm+",
            behaviorDesc = "주동이가 위로 꺾인 대형 포식어. 여름밤 불빛에 모여드는 하루살이 떼를 물 밖으로 거대하게 브리칭",
            scaleFactor = 1.9f,
            minJumpHeight = 3.5f,
            maxJumpHeight = 4.6f,
            jumpDuration = 1.5f,
            rewardCoins = 1000
        }
    };

    public static FishSpeciesData GetPreset(int index)
    {
        int clampedIdx = Mathf.Clamp(index - 1, 0, Presets.Count - 1);
        return Presets[clampedIdx];
    }

    public static FishSpeciesData GetPresetById(string id)
    {
        return Presets.Find(p => p.id == id) ?? Presets[0];
    }
}
