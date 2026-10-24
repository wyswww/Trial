using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
[RequireComponent(typeof(CharacterController), typeof(Health))]
// 玩家驾驶系统：世界 XZ 平面移动，碰撞交给 CharacterController，重力手动计算。
public class PlayerCCMovement : MonoBehaviour
{
    // 速度按秒计算，平滑时间控制输入追赶速度。
    public float moveSpeed = 5f;
    public float smoothTime = 0.15f;
    public float gravity = -9.81f;

    // 闪避持续时间（秒）；期间使用闪避速度而不是普通移动速度。
    [SerializeField, Min(0.01f)] private float dodgeDuration = 0.2f;
    // 从按下闪避开始计算冷却，记录的是下次允许闪避的时刻。
    [SerializeField, Min(0f)] private float dodgeCooldown = 1f;
    // 闪避速度（米/秒）；默认 15 × 0.2，空旷处一次约移动 3 米。
    [SerializeField, Min(0f)] private float dodgeSpeed = 15f;
    private Health health;
    // 本次闪避还剩多少秒；小于等于 0 代表未闪避。
    private float dodgeRemaining;
    private float nextDodgeTime;
    // 按下闪避时锁定的方向，途中改变移动输入不会改变本次闪避方向。
    private Vector3 dodgeDirection;
    public bool IsDodging => dodgeRemaining > 0f;
    private CharacterController _cc;
    // 原始输入像方向盘，平滑输入像车身，速度向量用于竖直下落。
    private Vector2 _rawInput;
    private Vector2 _smoothInput;
    private Vector3 _velocity;

    // 认领身上的碰撞移动组件。
    void Awake()
    {
        _cc = GetComponent<CharacterController>();
        health = GetComponent<Health>();
    }

    // Send Messages 模式下，PlayerInput 会按 "On" + Action名 反射调用此方法
    // 参数必须是 InputValue，且方法为 public
    // 限制输入长度，防止斜走更快，并立即转向。
    public void OnMove(InputValue value)
    {
        if (!isActiveAndEnabled || Time.timeScale <= 0f || (health != null && health.IsDead)) return;
        _rawInput = Vector2.ClampMagnitude(value.Get<Vector2>(), 1f);
        FaceMoveDirection();
    }

    // 组件启用时监听死亡，让死亡也能及时取消闪避。
    private void OnEnable()
    {
        if (health != null) health.OnDeath.AddListener(CancelDodge);
    }

    // 接收 Input Actions 的 Dodge 按下消息；松开、死亡、暂停或冷却中都不执行。
    public void OnDodge(InputValue value)
    {
        if (!value.isPressed || !isActiveAndEnabled || Time.timeScale <= 0f ||
            health == null || health.IsDead || IsDodging || Time.time < nextDodgeTime) return;
        // 优先使用当前移动输入；静止时改用角色朝向，最后归一化以保证斜向不加速。
        dodgeDirection = new Vector3(_rawInput.x, 0f, _rawInput.y);
        if (dodgeDirection.sqrMagnitude < 0.001f) dodgeDirection = transform.forward;
        dodgeDirection.y = 0f;
        dodgeDirection.Normalize();
        transform.rotation = Quaternion.LookRotation(dodgeDirection);
        dodgeRemaining = dodgeDuration;
        nextDodgeTime = Time.time + dodgeCooldown;
        // 只登记本移动组件提供的无敌，不直接改碰撞体，因此仍会被墙挡住。
        health.SetInvulnerable(this, true);
        GetComponent<KayKitCharacterAnimation>()?.PlayDodge(dodgeDuration);
        CombatRangeEffect.Show(transform.position, 0.65f, Color.cyan, dodgeDuration);
    }

    // 闪避结束、死亡和组件禁用共用此清理入口，移除本次闪避的无敌。
    private void CancelDodge()
    {
        dodgeRemaining = 0f;
        if (health != null) health.SetInvulnerable(this, false);
    }

    // 每帧先处理闪避或普通水平移动，再独立计算重力；暂停时不推进。
    void Update()
    {
        if (health != null && health.IsDead) { CancelDodge(); return; }
        if (Time.timeScale <= 0f) return;
        if (IsDodging)
        {
            // 最后一帧只使用剩余时间，避免低帧率使闪避距离超出设定。
            float stepTime = Mathf.Min(Time.deltaTime, dodgeRemaining);
            // 交给 CharacterController 扫掠并处理墙体碰撞，不直接瞬移 transform。
            _cc.Move(dodgeDirection * (dodgeSpeed * stepTime));
            dodgeRemaining -= stepTime;
            if (!IsDodging) CancelDodge();
        }
        else
        {
        // 输入平滑
        // 平滑输入追上目标，产生起步和松键缓冲。
        _smoothInput = Vector2.Lerp(_smoothInput, _rawInput,
            smoothTime <= 0f ? 1f : Time.deltaTime / smoothTime);

        // 水平移动
        // 输入映射到世界 XZ，乘帧时间得到本帧位移。
        Vector3 moveDir = new Vector3(_smoothInput.x, 0f, _smoothInput.y);
        _cc.Move(moveDir * moveSpeed * Time.deltaTime);

        }
        // 重力 / 贴地
        // 落地保留小向下速度帮助贴地，再累计重力。
        if (_cc.isGrounded && _velocity.y < 0)
        {
            _velocity.y = -0.2f;
        }
        _velocity.y += gravity * Time.deltaTime;
        _cc.Move(_velocity * Time.deltaTime);
    }

    // 朝原始输入转身，无输入则保留朝向。
    public void FaceMoveDirection()
    {
        if (IsDodging || Time.timeScale <= 0f) return;
        Vector3 direction = new Vector3(_rawInput.x, 0f, _rawInput.y);
        if (direction.sqrMagnitude > 0.0001f)
            transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
    }

    // 停用清空输入与速度，避免重新启动时残留。
    private void OnDisable()
    {
        CancelDodge();
        if (health != null) health.OnDeath.RemoveListener(CancelDodge);
        _rawInput = Vector2.zero;
        _smoothInput = Vector2.zero;
        _velocity = Vector3.zero;
    }
}
