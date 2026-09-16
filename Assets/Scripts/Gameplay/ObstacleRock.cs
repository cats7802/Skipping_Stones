using UnityEngine;

public class ObstacleRock : MonoBehaviour
{
    private bool hasCollided = false;

    private void Awake()
    {
        // 🌟 유저가 프리팹이나 씬에서 이미 메쉬/자식을 세팅해 두었는지 검사
        if (transform.childCount > 0 || GetComponent<MeshRenderer>() != null || GetComponentInChildren<MeshRenderer>() != null) return;

        GameObject userPrefab = null;
#if UNITY_EDITOR
        userPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/prefab/BG_Deco/ObstacleRock.prefab");
#endif
        if (userPrefab == null) userPrefab = Resources.Load<GameObject>("ObstacleRock");

        if (userPrefab != null)
        {
            Instantiate(userPrefab, transform);
            return;
        }

        CreateRockMesh();
    }

    private void CreateRockMesh()
    {
        GameObject rock = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        rock.name = "RockModel";
        rock.transform.SetParent(transform);
        rock.transform.localPosition = new Vector3(0f, 0.4f, 0f);
        rock.transform.localScale = new Vector3(2.4f, 1.6f, 2.4f);

        Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        if (shader != null)
        {
            Material mat = new Material(shader);
            mat.SetColor("_BaseColor", new Color(0.35f, 0.32f, 0.30f));
            rock.GetComponent<Renderer>().sharedMaterial = mat;
        }

        SphereCollider col = rock.GetComponent<SphereCollider>();
        if (col != null) col.isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        HandleStoneHit(other.gameObject);
    }

    private void OnCollisionEnter(Collision collision)
    {
        HandleStoneHit(collision.gameObject);
    }

    private void HandleStoneHit(GameObject hitObj)
    {
        if (hasCollided || hitObj == null) return;

        SkippingStone stone = hitObj.GetComponent<SkippingStone>() ?? hitObj.GetComponentInParent<SkippingStone>();
        if (stone != null)
        {
            hasCollided = true;
            GameController gc = Object.FindAnyObjectByType<GameController>();
            if (gc != null)
            {
                gc.lastTimingText = "💥 장애물 충돌 (Crash!)";
            }
            stone.CrashOnLand("장애물 바위 충돌 - 게임 오버", isRockObstacle: true);
        }
    }
}
