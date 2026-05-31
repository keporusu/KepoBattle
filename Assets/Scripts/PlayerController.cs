using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;

[RequireComponent(typeof(PhysicsMover))]
[RequireComponent(typeof(AttackExecutor))]
[RequireComponent(typeof(AnimatorTrigger))]
public class PlayerController : MonoBehaviour
{

    //SerializeField
    [SerializeField] private float jumpPower = 1.0f;
    [SerializeField] private float moveSpeed = 1.0f;
    
    //InputAction
    private InputSystem_Actions inputActions;
    private InputAction moveAction;
    private InputAction jumpAction;
    private InputAction attackAction;
    
    //キャッシュ
    private PhysicsMover physicsMover_Cache;
    private AttackExecutor attackExecutor_Cache;
    private AnimatorTrigger animatorTrigger_Cache;

    //State
    private bool isJumping=false;
    private float moveInput = 0f;

    void Awake()
    {
        inputActions = new InputSystem_Actions();
        moveAction = inputActions.Player.Move;
        jumpAction = inputActions.Player.Jump;
        attackAction = inputActions.Player.Attack;
    }


    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        physicsMover_Cache = GetComponent<PhysicsMover>();
        attackExecutor_Cache = GetComponent<AttackExecutor>();
        animatorTrigger_Cache = GetComponent<AnimatorTrigger>();

        //接地イベント登録
        physicsMover_Cache.OnGround+=OnGround;
    }

    private void OnEnable()
    {
        moveAction.Enable();
        moveAction.started += OnMoveStarted;
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
        moveAction.started -= OnMoveStarted;
        moveAction.performed -= OnMovePerformed;
        moveAction.canceled -= OnMoveCanceled;
        jumpAction.started -= OnJumpStarted;
        jumpAction.canceled -= OnJumpCanceled;
        attackAction.started -= OnAttack;
        inputActions.Disable();
    }

    private void Update()
    {
        //移動アニメーション(AnimController)
        animatorTrigger_Cache.SetSpeed(Mathf.Abs(physicsMover_Cache.Velocity.x));
        float fallSpeed= physicsMover_Cache.Velocity.y<0 ? -physicsMover_Cache.Velocity.y : 0;
        animatorTrigger_Cache.SetFallSpeed(fallSpeed);
    }
    
    private void OnMoveStarted(InputAction.CallbackContext ctx)
    {
        //移動時、入力の向きによって反転させる
        if (ctx.ReadValue<Vector2>().x > 0)
        {
            transform.localScale = new Vector3(1.0f, transform.localScale.y, transform.localScale.z);
        }
        else
        {
            transform.localScale = new Vector3(-1.0f, transform.localScale.y, transform.localScale.z);
        }
    }
    
    private void OnMovePerformed(InputAction.CallbackContext ctx)
    {
        moveInput = ctx.ReadValue<Vector2>().x * moveSpeed;
        physicsMover_Cache.Move(moveInput);
    }

    private void OnMoveCanceled(InputAction.CallbackContext ctx)
    {
        physicsMover_Cache.StopMove();
    }

    private void OnJumpStarted(InputAction.CallbackContext ctx)
    {
        
        if(physicsMover_Cache.IsAir)return;
        isJumping = true;
        physicsMover_Cache.StartJump(jumpPower);
        //ジャンプ状態遷移（AnimController）
        animatorTrigger_Cache.TriggerJump();
        Debug.Log("Jump started");
    }

    private void OnJumpCanceled(InputAction.CallbackContext ctx)
    {
        if(!isJumping)return;
        isJumping = false;
        physicsMover_Cache.StopJump();
        Debug.Log("Jump canceled");
    }

    private void OnGround()
    {
        //接地状態遷移（AnimController）
        animatorTrigger_Cache.TriggerGround();
        Debug.Log("Grounded");
    }
    

    private void OnAttack(InputAction.CallbackContext ctx)
    {
        attackExecutor_Cache.StartAttack1();
        animatorTrigger_Cache.TriggerAttack1();
        //animatorTrigger_Cache.TriggerAttack2();
        //animatorTrigger_Cache.TriggerAttack3();
    }
}
