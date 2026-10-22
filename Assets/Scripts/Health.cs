using UnityEngine;
using UnityEngine.Events;

[DisallowMultipleComponent]
public class Health : MonoBehaviour
{
    [SerializeField, Min(1)] private int maxHealth = 100;
    [SerializeField] private bool disableCollidersOnDeath;
    [SerializeField, Tooltip("临时血量日志；正式 UI 完成后可关闭。")]
    private bool logHealthChanges = true;
    [SerializeField] private UnityEvent onDeath = new UnityEvent();

    public int MaxHealth => maxHealth;
    public int CurrentHealth { get; private set; }
    public bool IsDead { get; private set; }
    public UnityEvent OnDeath => onDeath;

    private void Awake()
    {
        maxHealth = Mathf.Max(1, maxHealth);
        CurrentHealth = maxHealth;
        LogHealth("初始化");
    }
  
    public void TakeDamage(int amount)
    {
        if (IsDead || amount <= 0) return;
        int previousHealth = CurrentHealth;
        CurrentHealth = amount >= CurrentHealth ? 0 : CurrentHealth - amount;
        LogHealth($"受到伤害 -{previousHealth - CurrentHealth}");
        if (CurrentHealth > 0) return;

        // Set the guard before callbacks, including callbacks that deal damage again.
        IsDead = true;
        Destroy(gameObject);
        LogHealth("死亡");
        if (disableCollidersOnDeath)
        {
            foreach (Collider targetCollider in GetComponentsInChildren<Collider>(true))
            {
                if (targetCollider.GetComponentInParent<Health>() == this)
                {
                    targetCollider.enabled = false;
                }
            }
        }
        onDeath.Invoke();
    }

    public void Heal(int amount)
    {
        if (IsDead || amount <= 0)
        {
            return;
        }
        int healed = Mathf.Min(amount, maxHealth - CurrentHealth);
        if (healed == 0)
        {
            return;
        }
        CurrentHealth += healed;
        LogHealth($"回血 +{healed}");
    }

    private void LogHealth(string action)
    {
        if (logHealthChanges)
        {
            Debug.Log($"[Health] {name}：{action}，HP {CurrentHealth}/{MaxHealth}", this);
        }
    }

    private void OnValidate()
    {
        maxHealth = Mathf.Max(1, maxHealth);
    }
}
