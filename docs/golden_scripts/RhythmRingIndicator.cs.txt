using UnityEngine;

public class RhythmRingIndicator : MonoBehaviour
{
    [Header("참조")]
    public SkippingStone stone;

    [Header("링 크기 및 설정")]
    [Header("링 크기 및 설정")]
    public float targetRingRadius = 0.15f; // 1/2로 콤팩트하게 축소된 정밀 타깃 반경
    public float maxRingMultiplier = 8.5f; // 바깥 링 시작 영역 유지
    public int segments = 56;
    public float lineWidth = 0.022f;

    [Header("색상 테마 (불투명 중앙 붉은색 퍼펙트 코어 & 수축 열기 그라데이션)")]
    [Tooltip("중앙 퍼펙트 과녁 코어 색상 (진하고 선명한 붉은색 솔리드 불투명 컬러)")]
    public Color innerCoreColor = new Color(0.95f, 0.15f, 0.22f, 0.95f); // 강렬하고 진한 루비 레드 코어
    public Color innerRingBorderColor = new Color(1.0f, 0.35f, 0.40f, 1.0f); // 중앙 붉은 테두리 링

    [Tooltip("바깥 수축 링 초기 색상 (원거리 - 옐로우)")]
    public Color shrinkingColorStart = new Color(1.0f, 0.92f, 0.20f, 1.0f); // 옐로우
    [Tooltip("바깥 수축 링 중거리 색상 (오렌지)")]
    public Color shrinkingColorMid = new Color(1.0f, 0.55f, 0.15f, 1.0f);   // 오렌지
    [Tooltip("바깥 수축 링 초근접 색상 (근거리 - 레드)")]
    public Color shrinkingColorEnd = new Color(1.0f, 0.15f, 0.22f, 1.0f);   // 레드

    [Range(0.05f, 0.6f)]
    public float shrinkingDiscAlpha = 0.28f; // 바깥 수축 디스크 반투명 채움 농도

    private GameObject innerCoreObj;
    private GameObject innerBorderObj;
    private GameObject outerBorderObj;
    private GameObject shrinkingDiscObj;

    private MeshFilter innerCoreFilter;
    private MeshRenderer innerCoreRenderer;
    private Mesh innerCoreMesh;
    private Material innerCoreMat;

    private MeshFilter shrinkingDiscFilter;
    private MeshRenderer shrinkingDiscRenderer;
    private Mesh shrinkingDiscMesh;
    private Material shrinkingDiscMat;

    private LineRenderer innerRingBorder;
    private LineRenderer outerRingBorder;

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
        if (innerCoreObj != null) Destroy(innerCoreObj);
        if (innerBorderObj != null) Destroy(innerBorderObj);
        if (outerBorderObj != null) Destroy(outerBorderObj);
        if (shrinkingDiscObj != null) Destroy(shrinkingDiscObj);

        Shader rippleShader = Shader.Find("Custom/RhythmRingWaterRipple");
        Shader unlitShader = Shader.Find("Universal Render Pipeline/Unlit")
                             ?? Shader.Find("Sprites/Default")
                             ?? Shader.Find("Universal Render Pipeline/Particles/Unlit");

        Material lineMat = (rippleShader != null) ? new Material(rippleShader) : (unlitShader != null ? new Material(unlitShader) : new Material(Shader.Find("Standard")));
        innerCoreMat = (rippleShader != null) ? new Material(rippleShader) : (unlitShader != null ? new Material(unlitShader) : new Material(Shader.Find("Standard")));
        shrinkingDiscMat = (rippleShader != null) ? new Material(rippleShader) : (unlitShader != null ? new Material(unlitShader) : new Material(Shader.Find("Standard")));

