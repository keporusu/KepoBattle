using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;
using UnityEngine.EventSystems;
using Components.Movement;
using Components.Combat.Attack;
using Components.Animation;
using Core.Contracts;
using Cysharp.Threading.Tasks;
using Data;
using Systems;

namespace Components.Controller
{
    public class PlayerController : MonoBehaviour, ILevelObject
    {
        //レベルデータのキー
        private const string FacingRightKey = "facingRight";
        private const string PlayerNumberKey = "playerNumber";

        //SerializeField
        [SerializeField] private float jumpPower = 1.0f;
        [SerializeField] private float moveSpeed = 1.0f;
        [SerializeField] private int playerNumber = 1; //何Pか。1Pがカメラの追従対象になる
        [SerializeField] private float chargeMaxTime = 2.0f;
        [SerializeField] private float chargeMaxPower = 10.0f;
        [SerializeField] private float chargeMinPower = 5.0f;
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
        private PlayerUIController _playerUIController_Cache;

        //State
        private bool _isJumping = false;
        private bool _blockingMove = false;
        private bool _blockingAttack = false;
        private float _moveInput = 0f;
        private bool _isCharging = false;
        private float _chargeStartTime;
        private Vector2 _spawnPosition; //リスポーン先(配置された位置)
        
        public Vector2 Position => _physicsMover_Cache.Position;
        public Vector2 Velocity => _physicsMover_Cache.Velocity;
        public int PlayerNumber => playerNumber;
        //右を向いているか？
        //絵は右向きなので、ワールド上のスケールが正なら右向き
        //ルートが反転されていても正しく判定できるよう、localScale ではなく lossyScale で見る
        public bool IsFacingRight => sprite.transform.lossyScale.x > 0;
            
        void Awake()
        {
            if (sprite == null)
                throw new MissingReferenceException($"[{GetType().Name}] sprite が {gameObject.name} に設定されていません");

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

            if(!TryGetComponent(out _playerUIController_Cache))
                throw new MissingComponentException($"[{GetType().Name}] PlayerUIController が {gameObject.name} に見つかりません");
            
            //接地イベント登録
            _physicsMover_Cache.OnGround += OnGround;
            _physicsMover_Cache.OnForceAir += OnForceAir;
            //_attackExecutor_Cache.OnAttackFinish += CancelBlockingMove;

            //落下時はレベルをやり直す
            _physicsMover_Cache.OnFallOut += OnFallOut;

            //配置された位置をリスポーン先にする
            _spawnPosition = _physicsMover_Cache.Position;
            
            //最初のスプライトの向きによって最初の向きを決める
            _physicsMover_Cache.SetRight(IsFacingRight);

            //カメラの追従・デバッグ用の生成位置の基準として登録する
            if (GameUtility.Instance != null)
            {
                GameUtility.Instance.RegisterPlayer(this);
            }
        }

        private void OnDestroy()
        {
            if (GameUtility.Instance != null)
            {
                GameUtility.Instance.UnregisterPlayer(this);
            }
        }

