using UnityEngine;

// 远程敌人继承近战基类：共用追击、目标注入、暂停、死亡和波次计数，只改攻击方式。
public class RangedEnemyAI : EnemyAI
{
    // 可选子弹模板；留空时创建一个带碰撞检测的小球。伤害、速度、寿命由 Projectile 配置。
    [SerializeField] private Projectile projectilePrefab;
    // 可选枪口子物体；留空时从敌人根对象位置发射，注意模型原点高度。
    [SerializeField] private Transform muzzle;

    // 基类判断距离和冷却后调用这里；只替换出手方式，保留导航、暂停和死亡逻辑。
    protected override void PerformAttack()
    {
        Vector3 origin = muzzle != null ? muzzle.position : transform.position;
        // 优先瞄准玩家胶囊的身体中心，避免玩家原点在脚底时子弹总是打地面。
        CharacterController controller = player.GetComponent<CharacterController>();
        Vector3 aim = controller != null ? controller.bounds.center : player.transform.position;
        // 有模板就克隆模板，没有模板就生成默认球体；两种方式都由 Launch 注入场景引用。
        Projectile shot = projectilePrefab != null
            ? Instantiate(projectilePrefab, origin, Quaternion.identity)
            : Projectile.Create(origin);
        // 只计算发射瞬间的方向，投射物后续直飞，不继续追踪移动中的玩家。
        shot.Launch(health, player, owner, aim - origin);
    }
}