        // 🌟 실제 씬의 수면 머티리얼(M_StylizedWater)에서 실제 물 노멀 텍스처와 파동 파라미터 1:1 직결
        Material waterMat = Resources.Load<Material>("M_StylizedWater");
#if UNITY_EDITOR
        if (waterMat == null)
        {
            waterMat = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Design_sources/3D/Environments/SoStylized/Environment/Water/Materials/M_StylizedWater.mat");
        }
#endif
        if (waterMat != null)
        {
            Texture waterNormal = waterMat.GetTexture("_Normal");
            float waterScale = waterMat.HasProperty("_Water_Scale") ? waterMat.GetFloat("_Water_Scale") : 75f;
            float normalScale = (waterScale > 0.1f) ? (1.0f / waterScale) : 0.015f;

            if (innerCoreMat != null)
            {
                if (waterNormal != null) innerCoreMat.SetTexture("_NormalMap", waterNormal);
                innerCoreMat.SetFloat("_NormalScale", normalScale);
                innerCoreMat.SetFloat("_NormalSpeed", 0.5f);
                innerCoreMat.SetFloat("_DistortionStrength", 0.15f); // 은은한 0.15 수면 싱크 일렁임
            }
            if (shrinkingDiscMat != null)
            {
                if (waterNormal != null) shrinkingDiscMat.SetTexture("_NormalMap", waterNormal);
                shrinkingDiscMat.SetFloat("_NormalScale", normalScale);
                shrinkingDiscMat.SetFloat("_NormalSpeed", 0.5f);
                shrinkingDiscMat.SetFloat("_DistortionStrength", 0.15f);
            }
            if (lineMat != null)
            {
                if (waterNormal != null) lineMat.SetTexture("_NormalMap", waterNormal);
                lineMat.SetFloat("_NormalScale", normalScale);
                lineMat.SetFloat("_NormalSpeed", 0.5f);
                lineMat.SetFloat("_DistortionStrength", 0.15f);
            }
        }

        // 🌟 1. [안쪽] 진하고 불투명한 퍼펙트 타깃 코어 디스크 (Inner Target Core)
        innerCoreObj = new GameObject("InnerTargetCore_SolidWaterFixed");
        innerCoreFilter = innerCoreObj.AddComponent<MeshFilter>();
        innerCoreRenderer = innerCoreObj.AddComponent<MeshRenderer>();
        innerCoreRenderer.material = innerCoreMat;
        innerCoreRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        innerCoreRenderer.receiveShadows = false;
        innerCoreMesh = new Mesh { name = "InnerCoreMesh" };
        innerCoreFilter.mesh = innerCoreMesh;
        innerCoreObj.transform.position = Vector3.zero;
        innerCoreObj.transform.rotation = Quaternion.identity;

        // 🌟 2. [안쪽] 퍼펙트 타깃 테두리 링
        innerBorderObj = new GameObject("InnerTargetBorder_WaterFixed");
        innerRingBorder = innerBorderObj.AddComponent<LineRenderer>();
        ConfigureLineRenderer(innerRingBorder, lineMat, innerRingBorderColor, lineWidth);
        innerRingBorder.alignment = LineAlignment.TransformZ;
        innerBorderObj.transform.position = Vector3.zero;
        innerBorderObj.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

        // 🌟 3. [바깥쪽] 옐로우->오렌지->레드로 수축하는 반투명 디스크 (Shrinking Disc)
        shrinkingDiscObj = new GameObject("ShrinkingDisc_FilledWaterFixed");
        shrinkingDiscFilter = shrinkingDiscObj.AddComponent<MeshFilter>();
        shrinkingDiscRenderer = shrinkingDiscObj.AddComponent<MeshRenderer>();
        shrinkingDiscRenderer.material = shrinkingDiscMat;
        shrinkingDiscRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        shrinkingDiscRenderer.receiveShadows = false;
        shrinkingDiscMesh = new Mesh { name = "ShrinkingDiscMesh" };
        shrinkingDiscFilter.mesh = shrinkingDiscMesh;
        shrinkingDiscObj.transform.position = Vector3.zero;
        shrinkingDiscObj.transform.rotation = Quaternion.identity;

        // 🌟 4. [바깥쪽] 수축 링 테두리 라인
        outerBorderObj = new GameObject("OuterShrinkingBorder_WaterFixed");
        outerRingBorder = outerBorderObj.AddComponent<LineRenderer>();
        ConfigureLineRenderer(outerRingBorder, lineMat, shrinkingColorStart, lineWidth * 1.2f);
        outerRingBorder.alignment = LineAlignment.TransformZ;
        outerBorderObj.transform.position = Vector3.zero;
        outerBorderObj.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
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

    private Vector3 lockedImpactPos;
    private bool isTargetLocked = false;
    private int lastSkipIndex = -1;

