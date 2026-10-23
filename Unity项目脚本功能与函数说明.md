# Unity 项目脚本功能与函数说明

项目：`E:\Unity project\Trial`  
整理日期：2026-10-23  
范围：`Assets` 下全部 11 个 C# 脚本，包括 9 个游戏脚本和 2 个教程说明脚本；不包括 Unity 包和 Library 中的第三方源码。

## 1. 调用方式速查

| 类型 | 含义 |
|---|---|
| Unity 生命周期函数 | `Awake`、`Start`、`Update` 等，由 Unity 在对应时机自动调用 |
| 输入消息函数 | `OnMove`、`OnAttack` 等，由 PlayerInput 的 Send Messages 模式调用，函数名必须匹配 Action 名称 |
| 自定义函数 | 需要代码主动调用，例如 `TakeDamage()` |
| 事件监听函数 | 订阅事件后，在事件发生时调用，例如死亡触发 `Die()` |
| 属性 | 用于读取或设置状态，例如 `IsDead`，不是普通函数 |

## 2. 脚本总览

| 脚本 | 具体职责 | 主要依赖 |
|---|---|---|
| Health.cs | 血量、受伤、回血、无敌、死亡通知和角色销毁 | 所有拥有生命值的对象 |
| PlayerCCMovement.cs | 玩家移动、转向、重力、闪避及闪避无敌 | CharacterController、Health |
| PlayerCombat.cs | 普通攻击、Q 范围技能、独立冷却、死亡后停止玩家控制 | Health、PlayerInput、PlayerCCMovement |
| EnemyAI.cs | 导航追击、射程判断、攻击冷却、近战攻击和死亡处理 | NavMeshAgent、Health |
| RangedEnemyAI.cs | 继承通用敌人行为，把攻击替换为发射子弹 | EnemyAI、Projectile |
| Projectile.cs | 子弹生成、直线飞行、路径碰撞检测、排除发射者、命中和销毁 | SphereCollider、Rigidbody |
| WaveManager.cs | 开局刷怪、存活计数、胜负判定、停战和重开 | 玩家、敌人预制体、NavMesh |
| CombatRangeEffect.cs | 临时显示闪避或技能范围圆环，到期清理 | LineRenderer |
| Death_disappear.cs | 检查所属角色死亡，销毁挂载该脚本的物体 | 自身或父对象上的 Health |
| Readme.cs | 保存项目教程说明页的数据 | ScriptableObject |
| ReadmeEditor.cs | 编辑器说明页显示、布局加载和教程删除按钮 | UnityEditor、UI Toolkit |

## 3. Health.cs：生命系统

位置：`Assets/Scripts/Health.cs`

| 函数或属性 | 调用时机／调用者 | 具体功能 |
|---|---|---|
| Awake() | Unity 初始化组件时 | 最大血量至少为 1；当前血量设为最大值；输出初始化日志 |
| TakeDamage(int amount) | 攻击、技能、子弹等调用 | 已死、无敌或伤害不为正数时忽略；否则扣血，最低为 0；致命伤执行死亡处理 |
| SetInvulnerable(object source, bool active) | 闪避等效果调用 | active 为 true 时登记无敌来源，角色已死则不登记；为 false 时移除该来源 |
| Heal(int amount) | 外部主动调用 | 活着且回血量为正时回血，最多恢复到最大血量 |
| LogHealth(string action) | 初始化、受伤、回血和死亡时 | 根据开关输出行为和血量日志 |
| OnValidate() | Inspector 修改配置时 | 保证最大血量至少为 1 |
| MaxHealth | 外部读取 | 返回最大血量 |
| CurrentHealth | 外部读取 | 返回当前血量，只允许本类修改 |
| IsDead | 移动、战斗、AI 等读取 | 表示是否已死亡，只允许本类修改 |
| IsInvulnerable | 受伤时读取 | 无敌来源集合不为空时返回 true |
| OnDeath | 其他组件订阅时 | 暴露死亡事件，供移动、AI、管理器处理死亡后的工作 |

### TakeDamage 中的死亡顺序

