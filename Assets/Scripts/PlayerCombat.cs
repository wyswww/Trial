using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
[RequireComponent(typeof(Health), typeof(PlayerInput), typeof(PlayerCCMovement))]
public class PlayerCombat : MonoBehaviour
{
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
    private float _nextAttackTime;

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

    private void OnEnable()
    {
        _health.OnDeath.AddListener(StopPlayer);
        if (_health.IsDead)
        {
            StopPlayer();
        }
    }

    private void OnDisable()
    {
        _health.OnDeath.RemoveListener(StopPlayer);
    }

    // PlayerInput: Send Messages, player/Attack, <Mouse>/leftButton.
    public void OnAttack(InputValue value)
    {
        if (!isActiveAndEnabled || !value.isPressed)
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

        _nextAttackTime = Time.time + attackCooldown;
        // Apply a direction received this frame before querying the attack volume.
        _movement.FaceMoveDirection();
        Physics.SyncTransforms();
        Collider[] hits = Physics.OverlapSphere(attackPoint.position, attackRadius,
            enemyLayers, QueryTriggerInteraction.Collide);
        var damaged = new HashSet<Health>();
        foreach (Collider hit in hits)
        {
            Health target = hit.GetComponentInParent<Health>();
            if (target != null && target != _health && !target.IsDead && damaged.Add(target))
            {
                target.TakeDamage(attackDamage);
            }
        }
        LogAttack($"攻击触发：检测到 {hits.Length} 个 Collider，命中 {damaged.Count} 个 Health 目标" +
            (hits.Length == 0 ? "；请检查距离、朝向及 Enemy 层" : ""));
    }

    private void LogAttack(string message)
    {
        if (logAttacks)
        {
            Debug.Log($"[Combat] {name}：{message}", this);
        }
    }

    private void StopPlayer()
    {
        _input.DeactivateInput();
        _movement.enabled = false;
    }

    private void OnDrawGizmosSelected()
    {
        Transform point = attackPoint != null ? attackPoint : transform.Find("AttackPoint");
        if (point == null) return;
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(point.position, attackRadius);
    }
}
