using UnityEngine;

public class RhythmRingIndicator : MonoBehaviour
{
    [Header("참조")]
    public SkippingStone stone;

    [Header("링 크기 및 설정")]
    public float targetRingRadius = 0.29f; // 🌟 안쪽 원: 1/2 아담한 크기 유지 (0.29m)
    public float maxRingMultiplier = 5.2f; // 🌟 바깥 링: 이전의 넉넉한 크기(1.5m)로 복원하여 두 원 사이의 시각적 거리를 대폭 확장!
    public int segments = 56;
    public float lineWidth = 0.032f; // 🌟 날렵하고 세련된 두께 조정

    [Header("색상 테마")]
    public Color innerRingColor = new Color(0.1f, 0.85f, 1f, 0.9f);
    public Color perfectRingColor = new Color(0.15f, 1f, 0.5f, 1f);
    public Color outerRingColor = new Color(0.3f, 0.95f, 1f, 0.85f);

    private GameObject innerObj;
    private GameObject outerObj;
    private GameObject dropObj;

    private LineRenderer innerRing;
    private LineRenderer outerRing;
    private LineRenderer dropLine;
    private float burstTimer = 0f;
    private bool isBursting = false;
    private float waterLevel = 0f;

    private void Awake()
    {
        if (stone == null) stone = GetComponentInParent<SkippingStone>();
        CreateRingLines();
    }

    private void Start()
    {
        if (stone != null)
        {
            stone.OnSkipBounced += HandleBounceBurst;
        }
    }

    private void CreateRingLines()
    {
        Shader lineShader = Shader.Find("Universal Render Pipeline/Particles/Unlit") 
                            ?? Shader.Find("Sprites/Default") 
                            ?? Shader.Find("Legacy Shaders/Particles/Alpha Blended Premultiply");

        Material lineMat = (lineShader != null) ? new Material(lineShader) : new Material(Shader.Find("Standard"));

        // 1. 🌊 수면 기준 타깃 링 (Inner Target Ring: 수면 완전 수평 TransformZ 고정)
        innerObj = new GameObject("InnerTargetRing_WaterFixed");
        innerObj.transform.SetParent(null); // 스핀 회전 분리
        innerRing = innerObj.AddComponent<LineRenderer>();
        ConfigureLineRenderer(innerRing, lineMat, innerRingColor, lineWidth);
        innerRing.alignment = LineAlignment.TransformZ;
        innerObj.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

        // 2. 🌊 수면 수축 타이밍 링 (Outer Shrinking Ring: 수면 완전 수평 TransformZ 고정)
        outerObj = new GameObject("OuterShrinkingRing_WaterFixed");
        outerObj.transform.SetParent(null); // 스핀 회전 분리
        outerRing = outerObj.AddComponent<LineRenderer>();
        ConfigureLineRenderer(outerRing, lineMat, outerRingColor, lineWidth * 1.15f);
        outerRing.alignment = LineAlignment.TransformZ;
        outerObj.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

        // 3. ⚡ 돌에서 수면으로 떨어지는 레이저 가이드 드롭 라인 (돌 가림 방지를 위해 비활성화)
        dropObj = new GameObject("VerticalDropLine_Guide");
        dropObj.transform.SetParent(null);
        dropLine = dropObj.AddComponent<LineRenderer>();
        dropLine.useWorldSpace = true;
        dropLine.positionCount = 2;
        dropLine.startWidth = 0.005f;
        dropLine.endWidth = 0.008f;
        dropLine.material = lineMat;
        dropLine.enabled = false; // 돌 본체 시야 100% 확보
    }

    private void ConfigureLineRenderer(LineRenderer lr, Material mat, Color col, float width)
    {
        lr.useWorldSpace = true;
        lr.loop = true;
        lr.positionCount = segments;
        lr.startWidth = width;
        lr.endWidth = width;
        lr.material = mat;
        lr.startColor = col;
        lr.endColor = col;
        lr.numCornerVertices = 4;
        lr.numCapVertices = 4;
    }

