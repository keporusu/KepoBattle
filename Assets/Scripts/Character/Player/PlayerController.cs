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
        private InputSystem_Actions _inputActions;
        private InputAction _moveAction;
        private InputAction _jumpAction;
        private InputAction _attackAction;

        //キャッシュ
        private CharacterPhysicsMover _physicsMover_Cache;
        private AttackExecutor _attackExecutor_Cache;
        private AnimatorTrigger _animatorTrigger_Cache;
        private CameraController _cameraController_Cache;

        //State
        private bool _isJumping = false;
        private bool _blockingMove = false;
        private float _moveInput = 0f;


        void Awake()
        {
            _inputActions = new InputSystem_Actions();
            _moveAction = _inputActions.Player.Move;
            _jumpAction = _inputActions.Player.Jump;
            _attackAction = _inputActions.Player.Attack;
        }


        // Start is called once before the first execution of Update after the MonoBehaviour is created
        void Start()
        {
            _physicsMover_Cache = GetComponent<CharacterPhysicsMover>();
            _attackExecutor_Cache = GetComponent<AttackExecutor>();
            _animatorTrigger_Cache = GetComponent<AnimatorTrigger>();
            _cameraController_Cache = GetComponent<CameraController>();

            //接地イベント登録
            _physicsMover_Cache.OnGround += OnGround;
            _physicsMover_Cache.OnForceAir += OnForceAir;
            _attackExecutor_Cache.OnAttackFinish += CancelBlockingMove;
        }

        private void OnEnable()
        {
            _moveAction.Enable();
            _moveAction.performed += OnMovePerformed;
            _moveAction.canceled += OnMoveCanceled;
            _jumpAction.Enable();
            _jumpAction.started += OnJumpStarted;
            _jumpAction.canceled += OnJumpCanceled;
            _attackAction.Enable();
            _attackAction.started += OnAttack;
        }

        private void OnDisable()
        {
            _moveAction.performed -= OnMovePerformed;
            _moveAction.canceled -= OnMoveCanceled;
            _jumpAction.started -= OnJumpStarted;
            _jumpAction.canceled -= OnJumpCanceled;
            _attackAction.started -= OnAttack;
            _inputActions.Disable();
        }

        private void Update()
        {
            //移動アニメーション(AnimController)
            _animatorTrigger_Cache.SetSpeed(Mathf.Abs(_physicsMover_Cache.Velocity.x));
            float fallSpeed = -_physicsMover_Cache.Velocity.y;
            _animatorTrigger_Cache.SetFallSpeed(fallSpeed);
            //カメラ操作
            _cameraController_Cache.SetCameraPosition(transform.position);
        }

        private void OnMovePerformed(InputAction.CallbackContext ctx)
        {
            if (_blockingMove) return;
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

            _moveInput = moveX * moveSpeed;
            _physicsMover_Cache.Move(_moveInput);
        }

        private void OnMoveCanceled(InputAction.CallbackContext ctx)
        {
            CancelMove();
        }

        private void CancelMove()
        {
            _physicsMover_Cache.StopMove();
        }

        private void OnJumpStarted(InputAction.CallbackContext ctx)
        {
            StartJump();
        }

        private void StartJump()
        {
            if (_blockingMove) return;
            if (_physicsMover_Cache.IsAir) return;
            _isJumping = true;
            _physicsMover_Cache.StartJump(jumpPower);
            //ジャンプ状態遷移（AnimController）
            _animatorTrigger_Cache.TriggerJump();
            Debug.Log("Jump started");
        }

        private void OnJumpCanceled(InputAction.CallbackContext ctx)
        {
            if (!_isJumping) return;
            _isJumping = false;
            _physicsMover_Cache.StopJump();
            Debug.Log("Jump canceled");
        }

        private void OnGround()
        {
            //接地状態遷移（AnimController）
            _animatorTrigger_Cache.TriggerGround();
            Debug.Log("Grounded");
        }

        private void OnForceAir()
        {
            _animatorTrigger_Cache.TriggerAir();
        }

        private void OnAttack(InputAction.CallbackContext ctx)
        {
            if (_blockingMove) return;
            _blockingMove = true;

            //移動処理をキャンセルする
            CancelMove();

            _attackExecutor_Cache.StartAttack1();
            _animatorTrigger_Cache.TriggerAttack1();
            //animatorTrigger_Cache.TriggerAttack2();
            //animatorTrigger_Cache.TriggerAttack3();
        }

        private void CancelBlockingMove()
        {
            _blockingMove = false;

            //移動ボタンを押しっぱなしだった場合のため必要
            float currentMoveX = _moveAction.ReadValue<Vector2>().x;
            Move(currentMoveX);
        }
    }
}
