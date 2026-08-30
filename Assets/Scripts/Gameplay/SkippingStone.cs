using UnityEngine;
using System;
using System.Collections;
using SkippingStones.Terrain;

[RequireComponent(typeof(Rigidbody))]
public class SkippingStone : MonoBehaviour
{
    [Header("3D 프리팹 모델")]
    [Tooltip("사용자 지정 Stone 프리팹 (미지정 시 Assets/3D/prefab/Stone.prefab 자동 로드)")]
    public GameObject customStonePrefab;

    [Header("물리 및 이동 속성")]
    [Tooltip("전방 투척 파워 (수평 속도, m/s)")]
    public float forwardPower = 13.0f;

    [Tooltip("초기 발사 시 위쪽으로 솟구치는 상승력")]
    public float initialUpwardForce = 4.2f;

    [Tooltip("수면 바운스 시 위로 튀어오르는 기본 반사력 기준값")]
    public float baseBounceUpForce = 4.0f;

    [Tooltip("최대 수평 이동 속도 상한선")]
    public float maxHorizontalSpeed = 18.0f;

    [Tooltip("중력 가속도 배율")]
    public float gravityScale = 1.45f;

    [Tooltip("공기 저항 감쇠")]
    public float airDrag = 0.998f;

    [Header("비행 시 비주얼 연출")]
    [Tooltip("비행 중 돌의 시각적 확대 배율 (기본: 1.0f 원본 크기 유지, 필요시 확대 가능)")]
    public float inFlightVisualScale = 1.0f;

    [Header("타이밍 판정 관용도 (Time-to-Impact 기준)")]
    [Tooltip("타이밍 알림 및 판정이 시작되는 수면 위 높이 (m)")]
    public float timingWindowHeight = 2.8f;

    [Tooltip("PERFECT 판정 기준 착수 잔여 시간 (초, 표준 리듬게임 100ms)")]
    public float perfectWindowTime = 0.100f;

    [Tooltip("GREAT 판정 기준 착수 잔여 시간 (초, 표준 리듬게임 220ms)")]
    public float greatWindowTime = 0.220f;

    [Tooltip("GOOD 판정 기준 착수 잔여 시간 (초, 표준 리듬게임 380ms)")]
    public float goodWindowTime = 0.380f;

    [Tooltip("PERFECT 판정 기준 거리 (참조용 m)")]
    public float perfectDistance = 0.70f;

    [Tooltip("GREAT 판정 기준 거리 (참조용 m)")]
    public float greatDistance = 1.45f;

    [Tooltip("GOOD 판정 기준 거리 (참조용 m)")]
    public float goodDistance = 2.40f;

    [Header("마지막 '도로록~' 스키밍 피니시 설정")]
    [Tooltip("스키밍 피니시 발동 최소 스킵 횟수")]
    public int minSkimSkips = 5;

    [Tooltip("최대 스키밍 효과 도달 스킵 횟수 (30회 이상 시 최대 효과)")]
    public int maxSkimSkips = 30;

    [Header("비주얼 및 트레일")]
    public TrailRenderer trail;
    public Material trailCustomMaterial;
    public Material stoneCustomMaterial;
    public Color trailStartColor = new Color(0.25f, 0.85f, 1.0f, 0.40f);
    public Color trailEndColor = new Color(0.15f, 0.70f, 1.0f, 0f);

    [Header("🎯 리듬 링 비주얼 세부 설정")]
    [Tooltip("수면 링의 선 두께")]
    public float ringLineWidth = 0.032f;
    [Tooltip("퍼펙트 타깃 링의 기본 반경(m)")]
    public float ringTargetRadius = 0.29f;
    [Tooltip("바깥 수축 링의 시작 최대 배율")]
    public float ringMaxMultiplier = 5.2f;
    [Tooltip("돌-수면 수직 가이드 레이저 선 두께")]
    public float dropLineWidth = 0.006f;

    [Header("상태 모니터링")]
    public bool isThrown = false;
    public bool isSunk = false;
    public bool isCrashed = false;
    public bool isSkimming = false;
    public bool isGodMode = false;
    public float godModeTargetDistance = 0f; // 0이면 무제한, 지정 거리(m) 도달 시 자연스럽게 멈춤
    public int skipCount = 0;
    public float totalDistance = 0f;
    public float skimDistance = 0f;
    public bool isInTimingWindow = false;

    private bool hasTappedInCurrentBounce = false;
    private int earlyRetryCount = 0; // 하강 1회당 TOO EARLY 실수 만회 허용 횟수 (최대 1회)
    private Vector3 skimStartPos;
    private float skimDuration = 0f;
    private float maxSkimDuration = 0f;
    private float skimDecelRate = 0.97f;
    private float skimSplashTimer = 0f;

    [System.Serializable]
    public struct BounceRecord
    {
        public Vector3 position;
        public int skipIndex;
        public string grade;
        public float distance;
    }
    public System.Collections.Generic.List<BounceRecord> bounceHistory = new System.Collections.Generic.List<BounceRecord>();

    public event Action<int, string> OnSkipBounced;
    public event Action<float> OnStoneSunk;

    private Rigidbody rb;
    private Vector3 startPosition;
    public float waterLevel = 0f;