1. 将 `IsDead` 设为 true，防止回调重入后重复死亡。
2. 清空所有无敌来源。
3. 调用 `Destroy(gameObject)`，安排帧末销毁整个角色。
4. 根据配置关闭属于本 Health 的子碰撞体。
5. 调用 `onDeath.Invoke()` 广播死亡。

关键字段：`maxHealth` 是最大血量；`disableCollidersOnDeath` 控制死亡时是否关闭碰撞体；`invulnerabilitySources` 用 HashSet 保存不同无敌来源。

## 4. PlayerCCMovement.cs：玩家移动和闪避

位置：`Assets/Scripts/PlayerCCMovement.cs`

| 函数或属性 | 调用时机／调用者 | 具体功能 |
|---|---|---|
| Awake() | 初始化时 | 缓存 CharacterController 和 Health |
| OnEnable() | 组件启用时 | 监听死亡事件，死亡时调用 CancelDodge |
| OnMove(InputValue value) | Move 输入变化时 | 检查启用、暂停和死亡状态；读取二维输入；限制输入长度为 1；更新朝向 |
| OnDodge(InputValue value) | Dodge 输入消息到达时 | 只响应按下；检查死亡、暂停、闪避状态和冷却；锁定方向；启动闪避、登记无敌并显示青色圆环 |
| CancelDodge() | 闪避结束、死亡或禁用时 | 清空闪避剩余时间，移除本组件登记的无敌 |
| Update() | 每帧 | 死亡时取消闪避；暂停时不处理；执行闪避或普通水平移动，再计算重力 |
| FaceMoveDirection() | 移动输入或普通攻击调用 | 根据原始移动输入转向；闪避或暂停时不转向；没有输入则保留朝向 |
| OnDisable() | 禁用时 | 取消闪避、取消死亡监听，清空输入和平滑输入及速度 |
| IsDodging | 逻辑判断时读取 | 闪避剩余时间大于 0 时返回 true |

### Update 的内部步骤

| 步骤 | 实现与目的 |
|---|---|
| 闪避移动 | 本帧闪避时间取 deltaTime 和剩余闪避时间的较小值，避免最后一帧多移动 |
| 普通移动 | Vector2.Lerp 让平滑输入追赶原始输入，再映射到世界 XZ 平面 |
| 水平位移 | CharacterController.Move(方向 × 速度 × 时间)，由控制器处理墙体碰撞 |
| 贴地 | 已落地且竖直速度向下时，设置一个很小的向下速度 |
| 重力 | 竖直速度累加 gravity × deltaTime，再执行竖直 Move |

### 主要状态字段

| 字段 | 功能 |
|---|---|
| moveSpeed / smoothTime / gravity | 普通速度、输入平滑时间、重力加速度 |
| dodgeDuration / dodgeCooldown / dodgeSpeed | 闪避持续时间、冷却、速度 |
| dodgeRemaining / nextDodgeTime | 闪避剩余时间、下次可闪避时刻 |
| dodgeDirection | 按下闪避时锁定的方向，闪避途中不随输入改变 |
| _rawInput / _smoothInput / _velocity | 原始输入、平滑输入、竖直速度向量 |

静止时闪避使用角色当前朝向；有移动输入时使用输入方向。闪避期间登记本组件为无敌来源，结束时只移除自己的来源。

## 5. PlayerCombat.cs：普通攻击和范围技能

位置：`Assets/Scripts/PlayerCombat.cs`

| 函数 | 调用时机／调用者 | 具体功能 |
|---|---|---|
| Awake() | 初始化时 | 获取生命、输入、移动组件；未配置攻击点则查找 AttackPoint；敌人层为空时尝试使用 Enemy 层 |
| OnEnable() | 启用时 | 监听玩家死亡；已经死亡则立即停止控制 |
| OnDisable() | 禁用时 | 取消玩家死亡监听 |
| OnAttack(InputValue value) | Attack 输入消息 | 检查按下、暂停、死亡、攻击点和冷却；更新朝向、同步物理；检测攻击范围；对敌人去重后扣血 |
| OnSkill(InputValue value) | Skill 输入消息 | 检查按下、死亡、暂停和独立冷却；以玩家为中心检测范围；过滤自身、尸体和非敌人；去重扣血；显示黄色圆环 |
| LogAttack(string message) | 普通攻击过程中 | 根据开关输出命中数量或忽略攻击的原因 |
| StopPlayer() | 玩家死亡事件 | 停用 PlayerInput，禁用移动组件 |
| OnDrawGizmosSelected() | 编辑器选中对象时 | 在攻击点画红色辅助球，显示普通攻击范围 |

