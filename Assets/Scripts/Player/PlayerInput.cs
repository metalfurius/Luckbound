using UnityEngine;

public class PlayerInput : MonoBehaviour
{
    public Vector2 MovementInput { get; private set; }
    public bool JumpInputDown { get; private set; }
    public bool JumpInputUp { get; private set; }
    public bool AttackInput { get; private set; }

    private void Update()
    {
        var moveX = Input.GetAxis($"Horizontal");
        var moveY = Input.GetAxis($"Vertical");

        MovementInput = new Vector2(moveX, moveY);
        
        AttackInput = Input.GetButtonDown($"Fire1");
        JumpInputDown = Input.GetButtonDown($"Jump");
        JumpInputUp = Input.GetButtonUp($"Jump");
    }
}