using System;
using UnityEngine;

[RequireComponent(typeof(SphereCollider), typeof(Rigidbody))]
// 敌方直线投射物：固定步长飞行，扫掠检测沿途碰撞，并用统一消耗标记防止重复扣血。
public class Projectile : MonoBehaviour
{
    // 每颗弹对玩家的伤害；闪避无敌时由玩家 Health 拒绝扣血。
    [SerializeField, Min(1)] private int damage = 10;
    // 飞行速度，单位米/秒。
    [SerializeField, Min(0.1f)] private float speed = 8f;
    // 最多存活的游戏秒数，暂停期间不计时。
    [SerializeField, Min(0.01f)] private float lifetime = 4f;
    // 墙体层掩码；1 << 9 代表第 9 层 Environment，可在 Inspector 调整。
    [SerializeField] private LayerMask wallLayers = 1 << 9;
    // 发射者用于排除自身，target 是唯一能被这颗弹伤害的目标。
    private Health shooter;
    private Health target;
    // 通过管理器判断本局是否仍在战斗，结局后不再造成伤害。
    private WaveManager owner;
    private SphereCollider shape;
    private Rigidbody body;
    // 发射时确定的单位方向；age 是已经飞行的秒数。
    private Vector3 direction;
    private float age;
    // 初始化完成才允许飞行，防止子弹刚创建、尚未注入目标时误判。
    private bool launched;
    // 消耗锁：一旦命中或到期就置 true，即使同帧再次收到碰撞也不扣血。
    private bool consumed;

    // 设置 Trigger 和运动学刚体：不靠物理力推动，不阻挡玩家；移动由本脚本控制。
    private void Awake()
    {
        shape = GetComponent<SphereCollider>();
        shape.isTrigger = true;
        body = GetComponent<Rigidbody>();
        body.isKinematic = true;
        body.useGravity = false;
        // 刚体开启推测连续检测作为补充；沿途主要依靠下方球形扫掠防止穿过薄墙。
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
    }

    // 没有子弹 Prefab 时的备用创建方法；球体直径缩放为 0.24 米。
    public static Projectile Create(Vector3 position)
    {
        GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        visual.name = "EnemyProjectile";
        visual.transform.position = position;
        visual.transform.localScale = Vector3.one * 0.24f;
        // Unity 创建的基础球体已经带 SphereCollider，添加脚本会补齐所需的 Rigidbody。
        return visual.AddComponent<Projectile>();
    }

    // 发射入口：保存发射者、目标和管理器，把瞄准向量转换成固定飞行方向。
    public void Launch(Health source, Health victim, WaveManager manager, Vector3 aimDirection)
    {
        shooter = source;
        target = victim;
        owner = manager;
        direction = aimDirection.normalized;
        launched = true;
        // 忽略发射者所有子碰撞体；显式查询命中时还会再次排除发射者。
        if (source != null)
            foreach (Collider sourceCollider in source.GetComponentsInChildren<Collider>(true))
                Physics.IgnoreCollision(shape, sourceCollider);
        if (direction.sqrMagnitude < 0.001f) Consume();
    }

    // 将球碰撞体的局部半径换算成世界半径；取最大缩放轴，非等比缩放时更保守。
    private float Radius => shape.radius * Mathf.Max(Mathf.Abs(transform.lossyScale.x),
        Mathf.Abs(transform.lossyScale.y), Mathf.Abs(transform.lossyScale.z));

    // 物理固定更新：检查本步起点、检测整段飞行路径，再移动和累计寿命。
    private void FixedUpdate()
    {
        if (!launched || consumed || Time.timeScale <= 0f) return;
        if (target == null || target.IsDead || owner == null || !owner.IsPlaying)
        { Consume(); return; }
        // 同步玩家和墙体本帧的位置变化，让扫掠检测读取最新碰撞体。
        Physics.SyncTransforms();
        Vector3 centre = transform.TransformPoint(shape.center);
        // 先单独检查起点重叠，覆盖子弹出生在玩家或墙体内的情况，不只依靠前向扫掠。
        foreach (Collider overlap in Physics.OverlapSphere(centre, Radius, ~0, QueryTriggerInteraction.Collide))
            if (TryHit(overlap)) return;
        // 寿命将结束时缩短最后一步，避免飞出超过设定寿命的距离。
        float step = Mathf.Min(Time.fixedDeltaTime, lifetime - age);
        // 用有半径的球扫过本步路径；~0 表示查询所有层，TryHit 再筛掉无关对象。
        RaycastHit[] hits = Physics.SphereCastAll(centre, Radius, direction,
            speed * step, ~0, QueryTriggerInteraction.Collide);
        // 按距离从近到远处理，避免先伤到墙后的玩家，再发现前方其实有墙。
        Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        foreach (RaycastHit hit in hits)
            if (TryHit(hit.collider)) return;
        // 路径没有有效命中才移动，速度乘时间得到本步位移。
        body.MovePosition(body.position + direction * (speed * step));
        age += step;
        if (age >= lifetime) Consume();
    }

    // 返回 true 代表子弹已被消耗；扫掠、起点重叠和 Trigger 回调共用同一个判断。
    private bool TryHit(Collider other)
    {
        if (consumed || !launched || other == null || other == shape ||
            other.transform.IsChildOf(transform)) return false;
        // 子物体的 Collider 也向父对象找 Health；玩家的 CharacterController 同样能这样识别。
        Health hitHealth = other.GetComponentInParent<Health>();
        if (shooter != null && (hitHealth == shooter || other.transform.IsChildOf(shooter.transform))) return false;
        // 只认指定玩家和墙体层；其他敌人、装饰物或无关 Trigger 不消耗子弹。
        bool playerHit = target != null && hitHealth == target;
        bool wallHit = ((1 << other.gameObject.layer) & wallLayers.value) != 0;
        if (!playerHit && !wallHit) return false;
        // 必须先消耗再扣血：扣血触发的死亡回调可能立即结束本局。
        Consume();
        if (playerHit && Time.timeScale > 0f && owner != null && owner.IsPlaying && !target.IsDead)
            target.TakeDamage(damage);
        return true;
    }

    // 补充处理物理接触，特别是玩家移动到子弹上；与扫掠共用消耗锁。
    private void OnTriggerEnter(Collider other)
    {
        if (Time.timeScale > 0f && owner != null && owner.IsPlaying) TryHit(other);
    }

    // 立即关闭碰撞并锁定消耗，销毁安排在帧末，所以不能仅靠 Destroy 防重复伤害。
    private void Consume()
    {
        consumed = true;
        shape.enabled = false;
        Destroy(gameObject);
    }
}