| 机制 | 功能 |
|---|---|
| Physics.SyncTransforms() | 将最新 Transform 位置和朝向同步给物理查询 |
| Physics.OverlapSphere() | 获取攻击球形范围内的碰撞体 |
| GetComponentInParent<Health>() | 从身体子碰撞体找到所属角色的生命组件 |
| HashSet<Health> | 同一敌人有多个碰撞体时，一次攻击只扣一次血 |
| _nextAttackTime / nextSkillTime | 分别记录普攻和技能下次可释放时间，冷却互不占用 |
| enemyLayers | 限定需要查询的敌人层 |

普通攻击以 attackPoint 为中心；Q 技能以玩家位置为中心。当前普攻会输出日志，技能显示圆环。技能额外检查 Health 所在根对象的层是否属于 enemyLayers。

## 6. EnemyAI.cs：通用敌人行为

位置：`Assets/Scripts/EnemyAI.cs`

| 函数 | 调用时机／调用者 | 具体功能 |
|---|---|---|
| Awake() | 初始化时 | 获取 Health 和 NavMeshAgent；监听死亡；停车距离设为 attackRange - 0.1，最低为 0 |
| Initialize(Health target, WaveManager manager) | 管理器刷怪后 | 注入玩家和管理器引用；设置第一次允许攻击的时间 |
| Update() | 每帧 | 检查暂停、停战、死亡、目标和导航状态；超出射程则追击；进入射程则停车、转向，冷却结束后攻击 |
| PerformAttack() | Update 判断可以攻击时 | 基类直接给玩家扣血；virtual 允许子类重写攻击方式 |
| StopAgent() | 进入射程或目标失效时 | 暂停导航，清空路径与速度 |
| StopCombat() | 结算或死亡时 | 设置永久停战标记，并停止导航 |
| Die() | 自身死亡事件 | 停战、禁用导航、关闭子碰撞体、向管理器报告死亡、禁用 AI 组件 |
| OnDestroy() | 销毁时 | 取消死亡监听 |

| 字段 | 功能 |
|---|---|
| attackRange / attackInterval / attackDamage | 攻击距离、间隔、近战伤害，实际值以 Inspector 为准 |
| health / player / owner | 自身生命、玩家生命和管理器引用，protected 允许子类访问 |
| nextAttackTime | 下次允许攻击的游戏时刻 |
| stopped | 永久停战标记 |

距离判断先把 delta.y 清零，只比较水平距离。停车距离小于攻击距离，避免导航在射程外提前停住。StopAgent 是临时停车，StopCombat 是永久停战。

## 7. RangedEnemyAI.cs：远程攻击

位置：`Assets/Scripts/RangedEnemyAI.cs`

| 函数 | 调用时机／调用者 | 具体功能 |
|---|---|---|
| PerformAttack() | 父类判断可以攻击时 | 选择枪口或自身位置作为出生点；瞄准玩家 CharacterController 的包围盒中心；生成子弹；传入发射者、目标、管理器和方向 |

| 字段 | 功能 |
|---|---|
| projectilePrefab | 子弹预制体；未配置则调用 Projectile.Create 创建小球 |
| muzzle | 枪口 Transform；未配置则使用敌人根对象位置 |

该类只声明一个函数。追击、距离判断、攻击冷却和死亡处理都来自父类 EnemyAI。子弹发射后直飞，不持续追踪玩家。

## 8. Projectile.cs：子弹

位置：`Assets/Scripts/Projectile.cs`