    private void LateUpdate()
    {
        if (stone == null)
        {
            stone = FindAnyObjectByType<SkippingStone>();
            if (stone == null) return;
        }

        // 버스트 이펙트 연출 중
        if (isBursting)
        {
            UpdateBurstAnimation();
            return;
        }

        // 돌이 아직 발사되지 않았거나 침몰한 경우 숨김
        if (!stone.isThrown || stone.isSunk)
        {
            SetRingsActive(false);
            return;
        }

        float stoneHeight = stone.transform.position.y;
        float distToWater = Mathf.Max(0f, stoneHeight - waterLevel);
        Rigidbody rb = stone.GetComponent<Rigidbody>();
        float verticalVelocity = (rb != null) ? rb.linearVelocity.y : 0f;

        float dynWindow = (stone != null) ? Mathf.Lerp(stone.timingWindowHeight, 1.4f, Mathf.Clamp01(stone.skipCount / 30f)) : 2.4f;

        // 하강 중이면서 타이밍 윈도우 범위 내 진입 시 활성화
        bool shouldShow = (distToWater <= dynWindow * 1.35f && verticalVelocity < 1.0f);
        SetRingsActive(shouldShow);

        if (!shouldShow) return;

        Vector3 waterImpactCenter = new Vector3(stone.transform.position.x, waterLevel + 0.035f, stone.transform.position.z);

        // 🌟 불필요한 매 프레임 카메라 거리 계산을 완전히 제거하여 떨림 0% & 거울처럼 매끄러운 고정 크기 유지
        float compensatedTargetRadius = targetRingRadius;
        float compensatedLineWidth = lineWidth;

        if (innerRing != null)
        {
            innerRing.startWidth = compensatedLineWidth;
            innerRing.endWidth = compensatedLineWidth;
        }
        if (outerRing != null)
        {
            outerRing.startWidth = compensatedLineWidth * 1.15f;
            outerRing.endWidth = compensatedLineWidth * 1.15f;
        }

        float ratio = Mathf.Clamp01(distToWater / dynWindow);
        Color currentColor = (distToWater <= 0.45f) ? perfectRingColor : Color.Lerp(perfectRingColor, innerRingColor, ratio);

        // 1. 🌊 안쪽 타깃 링 (0.29m 고정 과녁)
        DrawFlatCircle(innerRing, waterImpactCenter, compensatedTargetRadius, currentColor);

        // 2. 🌊 바깥쪽 수축 링 (0.29m 고정 과녁을 향해 완벽하게 수축)
        float currentOuterRadius = compensatedTargetRadius * Mathf.Lerp(1.0f, maxRingMultiplier, ratio);
        DrawFlatCircle(outerRing, waterImpactCenter + Vector3.up * 0.005f, currentOuterRadius, currentColor);

        // 3. ⚡ 수직 드롭 라인은 조약돌 시야 확보를 위해 비활성화 유지
        if (dropLine != null)
        {
            dropLine.enabled = false;
        }
    }

    /// <summary>
    /// 수면(X-Z) 평면에 완전 수평으로 안착되는 매끄러운 타원/원형 링을 그립니다.
    /// </summary>
    private void DrawFlatCircle(LineRenderer lr, Vector3 center, float radius, Color color)
    {
        if (lr == null) return;
        lr.enabled = true;
        lr.startColor = color;
        lr.endColor = color;

        float angleStep = 360f / segments;
        for (int i = 0; i < segments; i++)
        {
            float rad = Mathf.Deg2Rad * (i * angleStep);
            float x = center.x + Mathf.Cos(rad) * radius;
            float z = center.z + Mathf.Sin(rad) * radius;
            lr.SetPosition(i, new Vector3(x, center.y, z));
        }
    }

    private void HandleBounceBurst(int count, string grade)
    {
        isBursting = true;
        burstTimer = 0f;
    }

    private void UpdateBurstAnimation()
    {
        burstTimer += Time.deltaTime;
        float burstDuration = 0.22f;

        if (burstTimer >= burstDuration)
        {
            isBursting = false;
            return;
        }

        float t = burstTimer / burstDuration;
        float expandRadius = targetRingRadius * (1f + t * 1.6f);
        Color burstCol = Color.Lerp(perfectRingColor, new Color(0.2f, 1f, 0.5f, 0f), t);

        if (stone != null)
        {
            Vector3 center = new Vector3(stone.transform.position.x, waterLevel + 0.04f, stone.transform.position.z);
            DrawFlatCircle(innerRing, center, expandRadius, burstCol);
        }

        if (outerRing != null) outerRing.enabled = false;
        if (dropLine != null) dropLine.enabled = false;
    }

    private void SetRingsActive(bool active)
    {
        if (innerRing != null) innerRing.enabled = active;
        if (outerRing != null) outerRing.enabled = active;
        if (dropLine != null) dropLine.enabled = active;
    }

    private void OnDestroy()
    {
        if (innerObj != null) Destroy(innerObj);
        if (outerObj != null) Destroy(outerObj);
        if (dropObj != null) Destroy(dropObj);
    }
}