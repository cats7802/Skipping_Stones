using UnityEngine;

public class ObstacleRock : MonoBehaviour
{
    private bool hasCollided = false;

    private void Awake()
    {
        // 🌟 유저가 프리팹이나 씬에서 이미 자식 메쉬를 세팅해 두었는지 1차 검사
        if (transform.childCount > 0) return;

        // 🌟 Resources/ObstacleRock.prefab 유저 프리팹 로드 검사
        GameObject userPrefab = Resources.Load<GameObject>("ObstacleRock");
        if (userPrefab != null)
        {
            Instantiate(userPrefab, transform);
            return;
        }

        // 🌟 없을 때만 임시 더미 생성 + 친절한 콘솔 알림
        CreateRockMesh();
        Debug.LogWarning("💡 [프리팹 알림] 'ObstacleRock'에 3D 프리팹이 없어 임시 더미로 자동 생성했습니다. (Assets/Resources/ObstacleRock.prefab 등록 시 자동 대체)");
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
        if (hasCollided) return;

        SkippingStone stone = other.GetComponent<SkippingStone>() ?? other.GetComponentInParent<SkippingStone>();
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
