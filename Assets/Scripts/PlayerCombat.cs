using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
[RequireComponent(typeof(Health), typeof(PlayerInput), typeof(PlayerCCMovement))]
// 近战裁判：收到输入后用球形范围筛选目标并扣血。
public class PlayerCombat : MonoBehaviour
{
    // 攻击点是球心，伤害、半径、冷却和敌人层决定效果。
    [SerializeField] private Transform attackPoint;
    [SerializeField, Min(1)] private int attackDamage = 20;
    [SerializeField, Min(0.01f)] private float attackRadius = 0.8f;
    [SerializeField, Min(0f)] private float attackCooldown = 0.6f;
    [SerializeField] private LayerMask enemyLayers;
    [SerializeField, Tooltip("临时记录攻击触发与命中数量，便于排查输入和攻击范围。")]
    private bool logAttacks = true;

    private Health _health;
    private PlayerInput _input;
    private PlayerCCMovement _movement;
    // 普通攻击下次可用的游戏时间；Time.time 在暂停时不增长。
    private float _nextAttackTime;
    // Q 技能参数：以玩家位置为中心的球形半径、单个敌人的伤害和冷却秒数。
    [SerializeField, Min(0.1f)] private float skillRadius = 3f;
    [SerializeField, Min(1)] private int skillDamage = 30;
    [SerializeField, Min(0f)] private float skillCooldown = 5f;
    // Q 技能使用独立冷却，不会占用普通攻击冷却。
    private float nextSkillTime;

    // 接收 Skill（Q）消息；只有按下且存活、未暂停、冷却结束时才释放。
    public void OnSkill(InputValue value)
    {
        if (!isActiveAndEnabled || !value.isPressed || Time.timeScale <= 0f ||
            _health.IsDead || Time.time < nextSkillTime) return;
        nextSkillTime = Time.time + skillCooldown;
        GetComponent<KayKitCharacterAnimation>()?.PlaySkill();
        // 把本帧 Transform 变化同步到物理系统，让范围检测使用最新位置。
        Physics.SyncTransforms();
        // 本次释放的受伤名单：同一 Health 下多个碰撞体只结算一次。
        var damaged = new HashSet<Health>();
        // 包含敌人 Trigger；除碰撞体层外还检查血条根对象层，排除自己和非敌人。
        foreach (Collider hit in Physics.OverlapSphere(transform.position, skillRadius,
            enemyLayers, QueryTriggerInteraction.Collide))
        {
            Health target = hit.GetComponentInParent<Health>();
            if (target != null && target != _health && !target.IsDead &&
                ((1 << target.gameObject.layer) & enemyLayers.value) != 0 && damaged.Add(target))
                target.TakeDamage(skillDamage);
        }
        // 只画范围提示，不依赖圆环自身碰撞来造成伤害。
        CombatRangeEffect.Show(transform.position, skillRadius, Color.yellow, 0.3f);
    }

    // 找齐组件，缺配置时按名称寻找。
    private void Awake()
    {
        _health = GetComponent<Health>();
        _input = GetComponent<PlayerInput>();
        _movement = GetComponent<PlayerCCMovement>();
        if (attackPoint == null)
        {
            attackPoint = transform.Find("AttackPoint");
        }
        if (enemyLayers.value == 0)
        {
            enemyLayers = LayerMask.GetMask("Enemy");
        }
    }

    // 启用登记死亡通知，已死则立即停止。
    private void OnEnable()
    {
        _health.OnDeath.AddListener(StopPlayer);
        if (_health.IsDead)
        {
            StopPlayer();
        }
    }

    // 停用取消登记，防止重复订阅。
    private void OnDisable()
    {
        _health.OnDeath.RemoveListener(StopPlayer);
    }

    // PlayerInput: Send Messages, player/Attack, <Mouse>/leftButton.
    // 输入只响应按下，再检查死亡、攻击点和冷却。
    public void OnAttack(InputValue value)
    {
        if (!isActiveAndEnabled || !value.isPressed || Time.timeScale <= 0f)
        {
            return;
        }
        if (_health.IsDead)
        {
            LogAttack("忽略攻击：玩家已死亡");
            return;
        }
        if (attackPoint == null)
        {
            LogAttack("忽略攻击：未配置 AttackPoint");
            return;
        }
        if (Time.time < _nextAttackTime)
        {
            LogAttack("忽略攻击：冷却中");
            return;
        }

        // 先预约下次出手，挥空也消耗冷却。
        _nextAttackTime = Time.time + attackCooldown;
        GetComponent<KayKitCharacterAnimation>()?.PlayAttack();
        // Apply a direction received this frame before querying the attack volume.
        // 先朝最新输入方向转身，再同步物理位置。
        _movement.FaceMoveDirection();
        Physics.SyncTransforms();
        // 球形撒网，包含指定层的 Trigger，不检测挥刀轨迹。
        Collider[] hits = Physics.OverlapSphere(attackPoint.position, attackRadius,
            enemyLayers, QueryTriggerInteraction.Collide);
        // 去重名单保证一只敌人多个碰撞体也只受伤一次。
        var damaged = new HashSet<Health>();
        foreach (Collider hit in hits)
        {
            // 从手脚等子物体向父层找血条，排除自己和尸体。
            Health target = hit.GetComponentInParent<Health>();
            if (target != null && target != _health && !target.IsDead && damaged.Add(target))
            {
                target.TakeDamage(attackDamage);
            }
        }
        LogAttack($"攻击触发：检测到 {hits.Length} 个 Collider，命中 {damaged.Count} 个 Health 目标" +
            (hits.Length == 0 ? "；请检查距离、朝向及 Enemy 层" : ""));
    }

    // 记录碰撞体数量和实际受伤人数，帮助排查挥空。
    private void LogAttack(string message)
    {
        if (logAttacks)
        {
            Debug.Log($"[Combat] {name}：{message}", this);
        }
    }

    // 死亡关闭输入与移动。
    private void StopPlayer()
    {
        _input.DeactivateInput();
        _movement.enabled = false;
    }

    // 红色辅助球只显示范围，本身不会伤害目标。
    private void OnDrawGizmosSelected()
    {
        Transform point = attackPoint != null ? attackPoint : transform.Find("AttackPoint");
        if (point == null) return;
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(point.position, attackRadius);
    }
}
