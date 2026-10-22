# 玩家近战配置

`Assets/Scenes/main_scene.unity` 已配置 Player、AttackPoint、TrainingDummy 和 Enemy 层，无需额外框架。运行前若 Unity 提示场景在磁盘上变化，请重新加载以读取配置。

## 挂载和 Inspector

| 对象 | 组件 / 字段 | 配置 |
| --- | --- | --- |
| Player | CharacterController、PlayerCCMovement、PlayerInput、Health、PlayerCombat | 全部在同一对象上 |
| PlayerInput | Actions / Default Map / Default Scheme / Behavior | PlayerIA / player / Keyboard / Send Messages |
| Player Health | Max Health / Disable Colliders On Death | 100 / 不勾选 |
| PlayerCombat | Attack Point | 拖入 Player 的 AttackPoint 子对象 |
| PlayerCombat | Attack Damage / Attack Radius / Attack Cooldown / Enemy Layers | 20 / 0.8 / 0.6 / 仅 Enemy |
| AttackPoint | 局部坐标 / 旋转 / 缩放 | (0, 0, 1) / (0, 0, 0) / (1, 1, 1)，不挂 Collider |
| TrainingDummy | Health | Max Health = 60，勾选 Disable Colliders On Death |
| TrainingDummy 及受击 Collider 子对象 | Layer | Enemy（第 8 层） |

PlayerIA 的 player Action Map 新增 Button 类型 Attack，绑定 `<Mouse>/leftButton`；沿用 Keyboard 控制方案，设备包含 Keyboard 和 Mouse。每次按下攻击一次，松开不攻击，长按不连发；冷却内的按下不排队。WASD 沿世界 XZ 移动，角色朝最后一次非零移动输入方向，松开后保持朝向。选中 Player 并开启 Scene Gizmos 可见红色攻击球。

## Health 接口与死亡

`MaxHealth`、`CurrentHealth`、`IsDead` 提供只读状态；Awake 时满血。调用 `TakeDamage(int)` / `Heal(int)` 扣血、回血，忽略非正数，生命值限定在 0 到 MaxHealth。死亡后不回血、不复活；禁用再启用也不会重置生命或再次触发死亡。

`OnDeath` 是 UnityEvent，可在 Inspector 配置或用 `health.OnDeath.AddListener(...)` 订阅。死亡标记在事件回调之前置位，保证重复伤害和回调内重入只触发一次死亡事件。PlayerCombat 自动订阅玩家死亡，停用 PlayerInput 并禁用移动，清除残余移动输入；目标死亡禁用本 Health 所属的全部子 Collider。

攻击查询使用 Physics.OverlapSphere，包含 Enemy 层的普通和 Trigger Collider；通过 GetComponentInParent 查找 Health，用本次攻击的 HashSet 按 Health 实例去重。一个目标的多个 Collider 应共用根对象上的一个 Health，独立目标各挂自己的 Health。

## Play Mode 验收

临时观察使用 Unity Console：Health 的 `Log Health Changes` 默认开启，初始化、有效扣血、有效回血和死亡时输出对象名及当前/最大生命值。点击日志可定位对应对象；无需搭建 UI。TrainingDummy 三次受击应依次显示 `HP 40/60`、`HP 20/60`、`HP 0/60`，随后仅一次“死亡”日志。满血回血和死亡后继续伤害不会刷日志。正式 UI 第 6 天完成后，可取消勾选此字段关闭日志。

如果看不到扣血：清空 Console 搜索框，开启普通 Log 显示；进入 Play Mode 后点击 Game 窗口，靠近并面向目标再按左键。PlayerCombat 的 `Log Attacks` 默认开启，`[Combat]` 日志显示攻击检测到的 Collider 和有效 Health 数量，以及冷却或缺少 AttackPoint 的原因。0 个 Collider 时查看选中 Player 的红色 Gizmo 是否覆盖 Enemy 层目标；有 Collider 但 0 个 Health 时检查其自身或父对象是否挂有存活的 Health。完全没有攻击日志时检查 Game 焦点以及 PlayerInput 的 Attack 绑定和 Send Messages 配置。

1. 靠近并面向 TrainingDummy，逐次点击左键，间隔至少 0.6 秒：CurrentHealth 应为 60 → 40 → 20 → 0，第三次死亡后 Collider 被禁用。
2. 给目标增加两个 Enemy 层子 Collider，确保同时在球内：每次仍只扣 20；把两个独立目标放入范围，每个都扣 20。
3. 在 0.6 秒内重复点击、持续按住、转向后攻击、背对目标或离开范围，分别检查冷却、单击触发、朝向和范围限制。
4. 对存活目标调用 Heal，不能超过 60；非正伤害和非正回血无效。死亡后继续 TakeDamage / Heal，OnDeath 计数仍为 1，CurrentHealth 保持 0。
5. 对玩家调用 TakeDamage(100)，确认 WASD 和左键均失效，正在移动时死亡也立即停止。

这次修改通过 Unity 工程现有编译引用进行 C# 编译检查；以上交互步骤仍需在 Unity Play Mode 中执行。