    private float currentPitchAngle = 0f;
    private float currentSpinAngle = 0f;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        if (rb == null)
        {
            Debug.LogWarning($"[SkippingStone] '{gameObject.name}' 프리팹에 Rigidbody 컴포넌트가 없습니다! 에디터 인스펙터에서 Rigidbody를 추가해주세요.");
        }
        else
        {
            rb.useGravity = false;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
        }
        startPosition = transform.position;

        SetupVisualModel();
        SetupTrail();
        EnsureRhythmRing();
        UpdateWaterLevel();
    }

    public void UpdateWaterLevel()
    {
        // WaterSurface 컴포넌트 우선 탐색 (이름 무관)
        WaterSurface ws = FindAnyObjectByType<WaterSurface>();
        if (ws != null)
        {
            Collider c = ws.GetComponent<Collider>();
            waterLevel = (c != null) ? c.bounds.max.y : ws.transform.position.y;
            return;
        }

        GameObject water = GameObject.Find("WaterSurface") ?? GameObject.Find("Water_Surface");
        if (water != null)
        {
            Collider col = water.GetComponent<Collider>();
            waterLevel = (col != null) ? col.bounds.max.y : water.transform.position.y;
        }
    }

    private void SetupVisualModel()
    {
        var rootRenderer = GetComponent<MeshRenderer>();
        var rootFilter = GetComponent<MeshFilter>();
        if (rootFilter != null && rootFilter.sharedMesh != null && (rootFilter.sharedMesh.name.Contains("Sphere") || rootFilter.sharedMesh.name.Contains("Pebble")))
        {
            if (Application.isPlaying)
            {
                Destroy(rootRenderer);
                Destroy(rootFilter);
            }
            else
            {
                DestroyImmediate(rootRenderer);
                DestroyImmediate(rootFilter);
            }
        }

        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Transform child = transform.GetChild(i);
            if (child.name.StartsWith("StoneModel_Fallback") || child.name.Contains("Fallback") || child.name.Contains("Sphere"))
            {
                if (Application.isPlaying) Destroy(child.gameObject);
                else DestroyImmediate(child.gameObject);
            }
        }

        var existingStoneModel = transform.Find("StoneModel_ZeroOffset");
        if (existingStoneModel == null)
        {
            for (int i = 0; i < transform.childCount; i++)
            {
                Transform child = transform.GetChild(i);
                if (child.GetComponentInChildren<MeshFilter>() != null)
                {
                    existingStoneModel = child;
                    break;
                }
            }
        }

        if (existingStoneModel == null)
        {
            GameObject prefab = customStonePrefab;
            if (prefab == null) prefab = Resources.Load<GameObject>("Stone");
#if UNITY_EDITOR
            if (prefab == null) prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/3D/prefab/Stone.prefab");
#endif
            if (prefab != null)
            {
                transform.localScale = Vector3.one;
                GameObject stoneInstance = Instantiate(prefab, transform);
                stoneInstance.name = "StoneModel_ZeroOffset";
                stoneInstance.transform.localPosition = Vector3.zero;
                stoneInstance.transform.localRotation = Quaternion.identity;
                stoneInstance.transform.localScale = Vector3.one;

                foreach (var col in stoneInstance.GetComponentsInChildren<Collider>(true))
                {
                    if (Application.isPlaying) Destroy(col);
                    else DestroyImmediate(col);
                }
            }
        }
    }

    private void SetupTrail()
    {
        if (trail == null)
        {
            trail = GetComponent<TrailRenderer>();
            if (trail == null)
            {
                Debug.LogWarning($"[SkippingStone] '{gameObject.name}'에 TrailRenderer 컴포넌트가 없습니다. 트레일 연출을 원하시면 프리팹에 TrailRenderer를 추가해주세요.");
                return;
            }
        }

        trail.time = 0.38f;
        trail.startWidth = 0.045f;
        trail.endWidth = 0.002f;
        trail.minVertexDistance = 0.06f;
        trail.textureMode = LineTextureMode.Stretch;
        trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        trail.receiveShadows = false;

        if (trailCustomMaterial != null)
        {
            trail.material = trailCustomMaterial;
        }
        else
        {
            Material loaded = Resources.Load<Material>("StoneTrail_Mat");
            if (loaded != null)
            {
                trailCustomMaterial = loaded;
                trail.material = loaded;
            }
            else
            {
                Shader trailShader = Shader.Find("Universal Render Pipeline/Particles/Unlit")
                                     ?? Shader.Find("Sprites/Default")
                                     ?? Shader.Find("Legacy Shaders/Particles/Alpha Blended Premultiply");
                if (trailShader != null)
                {
                    trail.material = new Material(trailShader);
                }
            }
        }

        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new GradientColorKey[] { new GradientColorKey(trailStartColor, 0.0f), new GradientColorKey(trailEndColor, 1.0f) },
            new GradientAlphaKey[] { new GradientAlphaKey(0.40f, 0.0f), new GradientAlphaKey(0.0f, 1.0f) }
        );
        trail.colorGradient = gradient;
        trail.emitting = true;
    }

    private void EnsureRhythmRing()
    {
        if (GetComponent<RhythmRingIndicator>() == null)
        {
            Debug.LogWarning($"[SkippingStone] '{gameObject.name}'에 RhythmRingIndicator 컴포넌트가 없습니다. 리듬 링 판정 연출을 원하시면 프리팹에 RhythmRingIndicator를 추가해주세요.");
        }
    }

    private void Start()
    {
        UpdateWaterLevel();
    }

    private void Update()
    {
        if (!isThrown || isSunk || isCrashed || isSkimming) return;

        Vector3 v = (rb != null) ? rb.linearVelocity : Vector3.zero;
        Vector3 hVel = new Vector3(v.x, 0f, v.z);
        if (hVel.sqrMagnitude > 0.05f)
        {
            Vector3 hDir = hVel.normalized;
            float vy = v.y;
            float targetPitch = Mathf.Clamp(vy * 6.5f, -36f, 45f);
            currentPitchAngle = Mathf.Lerp(currentPitchAngle, targetPitch, Time.deltaTime * 14f);
            currentSpinAngle = (currentSpinAngle + 1440f * Time.deltaTime) % 360f;

            Quaternion headingRot = Quaternion.LookRotation(hDir, Vector3.up);
            Quaternion pitchRot = Quaternion.Euler(-currentPitchAngle, 0f, 0f);
            Quaternion spinRot = Quaternion.Euler(0f, currentSpinAngle, 0f);

            transform.rotation = headingRot * pitchRot * spinRot;
        }
    }

    public void ResetStoneState()
    {
        StopAllCoroutines();
        scaleGrowCoroutine = null;

        for (int i = 0; i < transform.childCount; i++)
        {
            Transform c = transform.GetChild(i);
            if (c.GetComponentInChildren<MeshFilter>() != null)
            {
                c.localScale = Vector3.one;
            }
        }

        currentPitchAngle = 0f;
        currentSpinAngle = 0f;

        isThrown = false;
        isSunk = false;
        isCrashed = false;
        isSkimming = false;
        skipCount = 0;
        totalDistance = 0f;
        skimDistance = 0f;
        hasTappedInCurrentBounce = false;
        earlyRetryCount = 0;
        isInTimingWindow = false;

        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.isKinematic = true;
            rb.useGravity = false;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
        }

        if (trail != null)
        {
            trail.Clear();
            trail.time = 0.38f;
            trail.startWidth = 0.045f;
            trail.endWidth = 0.002f;
            trail.minVertexDistance = 0.06f;
            trail.textureMode = LineTextureMode.Stretch;

            Gradient g = new Gradient();
            g.SetKeys(
                new GradientColorKey[] { new GradientColorKey(trailStartColor, 0.0f), new GradientColorKey(trailEndColor, 1.0f) },
                new GradientAlphaKey[] { new GradientAlphaKey(0.40f, 0.0f), new GradientAlphaKey(0.0f, 1.0f) }
            );
            trail.colorGradient = g;
        }
    }

    public void Launch(Vector3 direction, float powerMultiplier)
    {
        StopAllCoroutines();
        UpdateWaterLevel();

        isThrown = true;
        isSunk = false;
        isCrashed = false;
        isSkimming = false;
        skipCount = 0;
        skimDistance = 0f;
        hasTappedInCurrentBounce = false;
        earlyRetryCount = 0;
        isInTimingWindow = false;
        waterSubmergeTimer = 0f;
        currentPitchAngle = 0f;
        currentSpinAngle = 0f;

        rb.isKinematic = false;
        rb.useGravity = true;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

        rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;

        float clampedPower = Mathf.Clamp(powerMultiplier, 0.4f, 1.5f);
        float finalSpeed = forwardPower * clampedPower;

        float upwardSpeed = initialUpwardForce * Mathf.Clamp(powerMultiplier, 0.8f, 1.3f);
        rb.linearVelocity = (direction.normalized * finalSpeed) + (Vector3.up * upwardSpeed);

        if (AudioManager.Instance != null) AudioManager.Instance.PlaySound(SoundType.ThrowWhoosh);
        HapticFeedbackHelper.TriggerLightTap();

        bounceHistory.Clear();
        bounceHistory.Add(new BounceRecord { position = transform.position, skipIndex = 0, grade = "START", distance = 0f });

        if (trail != null) trail.Clear();
        StartVisualGrowth();
    }

    private Coroutine scaleGrowCoroutine;

    private void StartVisualGrowth()
    {
        if (scaleGrowCoroutine != null) StopCoroutine(scaleGrowCoroutine);
        scaleGrowCoroutine = StartCoroutine(GrowVisualScaleRoutine());
    }

    private IEnumerator GrowVisualScaleRoutine()
    {
        Transform visualChild = null;
        for (int i = 0; i < transform.childCount; i++)
        {
            Transform c = transform.GetChild(i);
            if (c.GetComponentInChildren<MeshFilter>() != null)
            {
                visualChild = c;
                break;
            }
        }
        if (visualChild == null) yield break;

        visualChild.localScale = Vector3.one;

        // 배율이 1.0f에 가까우면 부드럽게 유지
        if (Mathf.Abs(inFlightVisualScale - 1.0f) < 0.01f)
        {
            visualChild.localScale = Vector3.one;
            yield break;
        }

        float elapsed = 0f;
        float duration = 0.8f;
        float targetScale = inFlightVisualScale;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float rawT = Mathf.Clamp01(elapsed / duration);
            float easeProgress = Mathf.Pow(rawT, 2.0f);
            float s = Mathf.Lerp(1.0f, targetScale, easeProgress);
            if (visualChild != null)
            {
                visualChild.localScale = new Vector3(s, s, s);
            }
            yield return null;
        }

        if (visualChild != null)
        {
            visualChild.localScale = new Vector3(targetScale, targetScale, targetScale);
        }
    }

    public float GetDynamicBounceForce(int currentSkip)
    {
        float progress = Mathf.Clamp01((currentSkip - 1) / 32f);
        float decayFactor = Mathf.Lerp(1.0f, 0.38f, Mathf.Sqrt(progress));
        return baseBounceUpForce * decayFactor;
    }

    public float GetCurrentPerfectDistance()
    {
        float p = Mathf.Clamp01(skipCount / 30f);
        return Mathf.Lerp(perfectDistance, 0.36f, p);
    }

    public float GetCurrentGreatDistance()
    {
        float p = Mathf.Clamp01(skipCount / 30f);
        return Mathf.Lerp(greatDistance, 0.78f, p);
    }

    public float GetCurrentGoodDistance()
    {
        float p = Mathf.Clamp01(skipCount / 30f);
        return Mathf.Lerp(goodDistance, 1.35f, p);
    }

    private float waterSubmergeTimer = 0f;
    private const float LATE_GRACE_WINDOW = 0.010f;

    private void FixedUpdate()
    {
        if (!isThrown || isSunk || isCrashed) return;

        if (isSkimming)
        {
            UpdateSkimming();
            return;
        }

        rb.AddForce(Physics.gravity * (gravityScale - 1f), ForceMode.Acceleration);
        rb.linearVelocity = new Vector3(rb.linearVelocity.x * airDrag, rb.linearVelocity.y, rb.linearVelocity.z * airDrag);

        totalDistance = Vector2.Distance(new Vector2(startPosition.x, startPosition.z), new Vector2(transform.position.x, transform.position.z));

        // 🌟 갓모드 곡선 추적: 베이크된 강줄기 스플라인(GlobalRiverPath)을 따라 부드럽게 진행 방향 유도
        if (isGodMode && GlobalRiverPath.Instance != null)
        {
            if (GlobalRiverPath.Instance.EvaluateAtDistance(totalDistance, out Vector3 riverCenterPos, out Vector3 riverTangent, out _, out float riverWaterY))
            {
                waterLevel = riverWaterY;

                Vector2 currentHVel = new Vector2(rb.linearVelocity.x, rb.linearVelocity.z);
                float speed = Mathf.Max(forwardPower * 0.8f, currentHVel.magnitude);

                // 강 중심선으로부터의 X/Z 오프셋을 중심선 쪽으로 부드럽게 복원 유도
                Vector3 toCenter = riverCenterPos - transform.position;
                Vector3 desiredHDir = (riverTangent + (toCenter * 0.18f)).normalized;
                Vector2 targetHDir = new Vector2(desiredHDir.x, desiredHDir.z).normalized;

                Vector2 newHDir = Vector2.Lerp(currentHVel.normalized, targetHDir, Time.fixedDeltaTime * 6.5f).normalized;
                rb.linearVelocity = new Vector3(newHDir.x * speed, rb.linearVelocity.y, newHDir.y * speed);

                float targetYaw = Mathf.Atan2(newHDir.x, newHDir.y) * Mathf.Rad2Deg;
                transform.rotation = Quaternion.Euler(0f, targetYaw, 0f);
            }
        }

        // 🌟 [수정] 수면 높이(waterLevel)를 기준으로 한 상대 높이 계산
        float distToWater = transform.position.y - waterLevel;
        float dynWindowHeight = Mathf.Lerp(timingWindowHeight, 1.4f, Mathf.Clamp01(skipCount / 30f));

        // 🌟 [핵심] 실제 돌 발밑에 WaterSurface가 존재하는지 검증 (허공 튕김 원천 차단)
        bool hasWaterBelow = CheckWaterUnderneath();

        // 수면 위 dynWindowHeight 이내로 접근하고 하강 중일 때 타이밍 윈도우 활성화
        if (hasWaterBelow && distToWater <= dynWindowHeight && distToWater >= -0.1f && rb.linearVelocity.y < 0.5f)
        {
            isInTimingWindow = true;
        }
        else
        {
            isInTimingWindow = false;
        }

        // 🌟 돌이 바운스되어 상승 중일 때 다음 하강을 위해 탭 상태 안전 리셋
        if (rb.linearVelocity.y > 0.5f)
        {
            hasTappedInCurrentBounce = false;
            earlyRetryCount = 0;
            waterSubmergeTimer = 0f;
        }

        // 🌟 갓모드 / 오토 바운스: 실제 물 위에서만 완벽한 퍼펙트 바운스 발동
        if (isGodMode && hasWaterBelow && distToWater <= 0.15f && rb.linearVelocity.y <= 0f)
        {
            // 목표 테스트 거리에 도달한 경우 바운스를 멈추고 스키밍 피니시 / 착수
            if (godModeTargetDistance > 0f && totalDistance >= godModeTargetDistance)
            {
                if (skipCount >= minSkimSkips && !isSkimming)
                {
                    StartSkimmingFinish();
                }
                else
                {
                    Sink($"테스트 목표 거리 ({godModeTargetDistance:F0}m) 도달 완료");
                }
                return;
            }

            TryRhythmBounce(0f, out _);
            return;
        }

        // 수면 착수 체크 (바운스 성공하지 못하고 수면에 도달했거나 발밑이 허공일 때)
        if (distToWater <= -0.04f && rb.linearVelocity.y <= 0f)
        {
            if (!hasWaterBelow)
            {
                // 물길 밖(강둑/지형)으로 날아가 착지한 경우 즉시 착지 피니시
                CrashOnLand("물길 이탈 / 지형 착지");
                return;
            }

            if (waterSubmergeTimer <= 0f)
            {
                waterSubmergeTimer = Time.time;
            }

            if (Time.time - waterSubmergeTimer > LATE_GRACE_WINDOW)
            {
                if (skipCount >= minSkimSkips && !isSkimming)
                {
                    StartSkimmingFinish();
                }
                else
                {
                    Sink("MISS - 타이밍 탭 실패!");
                }
            }
        }
        else if (distToWater > 0.1f)
        {
            waterSubmergeTimer = 0f;
        }

        if (distToWater < -1.2f)
        {
            Sink("침몰");
        }
    }

    public bool TryRhythmBounce(out string timingGrade)
    {
        return TryRhythmBounce(0f, out timingGrade);
    }

    public bool TryRhythmBounce(float steerAngleDegrees, out string timingGrade)
    {
        timingGrade = "";
        if (!isThrown || isSunk || isCrashed || isSkimming) return false;

        float distToWater = transform.position.y - waterLevel;
        float dynWindowHeight = Mathf.Lerp(timingWindowHeight, 1.4f, Mathf.Clamp01(skipCount / 30f));

        // 1. 방안 B: 타이밍 윈도우 진입 전(높은 상공)이거나 상승 중일 때의 탭은 무시 (소모하지 않음)
        if (distToWater > dynWindowHeight || rb.linearVelocity.y > 0.4f)
        {
            timingGrade = "";
            return false;
        }

        // 🌟 실제 돌 발밑에 물이 없으면 바운스 불가 (추락 유도)
        if (!CheckWaterUnderneath())
        {
            timingGrade = "NO WATER";
            return false;
        }

        // 2. 이미 이번 하강에서 탭을 소모한 경우 연타 차단
        if (hasTappedInCurrentBounce)
        {
            timingGrade = "ALREADY TAPPED";
            return false;
        }

        // 3. 타이밍 윈도우 진입 후 첫 탭 -> 즉시 이번 하강 1회 기회 소모!
        hasTappedInCurrentBounce = true;

        float verticalSpeed = Mathf.Max(0.5f, -rb.linearVelocity.y);
        float timeToImpact = Mathf.Max(0f, distToWater) / verticalSpeed;

        float bounceForce = GetDynamicBounceForce(skipCount + 1);
        float speedMultiplier = 1.0f;

        if (distToWater <= 0.02f)
        {
            timingGrade = "⚠️ LATE";
            bounceForce *= 0.85f;
            speedMultiplier = 0.90f;
        }
        else if (timeToImpact <= perfectWindowTime)
        {
            timingGrade = "🔥 PERFECT! 🔥";
            bounceForce *= 1.25f;
            speedMultiplier = 1.08f;
        }
        else if (timeToImpact <= greatWindowTime)
        {
            timingGrade = "⚡ GREAT! ⚡";
            bounceForce *= 1.10f;
            speedMultiplier = 1.02f;
        }
        else if (timeToImpact <= goodWindowTime)
        {
            timingGrade = "✨ GOOD";
            bounceForce *= 0.92f;
            speedMultiplier = 0.95f;
        }
        else
        {
            // 타이밍 윈도우 내이지만 착수까지 너무 많이 남음 (Too Early)
            earlyRetryCount++;
            if (earlyRetryCount <= 1)
            {
                // 1회차 Too Early 실수: 착수 타이밍에 맞춰 다시 제대로 누를 수 있도록 구제 (1회 재도전 기회 부여)
                hasTappedInCurrentBounce = false;
                timingGrade = "💦 TOO EARLY";
            }
            else
            {
                // 2회차 이상 막누름: 탭 기회 영구 소모 (연타 차단 및 침몰 유도)
                hasTappedInCurrentBounce = true;
                timingGrade = "💦 TOO EARLY (기회 소모!)";
            }
            return false;
        }

        waterSubmergeTimer = 0f;
        skipCount++;

        int comboTier = Mathf.Min(3, skipCount / 5);
        if (comboTier > 0)
        {
            float comboBonus = 1.0f + (comboTier * 0.06f);
            bounceForce *= comboBonus;
            speedMultiplier *= comboBonus;

            if (skipCount % 5 == 0)
            {
                timingGrade += $" \n★ {skipCount} COMBO BONUS! (+{comboTier * 6}%) ★";
            }
        }

        Vector2 hVel = new Vector2(rb.linearVelocity.x, rb.linearVelocity.z);
        float currentHSpd = hVel.magnitude;
        float newHSpd = Mathf.Min(maxHorizontalSpeed, currentHSpd * speedMultiplier);
        Vector2 hDir = hVel.normalized;
        if (hDir == Vector2.zero) hDir = Vector2.up;

        if (isGodMode && GlobalRiverPath.Instance != null)
        {
            if (GlobalRiverPath.Instance.EvaluateAtDistance(totalDistance, out Vector3 riverCenterPos, out Vector3 riverTangent, out _, out _))
            {
                Vector3 toCenter = riverCenterPos - transform.position;
                Vector3 desired = (riverTangent + (toCenter * 0.20f)).normalized;
                hDir = new Vector2(desired.x, desired.z).normalized;
            }
        }

        if (Mathf.Abs(steerAngleDegrees) > 0.01f)
        {
            Quaternion rot = Quaternion.Euler(0f, steerAngleDegrees, 0f);
            Vector3 rotated3D = rot * new Vector3(hDir.x, 0f, hDir.y);
            hDir = new Vector2(rotated3D.x, rotated3D.z).normalized;

            if (steerAngleDegrees < 0f) timingGrade += $" ◀ LEFT {Mathf.Abs(steerAngleDegrees):F0}°";
            else timingGrade += $" RIGHT {steerAngleDegrees:F0}° ▶";
        }

        rb.linearVelocity = new Vector3(hDir.x * newHSpd, bounceForce, hDir.y * newHSpd);
        transform.position = new Vector3(transform.position.x, waterLevel + 0.10f, transform.position.z);

        float newYaw = Mathf.Atan2(hDir.x, hDir.y) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0f, newYaw, 0f);
        rb.angularVelocity = new Vector3(0f, 45f, 0f);

        if (SplashEffectSpawner.Instance != null)
        {
            float splashScale = Mathf.Lerp(1.2f, 2.0f, Mathf.Clamp01(skipCount / 30f));
            SplashEffectSpawner.Instance.SpawnSplash(transform.position, (timingGrade.Contains("PERFECT")) ? splashScale : splashScale * 0.75f);
        }

        if (timingGrade.Contains("PERFECT"))
        {
            if (AudioManager.Instance != null) AudioManager.Instance.PlaySound(SoundType.BouncePerfect, 1.15f);
            HapticFeedbackHelper.TriggerPerfectImpact();
        }
        else if (timingGrade.Contains("GREAT") || timingGrade.Contains("GOOD"))
        {
            if (AudioManager.Instance != null) AudioManager.Instance.PlaySound(SoundType.BounceGood, 1.0f);
            HapticFeedbackHelper.TriggerMediumBounce();
        }
        else
        {
            if (AudioManager.Instance != null) AudioManager.Instance.PlaySound(SoundType.BounceWater, 0.9f);
            HapticFeedbackHelper.TriggerMediumBounce();
        }

        bounceHistory.Add(new BounceRecord { position = transform.position, skipIndex = skipCount, grade = timingGrade, distance = totalDistance });
        OnSkipBounced?.Invoke(skipCount, timingGrade);
        return true;
    }

    public void RecordBoostPadHit(Vector3 padPos)
    {
        bounceHistory.Add(new BounceRecord
        {
            position = new Vector3(padPos.x, transform.position.y, padPos.z),
            skipIndex = skipCount,
            grade = "BOOST_PAD",
            distance = totalDistance
        });
    }

    public void ApplySteerAngle(float steerAngleDegrees)
    {
        if (!isThrown || isSunk || isCrashed || isSkimming) return;

        Vector2 hVel = new Vector2(rb.linearVelocity.x, rb.linearVelocity.z);
        float spd = hVel.magnitude;
        if (spd < 0.1f) return;

        Quaternion rot = Quaternion.Euler(0f, steerAngleDegrees, 0f);
        Vector3 rotated3D = rot * new Vector3(hVel.x, 0f, hVel.y);
        Vector2 newHDir = new Vector2(rotated3D.x, rotated3D.z).normalized;

        rb.linearVelocity = new Vector3(newHDir.x * spd, rb.linearVelocity.y, newHDir.y * spd);

        float newYaw = Mathf.Atan2(newHDir.x, newHDir.y) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0f, newYaw, 0f);
    }

    public void StartSkimmingFinish()
    {
        if (isSkimming || isSunk || isCrashed) return;
        isSkimming = true;
        isInTimingWindow = false;
        hasTappedInCurrentBounce = true;
        skimStartPos = transform.position;
        skimDistance = 0f;
        skimDuration = 0f;
        skimSplashTimer = 0f;

        bounceHistory.Add(new BounceRecord { position = transform.position, skipIndex = skipCount, grade = "SKIM_START", distance = totalDistance });

        float skimRatio = Mathf.Clamp01((float)(skipCount - minSkimSkips) / Mathf.Max(1, (maxSkimSkips - minSkimSkips)));
        maxSkimDuration = Mathf.Lerp(0.9f, 3.2f, skimRatio);
        skimDecelRate = Mathf.Lerp(0.945f, 0.983f, skimRatio);

        transform.position = new Vector3(transform.position.x, waterLevel + 0.04f, transform.position.z);
        rb.useGravity = false;

        transform.rotation = Quaternion.Euler(0f, transform.rotation.eulerAngles.y, 0f);
        rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;

        Vector2 hVel = new Vector2(rb.linearVelocity.x, rb.linearVelocity.z);
        float hSpeed = Mathf.Max(7f, hVel.magnitude);
        Vector2 hDir = (hSpeed > 0.01f) ? hVel.normalized : Vector2.up;
        rb.linearVelocity = new Vector3(hDir.x * hSpeed, 0f, hDir.y * hSpeed);
        rb.angularVelocity = new Vector3(0f, 60f, 0f);

        GameController gc = UnityEngine.Object.FindAnyObjectByType<GameController>();
        if (gc != null)
        {
            gc.lastTimingText = "🌊 도로록~!";
            gc.bannerNotificationText = $"🌊 도로록~ 스키밍 피니시 발동! ({skipCount}스킵)";
        }

        if (AudioManager.Instance != null) AudioManager.Instance.PlaySound(SoundType.SkimSlide, 1.0f);
        HapticFeedbackHelper.TriggerMediumBounce();
    }

    private void UpdateSkimming()
    {
        skimDuration += Time.fixedDeltaTime;

        Vector3 v = rb.linearVelocity;
        v.x *= skimDecelRate;
        v.z *= skimDecelRate;
        float currentSpeed = new Vector2(v.x, v.z).magnitude;

        float bobbingY = waterLevel + 0.035f + Mathf.Sin(skimDuration * 38f) * 0.02f;
        transform.position = new Vector3(transform.position.x, bobbingY, transform.position.z);
        v.y = 0f;
        rb.linearVelocity = v;

        transform.rotation = Quaternion.Euler(0f, transform.rotation.eulerAngles.y, 0f);
        rb.angularVelocity = new Vector3(0f, 60f, 0f);

        skimDistance = Vector2.Distance(new Vector2(skimStartPos.x, skimStartPos.z), new Vector2(transform.position.x, transform.position.z));
        totalDistance = Vector2.Distance(new Vector2(startPosition.x, startPosition.z), new Vector2(transform.position.x, transform.position.z));

        skimSplashTimer += Time.fixedDeltaTime;
        if (skimSplashTimer >= 0.11f && currentSpeed > 1.8f)
        {
            skimSplashTimer = 0f;
            if (SplashEffectSpawner.Instance != null)
            {
                SplashEffectSpawner.Instance.SpawnSplash(transform.position, Mathf.Lerp(0.35f, 1.0f, currentSpeed / 18f));
            }
        }

        GameController gc = UnityEngine.Object.FindAnyObjectByType<GameController>();
        if (gc != null)
        {
            gc.bannerNotificationText = $"🌊 도로록~ 스키밍 피니시! (+{skimDistance:F1}m 보너스)";
        }

        if (skimDuration >= maxSkimDuration || currentSpeed < 0.9f)
        {
            Sink($"도로록~ 스키밍 완료 (+{skimDistance:F1}m)");
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (!isThrown || isSunk || isCrashed) return;

        string hitName = collision.gameObject.name.ToLower();
        bool isRock = hitName.Contains("rock") || hitName.Contains("obstacle");
        CrashOnLand(isRock ? "바위 장애물 충돌" : "지형 착지", isRockObstacle: isRock);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!isThrown || isSunk || isCrashed) return;

        // 1. WaterSurface 컴포넌트 검사로 수면 감지
        WaterSurface ws = other.GetComponent<WaterSurface>() ?? other.GetComponentInParent<WaterSurface>();
        if (ws != null)
        {
            waterLevel = other.bounds.max.y;
            if (!hasTappedInCurrentBounce && rb.linearVelocity.y <= 0f)
            {
                if (isGodMode)
                {
                    TryRhythmBounce(0f, out _);
                    return;
                }

                if (skipCount >= minSkimSkips && !isSkimming) StartSkimmingFinish();
                else Sink("수면 착수 - 탭 미입력");
            }
            return;
        }

        // 2. Terrain 컴포넌트 검사로 지형/바위 감지
        bool isTerrain = other.GetComponent<TerrainCollider>() != null || other.GetComponent<Terrain>() != null || other.GetComponent<MeshCollider>() != null;
        bool isRock = other.name.ToLower().Contains("rock") || other.name.ToLower().Contains("obstacle") || other.name.ToLower().Contains("ground") || other.name.ToLower().Contains("bank");

        if (isTerrain || isRock)
        {
            CrashOnLand(isRock ? "바위 장애물 충돌" : "지형 착지", isRockObstacle: isRock);
        }
    }

    public void CrashOnLand(string reason = "땅에 충돌 - 게임 오버", bool isRockObstacle = false)
    {
        if (isSunk || isCrashed) return;
        isCrashed = true;
        isThrown = false;
        isInTimingWindow = false;
        hasTappedInCurrentBounce = true;

        GameController gc = UnityEngine.Object.FindAnyObjectByType<GameController>();
        if (gc != null)
        {
            gc.lastTimingText = isRockObstacle ? "💥 바위 장애물 충돌! (Crash!)" : "💥 지형 충돌! (Crash on Land)";
            gc.bannerNotificationText = isRockObstacle ? "⚠️ 바위에 부딪혀 튕겨 오른 후 수면으로 가라앉습니다!" : "⚠️ 지형에 부딪혀 튕겨 오른 후 바닥에 착지했습니다!";
        }

        if (SplashEffectSpawner.Instance != null)
        {
            if (isRockObstacle) SplashEffectSpawner.Instance.SpawnCrashDustFX(transform.position, 1.2f);
            else SplashEffectSpawner.Instance.SpawnCrashDustFX(transform.position, 1.5f);
        }

        StartCoroutine(CrashBounceRoutine(reason, isRockObstacle));
    }

    private IEnumerator CrashBounceRoutine(string reason, bool isRockObstacle)
    {
        Vector2 hVel = new Vector2(rb.linearVelocity.x, rb.linearVelocity.z);
        Vector3 reboundDir = (hVel.sqrMagnitude > 0.1f) ? -new Vector3(hVel.x, 0f, hVel.y).normalized * 0.35f : Vector3.back * 0.35f;

        rb.useGravity = true;
        rb.constraints = RigidbodyConstraints.None;
        rb.linearVelocity = reboundDir * 3.6f + Vector3.up * 5.2f;
        rb.angularVelocity = new Vector3(UnityEngine.Random.Range(-12f, 12f), 8f, UnityEngine.Random.Range(-12f, 12f));

        yield return new WaitForSeconds(0.18f);

        float timeout = 4.0f;
        float elapsed = 0f;
        bool settled = false;

        while (elapsed < timeout && !settled)
        {
            elapsed += Time.deltaTime;
            Vector3 pos = transform.position;

            if (isRockObstacle)
            {
                if (pos.y <= waterLevel + 0.04f)
                {
                    settled = true;
                    if (SplashEffectSpawner.Instance != null)
                    {
                        SplashEffectSpawner.Instance.SpawnSplash(new Vector3(pos.x, waterLevel, pos.z), 1.0f);
                    }

                    rb.useGravity = false;
                    rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
                    rb.linearVelocity = new Vector3(rb.linearVelocity.x * 0.12f, -0.75f, rb.linearVelocity.z * 0.12f);
                    rb.angularVelocity = new Vector3(0f, 6f, 0f);

                    float sinkTimer = 0f;
                    while (sinkTimer < 0.9f)
                    {
                        sinkTimer += Time.deltaTime;
                        yield return null;
                    }
                    break;
                }
            }
            else
            {
                Ray ray = new Ray(pos + Vector3.up * 0.1f, Vector3.down);
                if (Physics.Raycast(ray, out RaycastHit hit, 0.22f, ~0, QueryTriggerInteraction.Ignore))
                {
                    if (hit.collider != null && hit.collider.gameObject != gameObject)
                    {
                        if (rb.linearVelocity.y <= 0.2f)
                        {
                            settled = true;
                            if (SplashEffectSpawner.Instance != null)
                            {
                                SplashEffectSpawner.Instance.SpawnCrashDustFX(pos, 0.7f);
                            }
                            break;
                        }
                    }
                }
                else if (pos.y <= waterLevel + 0.02f)
                {
                    settled = true;
                    if (SplashEffectSpawner.Instance != null)
                    {
                        SplashEffectSpawner.Instance.SpawnSplash(new Vector3(pos.x, waterLevel, pos.z), 0.8f);
                    }
                    rb.useGravity = false;
                    rb.linearVelocity = new Vector3(0f, -0.6f, 0f);
                    yield return new WaitForSeconds(0.8f);
                    break;
                }
            }

            yield return null;
        }

        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rb.isKinematic = true;

        bounceHistory.Add(new BounceRecord { position = transform.position, skipIndex = skipCount, grade = isRockObstacle ? "CRASH_ROCK" : "CRASH_LAND", distance = totalDistance });

        isSunk = true;
        OnStoneSunk?.Invoke(totalDistance);
    }

    public void Sink(string reason = "")
    {
        if (isGodMode || isSunk || isCrashed) return;
        isSunk = true;
        isSkimming = false;
        isInTimingWindow = false;

        StartCoroutine(WaterSinkRoutine(reason));
    }

    private IEnumerator WaterSinkRoutine(string reason)
    {
        rb.useGravity = false;
        rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;

        rb.linearVelocity = new Vector3(0f, -0.5f, 0f);
        rb.angularVelocity = new Vector3(0f, 6f, 0f);

        if (SplashEffectSpawner.Instance != null)
        {
            SplashEffectSpawner.Instance.SpawnSplash(transform.position, 0.8f);
        }

        if (AudioManager.Instance != null) AudioManager.Instance.PlaySound(SoundType.StoneSink, 1.0f);
        HapticFeedbackHelper.TriggerSink();

        bounceHistory.Add(new BounceRecord { position = transform.position, skipIndex = skipCount, grade = "FINISH", distance = totalDistance });

        OnStoneSunk?.Invoke(totalDistance);

        yield return new WaitForSeconds(0.4f);
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.isKinematic = true;
        }
    }

    /// <summary>
    /// 🌟 돌의 현재 X, Z 좌표 발밑에 실제 WaterSurface 콜라이더가 존재하는지 검증 (허공 튕김 원천 차단)
    /// </summary>
    public bool CheckWaterUnderneath()
    {
        // 1. 씬 내 모든 활성화된 WaterSurface 콜라이더의 X, Z 범위 검사
        var allWaters = FindObjectsByType<WaterSurface>(FindObjectsInactive.Exclude);
        if (allWaters != null && allWaters.Length > 0)
        {
            Vector3 pos = transform.position;
            foreach (var ws in allWaters)
            {
                if (ws == null || !ws.gameObject.activeInHierarchy) continue;
                Collider col = ws.GetComponent<Collider>();
                if (col != null)
                {
                    Bounds b = col.bounds;
                    // 돌의 X, Z 좌표가 실제 수면 콜라이더 바운드 영역 안에 들어있는지 정밀 확인
                    if (pos.x >= b.min.x && pos.x <= b.max.x && pos.z >= b.min.z && pos.z <= b.max.z)
                    {
                        return true;
                    }
                }
            }
            // 씬에 수면이 존재하는데 돌이 수면 바깥(허공/강변 밖)으로 나간 경우
            return false;
        }

        // 수면 컴포넌트가 하나도 없는 특수 상황 폴백 (기본 수면 높이 기준)
        return true;
    }
}