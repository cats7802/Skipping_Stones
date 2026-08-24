using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteInEditMode]
public class TerrainSeamless : MonoBehaviour
{
    [Header("1. 이음새 동기화 대상 축")]
    [Tooltip("X축(좌/우 경계면)을 매끄럽게 연결합니다.")]
    public bool seamlessX = false;

    [Tooltip("Z축(앞/뒤 경계면)을 매끄럽게 연결합니다.")]
    public bool seamlessZ = true;

    [Header("2. 보간(Blend) 설정")]
    [Tooltip("경계면에서 안쪽으로 보간(블렌딩)이 적용될 거리(미터)")]
    [Range(5f, 300f)]
    public float blendDistance = 60f;

    [Tooltip("높이맵(지형 높낮이) 이음새를 동기화합니다.")]
    public bool blendHeights = true;

    [Tooltip("텍스처(칠해진 잔디, 바위 등) 이음새를 동기화합니다.")]
    public bool blendTextures = true;

    public enum BlendCurveType
    {
        SmoothStep,   // 부드러운 S자 곡선 (기본)
        Linear,       // 선형 선형 보간
        SmootherStep  // 더 부드러운 5차 다항식 곡선
    }

    [Tooltip("경계면과 안쪽 지형을 섞어줄 보간 곡선 방식")]
    public BlendCurveType blendCurve = BlendCurveType.SmoothStep;

