using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;
using UnityEngine.EventSystems;
using Components.Movement;
using Components.Combat.Attack;
using Components.Animation;
using Components.Camera;

namespace Components.Controller
{
    public class PlayerController : MonoBehaviour
    {

        //SerializeField
        [SerializeField] private float jumpPower = 1.0f;
        [SerializeField] private float moveSpeed = 1.0f;
        [SerializeField] private Vector2 initialPosition = new Vector2(2.5f, 3.0f);
        [SerializeField] private GameObject sprite;

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
        private bool _blockingAttack = false;
        private float _moveInput = 0f;
        private bool _isCharging = false;
        
        public Vector2 Position => _physicsMover_Cache.Position;
        public Vector2 Velocity => _physicsMover_Cache.Velocity;
        //正方向を向いているか？
        public bool IsForward => transform.localScale.x > 0;
            
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
            if (!TryGetComponent(out _physicsMover_Cache))
                throw new MissingComponentException($"[{GetType().Name}] CharacterPhysicsMover が {gameObject.name} に見つかりません");

            if (!TryGetComponent(out _attackExecutor_Cache))
                throw new MissingComponentException($"[{GetType().Name}] AttackExecutor が {gameObject.name} に見つかりません");

            if (!TryGetComponent(out _animatorTrigger_Cache))
                throw new MissingComponentException($"[{GetType().Name}] AnimatorTrigger が {gameObject.name} に見つかりません");

            if (!TryGetComponent(out _cameraController_Cache))
                throw new MissingComponentException($"[{GetType().Name}] CameraController が {gameObject.name} に見つかりません");

            //接地イベント登録
            _physicsMover_Cache.OnGround += OnGround;
            _physicsMover_Cache.OnForceAir += OnForceAir;
            _attackExecutor_Cache.OnAttackFinish += CancelBlockingMove;
            
            //TODO: ここも初期化できるようにしたい（初期化の順番を考えないといけない）
            //_physicsMover_Cache.ResetAll(initialPosition);
            
            //最初のスプライトの向きによって最初の向きを決める
            if (sprite.transform.localScale.x < 0)
            {
                _physicsMover_Cache.SetRight(true);
            }
            else
            {
                _physicsMover_Cache.SetRight(false);
            }
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
            _attackAction.started += OnCharge;
            _attackAction.canceled += OnAttack;
        }

        private void OnDisable()
        {
            _moveAction.performed -= OnMovePerformed;
            _moveAction.canceled -= OnMoveCanceled;
            _jumpAction.started -= OnJumpStarted;
            _jumpAction.canceled -= OnJumpCanceled;
            _attackAction.started -= OnCharge;
            _attackAction.canceled -= OnAttack;
            _inputActions.Disable();
        }

        private void Update()
        {
            //移動アニメーション(AnimController)
            _animatorTrigger_Cache.SetSpeed(Mathf.Abs(_physicsMover_Cache.Velocity.x));
            float fallSpeed = -_physicsMover_Cache.Velocity.y;
            _animatorTrigger_Cache.SetFallSpeed(fallSpeed);
            //カメラ操作
            _cameraController_Cache.AdjustCameraPosition(transform.position);
            
            //UI上にマウスがある場合、攻撃できないようにする
            if (EventSystem.current)
            {
                if (EventSystem.current.IsPointerOverGameObject())
                {
                    _blockingAttack = true;
                }
                else
                {
                    _blockingAttack = false;
                }
            }
        }
        
        
        
        //移動
        private void OnMovePerformed(InputAction.CallbackContext ctx)
        {
            if (_blockingMove) return;
            Move(ctx.ReadValue<Vector2>().x);
        }

        private void Move(float moveX)
        {
            //移動時、入力の向きによって反転させる
            //TODO: キャラクターのスプライトを反転させるでいい。わざわざ全体を反転させないほうが良い
            if (moveX > 0)
            {
                //transform.localScale = new Vector3(1.0f, transform.localScale.y, transform.localScale.z);
                var scale = sprite.transform.localScale;
                sprite.transform.localScale = new Vector3(-Mathf.Abs(scale.x), scale.y, scale.z);
                _physicsMover_Cache.SetRight(true);
            }
            else if (moveX < 0)
            {
                //transform.localScale = new Vector3(-1.0f, transform.localScale.y, transform.localScale.z);
                var scale = sprite.transform.localScale;
                sprite.transform.localScale = new Vector3(Mathf.Abs(scale.x), scale.y, scale.z);
                _physicsMover_Cache.SetRight(false);
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
        
        
        
        //ジャンプ
        private void OnJumpStarted(InputAction.CallbackContext ctx)
        {
            if (_blockingMove) return;
            if (_physicsMover_Cache.IsAir) return;
            Jump();
        }

        private void Jump()
        {
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
        
        
        //接地/空中
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
        
        
        //攻撃
        private void OnCharge(InputAction.CallbackContext ctx)
        {
            if (_blockingAttack) return;
            
            if (_blockingMove) return;
            _blockingMove = true;
            
            //移動処理をキャンセルする
            CancelMove();
            
            _isCharging = true;
            _attackExecutor_Cache.StartCharge();
        }
        private void OnAttack(InputAction.CallbackContext ctx)
        {
            if(!_isCharging) return;
            
            //現状Attack1のみ
            //トリガもAttackExecutor側に任せる
            _attackExecutor_Cache.StartAttack(AttackType.Attack1);
        }
        
        

        private void CancelBlockingMove()
        {
            _blockingMove = false;

            //移動ボタンを押しっぱなしだった場合のため必要
            float currentMoveX = _moveAction.ReadValue<Vector2>().x;
            Move(currentMoveX);
        }

        public void Respawn()
        {
            _physicsMover_Cache.ResetAll(initialPosition);
            _animatorTrigger_Cache.TriggerJump();
        }

    }
}
