using UnityEngine;
using UnityEngine.AI;

[DisallowMultipleComponent]
[RequireComponent(typeof(Health), typeof(NavMeshAgent))]
public class EnemyAI : MonoBehaviour
{
    [SerializeField, Min(0.1f)] private float attackRange = 1.5f;
    [SerializeField, Min(0.01f)] private float attackInterval = 1f;
    [SerializeField, Min(1)] private int attackDamage = 10;
    private Health health;
    private Health player;
    private NavMeshAgent agent;
    private WaveManager owner;
    private float nextAttackTime;
    private bool stopped;

    private void Awake()
    {
        health = GetComponent<Health>();
        agent = GetComponent<NavMeshAgent>();
        health.OnDeath.AddListener(Die);
        agent.stoppingDistance = Mathf.Max(0f, attackRange - 0.1f);
    }

    // Only the spawning manager supplies scene references.
    public void Initialize(Health target, WaveManager manager)
    {
        player = target;
        owner = manager;
        nextAttackTime = Time.time + attackInterval;
    }

    private void Update()
    {
        if (stopped || health.IsDead) 
        {
            return;
        }
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
        Vector3 delta = player.transform.position - transform.position;
        delta.y = 0f;
        if (delta.sqrMagnitude > attackRange * attackRange)
        {
            agent.isStopped = false;
            agent.SetDestination(player.transform.position);
            return;
        }
        StopAgent();
        if (delta.sqrMagnitude > 0.001f)
        {
            transform.rotation = Quaternion.LookRotation(delta);
        }
        if (Time.time < nextAttackTime)
        {
            return;
        }
        nextAttackTime = Time.time + attackInterval;
        player.TakeDamage(attackDamage);
    }

    private void StopAgent()
    {
        if (agent.enabled && agent.isOnNavMesh)
        {
            agent.isStopped = true;
            agent.ResetPath();
            agent.velocity = Vector3.zero;
        }
    }

    public void StopCombat()
    {
        stopped = true;
        StopAgent();
    }

    private void Die()
    {
        StopCombat();
        agent.enabled = false;
        foreach (Collider hitbox in GetComponentsInChildren<Collider>(true))
            hitbox.enabled = false;
        if (owner != null) owner.NotifyEnemyDeath(this);
        enabled = false;
    }

    private void OnDestroy()
    {
        if (health != null) health.OnDeath.RemoveListener(Die);
    }
}
