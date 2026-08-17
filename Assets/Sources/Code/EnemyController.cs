using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum EnemyState
{
    Patrol,
    Chase,
    Attack,
    Evade,
    Stunned,
    Dead
}

[RequireComponent(typeof(Rigidbody2D), typeof(Animator))]
public class EnemyController : MonoBehaviour, IDamageable
{
    [Header("Current State")]
    [SerializeField] private EnemyState currentState = EnemyState.Patrol;

    [Header("Stats")]
    [SerializeField] private float maxHealth = 300f;
    [SerializeField] private bool isDummy = false;
    private float currentHealth;
    public bool IsDead => currentHealth <= 0 && !isDummy;

    [Header("Movement & Patrol")]
    [SerializeField] private float moveSpeed = 3f;
    [SerializeField] private float chaseSpeed = 5f;
    [SerializeField] private Transform groundCheck;
    [SerializeField] private Transform wallCheck;
    [SerializeField] private float groundCheckRadius = 0.2f; 
    [SerializeField] private float wallCheckRadius = 0.1f; 
    [SerializeField] private LayerMask groundLayer;

    [Header("Smart AI & Evasion")]
    [SerializeField] private float detectRadius = 8f;
    [SerializeField] private float attackRange = 1.6f;
    [SerializeField] private float evadeForceX = 7f;
    [SerializeField] private float evadeForceY = 6f;
    [SerializeField] private float evadeCooldown = 2.5f;
    [Range(0f, 1f)] [SerializeField] private float blockChance = 0.35f;

    [Header("Combat Settings")]
    [SerializeField] private Transform attackPoint;
    [SerializeField] private float attackHitboxRadius = 1.2f;
    [SerializeField] private float attackDamage = 15f;
    [SerializeField] private LayerMask targetLayer;
    [SerializeField] private float attackCooldown = 1.2f;

    [Header("Hit Feedback")]
    [SerializeField] private float knockbackForce = 4f;
    [SerializeField] private Color hitColor = new Color(1f, 0.4f, 0.4f, 1f);
    [SerializeField] private Color blockColor = new Color(0.4f, 0.8f, 1f, 1f);

    private Rigidbody2D rb;
    private Animator anim;
    private SpriteRenderer sr;
    private Color originalColor;

    private Transform currentTarget;
    private int movingDirection = 1;
    private float nextAttackTime = 0f;
    private float nextEvadeTime = 0f;
    private float stateTimer = 0f;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        anim = GetComponent<Animator>();
        sr = GetComponent<SpriteRenderer>();

