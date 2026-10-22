# 近战敌人和最小波次流程

## 已接入的场景

打开 `Assets/Scenes/main_scene.unity`。场景中的 WaveManager 已绑定 Player 的 Health 和 Enemy_Melee Prefab；Navigation 已绑定现有 NavMesh-Navigation 数据。main_scene 已加入 Build Settings，支持重新加载当前场景。默认在 WaveManager 周围 7 米内生成 3 个敌人，无需再手工放置敌人。TrainingDummy 是原有练习靶，不计入这 3 个敌人的波次。

## 组件挂载与字段

| 对象 | 组件与赋值 |
| --- | --- |
| Player 根对象 | CharacterController、PlayerCCMovement、PlayerInput、Health、PlayerCombat；PlayerCombat 的 AttackPoint 指向子对象 AttackPoint，Enemy Layers 选 Enemy（现有代码在空值时自动使用 Enemy 层）。 |
| Enemy_Melee Prefab 根对象 | Health（当前 60 HP）、CapsuleCollider、NavMeshAgent、EnemyAI；根对象和所有受击 Collider 所在对象设为 Enemy 层。不要添加玩家移动组件。 |
| EnemyAI | Attack Range = 1.5、Attack Interval = 1、Attack Damage = 10。玩家和管理器只由 EnemyAI.Initialize 注入，Prefab 无场景玩家引用。 |
| NavMeshAgent | 当前 Speed = 2.5、Radius = 0.5、Height = 2、Base Offset = 1；Stopping Distance 在 Awake 中设为攻击距离减 0.1。Agent Type 必须与导航烘焙设置一致。 |
| WaveManager 空对象 | WaveManager 组件；Player 拖玩家 Health；Enemy Prefab 拖 Enemy_Melee 上的 EnemyAI；Enemy Count = 3。Spawn Points 可留空，或填入场景空对象作为落点；Sample Distance = 2、Attempts Per Enemy = 20。 |
| Navigation | NavMeshSurface，包含地面与墙体的 Layer；修改关卡后重新 Bake，确保生成区域连通可行走。 |

最小 UI 使用 OnGUI，结局时显示胜利、失败或刷怪错误，以及可点击重开按钮，不需要 Canvas 或 EventSystem。如果换为正式 Canvas，可将按钮 OnClick 绑定 WaveManager.Restart，并用不受暂停影响的 UI 输入。

## 模型原点与地面高度

当前 Enemy_Melee 是高 2 米、原点位于中心的胶囊，Collider Center = (0,0,0)，因此 Base Offset = 1。NavMesh.SamplePosition 返回地面点，生成位置为落点加世界向上 Base Offset，不能直接把模型中心放到落点，否则下半身会埋地。保持敌人根对象单位缩放。

换成脚底原点模型时，Base Offset 改为 0；推荐用脚底原点的空父对象挂 Agent/Health/EnemyAI，将中心原点模型放在子对象 localPosition.y = 1，Collider Center.y = 1。不要同时抬高模型子对象和 Base Offset，否则会悬空。Spawn Point 应放在目标楼层附近；Sample Distance 不宜跨越多层楼。

## 行为与失败处理

- 只追击存活玩家，水平距离不超过 1.5 米时停止追击并攻击。首次伤害不早于生成后 1 秒，后续伤害间隔至少 1 秒；不追补卡顿期间的攻击。
- 每个敌人生成前按其 Agent Type 和 Area Mask 调用 NavMesh.SamplePosition；避开玩家 2 米内与已生成敌人的重叠位置。实例 Agent 无法绑定导航时销毁该实例并重试。
- 达到重试上限后显示刷怪错误，停止战斗并提供重开；不会将“生成了零个敌人”误当胜利。控制台记录失败的敌人序号。
- HashSet 保存成功生成的存活敌人；只有成功 Remove 才检查胜利，重复死亡通知不会重复扣数。原有 Health 的 IsDead 防止重复死亡事件。
- 敌人死亡回调立即停止路径、清零速度、禁用 Agent、攻击和所有子 Collider，再通知 WaveManager；Health 保留现有帧末销毁对象行为。
- 玩家死亡立即失败；结局停止敌人和玩家输入、移动、战斗，Time.timeScale = 0。重开先恢复为 1，再按路径重新加载当前场景。

## Play Mode 验收

1. 运行 main_scene，确认生成 3 个敌人，胶囊底部贴地并持续追击玩家。
2. 进入 1.5 米内观察 Health 日志：每次减少 10，间隔至少 1 秒；离开距离后停止受伤。
3. 用现有左键攻击击杀所有生成敌人（60 HP、每次 20 伤害，正常需 3 次命中），确认胜利；练习靶不影响结局。对同一死亡敌人重复通知不应改变计数。
4. 重开后确认 Time.timeScale = 1，玩家能移动、攻击，重新生成 3 个敌人；不抵抗时玩家死亡显示失败，随后敌人不再攻击。
5. 暂时清空 Navigation 的 NavMesh Data 或把刷怪点移出导航区域，确认显示刷怪错误而非胜利，重开按钮仍可点击。恢复导航数据后再次运行。

已用 Unity 6000.6.2f1 本地程序集进行 C# 编译检查；导航运行、场景视觉和按钮交互仍需在 Unity Play Mode 完成以上验收。编辑器若已有未保存的 main_scene，请从磁盘重新载入场景，以免旧内存状态覆盖本次接线。
