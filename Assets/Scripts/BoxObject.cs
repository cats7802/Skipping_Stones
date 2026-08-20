using UnityEngine;

[ExecuteAlways]
[SelectionBase]
public class BoxObject : MonoBehaviour
{
    [Header("박스 속성 설정")]
    [Tooltip("플레이 모드에서 자동 회전 여부")]
    public bool autoRotate = true;

    [Tooltip("초당 회전 속도 (각도)")]
    public Vector3 rotationSpeed = new Vector3(15f, 30f, 0f);

    [Header("기즈모 / 와이어프레임")]
    [Tooltip("선택 시 와이어프레임 기즈모 표시")]
    public bool showWireframe = true;
    public Color wireColor = Color.cyan;

    private void Update()
    {
        if (Application.isPlaying && autoRotate)
        {
            transform.Rotate(rotationSpeed * Time.deltaTime, Space.Self);
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (!showWireframe) return;

        Gizmos.color = wireColor;
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawWireCube(Vector3.zero, Vector3.one);
    }
}
