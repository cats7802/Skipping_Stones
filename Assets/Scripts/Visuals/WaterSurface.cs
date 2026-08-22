using UnityEngine;

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
            boxCol = gameObject.AddComponent<BoxCollider>();
            boxCol.isTrigger = true;
            boxCol.center = new Vector3(0f, -2.0f, 750f);
            boxCol.size = new Vector3(200f, 4.0f, 1500f);
        }
        else
        {
            boxCol.isTrigger = true;
        }
    }

    // 하위 호환용 빈 함수
    public void PlaceWaterAtPage(int page) { }
}