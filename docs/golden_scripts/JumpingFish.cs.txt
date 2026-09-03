using UnityEngine;

public enum FishAIState
{
    UnderwaterIdle, // 수중 제자리 살랑살랑 (Idle 애니메이션)
    UnderwaterSwim, // 수중 순찰 유영 (Run 애니메이션)
    LeapJump,       // 수면 박차고 파다닥 공중 도약
    Submerge        // 착수 및 잠수 복귀
}

public class JumpingFish : MonoBehaviour
{
    [Header("어종 식별 및 스펙")]
    public int fishIndex = 1;
    public string speciesId = "chinese_minnow";
    public string speciesName = "버들치";
    public float scaleFactor = 1.0f;
    public float jumpHeight = 2.0f;
    public float jumpDuration = 1.1f;
    public int rewardCoins = 100;
    public Sprite bookSprite;

    [Header("컴포넌트 바인딩")]
    public Animator animator;
    public Transform modelTransform;

    [Header("AI 및 런타임 상태")]
    public FishAIState currentState = FishAIState.UnderwaterIdle;

    private Vector3 baseOriginPos;
    private Vector3 currentPatrolTarget;
    private float stateTimer = 0f;
    private float nextStateDuration = 2f;
    private float leapProgress = 0f;
    private Vector3 leapStartPos;
    private Vector3 leapEndPos;

    private bool isCaught = false;
    private Transform stoneTransform;
    private float idleBobbingTimer = 0f;

    private void Awake()
    {
        baseOriginPos = transform.position;

        if (modelTransform == null)
        {
            Transform m = transform.Find("Model");
            if (m != null) modelTransform = m;
            else if (transform.childCount > 0) modelTransform = transform.GetChild(0);
        }

        if (animator == null && modelTransform != null)
        {
            animator = modelTransform.GetComponent<Animator>();
        }

        // 스케일 팩터 적용
        if (modelTransform != null)
        {
            modelTransform.localScale = Vector3.one * scaleFactor;
        }

        // 각 물고기마다 타이머 및 초기 위상 랜덤화 (모든 물고기가 동시에 멈추거나 움직이지 않도록 분산)
        idleBobbingTimer = Random.Range(0f, 100f);
        stateTimer = Random.Range(0f, 1.5f);

        // 초기 위치는 수면 아래 (-0.4m ~ -0.7m)
        baseOriginPos.y = -0.5f;
        transform.position = baseOriginPos;

        // 랜덤하게 50% 확률로 수중 유영 상태로 시작
        if (Random.value < 0.5f)
        {
            SwitchState(FishAIState.UnderwaterSwim);
        }
        else
        {
            SwitchState(FishAIState.UnderwaterIdle);
        }
    }

    private void Update()
    {
        if (isCaught) return;

        LocateStone();

        // 1. 돌 접근 감지 시 긴급/다이내믹 도약 트리거 (15m ~ 28m 거리 내)
        if (currentState != FishAIState.LeapJump && currentState != FishAIState.Submerge)
        {
            if (stoneTransform != null)
            {
                float distZ = transform.position.z - stoneTransform.position.z;
                float distH = Vector2.Distance(new Vector2(transform.position.x, transform.position.z), new Vector2(stoneTransform.position.x, stoneTransform.position.z));

                if (distZ > 5f && distZ < 28f && distH < 28f)
                {
                    TriggerLeapJump();
                }
            }
        }

        // 2. FSM 상태별 로직 처리
        switch (currentState)
        {
            case FishAIState.UnderwaterIdle:
                UpdateUnderwaterIdle();
                break;
            case FishAIState.UnderwaterSwim:
                UpdateUnderwaterSwim();
                break;
            case FishAIState.LeapJump:
                UpdateLeapJump();
                break;
            case FishAIState.Submerge:
                UpdateSubmerge();
                break;
        }
    }

    private void LocateStone()
    {
        if (stoneTransform == null)
        {
            SkippingStone s = FindAnyObjectByType<SkippingStone>();
            if (s != null) stoneTransform = s.transform;
            else
            {
                SkippingStones.Arcade.ArcadeSkippingStone asStone = FindAnyObjectByType<SkippingStones.Arcade.ArcadeSkippingStone>();
                if (asStone != null) stoneTransform = asStone.transform;
            }
        }
    }

