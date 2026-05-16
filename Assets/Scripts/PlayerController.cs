using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;

public class PlayerController : MonoBehaviour
{
    //AnimController遷移用プロパティ名
    private static readonly int Speed = Animator.StringToHash("Speed");
    private static readonly int Jump = Animator.StringToHash("Jump");
    private static readonly int Ground = Animator.StringToHash("Ground");
    private static readonly int FallSpeed = Animator.StringToHash("FallSpeed");
    private static readonly int Attack1 = Animator.StringToHash("Attack1");
    private static readonly int Attack2 = Animator.StringToHash("Attack2");
    private static readonly int Attack3 = Animator.StringToHash("Attack3");

    //SerializeField
    [SerializeField] private float jumpPower = 1.0f;
    [SerializeField] private float moveSpeed = 1.0f;
    //AnimatorでSpriteを動かすGameObjectを期待する
    [SerializeField] private GameObject animSprite;
    
    //InputAction
    private InputSystem_Actions inputActions;
    private InputAction moveAction;
    private InputAction jumpAction;
    private InputAction attackAction;
    
    //キャッシュ
    private PhysicsMover physicsMover_Cache;
    private AttackExecutor attackExecutor_Cache;
    private Animator animator_Cache;

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
        animator_Cache = animSprite.GetComponent<Animator>();
        if (physicsMover_Cache == null)
        {
            Debug.LogError("PhysicsMover component not found");
        }

        if (attackExecutor_Cache == null)
        {
            Debug.LogError("Attack component not found");
        }

        if (animator_Cache == null)
        {
            Debug.LogError("Animator component not found");
        }
        
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
        animator_Cache.SetFloat(Speed, Mathf.Abs(physicsMover_Cache.Velocity.x));
        
        float fallSpeed= physicsMover_Cache.Velocity.y<0 ? -physicsMover_Cache.Velocity.y : 0;
        animator_Cache.SetFloat(FallSpeed,fallSpeed);
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
        animator_Cache.SetTrigger(Jump);
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
        animator_Cache.SetTrigger(Ground);
        Debug.Log("Grounded");
    }
    

    private void OnAttack(InputAction.CallbackContext ctx)
    {
        attackExecutor_Cache.StartAttack1();
        animator_Cache.SetTrigger(Attack1);
        //animator_cache.SetTrigger(Attack2);
        //animator_cache.SetTrigger(Attack3);
    }
}
