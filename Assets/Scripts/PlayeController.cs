using UnityEngine;
using UnityEngine.InputSystem;

public class PlayeController : MonoBehaviour
{
    [SerializeField] private float jumpPower = 1.0f;
    [SerializeField] private float moveSpeed = 1.0f;
    private InputAction moveAction;
    private InputAction jumpAction;
    private PhysicsMover physicsMover_cache;

    //State
    private bool isJumping=false;
    private float moveInput = 0f;

    void Awake()
    {
        var inputActions = new InputSystem_Actions();
        moveAction = inputActions.Player.Move;
        jumpAction = inputActions.Player.Jump;
    }


    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        physicsMover_cache = GetComponent<PhysicsMover>();
        if (physicsMover_cache == null)
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
    }

    private void OnDisable()
    {
        moveAction.performed -= OnMovePerformed;
        moveAction.canceled -= OnMoveCanceled;
        moveAction.Disable();
        jumpAction.started -= OnJumpStarted;
        jumpAction.canceled -= OnJumpCanceled;
        jumpAction.Disable();
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
}
