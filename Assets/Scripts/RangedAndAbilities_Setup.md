# 远程敌人、Space 闪避、Q 范围技能

## 已接入

现有玩家继续只挂一个 PlayerCCMovement 和一个 PlayerCombat。Space → PlayerCCMovement.OnDodge，Q → PlayerCombat.OnSkill，左键仍调用 OnAttack，统一使用现有 PlayerInput 的 Send Messages，不轮询键盘、不新增第二套玩家控制器。

场景实际使用 `Assets/Input_editor/PlayerIA.inputactions` 的 `player` Action Map，已添加 Dodge / Skill 两个 Button。备用 `InputSystem_Actions.inputactions` 也添加对应动作。Space 的旧跳跃绑定已移除以避免同时触发。

main_scene 的 WaveManager 已增加 Ranged Enemy Prefab 引用；保留你当前 Inspector 中的 Enemy Count = 8、两个刷怪点与 Spawn Radius = 2，最后一只使用新 Enemy_Ranged，其余沿用 Enemy_Melee。可在 Inspector 改回 3，即两近战、一远程。胜利统计统一使用原有 HashSet。

## Inspector 配置

| 对象 / 组件 | 字段 |
| --- | --- |
| Player / PlayerInput | Actions = PlayerIA；Default Map = player；Behavior = Send Messages。Dodge = Space，Skill = Q。 |
| Player / PlayerCCMovement | Dodge Duration = 0.2 秒；Dodge Cooldown = 1 秒（从开始计算）；Dodge Speed = 15 米/秒，完整闪避约 3 米。 |
| Player / PlayerCombat | Skill Radius = 3；Skill Damage = 30；Skill Cooldown = 5；Enemy Layers = Enemy。 |
| Player / Health | 无需手动配置无敌；闪避调用按来源登记的无敌接口，所有 TakeDamage 均检查。 |
| Enemy_Ranged 根对象 | Health、CapsuleCollider、NavMeshAgent、RangedEnemyAI，不能再叠加一个 EnemyAI。根及受击 Collider 设 Enemy 层。 |
| RangedEnemyAI | Attack Range = 6；Attack Interval = 1.5；Projectile Prefab 可空，默认创建直径 0.24 米的小球；Muzzle 可空，默认从模型中心发射。可创建子空对象指定枪口，放在胸口附近。 |
| Enemy_Ranged / NavMeshAgent | 沿用现有胶囊的 Height = 2、Base Offset = 1；根单位缩放。停止距离由基类配置为 5.9。 |
| WaveManager | Ranged Enemy Prefab = 新 Enemy_Ranged 上的 RangedEnemyAI；Enemy Prefab 保留 Enemy_Melee。最后一个生成名额使用远程；清空远程字段则恢复全近战。 |

Projectile 的默认伤害是 10、速度 8 米/秒、寿命 4 秒。使用自定义投射物 Prefab 时，根对象挂 Projectile、SphereCollider（球心为 0）和 Rigidbody，并提供可见 MeshRenderer；在 Projectile 上配置这三个字段和 Wall Layers。SphereCollider 和 Rigidbody 在 Awake 中统一设置为 Trigger、Kinematic、无重力与 Continuous Speculative。不要额外挂第二个移动脚本或非 Trigger Collider。

当前墙和地面均使用 Environment（第 9 层），Projectile.Wall Layers 默认包含该层。墙体必须有 Collider；投射物使用显式 QueryTriggerInteraction.Collide，因此也能识别 Environment 层的 Trigger 墙。不要将普通技能检测区设为 Environment。玩家保留 CharacterController，不需要为了投射物另加 Rigidbody。球形扫掠明确检测玩家 Health 所属的 CharacterController/子 Collider，不依赖单一碰撞回调。

项目第 10 层现有名称为 `Projectile `（尾部有空格）；实现没有按这个名字查层，也无需改名。默认生成的投射物在 Default 层，Trigger 不阻挡玩家；如自定义层，请保证 Physics 碰撞矩阵允许与玩家、墙交互。显式扫掠是额外防穿透保障。

## 行为

- 远程沿用近战基类的目标注入、导航、死亡与暂停门禁，约 6 米停下，每 1.5 秒发射一次。发射瞬间锁定玩家当前身体中心，后续保持直线，不跟踪。敌人死亡后已发射的弹仍可继续飞行；玩家死亡或游戏结束后不会继续伤害。
- 投射物忽略发射者所有 Collider。每个 FixedUpdate 检查起点重叠，再按飞行距离 SphereCastAll 并按距离排序，先处理最近墙或玩家；Trigger 回调补充移动中接触。命中先设置 consumed、关 Collider，再伤害/销毁，因此同一弹只结算一次。闪避无敌挡住伤害时仍消耗弹。
- 闪避锁定按下瞬间的原始移动方向；无输入则沿角色朝向。只通过 CharacterController.Move 位移，保留重力与墙体碰撞；结束、禁用组件或死亡都会移除本次闪避的无敌来源，不会覆盖其他来源的无敌。
- Q 以玩家世界位置为球心球形检测，包含 Trigger，按 Health 去重，要求受击 Collider 与 Health 根对象均属于 Enemy Layers，避免误伤玩家或中立对象。半径内多个 Collider 的同一敌人只扣 30。
- 所有技能、普通攻击、敌人行为显式拒绝暂停输入；时间、寿命、冷却采用游戏时间。暂停中冻结并保留剩余闪避时间，恢复后继续；结局会禁用玩家脚本并清理闪避状态。
- 闪避显示青色小圆环；Q 显示黄色半径 3 的圆环，效果没有 Collider、不参与命中。

## 验收步骤

1. 从磁盘重新载入 main_scene，确认现有玩家动作和普通攻击正常，最后一个生成敌人在约 6 米停下并每 1.5 秒射击。
2. 观察投射物朝发射时的位置直飞；横移应能躲开。无碰撞时约 4 秒销毁；接近枪口不会伤到发射者。
3. 在墙后、薄墙前和静止玩家前测试：墙挡弹，玩家一次仅掉 10；为玩家增加受同一 Health 管理的子 Trigger，仍应只扣一次。
4. 按住方向再按 Space，确认约 0.2 秒闪避；静止按 Space 沿朝向；贴墙不能穿过。无敌期间接触弹/近战不掉血，结束后恢复受伤。连续按 Space 受 1 秒冷却限制。
5. Q 范围内敌人掉 30，多个 Collider 不叠加；半径外敌人和中立对象不扣血。5 秒内再次按 Q 无效果。
6. 将 Time.timeScale 临时设为 0 后按 Space/Q/左键，确认不触发；恢复为 1 后正常。游戏胜负后按键无效，重开无残留无敌和投射物。

验证记录：用 Unity 6000.6.2f1 本地程序集编译全部 Assets/Scripts；另外检查两个 Input Actions 的动作 ID 唯一性、绑定、场景 Prefab 引用。尚未在 Unity Play Mode 实测物理碰撞和交互。
