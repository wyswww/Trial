using UnityEngine;
using UnityEngine.Events;

[DisallowMultipleComponent]
// 通用生命账户：玩家和敌人都能用，负责加减血、广播死亡并安排销毁物体。
public class Health : MonoBehaviour
{
    // 最大血量像水杯容量，当前血量像水量；私有配置通过 SerializeField 显示在 Inspector。
    [SerializeField, Min(1)] private int maxHealth = 100;
    // 勾选后，死亡时关闭属于当前 Health 的碰撞体；是否勾选由 Inspector 保存。
    [SerializeField] private bool disableCollidersOnDeath;
    [SerializeField, Tooltip("临时血量日志；正式 UI 完成后可关闭。")]
    private bool logHealthChanges = true;
    [SerializeField] private UnityEvent onDeath = new UnityEvent();

    // 这些属性供其他脚本读取；当前血量和死亡状态只能由本脚本修改。
    public int MaxHealth => maxHealth;
    public int CurrentHealth { get; private set; }
    public bool IsDead { get; private set; }
    // 无敌来源名单：闪避等效果分别登记自己，避免一个效果结束时误关另一个效果的无敌。
    private readonly System.Collections.Generic.HashSet<object> invulnerabilitySources =
        new System.Collections.Generic.HashSet<object>();
    // 名单中还有任何一个来源，就仍处于无敌状态。
    public bool IsInvulnerable => invulnerabilitySources.Count > 0;

    // source 通常传调用组件的 this；active=true 登记，false 仅移除该来源。
    public void SetInvulnerable(object source, bool active)
    {
        if (active && !IsDead) invulnerabilitySources.Add(source);
        else invulnerabilitySources.Remove(source);
    }
    // 死亡广播入口：敌人 AI 和裁判可以登记死亡后的善后工作。
    public UnityEvent OnDeath => onDeath;

    // 出生时装满血，保证最大血量至少为 1，并按开关输出日志。
    private void Awake()
    {
        maxHealth = Mathf.Max(1, maxHealth);
        CurrentHealth = maxHealth;
        LogHealth("初始化");
    }

    // 扣血入口：死人不再受伤，非正伤害不收；致命伤将血量压到 0，避免负数。
    public void TakeDamage(int amount)
    {
        if (IsDead || IsInvulnerable || amount <= 0) return;
        int previousHealth = CurrentHealth;
        CurrentHealth = amount >= CurrentHealth ? 0 : CurrentHealth - amount;
        LogHealth($"受到伤害 -{previousHealth - CurrentHealth}");
        if (CurrentHealth > 0) return;

        // Set the guard before callbacks, including callbacks that deal damage again.
        // 先挂牌死亡，再通知别人，避免回调再次伤害而重复触发死亡。
        IsDead = true;
        // 死亡时清空所有临时无敌状态，后续不再接受新的无敌登记。
        invulnerabilitySources.Clear();
        // 预约销毁整个物体，不会立刻中断此方法；后面的关闭碰撞和广播仍会继续。
        Destroy(gameObject);
        LogHealth("死亡");
        if (disableCollidersOnDeath)
        {
            foreach (Collider targetCollider in GetComponentsInChildren<Collider>(true))
            {
                // 只关闭归这个血条管理的碰撞体，避免误关子层级中拥有独立血条的对象。
                if (targetCollider.GetComponentInParent<Health>() == this)
                {
                    targetCollider.enabled = false;
                }
            }
        }
        // 广播死亡！订阅者各自处理停战、人数统计或失败结算。
        onDeath.Invoke();
    }

    // 回血像加水：死人不能回，非正数不收，最多加到容量上限。
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

    // 可关闭的记账员：在 Console 记录血量变化，方便观察战斗。
    private void LogHealth(string action)
    {
        if (logHealthChanges)
        {
            Debug.Log($"[Health] {name}：{action}，HP {CurrentHealth}/{MaxHealth}", this);
        }
    }

    // 编辑器修改配置时兜底，防止最大血量小于 1。
    private void OnValidate()
    {
        maxHealth = Mathf.Max(1, maxHealth);
    }
}