    private void LateUpdate()
    {
        if (stone == null)
        {
            stone = GetComponentInParent<SkippingStone>() ?? FindAnyObjectByType<SkippingStone>();
            if (stone == null) return;
        }

        UpdateWaterLevel();

        if (isBursting)
        {
            UpdateBurstAnimation();
            return;
        }

        if (!stone.isThrown || stone.isSunk || stone.isCrashed)
        {
            SetRingsActive(false);
            isTargetLocked = false;
            return;
        }

        Rigidbody rb = stone.GetComponent<Rigidbody>();
        if (rb == null)
        {
            SetRingsActive(false);
            return;
        }

        Vector3 stonePos = stone.transform.position;
        Vector3 vel = rb.linearVelocity;
        float currentWaterY = (stone != null && stone.waterLevel > 0.1f) ? stone.waterLevel : waterLevel;

        // 새 바운스 주기 시작 시 타깃 락 리셋
        if (stone.skipCount != lastSkipIndex)
        {
            lastSkipIndex = stone.skipCount;
            isTargetLocked = false;
        }

        float effGravity = Mathf.Abs(Physics.gravity.y * (stone != null ? stone.gravityScale : 1f));
        if (effGravity < 0.1f) effGravity = 9.81f;

        float deltaY = stonePos.y - currentWaterY;
        float timeToImpact = 0f;

        // 🌟 바운스/정점 상승 구간에서 최초 1회만 착수 예상 지점을 계산하여 수면에 말뚝 고정(Target Lock)
        if (deltaY > 0.05f)
        {
            float a = 0.5f * effGravity;
            float b = -vel.y;
            float c = -deltaY;
            float discriminant = (b * b) - (4f * a * c);

            if (discriminant >= 0f)
            {
                float t1 = (-b + Mathf.Sqrt(discriminant)) / (2f * a);
                float t2 = (-b - Mathf.Sqrt(discriminant)) / (2f * a);
                timeToImpact = (t1 > 0f && t2 > 0f) ? Mathf.Min(t1, t2) : Mathf.Max(t1, t2);
            }
            else
            {
                timeToImpact = (vel.y < -0.1f) ? (deltaY / Mathf.Abs(vel.y)) : 0.5f;
            }

            Vector3 currentCalculatedImpact = new Vector3(
                stonePos.x + vel.x * timeToImpact,
                currentWaterY + 0.05f,
                stonePos.z + vel.z * timeToImpact
            );

            if (!isTargetLocked || vel.y > 0.5f)
            {
                lockedImpactPos = currentCalculatedImpact;
                isTargetLocked = true;
            }
        }
        else
        {
            timeToImpact = 0f;
            lockedImpactPos = new Vector3(stonePos.x, currentWaterY + 0.05f, stonePos.z);
            isTargetLocked = false;
        }

        // 표시 가시거리 및 판정 윈도우 계산
        float dynWindow = (stone != null) ? Mathf.Lerp(stone.timingWindowHeight, 1.4f, Mathf.Clamp01(stone.skipCount / 30f)) : 2.4f;
        bool shouldShow = (deltaY <= dynWindow * 1.5f || (timeToImpact > 0f && timeToImpact < 0.65f && vel.y < 2.5f));
        SetRingsActive(shouldShow);

        if (!shouldShow) return;

        // 🌟 돌의 고속 스핀(1440도/s)과 피치가 링 라인 렌더러에 간섭하여 떨리는 현상 원천 차단 (월드 수평 고정)
        if (innerBorderObj != null) innerBorderObj.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        if (outerBorderObj != null) outerBorderObj.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

        // SkippingStone 인스펙터 설정값 실시간 1:1 동기화
        float compensatedTargetRadius = (stone != null && stone.ringTargetRadius > 0.01f) ? stone.ringTargetRadius : targetRingRadius;
        float compensatedLineWidth = (stone != null && stone.ringLineWidth > 0.001f) ? stone.ringLineWidth : lineWidth;
        float curMaxMultiplier = (stone != null && stone.ringMaxMultiplier > 1.0f) ? stone.ringMaxMultiplier : maxRingMultiplier;

        if (innerRingBorder != null)
        {
            innerRingBorder.startWidth = compensatedLineWidth;
            innerRingBorder.endWidth = compensatedLineWidth;
        }
        if (outerRingBorder != null)
        {
            outerRingBorder.startWidth = compensatedLineWidth * 1.25f;
            outerRingBorder.endWidth = compensatedLineWidth * 1.25f;
        }

        // 남은 시간(timeToImpact) 및 높이 비율을 종합하여 링 수축 비율 계산 (0: 착수 직전, 1: 원거리)
        float maxExpectedTime = 0.50f;
        float timeRatio = Mathf.Clamp01(timeToImpact / maxExpectedTime);
        float heightRatio = Mathf.Clamp01(deltaY / dynWindow);
        float ratio = Mathf.Max(timeRatio, heightRatio * 0.7f);

        // 🌟 1. [바깥 수축 링] 옐로우(1.0) ➔ 오렌지(0.5) ➔ 레드(0.0) 열기 그라데이션
        Color currentOuterColor;
        if (ratio > 0.5f)
        {
            float t = (ratio - 0.5f) * 2f;
            currentOuterColor = Color.Lerp(shrinkingColorMid, shrinkingColorStart, t);
        }
        else
        {
            float t = ratio * 2f;
            currentOuterColor = Color.Lerp(shrinkingColorEnd, shrinkingColorMid, t);
        }

        Color discFillColor = currentOuterColor;
        discFillColor.a = shrinkingDiscAlpha;

        // 🎯 [안쪽] 1. 불투명하고 진한 퍼펙트 타깃 코어 디스크 (수면 고정)
        DrawFilledDisc(innerCoreFilter, innerCoreRenderer, innerCoreMesh, innerCoreMat, lockedImpactPos + Vector3.up * 0.002f, compensatedTargetRadius, innerCoreColor);

        // 🎯 [안쪽] 2. 퍼펙트 타깃 테두리 링
        DrawFlatCircle(innerRingBorder, lockedImpactPos + Vector3.up * 0.003f, compensatedTargetRadius, innerRingBorderColor);

        // ⏱️ [바깥쪽] 3. 옐로우/오렌지/레드로 좁혀지는 반투명 수축 디스크
        float currentOuterRadius = compensatedTargetRadius * Mathf.Lerp(1.0f, curMaxMultiplier, ratio);
        DrawFilledDisc(shrinkingDiscFilter, shrinkingDiscRenderer, shrinkingDiscMesh, shrinkingDiscMat, lockedImpactPos + Vector3.up * 0.001f, currentOuterRadius, discFillColor);

        // ⏱️ [바깥쪽] 4. 수축 외곽 테두리 링
        DrawFlatCircle(outerRingBorder, lockedImpactPos + Vector3.up * 0.004f, currentOuterRadius, currentOuterColor);
    }

