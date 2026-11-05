using UnityEngine;

public class KnightAnimationEvents : MonoBehaviour
{
    private PlayerCombat playerCombat;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        playerCombat = GetComponentInParent<PlayerCombat>();
    }

    // 由 1H_Melee_Attack_Slice_Horizontal 片段第 1 秒的动画事件调用。
    public void Attack_End()
    {
        if (playerCombat != null) playerCombat.EndAttack();
    }
}
