using UnityEngine;
using System;
using System.Collections;

[RequireComponent(typeof(Rigidbody))]
public class SkippingStone : MonoBehaviour
{
    [Header("3D 프리팹 모델")]
    [Tooltip("사용자 지정 Stone 프리팹 (미지정 시 Assets/3D/prefab/Stone.prefab 자동 로드)")]
    public GameObject customStonePrefab;

    [Header("물리 및 이동 속성")]
    public float forwardPower = 23f;

    [Tooltip("초기 발사 시 위쪽으로 솟구치는 상승력 (첫 착수까지 1.2~1.4초 시원한 포물선)")]
    public float initialUpwardForce = 5.5f;

    [Tooltip("수면 바운스 시 위로 튀어오르는 기본 반사력 기준값")]
    public float baseBounceUpForce = 5.2f;

    [Tooltip("최대 수평 이동 속도 상한선")]
    public float maxHorizontalSpeed = 36f;

    public float gravityScale = 1.35f;
    public float airDrag = 0.998f;

    [Header("타이밍 판정 관용도 (기본 기준값)")]
    [Tooltip("타이밍 알림 및 판정이 시작되는 수면 위 높이 (m)")]
    public float timingWindowHeight = 2.4f;

    [Tooltip("PERFECT 판정 기준 거리 (초기 m)")]
    public float perfectDistance = 0.70f;

    [Tooltip("GREAT 판정 기준 거리 (초기 m)")]
    public float greatDistance = 1.45f;

    [Tooltip("GOOD 판정 기준 거리 (초기 m)")]
    public float goodDistance = 2.40f;

    [Header("마지막 '도로록~' 스키밍 피니시 설정")]
    [Tooltip("스키밍 피니시 발동 최소 스킵 횟수")]
    public int minSkimSkips = 5;

    [Tooltip("최대 스키밍 효과 도달 스킵 횟수 (30회 이상 시 최대 효과)")]
    public int maxSkimSkips = 30;

    [Header("비주얼 및 트레일")]
    public TrailRenderer trail;
    public Material trailCustomMaterial;
    public Material stoneCustomMaterial; // 🌟 조약돌 전용 머티리얼
    public Color trailStartColor = new Color(0.25f, 0.85f, 1.0f, 0.40f);
    public Color trailEndColor = new Color(0.15f, 0.70f, 1.0f, 0f);

    [Header("상태 모니터링")]
    public bool isThrown = false;
    public bool isSunk = false;
    public bool isCrashed = false;
    public bool isSkimming = false;
    public bool isGodMode = false; // 🌟 갓모드 자동 비행 지원
    public int skipCount = 0;
    public float totalDistance = 0f;
    public float skimDistance = 0f;
    public bool isInTimingWindow = false;

    private bool hasTappedInCurrentBounce = false;
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
    private float waterLevel = 0f;

    private float currentPitchAngle = 0f;
    private float currentSpinAngle = 0f;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        if (rb == null) rb = gameObject.AddComponent<Rigidbody>();
        rb.useGravity = false;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
        rb.interpolation = RigidbodyInterpolation.None; // 손에 쥐고 있는 동안에는 본 애니메이션과 100% 일치하도록 None 설정
        startPosition = transform.position;

