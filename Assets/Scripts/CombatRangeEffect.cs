using UnityEngine;

// 临时范围提示：用线条画圆，不含 Collider，不负责伤害计算。
public class CombatRangeEffect : MonoBehaviour
{
    // 圆环还显示多少游戏秒；暂停时 deltaTime 为 0，因此提示也暂停消失。
    private float remaining;
    // 运行时单独创建的材质，圆环销毁时必须一起释放。
    private Material material;

    // 在指定位置画圆：centre 为圆心、radius 为半径、color 为颜色、duration 为显示秒数。
    public static void Show(Vector3 centre, float radius, Color color, float duration)
    {
        var effect = new GameObject("CombatRangeEffect");
        effect.transform.position = centre;
        var line = effect.AddComponent<LineRenderer>();
        var timer = effect.AddComponent<CombatRangeEffect>();
        timer.remaining = duration;
        // 优先使用 URP 无光照着色器；没有时尝试备用着色器，都没有则取消显示。
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Sprites/Default");
        if (shader == null) { Destroy(effect); return; }
        timer.material = new Material(shader);
        timer.material.SetColor("_BaseColor", color);
        timer.material.color = color;
        line.sharedMaterial = timer.material;
        // 圆上的点采用物体局部坐标，圆心由 effect 的世界位置决定。
        line.useWorldSpace = false;
        line.loop = true;
        line.widthMultiplier = 0.08f;
        // 用 64 个点连接成闭合圆，点越多看起来越平滑。
        line.positionCount = 64;
        for (int i = 0; i < 64; i++)
        {
            // 把一整圈 2π 弧度均分；余弦算 X，正弦算 Z，让圆平放在水平面上。
            float angle = i * Mathf.PI * 2f / 64f;
            line.SetPosition(i, new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius));
        }
    }

    // 按游戏时间倒计时，到期销毁提示物体。
    private void Update()
    {
        remaining -= Time.deltaTime;
        if (remaining <= 0f) Destroy(gameObject);
    }

    // 释放动态材质，避免多次释放技能后积累不用的材质。
    private void OnDestroy()
    {
        if (material != null) Destroy(material);
    }
}
