using UnityEngine;

public class FloatingTargetZone : MonoBehaviour
{
    public int targetScoreBonus = 2000;
    public float targetRadius = 3.5f;
    public string targetName = "🎯 과녁 타겟 (Target)";

    private bool isHit = false;
    private Transform stoneTrans;

    private void Awake()
    {
        // 🌟 유저가 프리팹이나 씬에서 이미 자식 메쉬를 세팅해 두었는지 1차 검사
        if (transform.childCount > 0) return;

        GameObject userPrefab = null;
#if UNITY_EDITOR
        userPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/prefab/BG_Deco/TargetZone.prefab");
#endif
        if (userPrefab == null) userPrefab = Resources.Load<GameObject>("TargetZone");

        if (userPrefab != null)
        {
            Instantiate(userPrefab, transform);
            return;
        }

        CreateVisuals();
    }

    private void CreateVisuals()
    {
        // 1. 외곽 링 (골드/옐로우)
        GameObject outerRing = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        outerRing.name = "OuterRing";
        outerRing.transform.SetParent(transform);
        outerRing.transform.localPosition = new Vector3(0f, 0.03f, 0f);
        outerRing.transform.localScale = new Vector3(targetRadius * 2f, 0.02f, targetRadius * 2f);

        // 2. 중간 링 (레드/오렌지)
        GameObject midRing = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        midRing.name = "MidRing";
        midRing.transform.SetParent(transform);
        midRing.transform.localPosition = new Vector3(0f, 0.04f, 0f);
        midRing.transform.localScale = new Vector3(targetRadius * 1.3f, 0.02f, targetRadius * 1.3f);

        // 3. 중심 불스아이 (시안/골드)
        GameObject centerRing = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        centerRing.name = "BullseyeCenter";
        centerRing.transform.SetParent(transform);
        centerRing.transform.localPosition = new Vector3(0f, 0.05f, 0f);
        centerRing.transform.localScale = new Vector3(targetRadius * 0.6f, 0.02f, targetRadius * 0.6f);

        Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        if (shader != null)
        {
            Material outerMat = new Material(shader);
            outerMat.SetColor("_BaseColor", new Color(1f, 0.85f, 0.1f, 0.85f));
            outerRing.GetComponent<Renderer>().sharedMaterial = outerMat;

            Material midMat = new Material(shader);
            midMat.SetColor("_BaseColor", new Color(1f, 0.25f, 0.25f, 0.9f));
            midRing.GetComponent<Renderer>().sharedMaterial = midMat;

            Material centerMat = new Material(shader);
            centerMat.SetColor("_BaseColor", new Color(0.2f, 0.95f, 1f, 0.95f));
            centerRing.GetComponent<Renderer>().sharedMaterial = centerMat;
        }

        DestroyImmediate(outerRing.GetComponent<Collider>());
        DestroyImmediate(midRing.GetComponent<Collider>());
        DestroyImmediate(centerRing.GetComponent<Collider>());
    }

    private float baseSpawnY = 16.04f;
    private bool hasInitializedY = false;

    private void Update()
    {
        if (isHit) return;

        if (!hasInitializedY)
        {
            baseSpawnY = transform.position.y;
            hasInitializedY = true;
        }

        float bobY = baseSpawnY + Mathf.Sin(Time.time * 2f + transform.position.x) * 0.02f;
        transform.position = new Vector3(transform.position.x, bobY, transform.position.z);

        if (stoneTrans == null)
        {
            SkippingStone foundStone = Object.FindAnyObjectByType<SkippingStone>();
            if (foundStone != null) stoneTrans = foundStone.transform;
            return;
        }

        Vector3 stonePos = stoneTrans.position;
        Vector2 sPos = new Vector2(stonePos.x, stonePos.z);
        Vector2 tPos = new Vector2(transform.position.x, transform.position.z);
        float dist = Vector2.Distance(sPos, tPos);

        if (dist <= targetRadius && Mathf.Abs(stonePos.y - transform.position.y) < 1.2f)
        {
            HitTarget(dist);
        }
    }

    private void HitTarget(float dist)
    {
        isHit = true;
        int bonus = targetScoreBonus;
        string hitMsg = "🎯 TARGET HIT! +2,000점";

        if (dist <= targetRadius * 0.4f)
        {
            bonus = 4000;
            hitMsg = "🔥 BULLSEYE! 정중앙 명중! +4,000점 🔥";
        }
        else if (dist <= targetRadius * 0.7f)
        {
            bonus = 3000;
            hitMsg = "⚡ GREAT TARGET! +3,000점 ⚡";
        }

        GameController gc = Object.FindAnyObjectByType<GameController>();
        if (gc != null)
        {
            gc.specialScore += bonus;
            gc.lastTimingText = hitMsg;
            gc.bannerNotificationText = hitMsg;
        }

        if (SplashEffectSpawner.Instance != null)
        {
            SplashEffectSpawner.Instance.SpawnSplash(transform.position, 2.5f);
        }
    }
}