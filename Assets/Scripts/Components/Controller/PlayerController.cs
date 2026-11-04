using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;
using UnityEngine.EventSystems;
using Components.Movement;
using Components.Combat.Attack;
using Components.Animation;
using Components.Identity;
using Core.Constants;
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

        //シェーダーのプロパティ
        private static readonly int FlashAmountId = Shader.PropertyToID("_FlashAmount");

        //SerializeField
        [SerializeField] private float jumpPower = 1.0f;
        [SerializeField] private float moveSpeed = 1.0f;
        [SerializeField] private int playerNumber = 1; //何Pか。1Pがカメラの追従対象になる
        [SerializeField] private float chargeMaxTime = 2.0f;
        [SerializeField] private float chargeMaxPower = 10.0f;
        [SerializeField] private float chargeMinPower = 5.0f;
        [SerializeField] private float chargeMaxFlashDuration = 0.2f; //チャージが満タンになったときに白く光る時間
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
        private SpriteRenderer _spriteRenderer_Cache;
        private MaterialPropertyBlock _spritePropertyBlock_Cache;

        //State
        private bool _isInputEnabled = false;
        private bool _isJumping = false;
        private bool _blockingMove = false;
        private bool _blockingAttack = false;
        private float _moveInput = 0f;
        private bool _isCharging = false;
        private float _chargeStartTime;
        private bool _isChargeMaxSoundPlayed = false; //今回のチャージで満タンの音を鳴らしたか？
        private Vector2 _spawnPosition; //リスポーン先(配置された位置)
        
        public Vector2 Position => _physicsMover_Cache.Position;
        public Vector2 Velocity => _physicsMover_Cache.Velocity;
        public int PlayerNumber => playerNumber;
        //右を向いているか？
        //絵は右向きなので、ワールド上のスケールが正なら右向き
        //ルートが反転されていても正しく判定できるよう、localScale ではなく lossyScale で見る
        public bool IsFacingRight => sprite.transform.lossyScale.x > 0;
        //チャージの溜まり具合(0~1)
        private float ChargeRatio => Mathf.Clamp01((Time.time - _chargeStartTime) / chargeMaxTime);
        //チャージが満タンになった瞬間に1になり、線形に0へ戻る値。満タンになる前は0
        private float ChargeMaxFlashAmount
        {
            get
            {
                var timeAfterMax = Time.time - _chargeStartTime - chargeMaxTime;
                if (timeAfterMax < 0.0f) return 0.0f;
                return 1.0f - Mathf.Clamp01(timeAfterMax / chargeMaxFlashDuration);
            }
        }

        void Awake()
        {
            if (sprite == null)
                throw new MissingReferenceException($"[{GetType().Name}] sprite が {gameObject.name} に設定されていません");

            if (!sprite.TryGetComponent(out _spriteRenderer_Cache))
                throw new MissingComponentException($"[{GetType().Name}] SpriteRenderer が {sprite.name} に見つかりません");

            _spritePropertyBlock_Cache = new MaterialPropertyBlock();

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
            var gameUtility = GetGameUtility();
            if (gameUtility != null)
            {
                gameUtility.RegisterPlayer(this);
            }
            
            //自身の設定
            EntityRoot.Require(this).SetIdentity(ObjectIdentity.Player);
        }

        private void OnDestroy()
        {
            //ステージの終了時は StageManager が先に破棄されていることがある
            var gameUtility = GetGameUtility();
            if (gameUtility != null)
            {
                gameUtility.UnregisterPlayer(this);
            }
        }

        /// <summary>
        /// 今のステージの GameUtility を取得する。ステージ外では null
        /// </summary>
        private static GameUtility GetGameUtility()
        {
            return StageManager.Current != null ? StageManager.Current.GameUtility : null;
        }

        private void OnEnable()
        {
            //編集中は入力を受け取らない
            //モードを切り替えるとレベルごと作り直されるため、生成時点のモードだけを見ればよい
            if (LevelManager.IsEditMode) return;

            _moveAction.performed += OnMovePerformed;
            _moveAction.canceled += OnMoveCanceled;
            _jumpAction.started += RequestSpringForce;
            _jumpAction.started += OnJumpStarted;
            _jumpAction.canceled += OnJumpCanceled;
            _attackAction.started += OnCharge;
            _attackAction.canceled += OnAttack;
            EnableInput();
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
            _isInputEnabled = false;
        }

        private void EnableInput()
        {
            _moveAction.Enable();
            _jumpAction.Enable();
            _attackAction.Enable();
            _isInputEnabled = true;
        }

        /// <summary>
        /// プレイヤーへの入力を全て切る
        /// </summary>
        private void DisableInput()
        {
            //入力を切ると押しっぱなしの操作に canceled が発火するので、先に状態を片付ける
            //チャージ中なら、攻撃を出さずに取りやめる
            if (_isCharging)
            {
                _isCharging = false;
                _attackExecutor_Cache.CancelCharge();
            }
            _blockingMove = false;
            //ジャンプの上昇は打ち切らず、最後まで飛ばす
            _isJumping = false;

            _inputActions.Disable();
            _isInputEnabled = false;
            CancelMove();
        }

        /// <summary>
        /// ステージの状態に合わせて入力を切り替える
        /// クリア後は入力を切り、やり直したら戻す
        /// </summary>
        private void UpdateInputEnabled()
        {
            //編集中は OnEnable の時点で入力を有効にしていない
            if (LevelManager.IsEditMode) return;

            //ステージ外では StageManager が無いので、入力を切らない
            var canReceiveInput = StageManager.Current == null || StageManager.Current.IsPlaying;
            if (canReceiveInput == _isInputEnabled) return;

            if (canReceiveInput)
            {
                EnableInput();
            }
            else
            {
                DisableInput();
            }
        }

        private void Update()
        {
            UpdateInputEnabled();

            //移動アニメーション(AnimController)
            _animatorTrigger_Cache.SetSpeed(Mathf.Abs(_physicsMover_Cache.Velocity.x));
            float fallSpeed = -_physicsMover_Cache.Velocity.y;
            _animatorTrigger_Cache.SetFallSpeed(fallSpeed);
            _animatorTrigger_Cache.SetIsAir(_physicsMover_Cache.IsAir);
            
            //UI上にマウスがある場合、攻撃できないようにする
            //レベル作成システムの GUI(IMGUI)は EventSystem で判定できないため別に見る
            var isOverUi = EventSystem.current && EventSystem.current.IsPointerOverGameObject();
            _blockingAttack = isOverUi || LevelEditor.IsPointerOverGui();
            
            //UI更新
            if (_isCharging)
            {
                var charge = (int)(chargeMinPower + (chargeMaxPower - chargeMinPower) * ChargeRatio);
                _playerUIController_Cache.ShowUpdateDebugText(charge.ToString());

                //チャージが溜まるほどマゼンタに近づける
                _spriteRenderer_Cache.color = Color.Lerp(Color.white, Color.magenta, ChargeRatio);
                SetSpriteFlashAmount(ChargeMaxFlashAmount);

                //チャージが満タンになったら音を鳴らす
                if (!_isChargeMaxSoundPlayed && ChargeRatio >= 1.0f)
                {
                    _isChargeMaxSoundPlayed = true;
                    SoundManager.Instance.PlaySe("ChargeMax");
                }
            }
            else
            {
                _playerUIController_Cache.ShowUpdateDebugText("");
                _spriteRenderer_Cache.color = Color.white;
                SetSpriteFlashAmount(0.0f);
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
            SoundManager.Instance.PlaySe("Jump");
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
            //Stand を通らずに空中へ移ると Jump トリガーが消費されずに残る
            //残したまま着地すると、地上で空中モーションに入って戻れなくなるので消しておく
            _animatorTrigger_Cache.ResetJump();

            //接地状態遷移（AnimController）
            _animatorTrigger_Cache.TriggerGround();
            Debug.Log("Grounded");
        }

        private void OnFallOut()
        {
            //レベルが無い場合はその場でリスポーンする
            var gameUtility = GetGameUtility();
            if (gameUtility != null)
            {
                SoundManager.Instance.PlaySe("StageFailed");
                gameUtility.RestartLevel();
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
            _isChargeMaxSoundPlayed = false;
            _attackExecutor_Cache.StartCharge();
        }
        private void OnAttack(InputAction.CallbackContext ctx)
        {
            if(!_isCharging) return;
            _isCharging = false;
            
            //チャージ量の決定
            var charge = chargeMinPower + (chargeMaxPower - chargeMinPower) * ChargeRatio;

            //現状Attack1のみ
            //トリガもAttackExecutor側に任せる
            _attackExecutor_Cache.StartAttack(AttackType.Attack1, charge);
            
            //移動を許可
            CancelBlockingMove();
        }
        
        

        /// <summary>
        /// スプライトの白さを設定する
        /// SpriteRenderer の色は乗算で真っ白にできないため、シェーダー(Sprites/Flash)側で白くする
        /// </summary>
        /// <param name="amount">0で元の色、1で真っ白</param>
        private void SetSpriteFlashAmount(float amount)
        {
            _spriteRenderer_Cache.GetPropertyBlock(_spritePropertyBlock_Cache);
            _spritePropertyBlock_Cache.SetFloat(FlashAmountId, amount);
            _spriteRenderer_Cache.SetPropertyBlock(_spritePropertyBlock_Cache);
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
