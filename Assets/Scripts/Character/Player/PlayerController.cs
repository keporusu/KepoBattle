using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;
using Character;

namespace Character.Player
{
    [RequireComponent(typeof(CharacterPhysicsMover))]
    [RequireComponent(typeof(AttackExecutor))]
    [RequireComponent(typeof(AnimatorTrigger))]
    [RequireComponent(typeof(CameraController))]
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
        private CharacterPhysicsMover physicsMover_Cache;
        private AttackExecutor attackExecutor_Cache;
        private AnimatorTrigger animatorTrigger_Cache;
        private CameraController cameraController_Cache;

        //State
        private bool isJumping = false;
        private bool blockingMove = false;
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
            physicsMover_Cache = GetComponent<CharacterPhysicsMover>();
            attackExecutor_Cache = GetComponent<AttackExecutor>();
            animatorTrigger_Cache = GetComponent<AnimatorTrigger>();
            cameraController_Cache = GetComponent<CameraController>();

            //接地イベント登録
            physicsMover_Cache.OnGround += OnGround;
            attackExecutor_Cache.OnAttackFinish += CancelBlockingMove;
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
            jumpAction.started -= OnJumpStarted;
            jumpAction.canceled -= OnJumpCanceled;
            attackAction.started -= OnAttack;
            inputActions.Disable();
        }

        private void Update()
        {
            //移動アニメーション(AnimController)
            animatorTrigger_Cache.SetSpeed(Mathf.Abs(physicsMover_Cache.Velocity.x));
            float fallSpeed = physicsMover_Cache.Velocity.y < 0 ? -physicsMover_Cache.Velocity.y : 0;
            animatorTrigger_Cache.SetFallSpeed(fallSpeed);
            //カメラ操作
            cameraController_Cache.SetCameraPosition(transform.position);
        }

        private void OnMovePerformed(InputAction.CallbackContext ctx)
        {
            if (blockingMove) return;
            Move(ctx.ReadValue<Vector2>().x);
        }

        private void Move(float moveX)
        {
            //移動時、入力の向きによって反転させる
            if (moveX > 0)
            {
                transform.localScale = new Vector3(1.0f, transform.localScale.y, transform.localScale.z);
            }
            else if (moveX < 0)
            {
                transform.localScale = new Vector3(-1.0f, transform.localScale.y, transform.localScale.z);
            }

            moveInput = moveX * moveSpeed;
            physicsMover_Cache.Move(moveInput);
        }

        private void OnMoveCanceled(InputAction.CallbackContext ctx)
        {
            CancelMove();
        }

        private void CancelMove()
        {
            physicsMover_Cache.StopMove();
        }

        private void OnJumpStarted(InputAction.CallbackContext ctx)
        {
            StartJump();
        }

        private void StartJump()
        {
            if (blockingMove) return;
            if (physicsMover_Cache.IsAir) return;
            isJumping = true;
            physicsMover_Cache.StartJump(jumpPower);
            //ジャンプ状態遷移（AnimController）
            animatorTrigger_Cache.TriggerJump();
            Debug.Log("Jump started");
        }

        private void OnJumpCanceled(InputAction.CallbackContext ctx)
        {
            if (!isJumping) return;
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
            if (blockingMove) return;
            blockingMove = true;

            //移動処理をキャンセルする
            CancelMove();

            attackExecutor_Cache.StartAttack1();
            animatorTrigger_Cache.TriggerAttack1();
            //animatorTrigger_Cache.TriggerAttack2();
            //animatorTrigger_Cache.TriggerAttack3();
        }

        private void CancelBlockingMove()
        {
            blockingMove = false;

            //移動ボタンを押しっぱなしだった場合のため必要
            float currentMoveX = moveAction.ReadValue<Vector2>().x;
            Move(currentMoveX);
        }
    }
}
