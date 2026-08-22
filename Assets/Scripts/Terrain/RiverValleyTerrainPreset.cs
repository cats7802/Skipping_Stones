using System;
using UnityEngine;

[CreateAssetMenu(fileName = "NewTerrainPreset", menuName = "SkippingStones/Terrain Preset", order = 1)]
public class RiverValleyTerrainPreset : ScriptableObject
{
    [Header("프리셋 정보")]
    public string presetName = "아기자기한 개울가";
    [TextArea(2, 4)]
    public string description = "따뜻하고 아늑한 호수/개울가 레퍼런스 스타일";

    [Header("3. 강 및 수면 설정 (River & Water)")]
    public float riverWidthMin = 32f;
    public float riverWidthMax = 48f;
    public float waterHeight = 16f;
    public float waterMeshWidth = 120f;
    public float riverBedDepth = 10f;
    public float meanderPrimaryAmp = 35f;
    public float meanderSecondaryAmp = 14f;
    public float meanderTertiaryAmp = 6f;
    public bool applyTertiaryToRiver = false;

    [Header("4. 산맥 및 계곡 평야 설정 (Mountains & Valley)")]
    public float plainStartHeight = 16.4f;
    public float plainStartHeightVariation = 0.6f;
    public float valleyMaxHeightMin = 22f;
    public float valleyMaxHeightMax = 32f;
    public float leftValleyWidthMin = 55f;
    public float leftValleyWidthMax = 85f;
    public float rightValleyWidthMin = 55f;
    public float rightValleyWidthMax = 85f;
    public float mountainFootTertiaryAmp = 15f;
    public float mountainFootNoiseAmp = 12f;
    public float mountainMaxHeightMin = 85f;
    public float mountainMaxHeightMax = 130f;
    public float mountainTransitionWidthMin = 140f;
    public float mountainTransitionWidthMax = 220f;

    public void ApplyToGenerator(RiverValleyTerrainGenerator gen)
    {
        if (gen == null) return;
        gen.riverWidthMin = riverWidthMin;
        gen.riverWidthMax = riverWidthMax;
        gen.waterHeight = waterHeight;
        gen.waterMeshWidth = waterMeshWidth;
        gen.riverBedDepth = riverBedDepth;
        gen.meanderPrimaryAmp = meanderPrimaryAmp;
        gen.meanderSecondaryAmp = meanderSecondaryAmp;
        gen.meanderTertiaryAmp = meanderTertiaryAmp;
        gen.applyTertiaryToRiver = applyTertiaryToRiver;

        gen.plainStartHeight = plainStartHeight;
        gen.plainStartHeightVariation = plainStartHeightVariation;
        gen.valleyMaxHeightMin = valleyMaxHeightMin;
        gen.valleyMaxHeightMax = valleyMaxHeightMax;
        gen.leftValleyWidthMin = leftValleyWidthMin;
        gen.leftValleyWidthMax = leftValleyWidthMax;
        gen.rightValleyWidthMin = rightValleyWidthMin;
        gen.rightValleyWidthMax = rightValleyWidthMax;
        gen.mountainFootTertiaryAmp = mountainFootTertiaryAmp;
        gen.mountainFootNoiseAmp = mountainFootNoiseAmp;
        gen.mountainMaxHeightMin = mountainMaxHeightMin;
        gen.mountainMaxHeightMax = mountainMaxHeightMax;
        gen.mountainTransitionWidthMin = mountainTransitionWidthMin;
        gen.mountainTransitionWidthMax = mountainTransitionWidthMax;
    }

    public void CopyFromGenerator(RiverValleyTerrainGenerator gen)
    {
        if (gen == null) return;
        riverWidthMin = gen.riverWidthMin;
        riverWidthMax = gen.riverWidthMax;
        waterHeight = gen.waterHeight;
        waterMeshWidth = gen.waterMeshWidth;
        riverBedDepth = gen.riverBedDepth;
        meanderPrimaryAmp = gen.meanderPrimaryAmp;
        meanderSecondaryAmp = gen.meanderSecondaryAmp;
        meanderTertiaryAmp = gen.meanderTertiaryAmp;
        applyTertiaryToRiver = gen.applyTertiaryToRiver;

        plainStartHeight = gen.plainStartHeight;
        plainStartHeightVariation = gen.plainStartHeightVariation;
        valleyMaxHeightMin = gen.valleyMaxHeightMin;
        valleyMaxHeightMax = gen.valleyMaxHeightMax;
        leftValleyWidthMin = gen.leftValleyWidthMin;
        leftValleyWidthMax = gen.leftValleyWidthMax;
        rightValleyWidthMin = gen.rightValleyWidthMin;
        rightValleyWidthMax = gen.rightValleyWidthMax;
        mountainFootTertiaryAmp = gen.mountainFootTertiaryAmp;
        mountainFootNoiseAmp = gen.mountainFootNoiseAmp;
        mountainMaxHeightMin = gen.mountainMaxHeightMin;
        mountainMaxHeightMax = gen.mountainMaxHeightMax;
        mountainTransitionWidthMin = gen.mountainTransitionWidthMin;
        mountainTransitionWidthMax = gen.mountainTransitionWidthMax;
    }
}
