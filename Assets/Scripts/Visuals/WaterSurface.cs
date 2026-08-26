using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class WaterSurface : MonoBehaviour
{
    private void Awake()
    {
        EnsureCollider();
    }

    private void EnsureCollider()
    {
        BoxCollider boxCol = GetComponent<BoxCollider>();
        if (boxCol == null)
        {
            Debug.LogWarning($"[WaterSurface] '{gameObject.name}' 수면 오브젝트에 BoxCollider 컴포넌트가 없습니다! 에디터 인스펙터에서 BoxCollider(Trigger)를 추가해주세요.");
        }
        else
        {
            boxCol.isTrigger = true;
        }
    }

    // 하위 호환용 빈 함수
    public void PlaceWaterAtPage(int page) { }
}