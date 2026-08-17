using UnityEngine;

public struct PlayerInputData
{
    public float moveX;
    public float moveY;
    public bool isRunning;
    public bool jumpPressed;
    public bool jumpHeld;
    public bool dashPressed;
    public bool attack1Pressed;
    public bool attack2Pressed;
    public bool attack3Pressed;
}

public class PlayerInputHandler : MonoBehaviour
{
    [Header("Input States")]
    public float MoveInputX { get; private set; }
    public float MoveInputY { get; private set; }
    public bool IsRunning { get; private set; }

    public bool JumpTriggered { get; private set; }
    public bool JumpHeld { get; private set; }
    public bool DashTriggered { get; private set; }

    public bool Attack1Triggered { get; private set; }
    public bool Attack2Triggered { get; private set; }
    public bool Attack3Triggered { get; private set; }

    public bool HurtTestTriggered { get; private set; }
    public bool DieTestTriggered { get; private set; }

    public PlayerInputData CurrentInput => new PlayerInputData
    {
        moveX = MoveInputX,
        moveY = MoveInputY,
        isRunning = IsRunning,
        jumpPressed = JumpTriggered,
        jumpHeld = JumpHeld,
        dashPressed = DashTriggered,
        attack1Pressed = Attack1Triggered,
        attack2Pressed = Attack2Triggered,
        attack3Pressed = Attack3Triggered
    };

    void Update()
    {
        MoveInputX = Input.GetAxisRaw("Horizontal");
        MoveInputY = Input.GetAxisRaw("Vertical");

        IsRunning = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        JumpHeld = Input.GetButton("Jump");

        if (Input.GetButtonDown("Jump")) JumpTriggered = true;
        if (Input.GetKeyDown(KeyCode.C) || Input.GetKeyDown(KeyCode.LeftControl)) DashTriggered = true;
        if (Input.GetMouseButtonDown(0)) Attack1Triggered = true;
        if (Input.GetMouseButtonDown(1)) Attack2Triggered = true;
        if (Input.GetKeyDown(KeyCode.F)) Attack3Triggered = true;

        if (Input.GetKeyDown(KeyCode.H)) HurtTestTriggered = true;
        if (Input.GetKeyDown(KeyCode.X)) DieTestTriggered = true;
    }

    public void ConsumeJump() => JumpTriggered = false;
    public void ConsumeDash() => DashTriggered = false;
    public void ConsumeAttack1() => Attack1Triggered = false;
    public void ConsumeAttack2() => Attack2Triggered = false;
    public void ConsumeAttack3() => Attack3Triggered = false;
    public void ConsumeHurtTest() => HurtTestTriggered = false;
    public void ConsumeDieTest() => DieTestTriggered = false;

    public void ClearTriggers()
    {
        JumpTriggered = false;
        DashTriggered = false;
        Attack1Triggered = false;
        Attack2Triggered = false;
        Attack3Triggered = false;
        HurtTestTriggered = false;
        DieTestTriggered = false;
    }
}