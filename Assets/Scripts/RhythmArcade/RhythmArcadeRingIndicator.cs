using System.Collections;
using UnityEngine;

namespace SkippingStones.RhythmArcade
{
    /// <summary>
    /// 🎯 [RhythmArcadeRingIndicator] BPM 비트 주기(1.0s~0.5s)에 정확히 1:1로 수축하는 착수 가이드 링
    /// </summary>
    public class RhythmArcadeRingIndicator : MonoBehaviour
    {
        [Header("링 메쉬 렌더러")]
        private GameObject ringObj;
        private MeshRenderer ringRenderer;
        private Material ringMat;

        private float beatDuration = 1.0f;
        private float elapsed = 0f;
        private bool isAnimating = false;
        private Vector3 targetPos;

        public void Initialize()
        {
            if (ringObj != null) Destroy(ringObj);

            ringObj = GameObject.CreatePrimitive(PrimitiveType.Quad);
            ringObj.name = "[RhythmArcade_Ring]";
            Destroy(ringObj.GetComponent<Collider>());
            ringObj.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

            ringRenderer = ringObj.GetComponent<MeshRenderer>();
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
            ringMat = new Material(shader);

            // 동적 링 텍스처 생성
            Texture2D ringTex = new Texture2D(64, 64, TextureFormat.RGBA32, false);
            Vector2 center = new Vector2(31.5f, 31.5f);
            for (int y = 0; y < 64; y++)
            {
                for (int x = 0; x < 64; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), center);
                    // 도넛 형태 링
                    float ringAlpha = Mathf.Clamp01(1f - Mathf.Abs(dist - 24f) / 4f);
                    ringTex.SetPixel(x, y, new Color(0.2f, 0.9f, 1.0f, ringAlpha * 0.8f));
                }
            }
            ringTex.Apply();

            ringMat.mainTexture = ringTex;
            ringRenderer.material = ringMat;
            ringRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            ringRenderer.receiveShadows = false;
            ringObj.SetActive(false);
        }

        public void ShowRing(Vector3 landingPos, float duration)
        {
            if (ringObj == null) Initialize();

            targetPos = landingPos;
            targetPos.y += 0.03f;
            ringObj.transform.position = targetPos;
            beatDuration = Mathf.Max(0.1f, duration);
            elapsed = 0f;
            isAnimating = true;
            ringObj.SetActive(true);
        }

        private void Update()
        {
            if (!isAnimating || ringObj == null) return;

            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / beatDuration);

            // 1. 크기 수축: 2.5x -> 1.0x (t=1.00 착수 순간 정확히 중심 안착)
            float scale = Mathf.Lerp(2.2f, 0.8f, t);
            ringObj.transform.localScale = new Vector3(scale, scale, 1.0f);

            // 2. 투명도 변화
            float alpha = Mathf.Lerp(0.3f, 1.0f, t);
            if (ringMat != null)
            {
                Color c = new Color(0.2f, 0.9f, 1.0f, alpha);
                ringMat.color = c;
            }

            if (t >= 1.0f)
            {
                isAnimating = false;
                ringObj.SetActive(false);
            }
        }

        public void HideRing()
        {
            isAnimating = false;
            if (ringObj != null) ringObj.SetActive(false);
        }

        private void OnDestroy()
        {
            if (ringObj != null) Destroy(ringObj);
            if (ringMat != null) Destroy(ringMat);
        }
    }
}
