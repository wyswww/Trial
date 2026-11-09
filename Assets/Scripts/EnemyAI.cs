using UnityEngine;
using UnityEngine.AI;

[DisallowMultipleComponent]
[RequireComponent(typeof(Health), typeof(NavMeshAgent))]
// 每只敌人的行动手册：裁判指定目标，导航司机负责追击，近身后定时攻击。
public class EnemyAI : MonoBehaviour
{
    // 战斗三件套：攻击距离、攻击间隔、攻击伤害；实际配置以 Inspector 保存的值为准。
    [SerializeField, Min(0.1f)] private float attackRange = 1.5f;
    [SerializeField, Min(0.01f)] private float attackInterval ;
    [SerializeField] private Animator enemyAnimator;
    [SerializeField] private AnimationClip attackClip;
   // 攻击开始后，等待多久才造成伤害
[SerializeField, Min(0f)] private float attackDelay = 0.3f;
[SerializeField] private GameObject attackWarning;

// 本次攻击预计命中的时间
private float attackHitTime;

// 是否正在等待攻击命中
private bool attacking;
     [SerializeField, Min(1)] private int attackDamage = 10;
    // 记住自己的血条、玩家血条、导航司机和负责统计生死的裁判。
    // protected 允许远程子类复用血条和目标；private 字段只由当前基类使用。
   
   private enum EnemyState
{
    Chase,     // 追击
    Windup,    // 攻击前摇
    Attack,    // 攻击
    Recovery,  // 攻击后恢复
    Dead       // 死亡
}
private EnemyState currentState;

    protected Health health;
    
    protected Health player;
    private NavMeshAgent agent;
    protected WaveManager owner;
    // 攻击闹钟：保存下一次允许攻击的时间点，不是剩余秒数。
    private float nextAttackTime;
    // 永久停战开关：打开后不会自行恢复战斗。
    private bool stopped;

    // 出生先找齐组件并订阅死亡通知；导航停车距离略小于攻击距离，确保走进攻击范围。
    protected virtual void Awake()

    {
        if (attackClip != null)
        {
            attackInterval=attackClip.length;
        }
        currentState=EnemyState.Chase;
        health = GetComponent<Health>();
        agent = GetComponent<NavMeshAgent>();
        health.OnDeath.AddListener(Die);
        agent.stoppingDistance = Mathf.Max(0f, attackRange - 0.1f);
        if (attackWarning != null)
{
    attackWarning.SetActive(false);
}
    }

    // Only the spawning manager supplies scene references.
    // 裁判发任务卡：记住攻击谁、归谁管。保存的是引用，不会复制玩家；此方法需要主动调用。
    public void Initialize(Health target, WaveManager manager)
    {
        player = target;
        owner = manager;
        // 例如现在 10 秒、间隔 1 秒，首次攻击最早在 11 秒；这里只记时间，不会阻塞等待。
        nextAttackTime = Time.time + attackInterval;
    }

    // 每帧依次过门禁：能行动吗？目标在吗？导航可用吗？然后决定追还是打。
    private void Update()
    {
        if (Time.timeScale <= 0f || stopped || health.IsDead)
        {
            return;
        }
        // 比赛结束或目标消失，就让导航司机刹车。
        if (player == null || player.IsDead || owner == null || !owner.IsPlaying)
        {
            StopAgent();
            return;
        }
        if (!agent.enabled || !agent.isOnNavMesh)
        {
            return;
        }
        // Character origins can be at different heights. Compare feet on the ground plane.
        // 像俯视地图一样只比较水平距离，忽略模型中心的高度差。
        Vector3 delta = player.transform.position - transform.position;
        delta.y = 0f;
        if (attacking)
{
    // 前摇期间站在原地
    StopAgent();

    if (Time.time >= attackHitTime)
    {
        attacking = false;
                if (attackWarning != null)
                {
                    attackWarning.SetActive(false);
                }

        // 命中时再检查玩家是否还在攻击范围内
        if (delta.sqrMagnitude <= attackRange * attackRange)
        {
            PerformAttack();
        }
    }

    // 正在攻击时，先不执行后面的追击和开始攻击逻辑
    return;
}
        // 用距离平方比较省去开平方；超出范围就更新目的地，让导航系统负责寻路。
        if (delta.sqrMagnitude > attackRange * attackRange)
        {
            agent.isStopped = false;
            currentState=EnemyState.Chase;

            agent.SetDestination(player.transform.position);
            return;
        }
        // 近身后先刹车，再面对玩家；这是暂时停车，玩家跑远后还能继续追。
        StopAgent();
        
        
        if (delta.sqrMagnitude > 0.001f)
        {
            transform.rotation = Quaternion.LookRotation(delta);
        }
        // 看表：冷却没到就等下一帧；时间到了也只攻击一次，再预约下一次出手。
        if (Time.time < nextAttackTime)
        {
            return;
        }
        nextAttackTime = Time.time + attackInterval;
        currentState=EnemyState.Windup;
        if (enemyAnimator != null)
{
    enemyAnimator.SetTrigger("Attack");
}
        if (enemyAnimator != null)
{
    enemyAnimator.SetTrigger("Attack");
}
        if (attackWarning != null)
{
    attackWarning.SetActive(true);
}
        // 零延迟立即攻击，否则等待命中时间。
        attackHitTime = Time.time + Mathf.Max(0f, attackDelay);
        if (attackDelay == 0f)
        {
            if (attackWarning != null)
{
    attackWarning.SetActive(false);
}
            PerformAttack();

            currentState=EnemyState.Attack;
            currentState=EnemyState.Recovery;
        }
        
        else attacking = true;
    }

    // 可被子类替换的攻击入口：近战默认直接扣血，远程子类改为生成投射物。
    protected virtual void PerformAttack() => player.TakeDamage(attackDamage);

    // 临时刹车：停止导航、清空路线和速度，但没有永久禁止战斗。
    private void StopAgent()
    {
        if (agent.enabled && agent.isOnNavMesh)
        {
            agent.isStopped = true;
            agent.ResetPath();
            agent.velocity = Vector3.zero;
        }
    }

    // 永久收工：设置停战标记，阻止后续帧继续追击或攻击。
    public void StopCombat()
    {
        stopped = true;
        attacking = false;
        StopAgent();
        if (attackWarning != null)
        {
            attackWarning.SetActive(false);
        }
    }

    // 死亡善后：停战、关导航和碰撞、向裁判报备；整个物体由 Health 负责销毁。
    private void Die()
    {
        currentState=EnemyState.Dead;
        StopCombat();
        agent.enabled = false;
        foreach (Collider hitbox in GetComponentsInChildren<Collider>(true))
            hitbox.enabled = false;
        if (owner != null) owner.NotifyEnemyDeath(this);
        enabled = false;
    }

    // 离场时取消死亡通知订阅，交还通知登记卡。
    private void OnDestroy()
    {
        if (health != null) health.OnDeath.RemoveListener(Die);
    }
}