        SetupVisualModel();
        SetupTrail();
        EnsureRhythmRing();
    }

    private void SetupVisualModel()
    {
        // 1. 루트 오브젝트에 붙어있는 임시 구체 메쉬/렌더러 제거 (바깥쪽 껍질 원천 차단)
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

        // 2. 기존 임시 구체/폴백 자식 오브젝트 완전 차단
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Transform child = transform.GetChild(i);
            if (child.name.StartsWith("StoneModel_Fallback") || child.name.Contains("Fallback") || child.name.Contains("Sphere"))
            {
                if (Application.isPlaying) Destroy(child.gameObject);
                else DestroyImmediate(child.gameObject);
            }
        }

        // 3. 자식에 이미 공식 StoneModel이 있는지 확인
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

                // 자식 내에 중복된 콜라이더 정리
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
                trail = gameObject.AddComponent<TrailRenderer>();
                Debug.LogWarning("💡 [프리팹 알림] 'Stone'에 TrailRenderer가 없어 코드가 임시로 자동 부착했습니다.");
            }
        }

        trail.time = 0.38f;
        trail.startWidth = 0.045f; // 🌟 조약돌 뒤로 튀어나오지 않고 돌을 돋보이게 하는 슬림 폭
        trail.endWidth = 0.002f;
        trail.minVertexDistance = 0.06f;
        trail.textureMode = LineTextureMode.Stretch; // 🌟 TGA 텍스처 맵을 트레일 궤적에 부드럽게 펼침
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
            gameObject.AddComponent<RhythmRingIndicator>();
            Debug.LogWarning("💡 [프리팹 알림] 'Stone'에 RhythmRingIndicator가 없어 코드가 임시로 자동 부착했습니다.");
        }
    }

    private void Start()
    {
        GameObject water = GameObject.Find("WaterSurface");
        if (water != null)
        {
            waterLevel = water.transform.position.y;
        }
    }

    private void Update()
    {
        if (!isThrown || isSunk || isCrashed || isSkimming) return;

        // 🌟 진행 방향 벡터 기반 공기역학 피칭 틸트 (+45도 앞들림 ~ -36도 숙임) & 고속 자전 스핀
        Vector3 v = (rb != null) ? rb.linearVelocity : Vector3.zero;
        Vector3 hVel = new Vector3(v.x, 0f, v.z);
        if (hVel.sqrMagnitude > 0.05f)
        {
            Vector3 hDir = hVel.normalized;
            float vy = v.y;
            // vy > 0 (상승): 피칭 업 (+45도), vy < 0 (하강): 피칭 다운 (-36도)
            float targetPitch = Mathf.Clamp(vy * 6.5f, -36f, 45f);
            currentPitchAngle = Mathf.Lerp(currentPitchAngle, targetPitch, Time.deltaTime * 14f);
            currentSpinAngle = (currentSpinAngle + 1440f * Time.deltaTime) % 360f;

            Quaternion headingRot = Quaternion.LookRotation(hDir, Vector3.up);
            Quaternion pitchRot = Quaternion.Euler(-currentPitchAngle, 0f, 0f);
            Quaternion spinRot = Quaternion.Euler(0f, currentSpinAngle, 0f);

            transform.rotation = headingRot * pitchRot * spinRot;
        }
    }

    /// <summary>
    /// 🌟 0초 인플레이스 리셋: 이전 판의 모든 물리, 플래그, 트레일 잔재를 100% 초기화
    /// </summary>
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
        isInTimingWindow = false;

        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.isKinematic = true;
            rb.useGravity = false;
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

        isThrown = true;
        isSunk = false;
        isCrashed = false;
        isSkimming = false;
        skipCount = 0;
        skimDistance = 0f;
        hasTappedInCurrentBounce = false;
        isInTimingWindow = false;
        waterSubmergeTimer = 0f;
        currentPitchAngle = 0f;
        currentSpinAngle = 0f;

        rb.isKinematic = false;
        rb.useGravity = true;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

        // 🌟 물리 회전 제약: X축(Pitch)과 Z축(Roll) 회전 잠금
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

        // 🌟 손에서 떠난 뒤 1초 동안 실물 크기(1.0x)에서 4.4x로 서서히 가속되며 확~ 커지는 Ease-In 줌업 (물리 콜라이더 100% 보존)
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

        visualChild.localScale = Vector3.one; // 손에서 던져질 때는 실물 손 크기에 딱 맞는 1.0배
        float elapsed = 0f;
        float duration = 1.0f;
        float targetScale = 4.4f; // 🌟 4.4배까지 확대하여 스핀과 틸팅 손맛 극대화

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float rawT = Mathf.Clamp01(elapsed / duration);

            // 🌟 Ease-In 2.5제곱 가속 커브:
            // 초반(0~0.5초): 손에서 떠날 때는 1.0~1.3배로 천천히 커지다가
            // 후반(0.6~1.0초): 카메라가 밀착되는 순간 1.5x ➔ 4.4x로 확~ 박진감 넘치게 줌업!
            float easeProgress = Mathf.Pow(rawT, 2.5f);
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
    private const float LATE_GRACE_WINDOW = 0.010f; // 🌟 10ms 레이트 관용 윈도우

    private void FixedUpdate()
    {
        if (!isThrown || isSunk || isCrashed) return;

        // 🌊 마지막 '도로록~' 스키밍 피니시 모드 중일 때
        if (isSkimming)
        {
            UpdateSkimming();
            return;
        }

        rb.AddForce(Physics.gravity * (gravityScale - 1f), ForceMode.Acceleration);
        rb.linearVelocity = new Vector3(rb.linearVelocity.x * airDrag, rb.linearVelocity.y, rb.linearVelocity.z * airDrag);

        totalDistance = Vector2.Distance(new Vector2(startPosition.x, startPosition.z), new Vector2(transform.position.x, transform.position.z));

        float height = transform.position.y;
        float dynWindowHeight = Mathf.Lerp(timingWindowHeight, 1.4f, Mathf.Clamp01(skipCount / 30f));

        if (height <= dynWindowHeight && height >= -0.1f && rb.linearVelocity.y < 0.5f)
        {
            isInTimingWindow = true;
        }
        else
        {
            isInTimingWindow = false;
        }

        if (hasTappedInCurrentBounce && rb.linearVelocity.y < -0.12f && height > waterLevel + 0.15f)
        {
            hasTappedInCurrentBounce = false;
            waterSubmergeTimer = 0f;
        }

        if (isGodMode)
        {
            // 🌟 갓모드: 외부 물리 침몰 및 타이밍 실패 체크를 100% 차단하고 완전 무적으로 비행
            return;
        }

        // 수면 착수 체크 (-10ms 정밀 관용 시간 적용)
        if (height <= waterLevel - 0.04f && rb.linearVelocity.y <= 0f && !hasTappedInCurrentBounce)
        {
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
        else if (height > waterLevel + 0.1f)
        {
            waterSubmergeTimer = 0f;
        }

        if (height < waterLevel - 1.2f)
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

        if (hasTappedInCurrentBounce || rb.linearVelocity.y > 0.4f)
        {
            timingGrade = "ALREADY TAPPED";
            return false;
        }

        float curGood = GetCurrentGoodDistance();
        if (distToWater > curGood)
        {
            timingGrade = "💦 TOO EARLY";
            return false;
        }

        hasTappedInCurrentBounce = true;
        waterSubmergeTimer = 0f;
        skipCount++;

        float bounceForce = GetDynamicBounceForce(skipCount);
        float speedMultiplier = 1.0f;

        float curPerfect = GetCurrentPerfectDistance();
        float curGreat = GetCurrentGreatDistance();

        if (distToWater <= 0.02f)
        {
            // 🌟 수면 접촉 직후 10ms 이내 관용 판정 (LATE)
            timingGrade = "⚠️ LATE";
            bounceForce *= 0.85f;
            speedMultiplier = 0.90f;
            Debug.Log($"[Rhythm Timing] ⚠️ LATE tap accepted (dist={distToWater:F3}m)");
        }
        else if (distToWater <= curPerfect)
        {
            timingGrade = "🔥 PERFECT! 🔥";
            bounceForce *= 1.25f;
            speedMultiplier = 1.08f;
            Debug.Log($"[Rhythm Timing] 🔥 PERFECT tap (dist={distToWater:F3}m)");
        }
        else if (distToWater <= curGreat)
        {
            timingGrade = "⚡ GREAT! ⚡";
            bounceForce *= 1.10f;
            speedMultiplier = 1.02f;
            Debug.Log($"[Rhythm Timing] ⚡ GREAT tap (dist={distToWater:F3}m)");
        }
        else
        {
            timingGrade = "✨ GOOD";
            bounceForce *= 0.92f;
            speedMultiplier = 0.95f;
            Debug.Log($"[Rhythm Timing] ✨ GOOD tap (dist={distToWater:F3}m)");
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

        // 🌟 스티어링 각도 회전 적용 (좌/우 플릭 조향: steerAngleDegrees)
        if (Mathf.Abs(steerAngleDegrees) > 0.01f)
        {
            Quaternion rot = Quaternion.Euler(0f, steerAngleDegrees, 0f);
            Vector3 rotated3D = rot * new Vector3(hDir.x, 0f, hDir.y);
            hDir = new Vector2(rotated3D.x, rotated3D.z).normalized;

            if (steerAngleDegrees < 0f)
            {
                timingGrade += $" ◀ LEFT {Mathf.Abs(steerAngleDegrees):F0}°";
            }
            else
            {
                timingGrade += $" RIGHT {steerAngleDegrees:F0}° ▶";
            }
        }

        rb.linearVelocity = new Vector3(hDir.x * newHSpd, bounceForce, hDir.y * newHSpd);
        transform.position = new Vector3(transform.position.x, waterLevel + 0.10f, transform.position.z);

        // 🌟 바운스 시에도 수평 유지 및 진행 방향 회전 반영
        float newYaw = Mathf.Atan2(hDir.x, hDir.y) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0f, newYaw, 0f);
        rb.angularVelocity = new Vector3(0f, 45f, 0f);

        if (SplashEffectSpawner.Instance != null)
        {
            float splashScale = Mathf.Lerp(1.2f, 2.0f, Mathf.Clamp01(skipCount / 30f));
            SplashEffectSpawner.Instance.SpawnSplash(transform.position, (timingGrade.Contains("PERFECT")) ? splashScale : splashScale * 0.75f);
        }

        // 🎵 오디오 & 📳 햅틱 연동
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
        Debug.Log($"🚀 [부스트 패드 기록] {totalDistance:F1}m 지점에서 부스트 패드 통과 기록 완료!");
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

        Debug.Log($"🎯 [Steering] 돌 진행 방향 조향 완료: {(steerAngleDegrees > 0 ? "+" : "")}{steerAngleDegrees}°");
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

        // 🌟 수평 고정 및 고속 Y 스핀
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

        // 🌟 Y축 수직 Up 유지 & 고속 스핀
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
        if (isGodMode || !isThrown || isSunk || isCrashed) return;

        string hitName = collision.gameObject.name.ToLower();
        Debug.Log($"💥 [SkippingStone] 지형/바위 충돌 발생: {hitName}");

        bool isRock = hitName.Contains("rock") || hitName.Contains("obstacle");
        CrashOnLand(isRock ? "바위 장애물 충돌 - 게임 오버" : "땅에 충돌 - 게임 오버", isRockObstacle: isRock);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (isGodMode || !isThrown || isSunk || isCrashed) return;

        string targetName = other.gameObject.name.ToLower();
        if (targetName.Contains("ground") || targetName.Contains("bank") || targetName.Contains("terrain") || targetName.Contains("obstacle") || targetName.Contains("rock"))
        {
            Debug.Log($"💥 [SkippingStone] 지형(Ground)/바위 Trigger 충돌: {other.gameObject.name}");
            bool isRock = targetName.Contains("rock") || targetName.Contains("obstacle");
            CrashOnLand(isRock ? "바위 장애물 충돌 - 게임 오버" : "지형/땅 충돌 - 게임 오버", isRockObstacle: isRock);
        }
    }

    public void CrashOnLand(string reason = "땅에 충돌 - 게임 오버", bool isRockObstacle = false)
    {
        if (isGodMode || isSunk || isCrashed) return;
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
        // 🌟 1. 크게 한 번 펄쩍 튀어오르는 충돌 반작용 액션!
        Vector2 hVel = new Vector2(rb.linearVelocity.x, rb.linearVelocity.z);
        Vector3 reboundDir = (hVel.sqrMagnitude > 0.1f) ? -new Vector3(hVel.x, 0f, hVel.y).normalized * 0.35f : Vector3.back * 0.35f;

        rb.useGravity = true;
        rb.constraints = RigidbodyConstraints.None; // 자유 텀블링 연출
        rb.linearVelocity = reboundDir * 3.6f + Vector3.up * 5.2f;
        rb.angularVelocity = new Vector3(UnityEngine.Random.Range(-12f, 12f), 8f, UnityEngine.Random.Range(-12f, 12f));

        // 🌟 2. 튀어오른 직후 초기 상승 시간 대기 (0.18초)
        yield return new WaitForSeconds(0.18f);

        float timeout = 4.0f;
        float elapsed = 0f;
        bool settled = false;

        // 🌟 3. 허공 멈춤 없이 수면 또는 바닥에 닿을 때까지 끝까지 중력 낙하 추적!
        while (elapsed < timeout && !settled)
        {
            elapsed += Time.deltaTime;
            Vector3 pos = transform.position;

            // [Case A: 바위 장애물 충돌 -> 수면까지 낙하 후 서서히 침몰]
            if (isRockObstacle)
            {
                if (pos.y <= waterLevel + 0.04f)
                {
                    settled = true;
                    // 수면 물보라 발생
                    if (SplashEffectSpawner.Instance != null)
                    {
                        SplashEffectSpawner.Instance.SpawnSplash(new Vector3(pos.x, waterLevel, pos.z), 1.0f);
                    }

                    // 수면 아래로 서서히 잠겨 들어가는 연출
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
            // [Case B: 지형/땅 충돌 -> 바닥에 완전히 충돌/착지할 때까지 물리 낙하]
            else
            {
                Ray ray = new Ray(pos + Vector3.up * 0.1f, Vector3.down);
                if (Physics.Raycast(ray, out RaycastHit hit, 0.22f, ~0, QueryTriggerInteraction.Ignore))
                {
                    if (hit.collider != null && hit.collider.gameObject != gameObject)
                    {
                        // 바닥에 닿아 하강 속도가 소진되었을 때 착지 완료
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
                    // 땅에서 튕겨 강물로 떨어진 경우 수면 침몰 처리
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

        // 🌟 4. 최종 정지 및 키네마틱 전환
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rb.isKinematic = true;

        bounceHistory.Add(new BounceRecord { position = transform.position, skipIndex = skipCount, grade = isRockObstacle ? "CRASH_ROCK" : "CRASH_LAND", distance = totalDistance });

        isSunk = true;
        OnStoneSunk?.Invoke(totalDistance);
        Debug.Log($"[Stone Crash Settled] {reason} | 최종 기록: {totalDistance:F1}m / {skipCount}회 스킵");
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
        // 🌟 수면에서 부드럽게 가라앉는 연출 (수평 관성 즉시 0 차단!)
        rb.useGravity = false;
        rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;

        // 수평 전진 관성을 0으로 완전히 소멸시키고 수직으로만 살짝 잠김
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
        Debug.Log($"[Stone Sunk] {reason} | 최종 기록: {totalDistance:F1}m / {skipCount}회 스킵 (스키밍 보너스: +{skimDistance:F1}m)");

        // 0.4초 후 완전 정지 및 키네마틱 전환
        yield return new WaitForSeconds(0.4f);
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.isKinematic = true;
        }
    }
}