    private void DrawFilledDisc(MeshFilter mf, MeshRenderer mr, Mesh mesh, Material mat, Vector3 center, float radius, Color color)
    {
        if (mr == null || mesh == null) return;
        mr.enabled = true;
        if (mat != null)
        {
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
        }

        Vector3[] vertices = new Vector3[segments + 1];
        int[] triangles = new int[segments * 6]; // 양면 렌더링 (Double-sided)
        Color[] colors = new Color[segments + 1];

        vertices[0] = center;
        colors[0] = color;

        float angleStep = 360f / segments;
        for (int i = 0; i < segments; i++)
        {
            float rad = Mathf.Deg2Rad * (i * angleStep);
            float x = center.x + Mathf.Cos(rad) * radius;
            float z = center.z + Mathf.Sin(rad) * radius;
            vertices[i + 1] = new Vector3(x, center.y, z);
            colors[i + 1] = color;

            int next = (i + 1) % segments;
            // 윗면 (상향 노멀)
            triangles[i * 6] = 0;
            triangles[i * 6 + 1] = next + 1;
            triangles[i * 6 + 2] = i + 1;

            // 아랫면 (하향 노멀)
            triangles[i * 6 + 3] = 0;
            triangles[i * 6 + 4] = i + 1;
            triangles[i * 6 + 5] = next + 1;
        }

        mesh.Clear();
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.colors = colors;
        mesh.RecalculateNormals();
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
        float burstDuration = 0.25f;

        if (burstTimer >= burstDuration)
        {
            isBursting = false;
            return;
        }

        float t = burstTimer / burstDuration;
        float expandRadius = targetRingRadius * (1f + t * 1.8f);
        Color burstCol = Color.Lerp(new Color(1.0f, 0.85f, 0.2f, 1.0f), new Color(1.0f, 0.3f, 0.1f, 0f), t);

        if (stone != null)
        {
            Vector3 center = new Vector3(stone.transform.position.x, waterLevel + 0.06f, stone.transform.position.z);
            DrawFlatCircle(innerRingBorder, center, expandRadius, burstCol);
        }

        if (innerCoreRenderer != null) innerCoreRenderer.enabled = false;
        if (shrinkingDiscRenderer != null) shrinkingDiscRenderer.enabled = false;
        if (outerRingBorder != null) outerRingBorder.enabled = false;
    }

    private void SetRingsActive(bool active)
    {
        if (innerCoreRenderer != null) innerCoreRenderer.enabled = active;
        if (shrinkingDiscRenderer != null) shrinkingDiscRenderer.enabled = active;
        if (innerRingBorder != null) innerRingBorder.enabled = active;
        if (outerRingBorder != null) outerRingBorder.enabled = active;
    }

    private void OnDestroy()
    {
        if (innerCoreObj != null) Destroy(innerCoreObj);
        if (innerBorderObj != null) Destroy(innerBorderObj);
        if (outerBorderObj != null) Destroy(outerBorderObj);
        if (shrinkingDiscObj != null) Destroy(shrinkingDiscObj);
        if (innerCoreMesh != null) Destroy(innerCoreMesh);
        if (shrinkingDiscMesh != null) Destroy(shrinkingDiscMesh);
    }
}