using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D), typeof(Animator))]
public class PlayerShadowController : MonoBehaviour, IDamageable
{
    [Header("Settings")]
    [SerializeField] private float delaySeconds = 0.3f;

    [Header("Player Target (Kéo GameObject Player vào đây)")]
    [SerializeField] private PlayerStatus playerStatus;

    [Header("Anchor Mechanic Settings")]
    [SerializeField] private bool isAnchored = false;
    public bool IsAnchored => isAnchored;
    [SerializeField] private Color anchorColor = new Color(0.35f, 0f, 0.75f, 0.85f);
    [SerializeField] private float anchorFallSpeed = 18f;

    [Header("Shadow Damage Transfer")]
    [Range(0f, 1f)] [SerializeField] private float damageTransferRatio = 0.5f;
    [SerializeField] private Color hitColor = new Color(1f, 0.3f, 0.3f, 0.7f);

    [Header("Movement Progression")]
    [SerializeField] private float walkSpeed = 5f;
    [SerializeField] private float runSpeed = 9f;
    [SerializeField] private float dashSpeed = 28f;
    [SerializeField] private float dashDuration = 0.12f;
    
    [Header("Hollow Knight Jump Feel")]
    [SerializeField] private float jumpForce = 14f;
    [SerializeField] private float fallMultiplier = 3f;
    [SerializeField] private float lowJumpMultiplier = 2.2f;
    [SerializeField] private float coyoteTime = 0.15f;
    [SerializeField] private float maxFallSpeed = 20f;
    [SerializeField] private float jumpApexThreshold = 1.5f;
    [SerializeField] private float jumpApexHangTime = 0.5f;
    [SerializeField] private float pogoForce = 12f;

    [Header("Attack & Hitbox Settings")]
    [SerializeField] private Transform attackPoint;
    [SerializeField] private float attackRange = 0.8f;
    [SerializeField] private LayerMask enemyLayer;

    [Header("Ground Check Settings")]
    [SerializeField] private Transform groundCheck;
    [SerializeField] private float groundCheckRadius = 0.2f;
    [SerializeField] private LayerMask groundLayer;

    private Rigidbody2D rb;
    private Animator anim;
    private SpriteRenderer sr;
    private Color originalColor;
    private PlayerReplayManager replayManager;

    private bool isGrounded;
    private bool isFacingRight = true;
    private bool isFallingToAnchor = false;
    
    // Quản lý kích hoạt Replay theo hành động của Player
    private bool hasStartedReplay = false;
    private float firstActionTime = -1f;

    private float coyoteTimeCounter = 0f;
    private float currentAttackDamage = 20f;
    private float dashTimer = 0f;
    private float dashDirection = 1f;
    private float lastProcessedTime = -999f;
    private Vector2 anchoredPosition;

    void Awake()
    {
        EnsurePlayerStatusReference();
    }

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        anim = GetComponent<Animator>();
        sr = GetComponent<SpriteRenderer>();
        replayManager = FindObjectOfType<PlayerReplayManager>();

        if (sr != null) originalColor = sr.color;

