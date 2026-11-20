using UnityEngine;

[RequireComponent(typeof(CharacterController), typeof(PlayerInputReader))]
public class PlayerMovement : MonoBehaviour
{
    [Header("Speed")]
    [SerializeField] private float walkSpeed = 4f;

    [Header("Gravity")]
    [SerializeField] private float gravity = -20f;

    private CharacterController _controller;
    private PlayerInputReader _input;
    private float _verticalVelocity;

    private void Awake()
    {
        _controller = GetComponent<CharacterController>();
        _input = GetComponent<PlayerInputReader>();
    }

    private void Update()
    {
        Vector2 move2D = Vector2.ClampMagnitude(_input.moveInput, 1f);
        Vector3 move = (transform.right * move2D.x + transform.forward * move2D.y) * walkSpeed;

        if (_controller.isGrounded && _verticalVelocity < 0f)
            _verticalVelocity = -2f;

        _verticalVelocity += gravity * Time.deltaTime;
        move.y = _verticalVelocity;

        _controller.Move(move * Time.deltaTime);
    }
}
