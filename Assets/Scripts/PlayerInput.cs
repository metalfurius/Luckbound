using UnityEngine;

public class PlayerInput : MonoBehaviour
{
    public Vector2 MovementInput { get; private set; }
    public bool JumpInput { get; private set; }

    void Update()
    {
        var _moveX = Input.GetAxis("Horizontal");
        var _moveY = Input.GetAxis("Vertical");

        MovementInput = new Vector2(_moveX, _moveY);

        JumpInput = Input.GetButtonDown("Jump");
    }
}