        EnsurePlayerStatusReference();

        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        rb.linearVelocity = Vector2.zero; // Đảm bảo bóng đứng yên tuyệt đối lúc đầu
    }

    private void EnsurePlayerStatusReference()
    {
        if (playerStatus != null) return;

        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
        {
            playerStatus = playerObj.GetComponent<PlayerStatus>();
            if (playerStatus != null) return;
        }

        SideScrollPlayer playerScript = FindObjectOfType<SideScrollPlayer>();
        if (playerScript != null)
        {
            playerStatus = playerScript.GetComponent<PlayerStatus>();
            if (playerStatus != null) return;
        }

#if UNITY_2023_1_OR_NEWER
        playerStatus = Object.FindFirstObjectByType<PlayerStatus>();
#else
        playerStatus = Object.FindObjectOfType<PlayerStatus>();
#endif
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.V))
        {
            ToggleAnchor();
        }
    }

    void FixedUpdate()
    {
        CheckGrounded();

        // 1. Trạng thái đã chốt Neo
        if (isAnchored)
        {
            rb.linearVelocity = Vector2.zero;
            transform.position = anchoredPosition;
            anim.SetFloat("Speed", 0f);
            return;
        }

        // 2. Trạng thái rơi cắm Neo xuống sàn
        if (isFallingToAnchor)
        {
            if (isGrounded)
            {
                isFallingToAnchor = false;
                isAnchored = true;
                anchoredPosition = transform.position;
                rb.linearVelocity = Vector2.zero;
                rb.bodyType = RigidbodyType2D.Kinematic;
                anim.SetFloat("Speed", 0f);
                if (sr != null) sr.color = anchorColor;
            }
            else
            {
                rb.linearVelocity = new Vector2(0f, -anchorFallSpeed);
                anim.SetFloat("Speed", 0f);
            }
            return;
        }

        if (replayManager == null) return;

        // 3. Kiểm tra xem Player đã bắt đầu hành động đầu tiên chưa
        if (!hasStartedReplay)
        {
            CheckForInitialPlayerMovement();
            if (!hasStartedReplay)
            {
                // Chưa đến lúc chạy: Đứng yên hoàn toàn tại vị trí ban đầu
                rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
                anim.SetFloat("Speed", 0f);
                ApplyCustomGravity(false);
                return;
            }
        }

        coyoteTimeCounter = isGrounded ? coyoteTime : coyoteTimeCounter - Time.fixedDeltaTime;

        float currentTargetTime = Time.time - delaySeconds;
        List<PlayerAction> actions = replayManager.GetActionsBetween(lastProcessedTime, currentTargetTime);

        // Kích hoạt các nút bấm (nhảy, lướt, chém)
        foreach (var action in actions)
        {
            ProcessAction(action);
        }

        // Xử lý di chuyển liên tục
        if (dashTimer > 0f)
        {
            dashTimer -= Time.fixedDeltaTime;
            rb.linearVelocity = new Vector2(dashDirection * dashSpeed, 0f);
        }
        else if (replayManager.GetActionAtTime(currentTargetTime, out PlayerAction latestAction))
        {
            // Quay mặt
            if (latestAction.isFacingRight != isFacingRight)
            {
                isFacingRight = latestAction.isFacingRight;
                Vector3 scale = transform.localScale;
                scale.x = isFacingRight ? Mathf.Abs(scale.x) : -Mathf.Abs(scale.x);
                transform.localScale = scale;
            }

            // Tính toán vận tốc (Chỉ dùng vận tốc để chạy, KHÔNG ép tọa độ vị trí)
            bool canRun = latestAction.isRunning && Mathf.Abs(latestAction.moveX) > 0.01f;
            float currentSpeed = canRun ? runSpeed : walkSpeed;
            rb.linearVelocity = new Vector2(latestAction.moveX * currentSpeed, rb.linearVelocity.y);

            // Cập nhật hoạt ảnh
            float animSpeed = Mathf.Abs(latestAction.moveX) > 0.01f ? (canRun ? 2f : 1f) : 0f;
            anim.SetFloat("Speed", isGrounded ? animSpeed : 0f);

            ApplyCustomGravity(latestAction.jumpHeld);
        }

        lastProcessedTime = currentTargetTime;
    }

    private void CheckForInitialPlayerMovement()
    {
        // Quét lịch sử hành động để tìm mốc thời gian phát sinh thao tác đầu tiên
        if (firstActionTime < 0f)
        {
            List<PlayerAction> initialActions = replayManager.GetActionsBetween(-1f, Time.time);
            foreach (var act in initialActions)
            {
                bool isActionOccurred = Mathf.Abs(act.moveX) > 0.01f || act.jumpPressed || act.dashPressed ||
                                        act.attack1Pressed || act.attack2Pressed || act.attack3Pressed;
                if (isActionOccurred)
                {
                    firstActionTime = act.time;
                    break;
                }
            }
        }

        // Nếu đã có hành động và đã trôi qua đủ thời gian trễ (delaySeconds)
        if (firstActionTime >= 0f && Time.time >= firstActionTime + delaySeconds)
        {
            hasStartedReplay = true;
            lastProcessedTime = firstActionTime;
        }
    }

    public void ToggleAnchor()
    {
        EnsurePlayerStatusReference();

        if (playerStatus == null) return;

        if (!isAnchored && !isFallingToAnchor)
        {
            if (playerStatus.ConsumeShadowEnergy(1))
            {
                if (isGrounded)
                {
                    isAnchored = true;
                    anchoredPosition = transform.position;
                    rb.linearVelocity = Vector2.zero;
                    rb.bodyType = RigidbodyType2D.Kinematic;
                    if (sr != null) sr.color = anchorColor;
                }
                else
                {
                    isFallingToAnchor = true;
                    rb.bodyType = RigidbodyType2D.Dynamic;
                    rb.linearVelocity = new Vector2(0f, -anchorFallSpeed);
                    if (sr != null) sr.color = anchorColor;
                }
            }
        }
        else
        {
            isAnchored = false;
            isFallingToAnchor = false;
            rb.bodyType = RigidbodyType2D.Dynamic;
            lastProcessedTime = Time.time - delaySeconds;
            if (sr != null) sr.color = originalColor;
        }
    }

    private void ProcessAction(PlayerAction action)
    {
        if (action.dashPressed)
        {
            dashTimer = dashDuration;
            dashDirection = Mathf.Abs(action.moveX) > 0.01f ? Mathf.Sign(action.moveX) : (action.isFacingRight ? 1f : -1f);
            anim.SetTrigger("Dash");
            return;
        }

        if (action.jumpPressed && (isGrounded || coyoteTimeCounter > 0f))
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
            coyoteTimeCounter = 0f;
        }

        if (action.attack1Pressed)
        {
            currentAttackDamage = 20f;
            if (!isGrounded && action.moveY < -0.3f)
            {
                anim.SetTrigger(isFacingRight ? "Attack1_Down" : "Attack1_DownLeft");
                rb.linearVelocity = new Vector2(rb.linearVelocity.x, pogoForce);
            }
            else
            {
                anim.SetTrigger("Attack1");
            }
        }
        else if (action.attack2Pressed)
        {
            currentAttackDamage = 35f;
            anim.SetTrigger("Attack2");
        }
        else if (action.attack3Pressed)
        {
            currentAttackDamage = 50f;
            anim.SetTrigger("Attack3");
        }
    }

    #region IDamageable Implementation
    public void TakeDamage(float damage)
    {
        EnsurePlayerStatusReference();

        float transferredDamage = damage * damageTransferRatio;

        if (playerStatus != null && !playerStatus.IsDead)
        {
            playerStatus.TakeDamage(transferredDamage);
        }

        if (sr != null)
        {
            StopCoroutine(nameof(HitFlashRoutine));
            StartCoroutine(nameof(HitFlashRoutine));
        }

        anim.SetTrigger("Hurt");
    }

    private IEnumerator HitFlashRoutine()
    {
        sr.color = hitColor;
        yield return new WaitForSeconds(0.1f);
        sr.color = (isAnchored || isFallingToAnchor) ? anchorColor : originalColor;
    }
    #endregion

    public void TriggerPlayerAttackHitbox()
    {
        if (attackPoint == null) return;

        Collider2D[] hitEnemies = Physics2D.OverlapCircleAll(attackPoint.position, attackRange, enemyLayer);
        foreach (Collider2D enemy in hitEnemies)
        {
            if (enemy.TryGetComponent<IDamageable>(out var damageableTarget))
            {
                damageableTarget.TakeDamage(currentAttackDamage);
                if (playerStatus != null) playerStatus.AddDamageEnergyCharge(currentAttackDamage);
            }
        }
    }

    private void ApplyCustomGravity(bool jumpHeld)
    {
        if (dashTimer > 0f) return;

        if (rb.linearVelocity.y < 0)
        {
            rb.linearVelocity += Vector2.up * Physics2D.gravity.y * (fallMultiplier - 1) * Time.fixedDeltaTime;
        }
        else if (Mathf.Abs(rb.linearVelocity.y) < jumpApexThreshold)
        {
            rb.linearVelocity += Vector2.up * Physics2D.gravity.y * (jumpApexHangTime - 1) * Time.fixedDeltaTime;
        }
        else if (rb.linearVelocity.y > 0 && !jumpHeld)
        {
            rb.linearVelocity += Vector2.up * Physics2D.gravity.y * (lowJumpMultiplier - 1) * Time.fixedDeltaTime;
        }

        if (rb.linearVelocity.y < -maxFallSpeed)
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, -maxFallSpeed);
        }
    }

    private void CheckGrounded()
    {
        isGrounded = groundCheck != null && Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundLayer);
    }

    private void OnDrawGizmos()
    {
        if (isAnchored)
        {
            Gizmos.color = new Color(0.7f, 0f, 1f, 0.8f);
            Gizmos.DrawWireSphere(transform.position, 0.6f);
            Gizmos.DrawLine(transform.position + Vector3.left * 0.5f, transform.position + Vector3.right * 0.5f);
            Gizmos.DrawLine(transform.position + Vector3.up * 0.5f, transform.position + Vector3.down * 0.5f);
        }

        if (attackPoint != null)
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawWireSphere(attackPoint.position, attackRange);
        }
    }
}