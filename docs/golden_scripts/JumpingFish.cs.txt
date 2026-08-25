using UnityEngine;

public class JumpingFish : MonoBehaviour
{
    [Header("물고기 정보")]
    public string speciesId = "minnow";
    public string speciesName = "피라미";
    public float jumpHeight = 2.8f;
    public float jumpDuration = 1.2f;

    private bool isJumping = false;
    private bool isCaught = false;
    private float jumpTimer = 0f;
    private Vector3 basePos;
    private Transform stoneTransform;
    private GameObject targetMarker;

    private void Awake()
    {
        basePos = transform.position;

        // 🌟 유저가 프리팹이나 씬에서 이미 자식 메쉬를 세팅해 두었는지 1차 검사
        if (transform.childCount > 0) return;

        GameObject userPrefab = null;
#if UNITY_EDITOR
        userPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/prefab/BG_Deco/JumpingFish.prefab");
#endif
        if (userPrefab == null) userPrefab = Resources.Load<GameObject>("JumpingFish");

        if (userPrefab != null)
        {
            Instantiate(userPrefab, transform);
            return;
        }

        CreateVisuals();
    }

    private void CreateVisuals()
    {
        // 1. 물고기 3D 타원형 본체
        GameObject body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        body.name = "FishBody";
        body.transform.SetParent(transform);
        body.transform.localPosition = Vector3.zero;
        body.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
        body.transform.localScale = new Vector3(0.3f, 0.6f, 0.3f);

        // 머티리얼 (어종별 색상)
        Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        if (shader != null)
        {
            Material mat = new Material(shader);
            if (speciesId == "flying_fish") mat.SetColor("_BaseColor", new Color(0.1f, 0.8f, 1f));
            else if (speciesId == "carp") mat.SetColor("_BaseColor", new Color(1f, 0.6f, 0.1f));
            else mat.SetColor("_BaseColor", new Color(0.4f, 0.85f, 0.9f));
            
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.9f);
            body.GetComponent<Renderer>().sharedMaterial = mat;
        }

        // 2. 수면 타깃 마커 (원형 링)
        targetMarker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        targetMarker.name = "WaterTargetMarker";
        targetMarker.transform.SetParent(transform);
        targetMarker.transform.position = new Vector3(basePos.x, 0.05f, basePos.z);
        targetMarker.transform.localScale = new Vector3(1.8f, 0.02f, 1.8f);

        if (shader != null)
        {
            Material ringMat = new Material(shader);
            ringMat.SetColor("_BaseColor", new Color(0.2f, 0.95f, 0.65f, 0.85f));
            targetMarker.GetComponent<Renderer>().sharedMaterial = ringMat;
        }

        // 기본 위치는 수면 아래
        transform.position = new Vector3(basePos.x, -1f, basePos.z);
    }

    private void Update()
    {
        if (isCaught) return;

        if (stoneTransform == null)
        {
            SkippingStone s = FindAnyObjectByType<SkippingStone>();
            if (s != null) stoneTransform = s.transform;
            return;
        }

        // 돌이 25m 앞까지 다가왔을 때 점프 시작
        float distH = Vector2.Distance(new Vector2(basePos.x, basePos.z), new Vector2(stoneTransform.position.x, stoneTransform.position.z));
        if (!isJumping && distH <= 25f && distH > 2f)
        {
            isJumping = true;
            jumpTimer = 0f;
            if (SplashEffectSpawner.Instance != null)
            {
                SplashEffectSpawner.Instance.SpawnSplash(basePos, 1.5f);
            }
        }

        if (isJumping)
        {
            jumpTimer += Time.deltaTime;
            float progress = jumpTimer / jumpDuration;

            if (progress <= 1f)
            {
                // 포물선 궤적 점프
                float yOffset = Mathf.Sin(progress * Mathf.PI) * jumpHeight;
                transform.position = new Vector3(basePos.x + (progress - 0.5f) * 3f, yOffset, basePos.z);
                transform.rotation = Quaternion.Euler(0f, 90f, -Mathf.Cos(progress * Mathf.PI) * 45f);

                // 돌과의 충돌 (스나이핑) 판정
                if (Vector3.Distance(transform.position, stoneTransform.position) < 1.8f)
                {
                    SnipeHit();
                }
            }
            else
            {
                // 수면으로 착수
                transform.position = new Vector3(basePos.x, -2f, basePos.z);
                isJumping = false;
            }
        }
    }

    private void SnipeHit()
    {
        if (isCaught) return;
        isCaught = true;

        if (AquariumManager.Instance != null)
        {
            AquariumManager.Instance.RegisterCaughtFish(speciesId);
        }

        GameController gc = FindAnyObjectByType<GameController>();
        if (gc != null)
        {
            gc.TriggerFishSnipeEffect(speciesName);
        }

        if (SplashEffectSpawner.Instance != null)
        {
            SplashEffectSpawner.Instance.SpawnSplash(transform.position, 2.5f);
        }

        if (AudioManager.Instance != null) AudioManager.Instance.PlaySound(SoundType.CoinJingle, 1.1f);
        HapticFeedbackHelper.TriggerLightTap();

        gameObject.SetActive(false);
    }
}
