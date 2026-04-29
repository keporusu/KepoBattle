using UnityEngine;
using UnityEngine.InputSystem;

public class PlayeController : MonoBehaviour
{
    [SerializeField] private float jumpPower = 1.0f;
    private InputAction moveAction;
    private InputAction jumpAction;
    private PhysicsMover physicsMover;
    
    //State
    private bool isJumping=false;

    void Awake()
    {
        var inputActions = new InputSystem_Actions();
        moveAction = inputActions.Player.Move;
        jumpAction = inputActions.Player.Jump;
    }


    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        physicsMover = GetComponent<PhysicsMover>();
        if (physicsMover == null)
        {
            Debug.LogError("Physics Mover component is missing");
            enabled = false;
        }
    }

    private void OnEnable()
    {
        jumpAction.Enable();
        jumpAction.started += OnJumpStarted;
        jumpAction.canceled += OnJumpCanceled;
    }

    private void OnDisable()
    {
        jumpAction.started -= OnJumpStarted;
        jumpAction.canceled -= OnJumpCanceled;
        jumpAction.Disable();
    }

    private void FixedUpdate()
    {
        
    }

    private void OnJumpStarted(InputAction.CallbackContext ctx)
    {
        
        if(physicsMover.IsAir)return;
        isJumping = true;
        physicsMover.StartJump(jumpPower);
        Debug.Log("Jump started");
    }

    private void OnJumpCanceled(InputAction.CallbackContext ctx)
    {
        if(!isJumping)return;
        isJumping = false;
        physicsMover.StopJump();
        Debug.Log("Jump canceled");
    }
}
