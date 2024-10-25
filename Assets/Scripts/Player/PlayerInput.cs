using UnityEngine;

public class PlayerInput : MonoBehaviour
{
    public Vector2 MovementInput { get; private set; }
    public bool JumpInputDown { get; private set; }
    public bool JumpInputUp { get; private set; }

    private void Update()
    {
        var moveX = Input.GetAxis("Horizontal");
        var moveY = Input.GetAxis("Vertical");

        MovementInput = new Vector2(moveX, moveY);

        JumpInputDown = Input.GetButtonDown("Jump");
        JumpInputUp = Input.GetButtonUp("Jump");
    }
}