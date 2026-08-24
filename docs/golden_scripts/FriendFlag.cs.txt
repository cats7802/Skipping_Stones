using UnityEngine;

public class FriendFlag : MonoBehaviour
{
    public string friendName = "라이언";
    public string rankText = "3위";
    public float targetDistance = 150f;

    private bool isPassed = false;
    private Transform stoneTrans;

    private void Awake()
    {
        // 🌟 유저가 프리팹이나 씬에서 이미 자식 메쉬를 세팅해 두었는지 1차 검사
        if (transform.childCount > 0) return;

        // 🌟 Resources/FriendFlag.prefab 유저 프리팹 로드 검사
        GameObject userPrefab = Resources.Load<GameObject>("FriendFlag");
        if (userPrefab != null)
        {
            Instantiate(userPrefab, transform);
            return;
        }

        // 🌟 없을 때만 임시 더미 생성 + 친절한 콘솔 알림
        CreateFlagVisuals();
        Debug.LogWarning("💡 [프리팹 알림] 'FriendFlag'에 3D 프리팹이 없어 임시 더미로 자동 생성했습니다. (Assets/Resources/FriendFlag.prefab 등록 시 자동 대체)");
    }

    private void CreateFlagVisuals()
    {
        // 깃대 (Pole)
        GameObject pole = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        pole.name = "FlagPole";
        pole.transform.SetParent(transform);
        pole.transform.localPosition = new Vector3(0f, 1.5f, 0f);
        pole.transform.localScale = new Vector3(0.08f, 1.5f, 0.08f);

        // 깃발 (Flag Banner)
        GameObject banner = GameObject.CreatePrimitive(PrimitiveType.Cube);
        banner.name = "FlagBanner";
        banner.transform.SetParent(transform);
        banner.transform.localPosition = new Vector3(0.6f, 2.5f, 0f);
        banner.transform.localScale = new Vector3(1.2f, 0.7f, 0.04f);

        Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        if (shader != null)
        {
            Material mat = new Material(shader);
            mat.SetColor("_BaseColor", new Color(1f, 0.85f, 0.2f)); // 카카오 옐로우
            banner.GetComponent<Renderer>().sharedMaterial = mat;
        }
    }

    private void Update()
    {
        if (isPassed) return;

        if (stoneTrans == null)
        {
            SkippingStone foundStone = Object.FindAnyObjectByType<SkippingStone>();
            if (foundStone != null) stoneTrans = foundStone.transform;
            return;
        }

        SkippingStone stoneComp = stoneTrans.GetComponent<SkippingStone>();
        if (stoneComp != null && stoneComp.totalDistance >= targetDistance)
        {
            isPassed = true;
            GameController gc = Object.FindAnyObjectByType<GameController>();
            if (gc != null)
            {
                gc.TriggerFriendOvertake(friendName, rankText);
            }
        }
    }
}
