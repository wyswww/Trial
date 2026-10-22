using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public class WaveManager : MonoBehaviour
{
    [SerializeField] private Health player;
    [SerializeField] private EnemyAI enemyPrefab;
    [SerializeField] private Transform[] spawnPoints;
    [SerializeField, Min(1)] private int enemyCount = 3;
    [SerializeField, Min(0.1f)] private float spawnRadius = 7f;
    [SerializeField, Min(0.1f)] private float sampleDistance = 2f;
    [SerializeField, Min(1)] private int attemptsPerEnemy = 20;
    private readonly HashSet<EnemyAI> alive = new HashSet<EnemyAI>();
    private bool spawning;
    private string result;
    public bool IsPlaying { get; private set; }
    public int AliveCount => alive.Count;

    private void Start()
    {
        Time.timeScale = 1f;
        if (player == null || enemyPrefab == null)
        {
            Finish("配置错误：请设置 Player 和 Enemy Prefab");
            return;
        }
        player.OnDeath.AddListener(OnPlayerDeath);
        IsPlaying = true;
        if (player.IsDead) { OnPlayerDeath(); return; }
        spawning = true;
        for (int i = 0; i < enemyCount; i++)
        {
            if (!TrySpawn(i))
            {
                spawning = false;
                Finish("刷怪失败：找不到有效导航落点，请检查 NavMesh");
                return;
            }
        }
        spawning = false;
    }

    private bool TrySpawn(int index)
    {
        NavMeshAgent template = enemyPrefab.GetComponent<NavMeshAgent>();
        var filter = new NavMeshQueryFilter
        {
            agentTypeID = template.agentTypeID,
            areaMask = template.areaMask
        };
        for (int attempt = 0; attempt < attemptsPerEnemy; attempt++)
        {
            Transform point = spawnPoints != null && spawnPoints.Length > 0
                ? spawnPoints[index % spawnPoints.Length] : null;
            Vector3 candidate = point != null ? point.position : transform.position;
            if (point == null || attempt > 0)
            {
                Vector2 offset = Random.insideUnitCircle * spawnRadius;
                candidate += new Vector3(offset.x, 0f, offset.y);
            }
            if (!NavMesh.SamplePosition(candidate, out NavMeshHit hit, sampleDistance, filter))
            {
                continue;
            }
            Vector3 playerGround = player.transform.position;
            playerGround.y = hit.position.y;
            if (Vector3.Distance(playerGround, hit.position) < 2f)
            {
                continue;
            }
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
            Vector3 position = hit.position + Vector3.up * template.baseOffset;
            EnemyAI enemy = Instantiate(enemyPrefab, position, Quaternion.identity);
            NavMeshAgent agent = enemy.GetComponent<NavMeshAgent>();
            if (!agent.enabled || !agent.isOnNavMesh)
            {
                Destroy(enemy.gameObject);
                continue;
            }
            alive.Add(enemy);
            enemy.Initialize(player, this);
            return true;
        }
        Debug.LogError($"[WaveManager] 敌人 {index + 1} 在 {attemptsPerEnemy} 次尝试后仍无法生成。", this);
        return false;
    }

    public void NotifyEnemyDeath(EnemyAI enemy)
    {
        if (!alive.Remove(enemy)) return;
        if (IsPlaying && !spawning && alive.Count == 0)
        {
            if (player == null || player.IsDead) OnPlayerDeath();
            else Finish("胜利！");
        }
    }

    private void OnPlayerDeath() => Finish("失败！");

    private void Finish(string message)
    {
        if (result != null) return;
        result = message;
        IsPlaying = false;
        foreach (EnemyAI enemy in alive)
            if (enemy != null) enemy.StopCombat();
        if (player != null)
        {
            var input = player.GetComponent<UnityEngine.InputSystem.PlayerInput>();
            if (input != null) input.DeactivateInput();
            var movement = player.GetComponent<PlayerCCMovement>();
            if (movement != null) movement.enabled = false;
            var combat = player.GetComponent<PlayerCombat>();
            if (combat != null) combat.enabled = false;
        }
        Debug.Log($"[WaveManager] {message}", this);
        Time.timeScale = 0f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void Restart()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().path);
    }

    // Minimal clickable UI; works while paused without a Canvas/EventSystem.
    private void OnGUI()
    {
        if (result == null) return;
        Rect panel = new Rect((Screen.width - 440f) / 2f, (Screen.height - 160f) / 2f, 440f, 160f);
        GUI.Box(panel, "游戏结束");
        GUI.Label(new Rect(panel.x + 20, panel.y + 40, 400, 45), result);
        if (GUI.Button(new Rect(panel.x + 140, panel.y + 100, 160, 40), "重开 / Restart")) Restart();
    }

    private void OnDestroy()
    {
        if (player != null) player.OnDeath.RemoveListener(OnPlayerDeath);
    }
}
