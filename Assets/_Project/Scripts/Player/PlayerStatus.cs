using UnityEngine;

namespace Player
{
    [RequireComponent(typeof(Animator))]
    public class PlayerStatus : MonoBehaviour
    {
        [Header("Health Settings")]
        [SerializeField] private float maxHealth = 100f;
        public float CurrentHealth { get; private set; }
        public bool IsDead => CurrentHealth <= 0;

        [Header("Stamina Settings")]
        [SerializeField] private float maxStamina = 100f;
        [SerializeField] private float staminaRegenRate = 18f;
        [SerializeField] private float staminaRegenDelay = 1.25f;
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

        public bool ConsumeShadowEnergy(int amount = 1)
        {
            if (CurrentShadowEnergy >= amount)
            {
                CurrentShadowEnergy -= amount;
                return true;
            }

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

            while (currentChargePercent >= 100f && CurrentShadowEnergy < maxShadowEnergy)
            {
                currentChargePercent -= 100f;
                CurrentShadowEnergy++;

                if (CurrentShadowEnergy >= maxShadowEnergy)
                {
                    currentChargePercent = 0f;
                    break;
                }
            }
        }

        public void TakeDamage(float amount)
        {
            if (IsDead) return;

            CurrentHealth = Mathf.Clamp(CurrentHealth - amount, 0, maxHealth);

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
            var player = GetComponent<SideScrollPlayer>();
            if (player != null && player.IsDashing) return;
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
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        [Header("Debug")]
        [SerializeField] private bool showDebugInfo = true;

        void OnGUI()
        {
            if (!showDebugInfo) return;

            GUIStyle style = new GUIStyle();
            style.fontSize = 18;
            style.normal.textColor = Color.white;
            style.fontStyle = FontStyle.Bold;

            float x = 20f;
            float y = 20f;
            float lineHeight = 26f;

            GUI.Label(new Rect(x, y, 400, lineHeight), $"HP: {CurrentHealth:F0} / {maxHealth}", style);
            y += lineHeight;

            GUI.Label(new Rect(x, y, 400, lineHeight), $"Stamina: {CurrentStamina:F0} / {maxStamina}", style);
            y += lineHeight;

            GUI.Label(new Rect(x, y, 400, lineHeight), $"Shadow Energy: {CurrentShadowEnergy} / {maxShadowEnergy}", style);
            y += lineHeight;

            GUI.Label(new Rect(x, y, 400, lineHeight), $"Charge: {currentChargePercent:F1}%", style);
            y += lineHeight;

            GUI.Label(new Rect(x, y, 400, lineHeight), $"IsDead: {IsDead}", style);
        }
#endif
    }
}