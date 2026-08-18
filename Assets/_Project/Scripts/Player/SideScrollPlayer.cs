using UnityEngine;
using Combat;
using PlayerReplay;

namespace Player
{
    public enum PlayerState
    {
        Idle,
        Walking,
        Running,
        Dashing,
        Jumping,
        Falling,
        Attacking,
        Dead
    }

    [RequireComponent(typeof(Rigidbody2D), typeof(Animator), typeof(PlayerStatus))]
    public class SideScrollPlayer : MonoBehaviour
    {
        [Header("State Management")] 
        [SerializeField] private PlayerState currentState = PlayerState.Idle;
        public PlayerState CurrentState => currentState;

        [Header("Movement Progression (Walk -> Run -> Dash)")] 
        [SerializeField] private float walkSpeed = 5f;
        [SerializeField] private float runSpeed = 9f;
        [SerializeField] private float dashSpeed = 28f;
        [SerializeField] private float dashDuration = 0.12f;
        [SerializeField] private float dashCooldown = 0.6f;
        [SerializeField] private float dashStaminaCost = 25f;

        [Header("Hollow Knight Jump Feel")] 
        [SerializeField] private float jumpForce = 14f;
        [SerializeField] private float jumpStaminaCost = 10f;
        [SerializeField] private float fallMultiplier = 3f;
        [SerializeField] private float lowJumpMultiplier = 2.2f;
        [SerializeField] private float coyoteTime = 0.1f;
        [SerializeField] private float jumpBufferTime = 0.1f;
        [SerializeField] private float maxFallSpeed = 20f;
        [SerializeField] private float jumpApexThreshold = 1.5f;
        [SerializeField] private float jumpApexHangTime = 0.5f;

        [Header("Attack & Hitbox Settings")] 
        [SerializeField] private float attackCooldown = 0.35f;
        [SerializeField] private float pogoForce = 12f;
        [SerializeField] private Transform attackPoint;
        [SerializeField] private float attackRange = 0.8f;
        [SerializeField] private LayerMask enemyLayer;

        [Header("Attack Damage & Stamina Costs")] 
        [SerializeField] private float attack1Damage = 20f;
        [SerializeField] private float attack1Cost = 15f;
        [SerializeField] private float attack2Damage = 35f;
        [SerializeField] private float attack2Cost = 25f;
        [SerializeField] private float attack3Damage = 50f;
        [SerializeField] private float attack3Cost = 40f;
        [SerializeField] private float runStaminaCost = 10f;

        [Header("Ground Check Settings")] 
        [SerializeField] private Transform groundCheck;
        [SerializeField] private float groundCheckRadius = 0.2f;
        [SerializeField] private LayerMask groundLayer;

        [Header("Input Buffer Settings")] 
        [SerializeField] private float attackBufferTime = 0.15f;
        private float attackBufferCounter = 0f;
        private int bufferedAttackType = 0;

        private bool attack1ExecutedThisFrame = false;
        private bool attack2ExecutedThisFrame = false;
        private bool attack3ExecutedThisFrame = false;
        private bool dashExecutedThisFrame = false;
        private bool jumpExecutedThisFrame = false;

        private float currentAttackDamage = 20f;
        private bool isPogoActive = false;

        private float dashTimer = 0f;
        private float nextDashTime = 0f;
        private float dashDirection = 1f;

        private Rigidbody2D rb;
        private Animator anim;
        private PlayerStatus status;
        private PlayerInputHandler inputHandler;
        private PlayerInputData activeInput;

        private bool isGrounded;
        private bool isFacingRight = true;
        private float coyoteTimeCounter;
        private float jumpBufferCounter;
        private float nextAttackTime = 0f;
        private float attackTimer = 0f;
        private PlayerReplayManager replayManager;

        void Start()
        {
            rb = GetComponent<Rigidbody2D>();
            anim = GetComponent<Animator>();
            status = GetComponent<PlayerStatus>();
            inputHandler = GetComponent<PlayerInputHandler>();
            replayManager = FindObjectOfType<PlayerReplayManager>();

            rb.interpolation = RigidbodyInterpolation2D.Interpolate;
            ChangeState(PlayerState.Idle);
        }

