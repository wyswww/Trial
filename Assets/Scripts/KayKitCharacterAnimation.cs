using UnityEngine;
using UnityEngine.AI;

// 仅驱动外观；伤害、位移、冷却仍由原战斗脚本负责。
[DisallowMultipleComponent]
public sealed class KayKitCharacterAnimation : MonoBehaviour
{
    [SerializeField] private Animator animator;
    [SerializeField] private bool ranged;
    private NavMeshAgent agent;
    private Health health;
    private Vector3 previousPosition;
    private float actionUntil;
    private string currentState;

    public void Configure(Animator target, bool isRanged)
    {
        animator = target;
        ranged = isRanged;
    }

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        health = GetComponent<Health>();
    }

    private void OnEnable() => previousPosition = transform.position;

    private void LateUpdate()
    {
        Vector3 delta = transform.position - previousPosition;
        previousPosition = transform.position;
        if (animator == null || Time.timeScale <= 0f ||
            (health != null && health.IsDead) || Time.time < actionUntil) return;
        Vector3 velocity = agent != null && agent.enabled && agent.isOnNavMesh
            ? agent.velocity
            : delta / Mathf.Max(Time.deltaTime, 0.0001f);
        velocity.y = 0f;
        ChangeState(velocity.sqrMagnitude > 0.01f ? "Move" : "Idle");
    }

    public void PlayAttack() => PlayAction("Attack", ranged ? 0.5f : 0.45f);
    public void PlaySkill() => PlayAction("Skill", 0.5f);
    public void PlayDodge(float duration) => PlayAction("Dodge", duration);

    private void PlayAction(string state, float duration)
    {
        if (animator == null || !animator.isActiveAndEnabled || Time.timeScale <= 0f) return;
        if (!animator.HasState(0, Animator.StringToHash("Base Layer." + state))) return;
        actionUntil = Time.time + duration;
        currentState = state;
        // 固定时间过渡允许快速连续动作重新从片段开头播放。
        animator.CrossFadeInFixedTime(state, 0.05f, 0, 0f);
    }

    private void ChangeState(string state)
    {
        if (currentState == state) return;
        currentState = state;
        animator.CrossFadeInFixedTime(state, 0.1f, 0, 0f);
    }
}