| 函数或属性 | 调用时机／调用者 | 具体功能 |
|---|---|---|
| Awake() | 初始化时 | 球碰撞体设为 Trigger；刚体设为运动学、关闭重力，使用推测连续碰撞检测 |
| Create(Vector3 position) | 没有子弹预制体时 | 创建默认球体，设置位置、缩放，添加 Projectile；所需 Rigidbody 由组件要求补齐 |
| Launch(Health source, Health victim, WaveManager manager, Vector3 aimDirection) | 远程敌人生成子弹后 | 保存发射者、目标和管理器；归一化方向；标记已发射；忽略发射者碰撞体；方向无效则消耗 |
| Radius | 物理查询时读取 | 将局部球半径乘最大世界缩放轴，得到保守的世界半径 |
| FixedUpdate() | 固定时间步 | 检查状态；处理起点重叠；扫掠路径并排序命中；未命中则移动；累计寿命，到期消耗 |
| TryHit(Collider other) | 查询或 Trigger 共用 | 排除子弹自身和发射者；只认指定玩家及墙体；有效命中先消耗，再按条件给玩家扣血 |
| OnTriggerEnter(Collider other) | 进入 Trigger 时 | 补充处理玩家主动走到子弹上等接触，交给 TryHit 判断 |
| Consume() | 命中、到期、目标或战斗失效时 | 标记已消耗，立即关闭碰撞体，安排销毁 |

### FixedUpdate 内部顺序

| 顺序 | 操作 | 目的 |
|---|---|---|
| 1 | 检查 launched、consumed 和暂停状态 | 尚未发射或已消耗时不处理 |
| 2 | 检查目标和管理器状态 | 目标消失、死亡或战斗结束时消耗 |
| 3 | Physics.SyncTransforms | 使用最新物理位置 |
| 4 | Physics.OverlapSphere | 覆盖子弹起点就在玩家或墙内的情况 |
| 5 | 计算本步时间 | 取 fixedDeltaTime 与剩余寿命的较小值 |
| 6 | Physics.SphereCastAll | 查询整段飞行路径，降低穿透薄墙的风险 |
| 7 | 按距离排序并调用 TryHit | 优先撞近处障碍，避免伤到墙后玩家 |
| 8 | Rigidbody.MovePosition | 没有有效命中才前进 |
| 9 | 累加 age，检查 lifetime | 到期销毁 |

### 不命中发射者的两层保证

1. Launch 中对发射者的子碰撞体调用 `Physics.IgnoreCollision`，忽略物理接触。
2. 主动查询仍需单独过滤，所以 TryHit 检查 `hitHealth == shooter` 或碰撞体是否在发射者子层级下，满足则跳过。

### 主要字段

| 字段 | 功能 |
|---|---|
| damage / speed / lifetime | 伤害、速度、寿命 |
| wallLayers | 可消耗子弹的墙体层掩码，默认第 9 层 |
| shooter / target / owner | 发射者、唯一可伤害目标、战斗管理器 |
| direction / age | 固定飞行方向、已飞行时间 |
| launched | 初始化完成才允许检测和飞行 |
| consumed | 消耗锁，防止多种检测在同一帧重复扣血 |

手写重点：先 Consume 再扣血，因为扣血可能触发死亡和结算；Destroy 到帧末才生效，不能只依赖 Destroy 防止重复命中。

## 9. WaveManager.cs：刷怪与胜负

位置：`Assets/Scripts/WaveManager.cs`

当前只有开局一批敌人，清完即胜利，没有自动下一波。

| 函数或属性 | 调用时机／调用者 | 具体功能 |
|---|---|---|
| Start() | 首次运行组件时 | 恢复时间；检查配置；监听玩家死亡；进入战斗状态；循环生成整批敌人 |
| TrySpawn(int index) | Start 循环调用 | 选择模板、寻找合法出生点、生成并复检导航、登记敌人、注入玩家和管理器 |
| NotifyEnemyDeath(EnemyAI enemy) | 敌人死亡时 | 从集合移除敌人，重复通知忽略；刷怪完成且清空敌人时判定胜负 |
| OnPlayerDeath() | 玩家死亡事件 | 调用 Finish，显示失败 |
| Finish(string message) | 胜负、配置错误或刷怪失败时 | 防重复结算；停止敌人和玩家；记录结果；暂停游戏时间；解锁并显示鼠标 |
| Restart() | 重开按钮或外部调用 | 恢复时间并重新加载当前场景 |
| OnGUI() | Unity 绘制即时 GUI 时 | 有结果时显示结算面板和重开按钮，暂停时也能点击 |
| OnDestroy() | 销毁时 | 取消玩家死亡监听 |
| IsPlaying | AI、子弹等读取 | 本局是否处于战斗状态 |
| AliveCount | 外部读取 | 存活集合中的敌人数量 |