        void Update()
        {
            if (status.IsDead) return;

            attack1ExecutedThisFrame = false;
            attack2ExecutedThisFrame = false;
            attack3ExecutedThisFrame = false;
            dashExecutedThisFrame = false;
            jumpExecutedThisFrame = false;

            if (inputHandler != null)
            {
                activeInput = inputHandler.CurrentInput;
            }

            HandleDashInput();
            UpdateAnimatorSpeed();
            HandleFlipping();
            UpdateJumpTimers();
            UpdateAttackBuffer();
            HandleJump();
            HandleAttacks();
            HandleDebugKeyboardTests();
            UpdatePlayerState();

            if (replayManager != null)
            {
                PlayerAction currentAction = new PlayerAction
                {
                    time = Time.time,
                    position = transform.position,
                    moveX = activeInput.moveX,
                    moveY = activeInput.moveY,
                    isRunning = activeInput.isRunning,
                    jumpPressed = jumpExecutedThisFrame,
                    jumpHeld = activeInput.jumpHeld,
                    dashPressed = dashExecutedThisFrame,
                    attack1Pressed = attack1ExecutedThisFrame,
                    attack2Pressed = attack2ExecutedThisFrame,
                    attack3Pressed = attack3ExecutedThisFrame,
                    isFacingRight = isFacingRight
                };
                replayManager.RecordAction(currentAction);
            }

            if (inputHandler != null)
            {
                inputHandler.ClearTriggers();
            }
        }

        void FixedUpdate()
        {
            if (status.IsDead) return;

            CheckGrounded();

            if (dashTimer > 0f)
            {
                dashTimer -= Time.fixedDeltaTime;
                rb.linearVelocity = new Vector2(dashDirection * dashSpeed, 0f);
                return;
            }

            bool hasMoveInput = Mathf.Abs(activeInput.moveX) > 0.01f;
            bool canRun = activeInput.isRunning && status.HasEnoughStamina(runStaminaCost * Time.fixedDeltaTime) &&
                          hasMoveInput;
            float currentSpeed = canRun ? runSpeed : walkSpeed;

            if (canRun)
            {
                status.UseStamina(runStaminaCost * Time.fixedDeltaTime, isContinuous: true);
            }

            rb.linearVelocity = new Vector2(activeInput.moveX * currentSpeed, rb.linearVelocity.y);
            ApplyCustomGravity();
        }

        private void HandleDashInput()
        {
            if (activeInput.dashPressed && Time.time >= nextDashTime && status.UseStamina(dashStaminaCost))
            {
                dashTimer = dashDuration;
                nextDashTime = Time.time + dashCooldown;
                dashDirection = Mathf.Abs(activeInput.moveX) > 0.01f
                    ? Mathf.Sign(activeInput.moveX)
                    : (isFacingRight ? 1f : -1f);

                anim.SetTrigger("Dash");
                ChangeState(PlayerState.Dashing);
                dashExecutedThisFrame = true;
            }
        }

        #region Attack & Damage Execution

        private void UpdateAttackBuffer()
        {
            if (activeInput.attack1Pressed)
            {
                bufferedAttackType = 1;
                attackBufferCounter = attackBufferTime;
            }
            else if (activeInput.attack2Pressed)
            {
                bufferedAttackType = 2;
                attackBufferCounter = attackBufferTime;
            }
            else if (activeInput.attack3Pressed)
            {
                bufferedAttackType = 3;
                attackBufferCounter = attackBufferTime;
            }
            else if (attackBufferCounter > 0f)
            {
                attackBufferCounter -= Time.deltaTime;
                if (attackBufferCounter <= 0f) bufferedAttackType = 0;
            }
        }

        private void HandleAttacks()
        {
            if (dashTimer > 0f || Time.time < nextAttackTime || attackBufferCounter <= 0f) return;

            if (bufferedAttackType == 1)
            {
                currentAttackDamage = attack1Damage;
                TryExecuteAttack(attack1Cost, () =>
                {
                    if (!isGrounded && activeInput.moveY < -0.3f)
                    {
                        anim.SetTrigger(isFacingRight ? "Attack1_Down" : "Attack1_DownLeft");
                        rb.linearVelocity = new Vector2(rb.linearVelocity.x, pogoForce);
                        isPogoActive = true;
                    }
                    else
                    {
                        anim.SetTrigger("Attack1");
                    }

                    attack1ExecutedThisFrame = true;
                });
                attackBufferCounter = 0f;
                bufferedAttackType = 0;
            }
            else if (bufferedAttackType == 2)
            {
                currentAttackDamage = attack2Damage;
                TryExecuteAttack(attack2Cost, () =>
                {
                    anim.SetTrigger("Attack2");
                    attack2ExecutedThisFrame = true;
                });
                attackBufferCounter = 0f;
                bufferedAttackType = 0;
            }
            else if (bufferedAttackType == 3)
            {
                currentAttackDamage = attack3Damage;
                TryExecuteAttack(attack3Cost, () =>
                {
                    anim.SetTrigger("Attack3");
                    attack3ExecutedThisFrame = true;
                });
                attackBufferCounter = 0f;
                bufferedAttackType = 0;
            }
        }

        private void TryExecuteAttack(float staminaCost, System.Action animationAction)
        {
            if (status.UseStamina(staminaCost))
            {
                animationAction.Invoke();
                nextAttackTime = Time.time + attackCooldown;
                attackTimer = 0.25f;
            }
        }

