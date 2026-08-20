using UnityEngine;

public class BoostPad : MonoBehaviour
{
    public float boostMultiplier = 1.35f;
    public float upwardLift = 4.0f;

    private bool isUsed = false;
    private Transform stoneTrans;

    private void Awake()
    {
        // 🌟 유저가 프리팹이나 씬에서 이미 자식 메쉬를 세팅해 두었는지 1차 검사
        if (transform.childCount > 0) return;

        // 🌟 Resources/BoostPad.prefab 유저 프리팹 로드 검사
        GameObject userPrefab = Resources.Load<GameObject>("BoostPad");
        if (userPrefab != null)
        {
            Instantiate(userPrefab, transform);
            return;
        }

        // 🌟 없을 때만 임시 더미 생성 + 친절한 콘솔 알림
        CreateVisuals();
        Debug.LogWarning("💡 [프리팹 알림] 'BoostPad'에 3D 프리팹이 없어 임시 더미로 자동 생성했습니다. (Assets/Resources/BoostPad.prefab 등록 시 자동 대체)");
    }

    private void CreateVisuals()
    {
        // 빛나는 부스트 패드 (화살표 형태 평면)
        GameObject pad = GameObject.CreatePrimitive(PrimitiveType.Cube);
        pad.name = "PadMesh";
        pad.transform.SetParent(transform);
        pad.transform.localPosition = new Vector3(0f, 0.05f, 0f);
        pad.transform.localScale = new Vector3(3.5f, 0.08f, 5.0f);

        Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        if (shader != null)
        {
            Material mat = new Material(shader);
            mat.SetColor("_BaseColor", new Color(0.1f, 1f, 0.6f, 0.9f));
            if (mat.HasProperty("_EmissionColor"))
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", new Color(0.1f, 1f, 0.6f) * 1.5f);
            }
            pad.GetComponent<Renderer>().sharedMaterial = mat;
        }
    }

    private void Update()
    {
        if (isUsed) return;

        if (stoneTrans == null)
        {
            SkippingStone s = FindAnyObjectByType<SkippingStone>();
            if (s != null) stoneTrans = s.transform;
            return;
        }

        // 돌이 패드 범위 위를 스쳐 지나갈 때 (방향 무관 전방향 감지)
        Vector3 stonePos = stoneTrans.position;
        Vector2 stoneP = new Vector2(stonePos.x, stonePos.z);
        Vector2 padP = new Vector2(transform.position.x, transform.position.z);
        if (Vector2.Distance(stoneP, padP) < 3.2f && stonePos.y < 1.6f && stonePos.y > -0.5f)
        {
            TriggerBoost();
        }
    }

    private void TriggerBoost()
    {
        isUsed = true;
        SkippingStone s = stoneTrans.GetComponent<SkippingStone>();
        if (s != null)
        {
            s.RecordBoostPadHit(transform.position);

            Rigidbody rb = s.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.linearVelocity = new Vector3(rb.linearVelocity.x * boostMultiplier, upwardLift, rb.linearVelocity.z * boostMultiplier);
            }

            GameController gc = FindAnyObjectByType<GameController>();
            if (gc != null)
            {
                gc.lastTimingText = "🚀 BOOST PAD! x1.35";
                gc.TriggerBoostPadEffect();
            }

            if (SplashEffectSpawner.Instance != null)
            {
                SplashEffectSpawner.Instance.SpawnSplash(transform.position, 2.5f);
            }

            if (AudioManager.Instance != null) AudioManager.Instance.PlaySound(SoundType.BoostPad, 1.2f);
            HapticFeedbackHelper.TriggerPerfectImpact();
        }
    }
}
