using UnityEngine;
using UnityEngine.InputSystem;

public class InputManager : MonoBehaviour
{   
    private PlayerInput playerInput;
    public PlayerInput.OnFootActions onFoot;
    private PlayerMotor motor;
    private PlayerLook look;

    void Awake()
    {
        playerInput = new PlayerInput();
        onFoot = playerInput.OnFoot;
        motor = GetComponent<PlayerMotor>();
        look = GetComponent<PlayerLook>();
        onFoot.Jump.performed += ctx => motor.Jump();
        onFoot.Crouch.started += ctx => motor.Crouch();
        onFoot.Crouch.canceled += ctx => motor.Crouch();
        onFoot.Sprint.performed += ctx => motor.Sprint();
        onFoot.Sprint.canceled += ctx => motor.Sprint();
    }


    // Update is called once per frame
    void Update()
    {
        //TEST
    }

    void FixedUpdate()
    {
        motor.ProcessMove(onFoot.Movement.ReadValue<Vector2>());
    }
    private void LateUpdate()
    {
        look.ProcessLook(onFoot.Look.ReadValue<Vector2>());
    }

    private void OnEnable()
    {
        onFoot.Enable();
    }

    private void OnDisable()
    {
        onFoot.Disable();
    }
}