### TrySpawn 内部步骤

| 步骤 | 规则 |
|---|---|
| 选择模板 | 配置远程模板时，最后一个名额生成远程敌人 |
| 设置查询筛选器 | 使用模板 Agent 类型和允许行走区域 |
| 选择候选点 | 优先指定出生点，失败后随机偏移；没有指定点则在管理器附近随机 |
| NavMesh.SamplePosition | 找附近导航地面 |
| 检查玩家距离 | 水平距离至少 2，避免贴脸出生 |
| 检查敌人间距 | 根据模板导航半径排除与已生成敌人重叠的位置 |
| 生成对象 | 导航地面位置加模板 baseOffset |
| 导航复检 | Agent 不可用或不在 NavMesh 上则销毁并重试 |
| 登记初始化 | 先加入 alive，再调用 Initialize(player, this) |
| 输出失败统计 | 尝试用完后报告导航采样、玩家距离、敌人重叠和 Agent 绑定失败次数 |

| 关键字段 | 功能 |
|---|---|
| player / enemyPrefab / rangedEnemyPrefab / spawnPoints | 玩家、近战模板、可选远程模板、出生点数组 |
| enemyCount / spawnRadius / sampleDistance / attemptsPerEnemy | 生成数量、随机半径、导航采样距离、每只最多尝试次数 |
| alive | HashSet 存活名单，避免重复登记或重复减数 |
| spawning | 标记刷怪尚未完成，避免入场过程中提前判胜 |
| result | null 表示尚未结束；有文字表示已结算 |

注意：找到 NavMesh 出生点不等于验证了到玩家的完整可达路径；Time.timeScale = 0 不会关闭所有脚本回调，因此各脚本还会检查暂停或 IsPlaying。

## 10. CombatRangeEffect.cs：临时范围圆环

位置：`Assets/Scripts/CombatRangeEffect.cs`

| 函数 | 调用时机／调用者 | 具体功能 |
|---|---|---|
| Show(Vector3 centre, float radius, Color color, float duration) | 闪避、Q 技能调用 | 创建对象和 LineRenderer；选择着色器并创建材质；设置颜色；用 64 个点画水平圆环；保存持续时间 |
| Update() | 每帧 | 剩余时间减 deltaTime，到期销毁 |
| OnDestroy() | 销毁时 | 释放运行时创建的材质，避免积累 |

圆环使用局部坐标，圆心由对象世界位置决定。X 使用 cos，Z 使用 sin，Y 为 0。圆环没有 Collider，不负责伤害。暂停时 deltaTime 为 0，因此显示倒计时暂停。

## 11. Death_disappear.cs：死亡移除辅助

位置：`Assets/Scripts/Death_disappear.cs`

| 函数 | 调用时机／调用者 | 具体功能 |
|---|---|---|
| Start() | 首次运行时 | 从自身或父对象查找 Health 并缓存 |
| Update() | 每帧 | 再次查找 Health；已死亡则销毁当前挂载物体 |

当前代码找不到 Health 时会因直接访问 `health.IsDead` 报空引用。手写时建议先判空：

```csharp
if (health != null && health.IsDead)
{
    Destroy(gameObject);
}
```

此处只是手写建议，整理文档时没有修改原脚本。Health 已经安排销毁整个角色，因此本脚本与它有部分职责重复。挂在模型子物体上时，本脚本只销毁那个子物体。

## 12. Readme.cs：教程说明数据

位置：`Assets/TutorialInfo/Readme.cs`

该脚本没有声明函数，继承 ScriptableObject，主要保存数据。

