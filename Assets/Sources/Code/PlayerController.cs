using UnityEngine;

[RequireComponent(typeof(Rigidbody2D), typeof(Animator))]
public class SideScrollPlayer : MonoBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float walkSpeed = 5f;
    [SerializeField] private float runSpeed = 9f;

    [Header("Hollow Knight Jump Feel")]
    [SerializeField] private float jumpForce = 13f;
    [SerializeField] private float fallMultiplier = 2.5f;
    [SerializeField] private float lowJumpMultiplier = 2f;
    [SerializeField] private float coyoteTime = 0.1f;
    [SerializeField] private float jumpBufferTime = 0.1f;

    [Header("Pogo & Attack Settings")]
    [SerializeField] private float pogoForce = 12f;
    [SerializeField] private float attackCooldown = 0.35f; // Thời gian chờ giữa 2 lần chém (giây)
    private float nextAttackTime = 0f;

    [Header("Ground Check Settings")]
    [SerializeField] private Transform groundCheck;
    [SerializeField] private float groundCheckRadius = 0.2f;
    [SerializeField] private LayerMask groundLayer;

    [Header("Components")]
    private Rigidbody2D rb;
    private Animator anim;

    private float moveInputX;
    private bool isRunning;
    private bool isGrounded;
    private bool isFacingRight = true;
    private bool isDead = false;

    // Bộ đếm thời gian
    private float coyoteTimeCounter;
    private float jumpBufferCounter;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        anim = GetComponent<Animator>();

        // Đảm bảo Rigidbody2D mượt hình
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
    }

    void Update()
    {
        if (isDead) return;

        // 1. Input di chuyển
        moveInputX = Input.GetAxisRaw("Horizontal");
        isRunning = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);

        // 2. Cập nhật Animator Speed
        UpdateAnimatorSpeed();

        // 3. Quay mặt nhân vật
        HandleFlipping();

        // 4. Coyote Time & Jump Buffer Logic
        UpdateJumpTimers();

        // 5. Xử lý Nhảy
        HandleJump();

        // 6. Xử lý Tấn công (Đã kiểm tra cooldown chống spam)
        HandleAttacks();

        // 7. Test Health
        HandleHealthTest();
    }

    void FixedUpdate()
    {
        if (isDead) return;

        CheckGrounded();

        // Di chuyển ngang
        float currentSpeed = isRunning ? runSpeed : walkSpeed;
        rb.linearVelocity = new Vector2(moveInputX * currentSpeed, rb.linearVelocity.y);

        // Tối ưu lực hút trọng lực
        ApplyCustomGravity();
    }

    private void UpdateJumpTimers()
    {
        if (isGrounded)
        {
            coyoteTimeCounter = coyoteTime;
        }
        else
        {
            coyoteTimeCounter -= Time.deltaTime;
        }

        if (Input.GetButtonDown("Jump"))
        {
            jumpBufferCounter = jumpBufferTime;
        }
        else
        {
            jumpBufferCounter -= Time.deltaTime;
        }
    }

    private void HandleJump()
    {
        if (jumpBufferCounter > 0f && coyoteTimeCounter > 0f)
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
            jumpBufferCounter = 0f;
            coyoteTimeCounter = 0f;
        }
    }

    private void ApplyCustomGravity()
    {
        if (rb.linearVelocity.y < 0)
        {
            rb.linearVelocity += Vector2.up * Physics2D.gravity.y * (fallMultiplier - 1) * Time.fixedDeltaTime;
        }
        else if (rb.linearVelocity.y > 0 && !Input.GetButton("Jump"))
        {
            rb.linearVelocity += Vector2.up * Physics2D.gravity.y * (lowJumpMultiplier - 1) * Time.fixedDeltaTime;
        }
    }

    private void CheckGrounded()
    {
        if (groundCheck != null)
        {
            isGrounded = Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundLayer);
        }
        else
        {
            isGrounded = true;
        }
    }

    private void UpdateAnimatorSpeed()
    {
        if (!isGrounded)
        {
            anim.SetFloat("Speed", 0f);
            return;
        }

        float animSpeedValue = 0f;
        if (Mathf.Abs(moveInputX) > 0.01f)
        {
            animSpeedValue = isRunning ? 2f : 1f;
        }

        anim.SetFloat("Speed", animSpeedValue);
    }

    private void HandleFlipping()
    {
        if (moveInputX > 0 && !isFacingRight)
        {
            Flip();
        }
        else if (moveInputX < 0 && isFacingRight)
        {
            Flip();
        }
    }

    private void Flip()
    {
        isFacingRight = !isFacingRight;
        
        // Gán trực tiếp giá trị tuyệt đối để tránh sai số lũy kế khi nhân *= -1
        Vector3 currentScale = transform.localScale;
        currentScale.x = isFacingRight ? Mathf.Abs(currentScale.x) : -Mathf.Abs(currentScale.x);
        transform.localScale = currentScale;
    }

    private void HandleAttacks()
    {
        // Chặn spam đòn đánh nếu chưa hết cooldown
        if (Time.time < nextAttackTime) return;

        float verticalInput = Input.GetAxisRaw("Vertical");

        // Click Chuột Trái
        if (Input.GetMouseButtonDown(0))
        {
            // Đang trên không + Giữ phím S/Mũi tên xuống -> Chém xuống & Pogo Jump
            if (!isGrounded && verticalInput < -0.1f)
            {
                // Kiểm tra hướng quay mặt để gọi Trigger tương ứng
                if (isFacingRight)
                {
                    anim.SetTrigger("Attack1_Down");
                }
                else
                {
                    anim.SetTrigger("Attack1_DownLeft");
                }

                // Đẩy nhân vật nảy người lên trên
                rb.linearVelocity = new Vector2(rb.linearVelocity.x, pogoForce);
                
                nextAttackTime = Time.time + attackCooldown;
            }
            // Đứng dưới đất hoặc chém bình thường -> Chém ngang
            else
            {
                anim.SetTrigger("Attack1");
                nextAttackTime = Time.time + attackCooldown;
            }
        }
        // Chuột phải (Attack 2)
        else if (Input.GetMouseButtonDown(1))
        {
            anim.SetTrigger("Attack2");
            nextAttackTime = Time.time + attackCooldown;
        }
        // Phím F (Attack 3)
        else if (Input.GetKeyDown(KeyCode.F))
        {
            anim.SetTrigger("Attack3");
            nextAttackTime = Time.time + attackCooldown;
        }
    }

    private void HandleHealthTest()
    {
        if (Input.GetKeyDown(KeyCode.H))
        {
            anim.SetTrigger("Hurt");
        }

        if (Input.GetKeyDown(KeyCode.X))
        {
            Die();
        }
    }

    public void Die()
    {
        isDead = true;
        anim.SetBool("IsDead", true);
        rb.linearVelocity = new Vector2(0, rb.linearVelocity.y);
    }

    private void OnDrawGizmosSelected()
    {
        if (groundCheck != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(groundCheck.position, groundCheckRadius);
        }
    }
}