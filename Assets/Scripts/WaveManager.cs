using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
// 竞技场裁判：只在开局刷一批敌人，清完判胜，没有下一波。
public class WaveManager : MonoBehaviour
{
    // 开赛资料：玩家、敌人模板、出生点。
    [SerializeField] private Health player;
    [SerializeField] private EnemyAI enemyPrefab;
    // 可选远程模板：赋值时最后一个名额生成远程敌人；留空则全是近战。
    [SerializeField] private EnemyAI rangedEnemyPrefab;
    [SerializeField] private Transform[] spawnPoints;
    // 刷怪配置：人数、随机半径、导航采样距离与尝试次数。
    [SerializeField, Min(1)] private int enemyCount = 3;
    [SerializeField, Min(0.1f)] private float spawnRadius = 7f;
    [SerializeField, Min(0.1f)] private float sampleDistance = 2f;
    // 每只敌人最多找多少次落点，不是敌人数量；找不到就明确显示刷怪失败。
    [SerializeField, Min(1)] private int attemptsPerEnemy = 20;
    // 存活点名册不重复登记，readonly 禁止更换册子但能增删名字。
    private readonly HashSet<EnemyAI> alive = new HashSet<EnemyAI>();
    // 入场期间不能提前判定清场胜利。
    private bool spawning;
    // 结算公告，null 代表尚未结束。
    private string result;
    // 比赛信号灯，外部只读。
    public bool IsPlaying { get; private set; }
    // 直接从点名册读取存活人数。
    public int AliveCount => alive.Count;

    // 恢复时间、检查配置、监听玩家死亡并一次生成整批敌人。
    private void Start()
    {
        Time.timeScale = 1f;
        if (player == null || enemyPrefab == null)
        {
            Finish("配置错误：请设置 Player 和 Enemy Prefab");
            return;
        }
        // 向血条登记：玩家死亡请通知裁判。
        player.OnDeath.AddListener(OnPlayerDeath);
        IsPlaying = true;
        if (player.IsDead) { OnPlayerDeath(); return; }
        spawning = true;
        for (int i = 0; i < enemyCount; i++)
        {
            // 任何一只刷怪失败就结束本局并显示原因。
            if (!TrySpawn(i))
            {
                spawning = false;
                Finish("刷怪失败：找不到有效导航落点，请检查 NavMesh");
                return;
            }
        }
        spawning = false;
    }

