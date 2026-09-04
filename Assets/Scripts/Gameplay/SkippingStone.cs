using UnityEngine;
using System;
using System.Collections;
using SkippingStones.Terrain;
using SkippingStones.Gameplay;
using SkippingStones.Gameplay.Calculators;

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
    public float initialUpwardForce = 2.5f;

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
    public float ringLineWidth = 0.022f;
    [Tooltip("퍼펙트 타깃 링의 기본 반경(m)")]
    public float ringTargetRadius = 0.15f;
    [Tooltip("바깥 수축 링의 시작 최대 배율")]
    public float ringMaxMultiplier = 8.5f;
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

    [Header("🌊 물수제비 모멘텀 (스태미나/라이프) 시스템")]
    [Tooltip("현재 모멘텀 게이지 (0 이하 시 침몰)")]
    public float currentMomentum = 5.0f;
    public float maxMomentum = 10.0f;

    [Header("🪷 연잎(Lily Pad) 착수 3턴 높이 보너스")]
    public int lilyBonusRemainingTurns = 0; // 3턴 동안 +0.5, +0.3, +0.1 보너스 적용

    [Header("🌊 수면 대칭 반사 그림자 (Water Reflection Shadow)")]
    private readonly WaterReflectionShadowController shadowController = new WaterReflectionShadowController();

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

    [System.Serializable]
    public struct TapDebugRecord
    {
        public Vector3 stoneWorldPos;
        public float distToWater;
        public float verticalSpeed;
        public string grade;
        public int skipIndex;
        public float timeStamp;
    }
    [Header("🔍 입력 렉 및 타이밍 분석용 디버그 기록")]
    public System.Collections.Generic.List<TapDebugRecord> tapDebugHistory = new System.Collections.Generic.List<TapDebugRecord>();

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
        shadowController.Setup();
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

    private RhythmRingIndicator spawnedRhythmRing;

    private void EnsureRhythmRing()
    {
        if (spawnedRhythmRing == null)
        {
            spawnedRhythmRing = FindAnyObjectByType<RhythmRingIndicator>();
        }

        if (spawnedRhythmRing == null)
        {
            // 🌟 돌의 자식(Child)이 아닌 독립된 월드 루트 오브젝트로 생성하여 물리 회전 상속을 100% 원천 차단
            GameObject ringObj = new GameObject("[RhythmRingIndicator_WorldEffect]");
            spawnedRhythmRing = ringObj.AddComponent<RhythmRingIndicator>();
        }

        if (spawnedRhythmRing != null)
        {
            spawnedRhythmRing.stone = this;
            spawnedRhythmRing.arcadeStone = null;
        }
    }

    private void Start()
    {
        UpdateWaterLevel();
    }

    private void Update()
    {
        if (!isThrown || isSunk || isCrashed || isSkimming) return;

        if (rb != null)
        {
            SkippingStones.Gameplay.Calculators.StonePhysicsCalculator.CalculateFlightRotation(
                rb.linearVelocity, currentPitchAngle, currentSpinAngle, Time.deltaTime,
                out currentPitchAngle, out currentSpinAngle, out Quaternion finalRot
            );
            if (finalRot != Quaternion.identity)
            {
                transform.rotation = finalRot;
            }
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
        lilyBonusRemainingTurns = 0;
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
        lilyBonusRemainingTurns = 0;
        hasTappedInCurrentBounce = false;
        earlyRetryCount = 0;
        isInTimingWindow = false;
        waterSubmergeTimer = 0f;
        currentPitchAngle = 0f;
        currentSpinAngle = 0f;
        currentMomentum = 6.0f; // 기본 시작 모멘텀 게이지 (6.0 / 10.0)

        if (rb == null) rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = false;
            rb.useGravity = true;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
        }

        float clampedPower = Mathf.Clamp(powerMultiplier, 0.4f, 1.5f);
        float finalSpeed = forwardPower * clampedPower;

        float upwardSpeed = initialUpwardForce * Mathf.Clamp(powerMultiplier, 0.8f, 1.3f);
        if (rb != null)
        {
            rb.linearVelocity = (direction.normalized * finalSpeed) + (Vector3.up * upwardSpeed);
        }

        if (AudioManager.Instance != null) AudioManager.Instance.PlaySound(SoundType.ThrowWhoosh);
        HapticFeedbackHelper.TriggerLightTap();

        bounceHistory.Clear();
        bounceHistory.Add(new BounceRecord { position = transform.position, skipIndex = 0, grade = "START", distance = 0f });

        // 🌟 다시 던지기 시 이전 투구의 모든 디버그 마커 오브젝트 및 기록 초기화
        tapDebugHistory.Clear();
        ClearAllTapDebugMarkers();

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
        return SkippingStones.Gameplay.Calculators.StonePhysicsCalculator.GetDynamicBounceForce(baseBounceUpForce, currentSkip);
    }

    private float waterSubmergeTimer = 0f;
    private const float LATE_GRACE_WINDOW = 0.120f; // 수면에 닿은 직후 120ms 동안 LATE 판정 구제 윈도우

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

        // 🌟 갓모드 곡선 추적: Pure Pursuit (전방 15m Look-Ahead 타겟팅) 자율주행 유도로 좌우 진동 완전 차단
        if (isGodMode && GlobalRiverPath.Instance != null)
        {
            if (GlobalRiverPath.Instance.GetClosestPointOnRiver(transform.position, out Vector3 riverCenterPos, out _, out float distAlongRiver))
            {
                float lookAheadDist = distAlongRiver + 15f;
                if (GlobalRiverPath.Instance.EvaluateAtDistance(lookAheadDist, out Vector3 lookAheadPos, out Vector3 lookAheadTangent, out _, out float riverWaterY))
                {
                    waterLevel = riverWaterY;

                    Vector2 currentHVel = new Vector2(rb.linearVelocity.x, rb.linearVelocity.z);
                    float speed = Mathf.Max(forwardPower * 0.8f, currentHVel.magnitude);

                    Vector3 toTarget = lookAheadPos - transform.position;
                    toTarget.y = 0f;
                    Vector2 targetHDir = new Vector2(toTarget.x, toTarget.z).normalized;
                    if (targetHDir == Vector2.zero) targetHDir = new Vector2(lookAheadTangent.x, lookAheadTangent.z).normalized;

                    Vector2 smoothedHDir = Vector2.Lerp(currentHVel.normalized, targetHDir, Time.fixedDeltaTime * 6.5f).normalized;
                    rb.linearVelocity = new Vector3(smoothedHDir.x * speed, rb.linearVelocity.y, smoothedHDir.y * speed);
                }
            }
        }

        float distToWater = transform.position.y - waterLevel;

        // 🌟 자동 바운스 (갓모드)
        if (isGodMode && distToWater <= 0.22f && rb.linearVelocity.y <= 0.5f)
        {
            TryRhythmBounce(0f, out _);
            return;
        }

        // 🌟 돌이 위로 솟구치며 비행 중일 때(상승 궤적) 다음 바운스를 위해 탭 상태 및 서브머지 타이머 자동 리셋
        if (rb.linearVelocity.y > 0.1f)
        {
            hasTappedInCurrentBounce = false;
            earlyRetryCount = 0;
            waterSubmergeTimer = 0f;
        }

        // 수면 착수 체크 (바운스 성공하지 못하고 수면에 도달했을 때)
        // 🌟 모멘텀 스태미나 기반 자동 구제: 플레이어가 LATE(-20cm), TOO LATE(-32cm) 탭마저 놓쳐 심해(-34cm)에 도달했을 때 발동!
        if (distToWater <= -0.06f && rb.linearVelocity.y <= 0f)
        {
            if (waterSubmergeTimer <= 0f)
            {
                waterSubmergeTimer = Time.time;
            }

            // 플레이어가 수동 탭할 수 있는 시간(LATE/TOO LATE)을 충분히 보장한 후 심해(-0.34m) 또는 0.4초 경과 시 자동 구제
            if (distToWater <= -0.34f || (Time.time - waterSubmergeTimer > 0.40f))
            {
                // 모멘텀이 0보다 큰 경우: BAD(-3.0) 자동 회생 바운스 발동!
                if (currentMomentum > 0.1f)
                {
                    hasTappedInCurrentBounce = false; // 자동 회생 강제 허용
                    TryRhythmBounce(0f, out _);
                    return;
                }
                else
                {
                    // 모멘텀이 완전히 고갈된 경우 최종 침몰/피니시
                    if (skipCount >= minSkimSkips && !isSkimming)
                    {
                        StartSkimmingFinish();
                    }
                    else
                    {
                        Sink("MISS - 모멘텀 고갈 침몰!");
                    }
                }
            }
        }
        else if (distToWater > 0.05f)
        {
            waterSubmergeTimer = 0f;
        }

        if (distToWater < -1.2f)
        {
            Sink("침몰");
        }
    }

    private void LateUpdate()
    {
        shadowController.UpdateShadow(transform.position, waterLevel, isThrown && !isSunk && !isCrashed);
    }

    public bool TryRhythmBounce(out string timingGrade)
    {
        return TryRhythmBounce(0f, out timingGrade);
    }

    public bool TryRhythmBounce(float steerAngleDegrees, out string timingGrade)
    {
        timingGrade = "";
        if (!isThrown || isCrashed || isSkimming) return false;

        float distToWater = transform.position.y - waterLevel;
        float dynWindowHeight = Mathf.Lerp(timingWindowHeight, 1.4f, Mathf.Clamp01(skipCount / 30f));

        // 🌟 상승 중이거나 정점 통과 후 하강 시작 시 탭 기회 자동 갱신
        if (rb != null && rb.linearVelocity.y > 0.1f)
        {
            hasTappedInCurrentBounce = false;
            earlyRetryCount = 0;
        }

        // 1. 이미 완전히 침몰 완료된 상태에서는 탭 무시
        if (isSunk) return false;

        // 2. 타이밍 윈도우 진입 전(높은 상공)이거나 상승 중일 때의 탭은 무시
        if (distToWater > dynWindowHeight || (rb != null && rb.linearVelocity.y > 0.4f))
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

        // 2. 이미 이번 하강에서 탭을 소모한 경우: 마커는 기록하고 튕김만 차단
        if (hasTappedInCurrentBounce)
        {
            timingGrade = "⚠️ ALREADY TAPPED (연타 입력)";
            TapDebugRecord spamRec = new TapDebugRecord
            {
                stoneWorldPos = transform.position,
                distToWater = distToWater,
                verticalSpeed = (rb != null) ? -rb.linearVelocity.y : 0f,
                grade = timingGrade,
                skipIndex = skipCount,
                timeStamp = Time.time
            };
            tapDebugHistory.Add(spamRec);
            SpawnTapDebugMarker(spamRec);
            return false;
        }

        // 3. 타이밍 윈도우 진입 후 첫 탭 -> 즉시 이번 하강 1회 기회 소모!
        hasTappedInCurrentBounce = true;

        float bounceForce = GetDynamicBounceForce(skipCount + 1);
        float speedMultiplier = 1.0f;
        float momentumDelta = 0f;

        // 🌟 [디렉터님 확정] + / - 양방향 리듬 판정 및 모멘텀 게이지 시스템 (0점 수면 밀착 대칭 PERFECT!)
        var evalResult = StoneTimingEvaluator.Evaluate(distToWater, bounceForce);
        timingGrade = evalResult.grade;

        if (!evalResult.isSuccess)
        {
            if (evalResult.isEarlyRetry)
            {
                earlyRetryCount++;
                if (earlyRetryCount <= 1)
                {
                    hasTappedInCurrentBounce = false;
                }
                else
                {
                    hasTappedInCurrentBounce = true;
                    timingGrade = "💦 TOO EARLY (기회 소모!)";
                }
            }
            return false;
        }

        momentumDelta = evalResult.momentumDelta;
        if (evalResult.fixedBounceForce > 0f)
        {
            bounceForce = evalResult.fixedBounceForce;
        }
        else
        {
            bounceForce *= evalResult.bounceForceMultiplier;
        }
        speedMultiplier = evalResult.speedMultiplier;

        // 모멘텀 게이지 갱신
        currentMomentum = Mathf.Clamp(currentMomentum + momentumDelta, 0f, maxMomentum);

        // 게이지가 완전히 바닥났을 때: 5스킵 이상이면 도로록~ 스키밍 피니시, 미만이면 즉시 침몰
        if (currentMomentum <= 0.01f && (timingGrade.Contains("LATE") || timingGrade.Contains("BAD")))
        {
            if (skipCount >= minSkimSkips && !isSkimming)
            {
                StartSkimmingFinish();
            }
            else
            {
                Sink($"모멘텀 소진 침몰 ({timingGrade})");
            }
            return false;
        }

        // 🔍 입력 렉 및 탭 지점 정밀 디버그 기록
        float currentVSpeed = (rb != null) ? -rb.linearVelocity.y : 0f;
        TapDebugRecord debugRec = new TapDebugRecord
        {
            stoneWorldPos = transform.position,
            distToWater = distToWater,
            verticalSpeed = currentVSpeed,
            grade = timingGrade,
            skipIndex = skipCount,
            timeStamp = Time.time
        };
        tapDebugHistory.Add(debugRec);

        // 🌟 씬에 지워지지 않는 3D 디버그 마커 생성
        SpawnTapDebugMarker(debugRec);

        waterSubmergeTimer = 0f;
        skipCount++;

        // 🪷 연잎(Lily Pad) 착수 판정 (착수 반경 1.5m 이내에 LilyPad가 있을 때 3턴 보너스 충전)
        Collider[] nearbyCols = Physics.OverlapSphere(transform.position, 1.6f);
        LilyPad steppedLily = null;
        for (int i = 0; i < nearbyCols.Length; i++)
        {
            steppedLily = nearbyCols[i].GetComponentInParent<LilyPad>() ?? nearbyCols[i].GetComponent<LilyPad>();
            if (steppedLily != null) break;
        }

        if (steppedLily != null)
        {
            steppedLily.OnStepped();
            lilyBonusRemainingTurns = 3;
            currentMomentum = Mathf.Min(maxMomentum, currentMomentum + 1.0f); // 모멘텀 보너스 +1.0
        }

        // 🪷 연잎 착수 3턴 점진적 높이 보너스 (+0.5m -> +0.3m -> +0.1m)
        if (lilyBonusRemainingTurns > 0)
        {
            float heightBonus = 0f;
            if (lilyBonusRemainingTurns == 3) heightBonus = 0.5f;
            else if (lilyBonusRemainingTurns == 2) heightBonus = 0.3f;
            else if (lilyBonusRemainingTurns == 1) heightBonus = 0.1f;

            bounceForce += heightBonus;
            timingGrade += $"\n🪷 LILY HOP! (+{heightBonus:F1}m)";
            lilyBonusRemainingTurns--;
        }

        if (SkippingStones.UI.InGameMomentumHUD.Instance != null)
        {
            SkippingStones.UI.InGameMomentumHUD.Instance.TriggerGradePopup(timingGrade);
        }

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
            if (GlobalRiverPath.Instance.GetClosestPointOnRiver(transform.position, out _, out _, out float distAlongRiver))
            {
                float lookAheadDist = distAlongRiver + 15f;
                if (GlobalRiverPath.Instance.EvaluateAtDistance(lookAheadDist, out Vector3 lookAheadPos, out Vector3 lookAheadTan, out _, out _))
                {
                    Vector3 toTarget = lookAheadPos - transform.position;
                    toTarget.y = 0f;
                    Vector2 targetHDir = new Vector2(toTarget.x, toTarget.z).normalized;
                    if (targetHDir != Vector2.zero)
                    {
                        hDir = targetHDir;
                    }
                }
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

        // 🌟 수면 아래(LATE/TOO LATE/BAD)에서 바운스 시, 돌 위치를 즉시 수면 위로 올려서 침몰 재판정 원천 차단
        if (transform.position.y < waterLevel + 0.02f)
        {
            transform.position = new Vector3(transform.position.x, waterLevel + 0.02f, transform.position.z);
        }
        waterSubmergeTimer = 0f;

        rb.linearVelocity = new Vector3(hDir.x * newHSpd, bounceForce, hDir.y * newHSpd);

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
        if (!isThrown || isSunk || isCrashed || isSkimming || rb == null) return;

        rb.linearVelocity = SkippingStones.Gameplay.Calculators.StonePhysicsCalculator.ApplySteerToVelocity(rb.linearVelocity, steerAngleDegrees);

        Vector2 hVel = new Vector2(rb.linearVelocity.x, rb.linearVelocity.z);
        if (hVel.sqrMagnitude > 0.01f)
        {
            float newYaw = Mathf.Atan2(hVel.x, hVel.y) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0f, newYaw, 0f);
        }
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

        // 🌟 수면 콜라이더(WaterSurface) 접촉은 물리 충돌(지형 충돌)에서 완전 무시
        if (collision.gameObject.GetComponent<WaterSurface>() != null || collision.gameObject.GetComponentInParent<WaterSurface>() != null)
        {
            return;
        }

        string hitName = collision.gameObject.name.ToLower();
        if (hitName.Contains("water") || hitName.Contains("surface") || hitName.Contains("river") || hitName.Contains("lake") || hitName.Contains("stream"))
        {
            return;
        }

        bool isRock = hitName.Contains("rock") || hitName.Contains("obstacle");

        // 🌟 갓모드: 강 한가운데 놓인 바위 장애물(Rock/Obstacle)은 무시하고 통과, 실제 육지 지형은 충돌 유지
        if (isGodMode && isRock) return;

        CrashOnLand(isRock ? "바위 장애물 충돌" : "지형 착지", isRockObstacle: isRock);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!isThrown || isSunk || isCrashed) return;

        // 1. WaterSurface 컴포넌트 감지 시 수면 높이만 동적으로 갱신하고 충돌 검사 종료
        WaterSurface ws = other.GetComponent<WaterSurface>() ?? other.GetComponentInParent<WaterSurface>();
        if (ws != null)
        {
            waterLevel = other.bounds.max.y;
            return;
        }

        string colName = other.name.ToLower();
        if (colName.Contains("water") || colName.Contains("surface") || colName.Contains("river") || colName.Contains("lake") || colName.Contains("stream"))
        {
            waterLevel = other.bounds.max.y;
            return;
        }

        // 2. Terrain 컴포넌트 검사로 지형/바위 감지 (돌이 실제 수면 위로 나와서 지형에 닿았을 때만)
        bool isTerrain = other.GetComponent<TerrainCollider>() != null || other.GetComponent<UnityEngine.Terrain>() != null || other.GetComponent<MeshCollider>() != null;
        bool isRock = colName.Contains("rock") || colName.Contains("obstacle");
        bool isGround = colName.Contains("ground") || colName.Contains("bank");

        // 🌟 갓모드: 강 한가운데 놓인 바위 장애물은 무시하고 통과
        if (isGodMode && isRock) return;

        if (isTerrain || isRock || isGround)
        {
            CrashOnLand(isRock ? "바위 장애물 충돌" : "지형 착지", isRockObstacle: isRock);
        }
    }

    public void CrashOnLand(string reason = "땅에 충돌 - 게임 오버", bool isRockObstacle = false)
    {
        if (isSunk || isCrashed) return;
        if (isGodMode && isRockObstacle) return; // 갓모드 바위 장애물 충돌 방어
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
        // 🌟 갓모드라도 스키밍(도로록) 피니시가 완료되었거나 명시적 완주 시 정상 침몰 및 결과 처리
        if (isSunk || isCrashed) return;
        if (isGodMode && !isSkimming) return;

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
        // 1. 씬 내 모든 활성화된 WaterSurface 콜라이더의 X, Z 범위 검사 (안전 마진 1.5m 부여)
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
                    // 돌의 X, Z 좌표가 실제 수면 콜라이더 바운드 영역 안에 들어있는지 정밀 확인 (경계 오차 방지 마진 1.5m)
                    if (pos.x >= (b.min.x - 1.5f) && pos.x <= (b.max.x + 1.5f) && pos.z >= (b.min.z - 1.5f) && pos.z <= (b.max.z + 1.5f))
                    {
                        return true;
                    }
                }
            }

            // 2. 곡선 스플라인 강물 경로가 있는 경우, 강 중심선으로부터의 거리로 2차 검증
            if (GlobalRiverPath.Instance != null && GlobalRiverPath.Instance.GetClosestPointOnRiver(pos, out Vector3 riverCenter, out _, out _))
            {
                float riverDist = Vector2.Distance(new Vector2(pos.x, pos.z), new Vector2(riverCenter.x, riverCenter.z));
                if (riverDist <= 28f) // 표준 강 폭 허용 범위
                {
                    return true;
                }
            }

            // 씬에 수면이 존재하는데 돌이 수면 바깥(완전 육지 지형 밖)으로 나간 경우
            return false;
        }

        // 수면 컴포넌트가 하나도 없는 특수 상황 폴백 (기본 수면 높이 기준)
        return true;
    }

    /// <summary>
    /// 🌟 이전 투구의 모든 디버그 마커 오브젝트들을 씬에서 완전히 정리
    /// </summary>
    public static void ClearAllTapDebugMarkers()
    {
        var markers = GameObject.FindObjectsByType<GameObject>(FindObjectsInactive.Include);
        foreach (var obj in markers)
        {
            if (obj != null && obj.name.StartsWith("[TAP_DEBUG]"))
            {
                if (Application.isPlaying) Destroy(obj);
                else DestroyImmediate(obj);
            }
        }
    }

    /// <summary>
    /// 🌟 스페이스바(탭) 입력 순간 씬에 영구 3D 디버그 마커 생성 (일시정지 후 씬 뷰에서 정밀 확인 가능)
    /// </summary>
    private void SpawnTapDebugMarker(TapDebugRecord record)
    {
        GameObject markerRoot = new GameObject($"[TAP_DEBUG] Skip#{record.skipIndex + 1}_{record.grade}_H={record.distToWater:F2}m");
        
        // 1. 공중 돌 위치 구체 (Sphere)
        GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        sphere.name = "StonePos_AtTap";
        sphere.transform.SetParent(markerRoot.transform);
        sphere.transform.position = record.stoneWorldPos;
        sphere.transform.localScale = Vector3.one * 0.16f;

        Color markCol = record.grade.Contains("PERFECT") ? Color.green :
                        record.grade.Contains("GREAT") ? Color.cyan :
                        record.grade.Contains("GOOD") ? Color.yellow :
                        record.grade.Contains("TOO EARLY") ? new Color(1.0f, 0.55f, 0.15f, 1.0f) :
                        record.grade.Contains("LATE") ? Color.magenta : Color.red;

        Renderer sphereRend = sphere.GetComponent<Renderer>();
        Material markMat = new Material(Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default"));
        markMat.color = markCol;
        if (sphereRend != null)
        {
            sphereRend.sharedMaterial = markMat;
        }

        // 2. 수직 레이저 기준선 (돌 -> 수면)
        GameObject lineObj = new GameObject("VerticalDropLine");
        lineObj.transform.SetParent(markerRoot.transform);
        LineRenderer lr = lineObj.AddComponent<LineRenderer>();
        lr.useWorldSpace = true;
        lr.positionCount = 2;
        lr.startWidth = 0.015f;
        lr.endWidth = 0.015f;
        lr.sharedMaterial = markMat;
        lr.startColor = markCol;
        lr.endColor = markCol;
        lr.SetPosition(0, record.stoneWorldPos);
        lr.SetPosition(1, new Vector3(record.stoneWorldPos.x, waterLevel, record.stoneWorldPos.z));

        // 3. 수면 기준 십자 마커 (착수 수면)
        GameObject waterCross = GameObject.CreatePrimitive(PrimitiveType.Quad);
        waterCross.name = "WaterImpact_Plane";
        waterCross.transform.SetParent(markerRoot.transform);
        waterCross.transform.position = new Vector3(record.stoneWorldPos.x, waterLevel + 0.01f, record.stoneWorldPos.z);
        waterCross.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        waterCross.transform.localScale = Vector3.one * 0.35f;
        Renderer quadRend = waterCross.GetComponent<Renderer>();
        if (quadRend != null)
        {
            Material quadMat = new Material(markMat);
            Color c = markCol;
            c.a = 0.5f;
            quadMat.color = c;
            quadRend.sharedMaterial = quadMat;
        }

        // 충돌 방지 콜라이더 제거
        foreach (var col in markerRoot.GetComponentsInChildren<Collider>())
        {
            if (Application.isPlaying) Destroy(col);
            else DestroyImmediate(col);
        }

        Debug.Log($"<color=#{ColorUtility.ToHtmlStringRGB(markCol)}><b>[TAP TELEMETRY]</b> Skip #{record.skipIndex + 1} | 판정: {record.grade} | 수면고도차: {record.distToWater:F3}m | 하강속도: {record.verticalSpeed:F2}m/s</color>", markerRoot);
    }

    private void OnDrawGizmos()
    {
        if (tapDebugHistory == null || tapDebugHistory.Count == 0) return;

        foreach (var rec in tapDebugHistory)
        {
            Gizmos.color = rec.grade.Contains("PERFECT") ? Color.green :
                           rec.grade.Contains("GREAT") ? Color.cyan :
                           rec.grade.Contains("LATE") ? Color.magenta : Color.red;

            Gizmos.DrawWireSphere(rec.stoneWorldPos, 0.12f);
            Gizmos.DrawLine(rec.stoneWorldPos, new Vector3(rec.stoneWorldPos.x, waterLevel, rec.stoneWorldPos.z));
        }
    }

    private void OnDestroy()
    {
        if (spawnedRhythmRing != null)
        {
            Destroy(spawnedRhythmRing.gameObject);
            spawnedRhythmRing = null;
        }

        shadowController.Cleanup();
    }
}