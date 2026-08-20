using UnityEngine;

/// <summary>
/// Water_Surface 컴포넌트.
/// BG_01이 통합 청크로 스트리밍되므로 이 스크립트는
/// 콜라이더 보장 역할만 담당합니다.
/// </summary>
public class WaterSurface : MonoBehaviour
{
    [Header("호수 수면 설정")]
    public float chunkSize = 1500f;

    private void Awake()
    {
        EnsureCollider();
    }

    private void Start()
    {
        EnsureCollider();
    }

    private void EnsureCollider()
    {
        // 돌의 수면 물리 튕김을 위한 BoxCollider 보장
        BoxCollider boxCol = GetComponent<BoxCollider>();
        if (boxCol == null) boxCol = gameObject.AddComponent<BoxCollider>();
        boxCol.isTrigger = true;
        if (boxCol.size.z < 100f)
        {
            boxCol.center = new Vector3(0f, -0.2f, chunkSize * 0.5f);
            boxCol.size = new Vector3(60f, 0.4f, chunkSize);
        }
    }

    // PlaceWaterAtPage는 LakeEnvironmentManager.PlaceBGAtPage로 통합됨 (하위 호환용 stub 유지)
    public void PlaceWaterAtPage(int page) { }
}