        public void TriggerPlayerAttackHitbox()
        {
            if (attackPoint == null) return;

            Collider2D[] hitEnemies = Physics2D.OverlapCircleAll(attackPoint.position, attackRange, enemyLayer);
            foreach (Collider2D enemy in hitEnemies)
            {
                if (enemy.TryGetComponent<IDamageable>(out var damageableTarget))
                {
                    damageableTarget.TakeDamage(currentAttackDamage);
                    if (status != null) status.AddDamageEnergyCharge(currentAttackDamage);
                }
            }
        }

        #endregion

        #region Debug & Controls

        private void HandleDebugKeyboardTests()
        {
            if (Input.GetKeyDown(KeyCode.H))
            {
                status.TakeDamage(20f);
                if (!status.IsDead) anim.SetTrigger("Hurt");
            }

            if (Input.GetKeyDown(KeyCode.R))
            {
                status.Heal(30f);
            }
        }

        private void ChangeState(PlayerState newState)
        {
            if (currentState == newState) return;
            currentState = newState;
        }

        private void UpdatePlayerState()
        {
            if (status.IsDead)
            {
                if (currentState != PlayerState.Dead)
                {
                    ChangeState(PlayerState.Dead);
                    anim.SetBool("IsDead", true);
                    rb.linearVelocity = new Vector2(0, rb.linearVelocity.y);
                }

                return;
            }

            if (dashTimer > 0f) return;

            if (attackTimer > 0f)
            {
                attackTimer -= Time.deltaTime;
                ChangeState(PlayerState.Attacking);
                return;
            }

            if (!isGrounded)
            {
                ChangeState(rb.linearVelocity.y > 0.1f ? PlayerState.Jumping : PlayerState.Falling);
                return;
            }

            if (Mathf.Abs(activeInput.moveX) > 0.01f)
            {
                ChangeState(activeInput.isRunning && status.HasEnoughStamina(runStaminaCost * Time.deltaTime)
                    ? PlayerState.Running
                    : PlayerState.Walking);
            }
            else
            {
                ChangeState(PlayerState.Idle);
            }
        }

        private void UpdateJumpTimers()
        {
            coyoteTimeCounter = isGrounded ? coyoteTime : coyoteTimeCounter - Time.deltaTime;
            jumpBufferCounter = activeInput.jumpPressed ? jumpBufferTime : jumpBufferCounter - Time.deltaTime;
        }

        private void HandleJump()
        {
            if (dashTimer > 0f) return;

            if (jumpBufferCounter > 0f && coyoteTimeCounter > 0f)
            {
                if (status.UseStamina(jumpStaminaCost))
                {
                    rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
                    jumpBufferCounter = 0f;
                    coyoteTimeCounter = 0f;
                    jumpExecutedThisFrame = true;
                    isPogoActive = false;
                    ChangeState(PlayerState.Jumping);
                }
            }
        }

        private void ApplyCustomGravity()
        {
            if (dashTimer > 0f) return;

            if (isPogoActive && rb.linearVelocity.y <= 0f)
            {
                isPogoActive = false;
            }

            if (rb.linearVelocity.y < 0)
            {
                rb.linearVelocity += Vector2.up * Physics2D.gravity.y * (fallMultiplier - 1) * Time.fixedDeltaTime;
            }
            else if (Mathf.Abs(rb.linearVelocity.y) < jumpApexThreshold)
            {
                rb.linearVelocity += Vector2.up * Physics2D.gravity.y * (jumpApexHangTime - 1) * Time.fixedDeltaTime;
            }
            else if (rb.linearVelocity.y > 0 && !activeInput.jumpHeld && !isPogoActive)
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
            isGrounded = groundCheck != null &&
                         Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundLayer);
        }

        private void UpdateAnimatorSpeed()
        {
            if (!isGrounded)
            {
                anim.SetFloat("Speed", 0f);
                return;
            }

            float animSpeed = Mathf.Abs(activeInput.moveX) > 0.01f
                ? (activeInput.isRunning && status.HasEnoughStamina(runStaminaCost * Time.deltaTime) ? 2f : 1f)
                : 0f;
            anim.SetFloat("Speed", animSpeed);
        }

        private void HandleFlipping()
        {
            if (dashTimer > 0f) return;

            if ((activeInput.moveX > 0 && !isFacingRight) || (activeInput.moveX < 0 && isFacingRight))
            {
                isFacingRight = !isFacingRight;
                Vector3 scale = transform.localScale;
                scale.x = isFacingRight ? Mathf.Abs(scale.x) : -Mathf.Abs(scale.x);
                transform.localScale = scale;
            }

            anim.SetBool("IsFacingRight", isFacingRight);
        }

        #endregion

        private void OnDrawGizmosSelected()
        {
            if (groundCheck != null)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawWireSphere(groundCheck.position, groundCheckRadius);
            }

            if (attackPoint != null)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireSphere(attackPoint.position, attackRange);
            }
        }
    }
}