    private void SwitchState(FishAIState newState)
    {
        currentState = newState;
        stateTimer = 0f;

        if (animator != null)
        {
            bool isSwimming = (newState == FishAIState.UnderwaterSwim || newState == FishAIState.LeapJump);
            animator.SetBool("isSwimming", isSwimming);
        }

        switch (newState)
        {
            case FishAIState.UnderwaterIdle:
                nextStateDuration = Random.Range(1.5f, 3.5f);
                break;
            case FishAIState.UnderwaterSwim:
                nextStateDuration = Random.Range(2.5f, 5.0f);
                PickNextPatrolPoint();
                break;
            case FishAIState.LeapJump:
                leapProgress = 0f;
                leapStartPos = transform.position;
                Vector3 forwardDir = transform.forward;
                leapEndPos = leapStartPos + forwardDir * Random.Range(3.5f, 5.5f);
                leapEndPos.y = -0.5f;

                if (SplashEffectSpawner.Instance != null)
                {
                    SplashEffectSpawner.Instance.SpawnSplash(new Vector3(leapStartPos.x, 0f, leapStartPos.z), 1.2f * scaleFactor);
                }
                break;
            case FishAIState.Submerge:
                nextStateDuration = 0.5f;
                if (SplashEffectSpawner.Instance != null)
                {
                    SplashEffectSpawner.Instance.SpawnSplash(new Vector3(transform.position.x, 0f, transform.position.z), 1.5f * scaleFactor);
                }
                break;
        }
    }

    private void UpdateUnderwaterIdle()
    {
        stateTimer += Time.deltaTime;
        idleBobbingTimer += Time.deltaTime * 2.5f;

        // 제자리에서 살랑살랑 상하좌우 미세 부유 운동
        float bobbingY = -0.5f + Mathf.Sin(idleBobbingTimer) * 0.08f;
        transform.position = new Vector3(transform.position.x, bobbingY, transform.position.z);

        if (stateTimer >= nextStateDuration)
        {
            // 가끔 자연스러운 랜덤 점프 (35% 확률) or 유영 전환
            if (Random.value < 0.35f) TriggerLeapJump();
            else SwitchState(FishAIState.UnderwaterSwim);
        }
    }

    private void UpdateUnderwaterSwim()
    {
        stateTimer += Time.deltaTime;

        Vector3 dir = (currentPatrolTarget - transform.position);
        dir.y = 0f;

        if (dir.sqrMagnitude > 0.05f)
        {
            Quaternion targetRot = Quaternion.LookRotation(dir);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * 3.5f);
            
            float moveSpeed = 1.8f * (0.8f + scaleFactor * 0.2f);
            transform.position += transform.forward * (moveSpeed * Time.deltaTime);
        }

        transform.position = new Vector3(transform.position.x, -0.5f, transform.position.z);

        if (stateTimer >= nextStateDuration || dir.magnitude < 0.5f)
        {
            SwitchState(FishAIState.UnderwaterIdle);
        }
    }

    private void UpdateLeapJump()
    {
        leapProgress += Time.deltaTime / Mathf.Max(0.1f, jumpDuration);
        float t = Mathf.Clamp01(leapProgress);

        Vector3 currentHorizontal = Vector3.Lerp(leapStartPos, leapEndPos, t);
        float currentY = -0.5f + Mathf.Sin(t * Mathf.PI) * (jumpHeight + 0.5f);
        transform.position = new Vector3(currentHorizontal.x, currentY, currentHorizontal.z);

        float pitchAngle = Mathf.Cos(t * Mathf.PI) * 45f;
        Vector3 jumpDir = (leapEndPos - leapStartPos).normalized;
        if (jumpDir != Vector3.zero)
        {
            Quaternion baseLook = Quaternion.LookRotation(jumpDir);
            transform.rotation = baseLook * Quaternion.Euler(-pitchAngle, 0f, 0f);
        }

        if (stoneTransform != null && !isCaught)
        {
            float hitDist = Vector3.Distance(transform.position, stoneTransform.position);
            if (hitDist < 1.8f * scaleFactor)
            {
                SnipeHit();
                return;
            }
        }

        if (leapProgress >= 1.0f)
        {
            SwitchState(FishAIState.Submerge);
        }
    }

    private void UpdateSubmerge()
    {
        stateTimer += Time.deltaTime;
        transform.position = new Vector3(transform.position.x, -0.6f, transform.position.z);

        if (stateTimer >= nextStateDuration)
        {
            SwitchState(FishAIState.UnderwaterIdle);
        }
    }

    public void TriggerLeapJump()
    {
        if (currentState == FishAIState.LeapJump) return;
        SwitchState(FishAIState.LeapJump);
    }

    private void PickNextPatrolPoint()
    {
        Vector2 circle = Random.insideUnitCircle * Random.Range(2.5f, 5.0f);
        currentPatrolTarget = new Vector3(baseOriginPos.x + circle.x, -0.5f, baseOriginPos.z + circle.y);
    }

    private void SnipeHit()
    {
        if (isCaught) return;
        isCaught = true;

        if (AquariumManager.Instance != null)
        {
            AquariumManager.Instance.RegisterCaughtFish(speciesId);
        }

        GameController gc = FindAnyObjectByType<GameController>();
        if (gc != null)
        {
            gc.TriggerFishSnipeEffect(speciesName);
        }

        if (SplashEffectSpawner.Instance != null)
        {
            SplashEffectSpawner.Instance.SpawnSplash(transform.position, 2.5f * scaleFactor);
        }

        if (AudioManager.Instance != null) AudioManager.Instance.PlaySound(SoundType.CoinJingle, 1.15f);
        HapticFeedbackHelper.TriggerLightTap();

        gameObject.SetActive(false);
    }
}
