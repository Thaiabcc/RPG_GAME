using UnityEngine;

[RequireComponent(typeof(Animator))]
public class PlayerStatus : MonoBehaviour
{
    [Header("Health Settings")]
    [SerializeField] private float maxHealth = 100f;
    public float CurrentHealth { get; private set; }
    public bool IsDead => CurrentHealth <= 0;

    [Header("Stamina Settings")]
    [SerializeField] private float maxStamina = 100f;
    [SerializeField] private float staminaRegenRate = 15f;
    [SerializeField] private float staminaRegenDelay = 1f;
    public float CurrentStamina { get; private set; }

    [Header("Shadow Energy Settings")]
    [SerializeField] private int maxShadowEnergy = 3;
    public int CurrentShadowEnergy { get; private set; }

    [Header("Recharge Configuration")]
    [SerializeField] private float damageThreshold = 100f;
    [SerializeField] private float rechargePercentPerThreshold = 15f;
    
    private float currentChargePercent = 0f;
    private float lastStaminaUseTime;
    private Animator anim;

    void Awake()
    {
        CurrentHealth = maxHealth;
        CurrentStamina = maxStamina;
        CurrentShadowEnergy = maxShadowEnergy;
        anim = GetComponent<Animator>();
    }

    void Update()
    {
        RegenerateStamina();
    }

    #region Shadow Energy Logic
    public bool ConsumeShadowEnergy(int amount = 1)
    {
        if (CurrentShadowEnergy >= amount)
        {
            CurrentShadowEnergy -= amount;
            PrintEnergyDebug("<color=#FF00FF>[SHADOW ENERGY USED]</color>");
            return true;
        }

        Debug.Log("<color=orange><b>[ACTION BLOCKED]:</b> Không đủ Shadow Energy (Cần 1 điểm)!</color>");
        return false;
    }

    public void AddDamageEnergyCharge(float damageDealt)
    {
        if (CurrentShadowEnergy >= maxShadowEnergy)
        {
            currentChargePercent = 0f;
            return;
        }

        float gainedPercent = (damageDealt / damageThreshold) * rechargePercentPerThreshold;
        currentChargePercent += gainedPercent;

        Debug.Log($"<color=cyan>[ENERGY CHARGING]:</color> Gây {damageDealt} dmg (+{gainedPercent:F1}%) | Tiến độ điểm tiếp theo: <color=yellow>{currentChargePercent:F1}% / 100%</color>");

        while (currentChargePercent >= 100f && CurrentShadowEnergy < maxShadowEnergy)
        {
            currentChargePercent -= 100f;
            CurrentShadowEnergy++;
            PrintEnergyDebug("<color=#00FF66><b>[SHADOW ENERGY RECHARGED +1 POINT]</b></color>");

            if (CurrentShadowEnergy >= maxShadowEnergy)
            {
                currentChargePercent = 0f;
                break;
            }
        }
    }

    private void PrintEnergyDebug(string header)
    {
        string energyIcons = new string('◆', CurrentShadowEnergy) + new string('◇', maxShadowEnergy - CurrentShadowEnergy);
        Debug.Log($"{header} Hiện có: <color=#00FFFF>[{energyIcons}]</color> ({CurrentShadowEnergy}/{maxShadowEnergy} Điểm) | Tiến độ sạc: <color=yellow>{currentChargePercent:F1}%</color>");
    }
    #endregion

    #region Health & Stamina Operations
    public void TakeDamage(float amount)
    {
        if (IsDead) return;

        CurrentHealth = Mathf.Clamp(CurrentHealth - amount, 0, maxHealth);
        Debug.Log($"<color=red>[PLAYER HEALTH]:</color> -{amount} HP | Còn lại: {CurrentHealth}/{maxHealth}");

        if (IsDead)
        {
            if (anim != null) anim.SetBool("IsDead", true);
        }
        else
        {
            if (anim != null)
            {
                anim.ResetTrigger("Hurt");
                anim.SetTrigger("Hurt");
            }
        }
    }

    public void Heal(float amount)
    {
        if (IsDead) return;
        CurrentHealth = Mathf.Min(CurrentHealth + amount, maxHealth);
        CurrentStamina = maxStamina;
    }

    public bool HasEnoughStamina(float cost) => CurrentStamina >= cost;

    public bool UseStamina(float cost, bool isContinuous = false)
    {
        if (!HasEnoughStamina(cost)) return false;

        CurrentStamina = Mathf.Max(0, CurrentStamina - cost);
        lastStaminaUseTime = Time.time;
        return true;
    }

    private void RegenerateStamina()
    {
        if (IsDead) return;

        if (Time.time >= lastStaminaUseTime + staminaRegenDelay && CurrentStamina < maxStamina)
        {
            CurrentStamina = Mathf.Min(maxStamina, CurrentStamina + staminaRegenRate * Time.deltaTime);
        }
    }
    #endregion
}