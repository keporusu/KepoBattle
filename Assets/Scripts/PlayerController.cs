using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviour
{
    [SerializeField] private float jumpPower = 1.0f;
    [SerializeField] private float moveSpeed = 1.0f;
    private InputAction moveAction;
    private InputAction jumpAction;
    private InputAction attackAction;
    private PhysicsMover physicsMover_cache;
    private Attack attack_cache;

    //State
    private bool isJumping=false;
    private float moveInput = 0f;

    void Awake()
    {
        var inputActions = new InputSystem_Actions();
        moveAction = inputActions.Player.Move;
        jumpAction = inputActions.Player.Jump;
        attackAction = inputActions.Player.Attack;
    }


    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        physicsMover_cache = GetComponent<PhysicsMover>();
        attack_cache = GetComponent<Attack>();
        if (physicsMover_cache == null || attack_cache==null)
        {
            Debug.LogError("Physics Mover component is missing");
            enabled = false;
        }
    }

    private void OnEnable()
    {
        moveAction.Enable();
        moveAction.performed += OnMovePerformed;
        moveAction.canceled += OnMoveCanceled;
        jumpAction.Enable();
        jumpAction.started += OnJumpStarted;
        jumpAction.canceled += OnJumpCanceled;
        attackAction.Enable();
        attackAction.started += OnAttack;
    }

    private void OnDisable()
    {
        moveAction.performed -= OnMovePerformed;
        moveAction.canceled -= OnMoveCanceled;
        moveAction.Disable();
        jumpAction.started -= OnJumpStarted;
        jumpAction.canceled -= OnJumpCanceled;
        jumpAction.Disable();
        attackAction.started -= OnAttack;
    }

    private void Update()
    {
        //physicsMover_cache.Move(moveInput * moveSpeed);
    }

    private void OnMovePerformed(InputAction.CallbackContext ctx)
    {
        moveInput = ctx.ReadValue<Vector2>().x * moveSpeed;
        physicsMover_cache.Move(moveInput);
    }

    private void OnMoveCanceled(InputAction.CallbackContext ctx)
    {
        physicsMover_cache.StopMove();
    }

    private void OnJumpStarted(InputAction.CallbackContext ctx)
    {
        
        if(physicsMover_cache.IsAir)return;
        isJumping = true;
        physicsMover_cache.StartJump(jumpPower);
        Debug.Log("Jump started");
    }

    private void OnJumpCanceled(InputAction.CallbackContext ctx)
    {
        if(!isJumping)return;
        isJumping = false;
        physicsMover_cache.StopJump();
        Debug.Log("Jump canceled");
    }

    private void OnAttack(InputAction.CallbackContext ctx)
    {
        attack_cache.StartAttack();
    }
}
