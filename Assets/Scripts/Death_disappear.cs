using UnityEngine;

// 血条退场助手：找到自身或父层 Health，死亡后销毁当前物体；Health 本身也会安排销毁。
public class Death_disappear : MonoBehaviour
{

    // 缓存本物体所属的血条；通常放在模型子物体上，血条在父对象。
    private Health health;
   
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    // 首次寻找血条，也会检查自身。
    void Start()
    {
        health = GetComponentInParent<Health>();
    }

    // Update is called once per frame
    // 每帧重新找血条；找不到时当前代码会报空引用。
    void Update()
    {
         health = GetComponentInParent<Health>();
         if (health.IsDead)
         {
            // 只销毁挂着这个脚本的物体，放在子物体上就只销毁该子物体。
            Destroy(gameObject);
         }
    }
}