        if (sr != null) originalColor = sr.color;
        currentHealth = maxHealth;
    }

    void Update()
    {
        if (IsDead) return;

        if (stateTimer > 0f)
        {
            stateTimer -= Time.deltaTime;
            return;
        }

        EvaluateTarget();
        HandleFSM();
    }

    void FixedUpdate()
    {
        if (IsDead || stateTimer > 0f) return;

        switch (currentState)
        {
            case EnemyState.Patrol:
                PatrolLogic();
                break;
            case EnemyState.Chase:
                ChaseLogic();
                break;
        }
    }

    #region AI FSM Logic
    private void EvaluateTarget()
    {
        Collider2D[] targets = Physics2D.OverlapCircleAll(transform.position, detectRadius, targetLayer);
        Transform closest = null;
        float minDistance = Mathf.Infinity;

        foreach (var col in targets)
        {
            float dist = Vector2.Distance(transform.position, col.transform.position);
            if (dist < minDistance)
            {
                minDistance = dist;
                closest = col.transform;
            }
        }

        currentTarget = closest;
    }

    private void HandleFSM()
    {
        if (currentTarget == null)
        {
            currentState = EnemyState.Patrol;
            return;
        }

        float dist = Vector2.Distance(transform.position, currentTarget.position);

        if ((currentTarget.position.x > transform.position.x && movingDirection == -1) ||
            (currentTarget.position.x < transform.position.x && movingDirection == 1))
        {
            Flip();
        }

        if (dist <= attackRange)
        {
            currentState = EnemyState.Attack;
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
            anim.SetBool("IsWalking", false);

            if (Time.time >= nextAttackTime)
            {
                ExecuteAttack();
                nextAttackTime = Time.time + attackCooldown;
            }
        }
        else
        {
            currentState = EnemyState.Chase;
        }
    }

    private void PatrolLogic()
    {
        rb.linearVelocity = new Vector2(movingDirection * moveSpeed, rb.linearVelocity.y);
        anim.SetBool("IsWalking", true);

        bool isGrounded = groundCheck != null && Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundLayer);
        bool isHitWall = wallCheck != null && Physics2D.OverlapCircle(wallCheck.position, wallCheckRadius, groundLayer);

        if (!isGrounded || isHitWall)
        {
            Flip();
        }
    }

    private void ChaseLogic()
    {
        bool isGrounded = groundCheck != null && Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundLayer);
        bool isHitWall = wallCheck != null && Physics2D.OverlapCircle(wallCheck.position, wallCheckRadius, groundLayer);

        if (!isGrounded || isHitWall)
        {
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
            anim.SetBool("IsWalking", false);
        }
        else
        {
            rb.linearVelocity = new Vector2(movingDirection * chaseSpeed, rb.linearVelocity.y);
            anim.SetBool("IsWalking", true);
        }
    }

    private void ExecuteAttack()
    {
        int attackIndex = Random.Range(0, 2);
        anim.SetTrigger(attackIndex == 0 ? "Attack1" : "Attack2");
    }

    private void TriggerEvade(Vector2 sourcePosition)
    {
        if (Time.time < nextEvadeTime) return;

        currentState = EnemyState.Evade;
        nextEvadeTime = Time.time + evadeCooldown;
        stateTimer = 0.35f;

        float awayDir = Mathf.Sign(transform.position.x - sourcePosition.x);
        rb.linearVelocity = new Vector2(awayDir * evadeForceX, evadeForceY);
        anim.SetTrigger("Hurt");
    }
    #endregion

    #region Damage, Block & Hit Events
    public void TakeDamage(float damage)
    {
        if (IsDead) return;

        if (Random.value < blockChance && currentState != EnemyState.Evade)
        {
            float reducedDamage = damage * 0.2f;
            currentHealth = Mathf.Max(0, currentHealth - reducedDamage);
            Debug.Log($"<color=cyan>[BLOCKED]:</color> Quái chặn đòn! Nhận {reducedDamage} HP (Giảm 80%)");
            
            StartCoroutine(FlashRoutine(blockColor));
            if (currentTarget != null) TriggerEvade(currentTarget.position);
            return;
        }

        currentHealth = Mathf.Max(0, currentHealth - damage);
        Debug.Log($"<color=yellow>[HIT]:</color> {gameObject.name} trúng đòn -{damage} HP | Còn: <color=red>{currentHealth}/{maxHealth}</color>");

        if (currentTarget != null)
        {
            float hitDir = Mathf.Sign(transform.position.x - currentTarget.position.x);
            rb.linearVelocity = new Vector2(hitDir * knockbackForce, 2f);
            stateTimer = 0.15f;
        }

        StartCoroutine(FlashRoutine(hitColor));

        if (IsDead)
        {
            Die();
        }
        else
        {
            anim.SetTrigger("Hurt");
            if (currentTarget != null && Vector2.Distance(transform.position, currentTarget.position) < 1.5f)
            {
                TriggerEvade(currentTarget.position);
            }
        }
    }

    public void TriggerAttackHitbox()
    {
        if (attackPoint == null) return;

        Collider2D[] hits = Physics2D.OverlapCircleAll(attackPoint.position, attackHitboxRadius, targetLayer);
        foreach (Collider2D hit in hits)
        {
            if (hit.TryGetComponent<IDamageable>(out var damageableTarget))
            {
                damageableTarget.TakeDamage(attackDamage);
            }
            else if (hit.TryGetComponent<PlayerStatus>(out var playerStatus))
            {
                playerStatus.TakeDamage(attackDamage);
            }
        }
    }

    private IEnumerator FlashRoutine(Color color)
    {
        if (sr == null) yield break;
        sr.color = color;
        yield return new WaitForSeconds(0.08f);
        sr.color = originalColor;
    }

    private void Flip()
    {
        movingDirection *= -1;
        Vector3 scale = transform.localScale;
        scale.x = Mathf.Abs(scale.x) * movingDirection;
        transform.localScale = scale;
    }

    private void Die()
    {
        anim.SetBool("IsDead", true);
        rb.linearVelocity = Vector2.zero;

        foreach (var col in GetComponents<Collider2D>())
        {
            col.enabled = false;
        }

        enabled = false;
        Destroy(gameObject, 2.5f);
    }
    #endregion

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectRadius);

        if (attackPoint != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(attackPoint.position, attackHitboxRadius);
        }
        if (groundCheck != null)
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawWireSphere(groundCheck.position, groundCheckRadius);
        }
        if (wallCheck != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(wallCheck.position, wallCheckRadius);
        }
    }
}