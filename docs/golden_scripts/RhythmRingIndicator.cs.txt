using UnityEngine;

public class RhythmRingIndicator : MonoBehaviour
{
    [Header("참조")]
    public SkippingStone stone;

    [Header("링 크기 및 설정")]
    public float targetRingRadius = 0.29f;
    public float maxRingMultiplier = 5.2f;
    public int segments = 56;
    public float lineWidth = 0.032f;

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
        UpdateWaterLevel();
    }

    private void Start()
    {
        if (stone != null)
        {
            stone.OnSkipBounced += HandleBounceBurst;
        }
        UpdateWaterLevel();
    }

    public void UpdateWaterLevel()
    {
        if (stone != null && stone.waterLevel > 0.1f)
        {
            waterLevel = stone.waterLevel;
            return;
        }

        GameObject water = GameObject.Find("WaterSurface") ?? GameObject.Find("Water_Surface");
        if (water != null)
        {
            Collider col = water.GetComponent<Collider>();
            waterLevel = (col != null) ? col.bounds.max.y : water.transform.position.y;
        }
    }

    private void CreateRingLines()
    {
        // 기존 생성된 하위 링 오브젝트 정리
        if (innerObj != null) Destroy(innerObj);
        if (outerObj != null) Destroy(outerObj);
        if (dropObj != null) Destroy(dropObj);

        Shader lineShader = Shader.Find("Universal Render Pipeline/Particles/Unlit")
                            ?? Shader.Find("Sprites/Default")
                            ?? Shader.Find("Legacy Shaders/Particles/Alpha Blended Premultiply");

        Material lineMat = (lineShader != null) ? new Material(lineShader) : new Material(Shader.Find("Standard"));

        innerObj = new GameObject("InnerTargetRing_WaterFixed");
        innerObj.transform.SetParent(transform);
        innerRing = innerObj.AddComponent<LineRenderer>();
        ConfigureLineRenderer(innerRing, lineMat, innerRingColor, lineWidth);
        innerRing.alignment = LineAlignment.TransformZ;
        innerObj.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

        outerObj = new GameObject("OuterShrinkingRing_WaterFixed");
        outerObj.transform.SetParent(transform);
        outerRing = outerObj.AddComponent<LineRenderer>();
        ConfigureLineRenderer(outerRing, lineMat, outerRingColor, lineWidth * 1.15f);
        outerRing.alignment = LineAlignment.TransformZ;
        outerObj.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

        dropObj = new GameObject("VerticalDropLine_Guide");
        dropObj.transform.SetParent(transform);
        dropLine = dropObj.AddComponent<LineRenderer>();
        dropLine.useWorldSpace = true;
        dropLine.positionCount = 2;
        dropLine.startWidth = 0.006f;
        dropLine.endWidth = 0.009f;
        dropLine.material = lineMat;
        dropLine.enabled = false;
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

        UpdateWaterLevel();

        if (isBursting)
        {
            UpdateBurstAnimation();
            return;
        }

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

        bool shouldShow = (distToWater <= dynWindow * 1.35f && verticalVelocity < 1.0f);
        SetRingsActive(shouldShow);

        if (!shouldShow) return;

        // 수면 위 0.05m 높이에 정확히 안착
        Vector3 waterImpactCenter = new Vector3(stone.transform.position.x, waterLevel + 0.05f, stone.transform.position.z);

        // 🌟 돌의 고속 스핀(1440도/s)과 피치가 링 라인 렌더러에 간섭하여 떨리는 현상 원천 차단 (월드 수평 고정)
        if (innerObj != null) innerObj.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        if (outerObj != null) outerObj.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

        // SkippingStone 인스펙터 설정값 실시간 1:1 동기화
        float compensatedTargetRadius = (stone != null && stone.ringTargetRadius > 0.01f) ? stone.ringTargetRadius : targetRingRadius;
        float compensatedLineWidth = (stone != null && stone.ringLineWidth > 0.001f) ? stone.ringLineWidth : lineWidth;
        float curMaxMultiplier = (stone != null && stone.ringMaxMultiplier > 1.0f) ? stone.ringMaxMultiplier : maxRingMultiplier;
        float curDropLineWidth = (stone != null && stone.dropLineWidth > 0.001f) ? stone.dropLineWidth : 0.006f;

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

        DrawFlatCircle(innerRing, waterImpactCenter, compensatedTargetRadius, currentColor);

        float currentOuterRadius = compensatedTargetRadius * Mathf.Lerp(1.0f, curMaxMultiplier, ratio);
        DrawFlatCircle(outerRing, waterImpactCenter + Vector3.up * 0.005f, currentOuterRadius, currentColor);

        // 🌟 돌에서 수면 링 중심까지 수직으로 떨어지는 가이드 레이저 라인 활성화
        if (dropLine != null)
        {
            dropLine.enabled = true;
            dropLine.startWidth = curDropLineWidth;
            dropLine.endWidth = curDropLineWidth * 1.5f;
            dropLine.startColor = currentColor;
            dropLine.endColor = currentColor;
            dropLine.SetPosition(0, stone.transform.position);
            dropLine.SetPosition(1, waterImpactCenter);
        }
    }

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
        UpdateWaterLevel();
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
            if (innerObj != null) innerObj.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            Vector3 center = new Vector3(stone.transform.position.x, waterLevel + 0.06f, stone.transform.position.z);
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