    // 找合法座位：可站导航地面、离玩家至少两单位、不挤占其他敌人。
    private bool TrySpawn(int index)
    {
        // index 从 0 开始，因此 enemyCount - 1 是本批最后一个生成名额。
        EnemyAI selectedPrefab = rangedEnemyPrefab != null && index == enemyCount - 1 ? rangedEnemyPrefab : enemyPrefab;
        NavMeshAgent template = selectedPrefab.GetComponent<NavMeshAgent>();
        // 用敌人模板的导航类型和可走区域筛选。
        var filter = new NavMeshQueryFilter
        {
            agentTypeID = template.agentTypeID,
            // 只在该 Agent 允许行走的导航区域中寻找落点。
            areaMask = template.areaMask
        };
        for (int attempt = 0; attempt < attemptsPerEnemy; attempt++)
        {
            // 出生点轮流使用，无有效点则以管理器位置为中心。
            Transform point = spawnPoints != null && spawnPoints.Length > 0
                ? spawnPoints[index % spawnPoints.Length] : null;
            Vector3 candidate = point != null ? point.position : transform.position;
            // 先试指定点，失败再随机偏移；没有指定点则立即随机。
            if (point == null || attempt > 0)
            {
                Vector2 offset = Random.insideUnitCircle * spawnRadius;
                candidate += new Vector3(offset.x, 0f, offset.y);
            }
            // 找到附近导航地面，不代表检查了到玩家的完整路径。
            if (!NavMesh.SamplePosition(candidate, out NavMeshHit hit, sampleDistance, filter))
            {
                continue;
            }
            // 忽略玩家高度计算距离，防止贴脸出生。
            Vector3 playerGround = player.transform.position;
            playerGround.y = hit.position.y;
            if (Vector3.Distance(playerGround, hit.position) < 2f)
            {
                continue;
            }
            // 逐个查座位间距，避免敌人挤在一起。
            bool occupied = false;
            foreach (EnemyAI other in alive)
            {
                Vector3 separation = other.transform.position - hit.position;
                separation.y = 0f;
                if (separation.magnitude < template.radius * 2f + 0.1f)
                {
                    occupied = true;
                    break;
                }
            }
            if (occupied)
            {
                continue;
            }
            // SamplePosition gives the navigation surface, not the model's centre.
            // 地面位置加上模板导航高度偏移。
            Vector3 position = hit.position + Vector3.up * template.baseOffset;
            EnemyAI enemy = Instantiate(selectedPrefab, position, Quaternion.identity);
            NavMeshAgent agent = enemy.GetComponent<NavMeshAgent>();
            // 生成后复检导航，不可用则销毁并重试。
            if (!agent.enabled || !agent.isOnNavMesh)
            {
                Destroy(enemy.gameObject);
                continue;
            }
            // 先登记名单，再发目标和负责人引用。
            alive.Add(enemy);
            enemy.Initialize(player, this);
            return true;
        }
        Debug.LogError($"[WaveManager] 敌人 {index + 1} 在 {attemptsPerEnemy} 次尝试后仍无法生成。", this);
        return false;
    }

    // 收到死亡电话先划掉名字，重复通知不会重复计数。
    public void NotifyEnemyDeath(EnemyAI enemy)
    {
        if (!alive.Remove(enemy)) return;
        // 入场完成且名单清空才判胜，玩家死亡则判负。
        if (IsPlaying && !spawning && alive.Count == 0)
        {
            if (player == null || player.IsDead) OnPlayerDeath();
            else Finish("胜利！");
        }
    }

    // 玩家死亡进入统一失败结算。
    private void OnPlayerDeath() => Finish("失败！");

    // 终场哨只吹一次，停双方行动并显示结果。
    private void Finish(string message)
    {
        if (result != null) return;
        result = message;
        IsPlaying = false;
        foreach (EnemyAI enemy in alive)
            if (enemy != null) enemy.StopCombat();
        if (player != null)
        {
            // 结局统一关闭输入、移动与战斗；禁用移动也会清理闪避无敌。
            var input = player.GetComponent<UnityEngine.InputSystem.PlayerInput>();
            if (input != null) input.DeactivateInput();
            var movement = player.GetComponent<PlayerCCMovement>();
            if (movement != null) movement.enabled = false;
            var combat = player.GetComponent<PlayerCombat>();
            if (combat != null) combat.enabled = false;
        }
        Debug.Log($"[WaveManager] {message}", this);
        // 暂停游戏时间，不等于关闭所有脚本回调。
        Time.timeScale = 0f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    // 恢复时间后重新加载当前场景。
    public void Restart()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().path);
    }

    // Minimal clickable UI; works while paused without a Canvas/EventSystem.
    // 时间暂停也能点击的临时结算牌。
    private void OnGUI()
    {
        if (result == null) return;
        Rect panel = new Rect((Screen.width - 440f) / 2f, (Screen.height - 160f) / 2f, 440f, 160f);
        GUI.Box(panel, "游戏结束");
        GUI.Label(new Rect(panel.x + 20, panel.y + 40, 400, 45), result);
        if (GUI.Button(new Rect(panel.x + 140, panel.y + 100, 160, 40), "重开 / Restart")) Restart();
    }

    // 裁判离场取消死亡订阅。
    private void OnDestroy()
    {
        if (player != null) player.OnDeath.RemoveListener(OnPlayerDeath);
    }
}
