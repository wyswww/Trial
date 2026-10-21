using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class PlayerCCMovement : MonoBehaviour
{
    public float moveSpeed = 5f;
    public float smoothTime = 0.15f;
    public float gravity = -9.81f;

    private CharacterController _cc;
    private Vector2 _rawInput;
    private Vector2 _smoothInput;
    private Vector3 _velocity;

    void Awake()
    {
        _cc = GetComponent<CharacterController>();
    }

    // Send Messages 模式下，PlayerInput 会按 "On" + Action名 反射调用此方法
    // 参数必须是 InputValue，且方法为 public
    public void OnMove(InputValue value)
    {
        _rawInput = value.Get<Vector2>();
        Debug.Log("输入：" + _rawInput);
    }

    void Update()
    {
        // 输入平滑
        _smoothInput = Vector2.Lerp(_smoothInput, _rawInput, Time.deltaTime / smoothTime);

        // 水平移动
        Vector3 moveDir = transform.right * _smoothInput.x + transform.forward * _smoothInput.y;
        _cc.Move(moveDir * moveSpeed * Time.deltaTime);

        // 重力 / 贴地
        if (_cc.isGrounded && _velocity.y < 0)
        {
            _velocity.y = -0.2f;
        }
        _velocity.y += gravity * Time.deltaTime;
        _cc.Move(_velocity * Time.deltaTime);
    }
}