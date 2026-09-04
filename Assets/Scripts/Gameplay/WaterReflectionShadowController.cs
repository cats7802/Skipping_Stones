using UnityEngine;

namespace SkippingStones.Gameplay
{
    /// <summary>
    /// 🌊 수면 대칭 반사 그림자(Water Reflection Shadow)의 생성, 텍스처 베이킹 및 라이프사이클을 전담하는 헬퍼
    /// </summary>
    public class WaterReflectionShadowController
    {
        private GameObject waterReflectionObj;
        private MeshRenderer waterReflectionRenderer;
        private Material waterReflectionMat;

        public void Setup()
        {
            Cleanup();

            // 1. 평면 메쉬 생성 (물결 표면에 납작하게 밀착)
            waterReflectionObj = GameObject.CreatePrimitive(PrimitiveType.Quad);
            waterReflectionObj.name = "[Water_Reflection_Shadow]";
            SafeDestroy(waterReflectionObj.GetComponent<Collider>());
            waterReflectionObj.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

            waterReflectionRenderer = waterReflectionObj.GetComponent<MeshRenderer>();
            Shader unlit = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
            waterReflectionMat = (unlit != null) ? new Material(unlit) : new Material(Shader.Find("Standard"));

            // URP Unlit 머티리얼을 투명(Transparent Alpha Blended) 모드로 정식 세팅
            if (waterReflectionMat.HasProperty("_Surface"))
            {
                waterReflectionMat.SetFloat("_Surface", 1.0f); // 1 = Transparent
                waterReflectionMat.SetFloat("_Blend", 0.0f);   // 0 = Alpha
                waterReflectionMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                waterReflectionMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                waterReflectionMat.SetInt("_ZWrite", 0);
                waterReflectionMat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent + 10;
            }

            // 2. 가장자리가 부드럽게 페이드아웃되는 원형 알파 텍스처 동적 생성
            Texture2D softShadowTex = new Texture2D(64, 64, TextureFormat.RGBA32, false);
            softShadowTex.wrapMode = TextureWrapMode.Clamp;
            Vector2 center = new Vector2(31.5f, 31.5f);
            float radius = 31.5f;

            for (int y = 0; y < 64; y++)
            {
                for (int x = 0; x < 64; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), center);
                    float normDist = Mathf.Clamp01(dist / radius);
                    float alpha = Mathf.SmoothStep(1.0f, 0.0f, normDist);
                    alpha = Mathf.Pow(alpha, 1.8f);
                    softShadowTex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }
            softShadowTex.Apply();

            waterReflectionMat.mainTexture = softShadowTex;
            waterReflectionMat.color = new Color(0.02f, 0.08f, 0.16f, 0.35f);
            if (waterReflectionMat.HasProperty("_BaseColor"))
            {
                waterReflectionMat.SetColor("_BaseColor", new Color(0.02f, 0.08f, 0.16f, 0.35f));
            }

            waterReflectionRenderer.material = waterReflectionMat;
            waterReflectionRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            waterReflectionRenderer.receiveShadows = false;

            waterReflectionObj.transform.localScale = new Vector3(0.25f, 0.25f, 1.0f);
            waterReflectionObj.SetActive(false);
        }

        public void UpdateShadow(Vector3 stonePosition, float waterLevel, bool isActiveFlight)
        {
            if (waterReflectionObj == null) return;

            if (!isActiveFlight)
            {
                if (waterReflectionObj.activeSelf) waterReflectionObj.SetActive(false);
                return;
            }

            float dist = stonePosition.y - waterLevel;

            if (dist >= -0.35f && dist <= 3.5f)
            {
                if (!waterReflectionObj.activeSelf) waterReflectionObj.SetActive(true);

                waterReflectionObj.transform.position = new Vector3(stonePosition.x, waterLevel + 0.008f, stonePosition.z);
                waterReflectionObj.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

                float closeness = Mathf.Clamp01(1f - (dist / 2.8f));
                float shadowScale = Mathf.Lerp(0.30f, 0.14f, closeness);
                waterReflectionObj.transform.localScale = new Vector3(shadowScale, shadowScale, 1.0f);

                if (waterReflectionMat != null)
                {
                    float shadowAlpha = Mathf.Lerp(0.08f, 0.35f, closeness);
                    waterReflectionMat.color = new Color(0.03f, 0.10f, 0.20f, shadowAlpha);
                }
            }
            else
            {
                if (waterReflectionObj.activeSelf) waterReflectionObj.SetActive(false);
            }
        }

        public void Cleanup()
        {
            if (waterReflectionObj != null)
            {
                if (waterReflectionMat != null)
                {
                    if (waterReflectionMat.mainTexture != null)
                    {
                        SafeDestroy(waterReflectionMat.mainTexture);
                    }
                    SafeDestroy(waterReflectionMat);
                }
                SafeDestroy(waterReflectionObj);
                waterReflectionObj = null;
            }
        }

        private static void SafeDestroy(Object obj)
        {
            if (obj == null) return;
            if (Application.isPlaying) Object.Destroy(obj);
            else Object.DestroyImmediate(obj);
        }
    }
}