| 成员 | 功能 |
|---|---|
| commonStyle | 通用页面样式 |
| darkStyle / lightStyle | 深色和浅色主题样式 |
| icon / title | 图标和标题 |
| sections | 说明段落数组 |
| loadedLayout | 记录是否加载过教程布局 |
| Section 内部类 | 可序列化段落数据，保存 heading、text、linkText、url |

## 13. ReadmeEditor.cs：教程编辑器界面

位置：`Assets/TutorialInfo/Editor/ReadmeEditor.cs`

只在 Unity 编辑器中运行，不参与游戏战斗。

| 函数 | 调用时机／调用者 | 具体功能 |
|---|---|---|
| static ReadmeEditor() | 编辑器加载类时 | 注册延迟回调，待编辑器准备好再自动选择说明页 |
| SelectReadmeAutomatically() | 编辑器延迟回调 | 每个会话自动展示一次；资源未加载过布局时加载教程布局 |
| SelectReadme() | 自动显示或删除流程 | 查找唯一 Readme 资源，选中并返回；数量不是 1 则返回 null |
| RemoveTutorial() | 点击删除按钮时 | 弹窗确认；确认后删除教程目录、说明资源及相关 meta，刷新资源数据库 |
| OnHeaderGUI() | 绘制 Inspector 标题时 | 空实现，关闭默认标题绘制 |
| OnInspectorGUI() | 绘制传统 Inspector 时 | 空实现，改用 UI Toolkit 页面 |
| CreateInspectorGUI() | 创建 Inspector 时 | 创建容器、添加样式、图标、标题、各段内容、链接和删除按钮 |
| ChainWithClass(VisualElement created, string className) | CreateInspectorGUI 内部 | 局部函数：给 UI 元素添加样式类名并返回该元素 |
| 链接 ClickEvent 回调 | 点击段落链接时 | Application.OpenURL(section.url) 打开链接 |

## 14. 手写顺序与调用关系

建议顺序：

1. Health：先完成受伤、死亡事件和无敌来源。
2. CombatRangeEffect：独立练习圆环创建与清理。
3. PlayerCCMovement：输入、移动、重力，再加闪避和无敌。
4. PlayerCombat：先普攻，再技能、去重和冷却。
5. EnemyAI：目标初始化、导航追击、距离、冷却和死亡。
6. Projectile：初始化、飞行、命中筛选、寿命和消耗锁。
7. RangedEnemyAI：重写攻击，接上子弹。
8. WaveManager：刷怪、目标注入、计数、胜负与重开。
9. Death_disappear：理解后判断是否需要保留。
10. Readme 和 ReadmeEditor：最后练习编辑器扩展。

脚本互相引用，未写齐依赖类时出现“找不到类型”的编译错误，需要补齐类后再运行。当前代码依赖 Input System、AI Navigation 等包，手写时还需配置 PlayerInput、Input Actions、Layer、NavMesh 和预制体引用。

| 流程 | 调用链 |
|---|---|
| 开局 | WaveManager.Start → TrySpawn → EnemyAI.Initialize |
| 玩家移动 | PlayerInput 的 Move → PlayerCCMovement.OnMove → Update 执行移动 |
| 玩家闪避 | PlayerInput 的 Dodge → OnDodge → 登记无敌 → Update 闪避 → CancelDodge |
| 玩家攻击 | PlayerInput 的 Attack / Skill → PlayerCombat → Health.TakeDamage |
| 近战敌人攻击 | EnemyAI.Update → PerformAttack → 玩家 Health.TakeDamage |
| 远程敌人攻击 | EnemyAI.Update → RangedEnemyAI.PerformAttack → Projectile.Launch → FixedUpdate / OnTriggerEnter → TryHit |
| 敌人死亡 | Health.TakeDamage → OnDeath → EnemyAI.Die → WaveManager.NotifyEnemyDeath |
| 玩家死亡 | Health.TakeDamage → OnDeath → 移动取消闪避、战斗停止控制、管理器判负；各监听者处理各自职责 |
| 胜利 | 最后一名敌人死亡 → NotifyEnemyDeath → Finish |
| 重开 | OnGUI 按钮 → Restart → 重新加载当前场景 |
