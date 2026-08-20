using UnityEngine;

public class SplashEffectSpawner : MonoBehaviour
{
    public static SplashEffectSpawner Instance { get; private set; }

    [Header("호수 물보라 효과 색상 (맑고 깨끗한 화이트 & 에메랄드 스카이블루)")]
    public Color waterSplashColor = new Color(0.90f, 0.96f, 1.0f, 0.90f); // 맑고 투명한 물보라 물방울
    public Color foamColor = new Color(0.92f, 0.97f, 1.0f, 0.45f);        // 은은하고 투명한 수면 파문

    private ParticleSystem splashParticleSystem;
    private ParticleSystem rippleParticleSystem;
    private ParticleSystem crashDustParticleSystem;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        SetupParticleSystems();
    }

    private Texture2D softCircleTex;

    private Texture2D GetOrCreateSoftCircleTexture()
    {
        if (softCircleTex != null) return softCircleTex;
        int size = 32;
        softCircleTex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        softCircleTex.wrapMode = TextureWrapMode.Clamp;
        Vector2 center = new Vector2(size * 0.5f, size * 0.5f);
        float radius = size * 0.48f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), center);
                float alpha = Mathf.Clamp01(1f - (dist / radius));
                alpha = Mathf.SmoothStep(0f, 1f, alpha);
                softCircleTex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }
        softCircleTex.Apply();
        return softCircleTex;
    }

    private Material GetOrCreateParticleMaterial(Color baseColor, string matName)
    {
        Shader s = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (s == null) s = Shader.Find("Particles/Standard Unlit");
        if (s == null) s = Shader.Find("Sprites/Default");
        if (s == null) s = Shader.Find("Unlit/Color");

        Material mat = new Material(s) { name = matName };
        mat.SetFloat("_Surface", 1); // Transparent
        mat.SetFloat("_Blend", 0);   // Alpha Blend
        mat.SetInt("_ZWrite", 0);
        mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");

        Texture2D tex = GetOrCreateSoftCircleTexture();
        if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", tex);
        if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tex);
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", baseColor);
        if (mat.HasProperty("_Color")) mat.SetColor("_Color", baseColor);
        return mat;
    }

    private Texture2D ringTexture;

    private Texture2D GetOrCreateConcentricRingTexture()
    {
        if (ringTexture != null) return ringTexture;
        int size = 64;
        ringTexture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        ringTexture.wrapMode = TextureWrapMode.Clamp;
        Vector2 center = new Vector2(size * 0.5f, size * 0.5f);
        float maxR = size * 0.48f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), center);
                if (d > maxR)
                {
                    ringTexture.SetPixel(x, y, Color.clear);
                }
                else
                {
                    // 🌟 2중 동심원 링 파문 쉐이더 텍스처
                    float ring1 = Mathf.Exp(-Mathf.Pow(d - maxR * 0.85f, 2f) / 12f);
                    float ring2 = Mathf.Exp(-Mathf.Pow(d - maxR * 0.50f, 2f) / 16f);
                    float alpha = Mathf.Clamp01(ring1 * 0.95f + ring2 * 0.65f);
                    ringTexture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }
        }
        ringTexture.Apply();
        return ringTexture;
    }

    private void SetupParticleSystems()
    {
        // 1. 물보라(Splash) 파티클 시스템 - 작고 섬세한 원형 물방울
        GameObject splashObj = new GameObject("SplashParticleFX");
        splashObj.transform.SetParent(transform);
        splashParticleSystem = splashObj.AddComponent<ParticleSystem>();

        var splashRend = splashObj.GetComponent<ParticleSystemRenderer>();
        if (splashRend != null)
        {
            splashRend.material = GetOrCreateParticleMaterial(waterSplashColor, "SplashDropletMat");
        }

        var main = splashParticleSystem.main;
        main.loop = false;
        main.playOnAwake = false;
        main.startLifetime = 0.45f;
        main.startSpeed = 4.5f;
        main.startSize = 0.07f;
        main.startColor = waterSplashColor;
        main.gravityModifier = 1.6f;

        var emission = splashParticleSystem.emission;
        emission.rateOverTime = 0;

        var shape = splashParticleSystem.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 35f;
        shape.radius = 0.1f;

        var colorOverLifetime = splashParticleSystem.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient grad = new Gradient();
        grad.SetKeys(
            new GradientColorKey[] { new GradientColorKey(waterSplashColor, 0f), new GradientColorKey(new Color(0.75f, 0.90f, 1.0f), 1f) },
            new GradientAlphaKey[] { new GradientAlphaKey(0.9f, 0f), new GradientAlphaKey(0f, 1f) }
        );
        colorOverLifetime.color = grad;

        // 2. 수면 파문(Ripple Rings) 파티클 시스템 - 고화질 2중 동심원 링 파문
        GameObject rippleObj = new GameObject("RippleParticleFX");
        rippleObj.transform.SetParent(transform);
        rippleParticleSystem = rippleObj.AddComponent<ParticleSystem>();

        var rippleRend = rippleObj.GetComponent<ParticleSystemRenderer>();
        if (rippleRend != null)
        {
            Material rMat = GetOrCreateParticleMaterial(foamColor, "ConcentricRippleMat");
            Texture2D rTex = GetOrCreateConcentricRingTexture();
            if (rMat.HasProperty("_MainTex")) rMat.SetTexture("_MainTex", rTex);
            if (rMat.HasProperty("_BaseMap")) rMat.SetTexture("_BaseMap", rTex);
            rippleRend.material = rMat;
        }

        var rMain = rippleParticleSystem.main;
        rMain.loop = false;
        rMain.playOnAwake = false;
        rMain.startLifetime = 0.75f;
        rMain.startSpeed = 0f;
        rMain.startSize = 0.35f;
        rMain.startColor = foamColor;

        var rEmission = rippleParticleSystem.emission;
        rEmission.rateOverTime = 0;

        var rSize = rippleParticleSystem.sizeOverLifetime;
        rSize.enabled = true;
        AnimationCurve sizeCurve = new AnimationCurve();
        sizeCurve.AddKey(0f, 0.2f);
        sizeCurve.AddKey(1f, 2.2f);
        rSize.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

        var rColor = rippleParticleSystem.colorOverLifetime;
        rColor.enabled = true;
        Gradient rGrad = new Gradient();
        rGrad.SetKeys(
            new GradientColorKey[] { new GradientColorKey(foamColor, 0f), new GradientColorKey(new Color(0.85f, 0.95f, 1.0f), 1f) },
            new GradientAlphaKey[] { new GradientAlphaKey(0.85f, 0f), new GradientAlphaKey(0f, 1f) }
        );
        rColor.color = rGrad;

        // 3. 💥 지형/바위 충돌(Crash Dust & Debris) 파티클 시스템
        GameObject crashObj = new GameObject("CrashDustParticleFX");
        crashObj.transform.SetParent(transform);
        crashDustParticleSystem = crashObj.AddComponent<ParticleSystem>();

        var crashRend = crashObj.GetComponent<ParticleSystemRenderer>();
        if (crashRend != null)
        {
            crashRend.material = GetOrCreateParticleMaterial(new Color(0.75f, 0.65f, 0.50f, 0.8f), "CrashDustMat");
        }

        var cMain = crashDustParticleSystem.main;
        cMain.loop = false;
        cMain.playOnAwake = false;
        cMain.startLifetime = 0.55f;
        cMain.startSpeed = 4.5f;
        cMain.startSize = 0.08f;
        cMain.startColor = new Color(0.72f, 0.58f, 0.42f, 0.85f);
        cMain.gravityModifier = 1.6f;

        var cEmission = crashDustParticleSystem.emission;
        cEmission.rateOverTime = 0;

        var cShape = crashDustParticleSystem.shape;
        cShape.shapeType = ParticleSystemShapeType.Sphere;
        cShape.radius = 0.15f;

        var cColor = crashDustParticleSystem.colorOverLifetime;
        cColor.enabled = true;
        Gradient cGrad = new Gradient();
        cGrad.SetKeys(
            new GradientColorKey[] { new GradientColorKey(new Color(0.85f, 0.7f, 0.5f), 0f), new GradientColorKey(new Color(0.45f, 0.35f, 0.25f), 1f) },
            new GradientAlphaKey[] { new GradientAlphaKey(0.9f, 0f), new GradientAlphaKey(0f, 1f) }
        );
        cColor.color = cGrad;
    }

    public void SpawnSplash(Vector3 hitPoint, float intensity = 1f)
    {
        if (splashParticleSystem != null)
        {
            splashParticleSystem.transform.position = hitPoint + Vector3.up * 0.05f;
            splashParticleSystem.Emit((int)(24 * intensity));
        }

        if (rippleParticleSystem != null)
        {
            rippleParticleSystem.transform.position = new Vector3(hitPoint.x, 0.02f, hitPoint.z);
            rippleParticleSystem.Emit(1);
        }
    }

    public void SpawnCrashDustFX(Vector3 hitPoint, float intensity = 1f)
    {
        if (crashDustParticleSystem != null)
        {
            crashDustParticleSystem.transform.position = hitPoint + Vector3.up * 0.1f;
            crashDustParticleSystem.Emit((int)(22 * intensity));
        }
    }
}