    private float EvaluateBlend(float t)
    {
        t = Mathf.Clamp01(t);
        switch (blendCurve)
        {
            case BlendCurveType.Linear:
                return t;
            case BlendCurveType.SmootherStep:
                return t * t * t * (t * (t * 6f - 15f) + 10f);
            case BlendCurveType.SmoothStep:
            default:
                return t * t * (3f - 2f * t);
        }
    }

#if UNITY_EDITOR
    [ContextMenu("경계면 이음새 맞추기 (Apply Seamless Stitch)")]
    public void MakeSeamless()
    {
        Terrain terrain = GetComponent<Terrain>();
        if (terrain == null || terrain.terrainData == null)
        {
            Debug.LogError("[TerrainSeamless] Terrain 컴포넌트 또는 TerrainData를 찾을 수 없습니다.");
            return;
        }

        TerrainData tData = terrain.terrainData;
        Undo.RegisterCompleteObjectUndo(tData, "Apply Terrain Seamless Stitch");

        int hRes = tData.heightmapResolution;
        int aRes = tData.alphamapResolution;
        Vector3 tSize = tData.size;

        // 1. Blend Heights
        if (blendHeights)
        {
            float[,] heights = tData.GetHeights(0, 0, hRes, hRes);
            int blendPixelsX = Mathf.Clamp(Mathf.RoundToInt((blendDistance / tSize.x) * (hRes - 1)), 1, (hRes - 1) / 2);
            int blendPixelsZ = Mathf.Clamp(Mathf.RoundToInt((blendDistance / tSize.z) * (hRes - 1)), 1, (hRes - 1) / 2);

            // Z-axis seamless blend
            if (seamlessZ)
            {
                for (int x = 0; x < hRes; x++)
                {
                    float hStart = heights[0, x];
                    float hEnd = heights[hRes - 1, x];
                    float avgH = (hStart + hEnd) * 0.5f;

                    // Apply average to boundaries
                    heights[0, x] = avgH;
                    heights[hRes - 1, x] = avgH;

                    // Blend inwards
                    for (int z = 1; z < blendPixelsZ; z++)
                    {
                        float weight = EvaluateBlend((float)z / blendPixelsZ); // 0 at edge, 1 at interior
                        heights[z, x] = Mathf.Lerp(avgH, heights[z, x], weight);

                        int endZ = (hRes - 1) - z;
                        heights[endZ, x] = Mathf.Lerp(avgH, heights[endZ, x], weight);
                    }
                }
            }

            // X-axis seamless blend
            if (seamlessX)
            {
                for (int z = 0; z < hRes; z++)
                {
                    float hStart = heights[z, 0];
                    float hEnd = heights[z, hRes - 1];
                    float avgH = (hStart + hEnd) * 0.5f;

                    // Apply average to boundaries
                    heights[z, 0] = avgH;
                    heights[z, hRes - 1] = avgH;

                    // Blend inwards
                    for (int x = 1; x < blendPixelsX; x++)
                    {
                        float weight = EvaluateBlend((float)x / blendPixelsX);
                        heights[z, x] = Mathf.Lerp(avgH, heights[z, x], weight);

                        int endX = (hRes - 1) - x;
                        heights[z, endX] = Mathf.Lerp(avgH, heights[z, endX], weight);
                    }
                }
            }

            tData.SetHeights(0, 0, heights);
        }

        // 2. Blend Alphamaps (Textures)
        if (blendTextures && tData.alphamapLayers > 0)
        {
            float[,,] alphamaps = tData.GetAlphamaps(0, 0, aRes, aRes);
            int layers = tData.alphamapLayers;

            int blendAlphaX = Mathf.Clamp(Mathf.RoundToInt((blendDistance / tSize.x) * (aRes - 1)), 1, (aRes - 1) / 2);
            int blendAlphaZ = Mathf.Clamp(Mathf.RoundToInt((blendDistance / tSize.z) * (aRes - 1)), 1, (aRes - 1) / 2);

            // Z-axis seamless texture blend
            if (seamlessZ)
            {
                for (int x = 0; x < aRes; x++)
                {
                    float[] avgLayers = new float[layers];
                    for (int l = 0; l < layers; l++)
                    {
                        avgLayers[l] = (alphamaps[0, x, l] + alphamaps[aRes - 1, x, l]) * 0.5f;
                    }

                    // Set edge values
                    for (int l = 0; l < layers; l++)
                    {
                        alphamaps[0, x, l] = avgLayers[l];
                        alphamaps[aRes - 1, x, l] = avgLayers[l];
                    }

                    // Blend inwards
                    for (int z = 1; z < blendAlphaZ; z++)
                    {
                        float weight = EvaluateBlend((float)z / blendAlphaZ);
                        int endZ = (aRes - 1) - z;

                        float sumStart = 0f;
                        float sumEnd = 0f;

                        for (int l = 0; l < layers; l++)
                        {
                            alphamaps[z, x, l] = Mathf.Lerp(avgLayers[l], alphamaps[z, x, l], weight);
                            alphamaps[endZ, x, l] = Mathf.Lerp(avgLayers[l], alphamaps[endZ, x, l], weight);

                            sumStart += alphamaps[z, x, l];
                            sumEnd += alphamaps[endZ, x, l];
                        }

                        // Normalize splatmap layers to sum to 1.0
                        if (sumStart > 0.0001f)
                        {
                            for (int l = 0; l < layers; l++) alphamaps[z, x, l] /= sumStart;
                        }
                        if (sumEnd > 0.0001f)
                        {
                            for (int l = 0; l < layers; l++) alphamaps[endZ, x, l] /= sumEnd;
                        }
                    }
                }
            }

            // X-axis seamless texture blend
            if (seamlessX)
            {
                for (int z = 0; z < aRes; z++)
                {
                    float[] avgLayers = new float[layers];
                    for (int l = 0; l < layers; l++)
                    {
                        avgLayers[l] = (alphamaps[z, 0, l] + alphamaps[z, aRes - 1, l]) * 0.5f;
                    }

                    // Set edge values
                    for (int l = 0; l < layers; l++)
                    {
                        alphamaps[z, 0, l] = avgLayers[l];
                        alphamaps[z, aRes - 1, l] = avgLayers[l];
                    }

                    // Blend inwards
                    for (int x = 1; x < blendAlphaX; x++)
                    {
                        float weight = EvaluateBlend((float)x / blendAlphaX);
                        int endX = (aRes - 1) - x;

                        float sumStart = 0f;
                        float sumEnd = 0f;

                        for (int l = 0; l < layers; l++)
                        {
                            alphamaps[z, x, l] = Mathf.Lerp(avgLayers[l], alphamaps[z, x, l], weight);
                            alphamaps[z, endX, l] = Mathf.Lerp(avgLayers[l], alphamaps[z, endX, l], weight);

                            sumStart += alphamaps[z, x, l];
                            sumEnd += alphamaps[z, endX, l];
                        }

                        if (sumStart > 0.0001f)
                        {
                            for (int l = 0; l < layers; l++) alphamaps[z, x, l] /= sumStart;
                        }
                        if (sumEnd > 0.0001f)
                        {
                            for (int l = 0; l < layers; l++) alphamaps[z, endX, l] /= sumEnd;
                        }
                    }
                }
            }

            tData.SetAlphamaps(0, 0, alphamaps);
        }

        EditorUtility.SetDirty(tData);
        Debug.Log($"[TerrainSeamless] '{terrain.name}'의 경계면 이음새 동기화 완료! (적용 축: X={seamlessX}, Z={seamlessZ}, 보간 거리: {blendDistance}m)");
    }
#endif
}