        private void OnEnable()
        {
            _moveAction.Enable();
            _moveAction.performed += OnMovePerformed;
            _moveAction.canceled += OnMoveCanceled;
            _jumpAction.Enable();
            _jumpAction.started += RequestSpringForce;
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
            _jumpAction.started -= RequestSpringForce;
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
            _animatorTrigger_Cache.SetIsAir(_physicsMover_Cache.IsAir);
            
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
            
            //UI更新
            if (_isCharging)
            {
                var charge = (int)(chargeMinPower + (chargeMaxPower - chargeMinPower) *
                    Mathf.Clamp01((Time.time - _chargeStartTime) / chargeMaxTime));
                _playerUIController_Cache.ShowUpdateDebugText(charge.ToString());
            }
            else
            {
                _playerUIController_Cache.ShowUpdateDebugText("");
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
            if (moveX > 0)
            {
                SetFacingRight(true);
            }
            else if (moveX < 0)
            {
                SetFacingRight(false);
            }

            _moveInput = moveX * moveSpeed;
            _physicsMover_Cache.Move(_moveInput);
        }

        /// <summary>
        /// 向きを設定する
        /// スプライトのみを反転させ、ルートの Transform は反転させない
        /// </summary>
        /// <param name="right">右向きにするか？</param>
        public void SetFacingRight(bool right)
        {
            //親(ルート)が反転されていても、ワールド上で狙った向きになるよう親の符号を掛ける
            var spriteTransform = sprite.transform;
            var parentSign = spriteTransform.parent != null ? Mathf.Sign(spriteTransform.parent.lossyScale.x) : 1.0f;
            var worldSign = right ? 1.0f : -1.0f;
            var scale = spriteTransform.localScale;
            spriteTransform.localScale = new Vector3(Mathf.Abs(scale.x) * worldSign * parentSign, scale.y, scale.z);

            //Start 前(レベル読み込み直後)はまだキャッシュが無い
            //その場合は Start でスプライトの向きから反映される
            if (_physicsMover_Cache != null)
            {
                _physicsMover_Cache.SetRight(right);
            }
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
            Jump(jumpPower);
        }

        private void Jump(float power)
        {
            //ジャンプ処理
            _isJumping = true;
            _physicsMover_Cache.StartJump(power);
            
            //ジャンプ状態遷移（AnimController）
            _animatorTrigger_Cache.TriggerJump();
            Debug.Log("Jump started");
            
        }

        private async void RequestSpringForce(InputAction.CallbackContext ctx)
        {
            //バネで飛ぶ時の追加の力
            _physicsMover_Cache.RequestExtraSpringForceVelocity(true);
            await UniTask.Delay(TimeSpan.FromSeconds(0.2f));
            _physicsMover_Cache.RequestExtraSpringForceVelocity(false);
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

        private void OnFallOut()
        {
            //レベルが無い場合はその場でリスポーンする
            if (GameUtility.Instance != null)
            {
                GameUtility.Instance.RestartLevel();
            }
            else
            {
                Respawn();
            }
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
            _chargeStartTime = Time.time;
            _attackExecutor_Cache.StartCharge();
        }
        private void OnAttack(InputAction.CallbackContext ctx)
        {
            if(!_isCharging) return;
            _isCharging = false;
            
            //チャージ量の決定
            var charge = chargeMinPower + (chargeMaxPower - chargeMinPower) *
                Mathf.Clamp01((Time.time - _chargeStartTime) / chargeMaxTime);
            
            //現状Attack1のみ
            //トリガもAttackExecutor側に任せる
            _attackExecutor_Cache.StartAttack(AttackType.Attack1, charge);
            
            //移動を許可
            CancelBlockingMove();
        }
        
        

        private void CancelBlockingMove()
        {
            _blockingMove = false;

            //移動ボタンを押しっぱなしだった場合のため必要
            float currentMoveX = _moveAction.ReadValue<Vector2>().x;
            Move(currentMoveX);
        }
        
        
        /// <summary>
        /// 外部からの強制ジャンプリクエスト
        /// </summary>
        /// <param name="power">ジャンプ力</param>
        public void RequestJump(float power)
        {
            Jump(power);
        }
        
        public void Respawn()
        {
            _physicsMover_Cache.ResetAll(_spawnPosition);
            _animatorTrigger_Cache.TriggerJump();
        }

        public void ApplyLevelParameters(LevelObjectParameters parameters)
        {
            if (parameters.TryGetBool(FacingRightKey, out var facingRight))
            {
                SetFacingRight(facingRight);
            }

            if (parameters.TryGetInt(PlayerNumberKey, out var number))
            {
                playerNumber = number;
            }
        }

        public void ExportLevelParameters(LevelObjectParameters parameters)
        {
            parameters.SetBool(FacingRightKey, IsFacingRight);
            parameters.SetInt(PlayerNumberKey, playerNumber);
        }

    }
}
