using UnityEngine;
using System;

namespace Core.Movement
{
    public readonly struct MovementSettings
    {
        public readonly float Gravity;
        public readonly float Weight;     // コンストラクタで 0.001f 以上にクランプ
        public readonly float Friction;
        public readonly float PushSpeed;  // 現状 1.5f 直書き（L166, L170）

        public MovementSettings(float gravity, float weight, float friction, float pushSpeed)
        {
            Gravity = gravity;
            Weight = Math.Max(0.001f, weight);
            Friction = friction;
            PushSpeed = pushSpeed;
        }
    }

    public readonly struct MoveStep
    {
        public readonly Vector2 NextPosition;
        public readonly Vector2 Velocity;

        public MoveStep(Vector2 nextPosition, Vector2 velocity)
        {
            NextPosition = nextPosition;
            Velocity = velocity;
        }
    }

    public sealed class MovementSolver
    {
        private readonly MovementSettings _settings;

        public MovementSolver(MovementSettings settings)
        {
            _settings = settings;
            Reset();
        }

        public bool IsAir => _isAir;
        public Vector2 Velocity { get; private set; }

        private bool _isBraking;
        private bool _isAir;
        private float _movingVelocity;
        private Vector2 _forceVelocity;

        //スナップ待ちが無い状態を NaN で表す
        private float _snapGroundY;


        public event Action OnGround;
        public event Action OnForceAir;

        /// <summary>
        /// 移動値をセットする
        /// この移動値はStepで使われる
        /// </summary>
        /// <param name="velocity">速度</param>
        public void InputMove(float velocity)
        {
            _isBraking = false;
            _movingVelocity = velocity;
        }
        
        /// <summary>
        /// ブレーキを設定する
        /// </summary>
        public void CutMove()
        {
            _isBraking = true;
        }
        
        /// <summary>
        /// 速度を直接0に設定する
        /// </summary>
        /// <param name="cutX">x方向を切るか？</param>
        /// <param name="cutY">y方向を切るか？</param>
        public void CutVelocity(bool cutX = true, bool cutY = true)
        {
            if (cutX)
            {
                _forceVelocity.x = 0.0f;
            }

            if (cutY)
            {
                _forceVelocity.y = 0.0f;
            }
        }
        
        /// <summary>
        /// 自分に特定の方向に速度を加える
        /// </summary>
        /// <param name="velocity">加える速度</param>
        /// <param name="forceMode">一回停止させてから力を加えるか？</param>
        public void AddForceVelocity(Vector2 velocity, bool forceMode)
        {
            if (forceMode)
            {
                _movingVelocity = 0.0f;
                _forceVelocity = new Vector2();
            }

            _forceVelocity += velocity;
            if (velocity.y > 0.0f)
            {
                _isAir = true;
            }
        }
        
        /// <summary>
        /// 全ての状態を初期状態に戻す
        /// 位置は Rigidbody 側の管理なのでここでは扱わない
        /// </summary>
        public void Reset()
        {
            _movingVelocity = 0.0f;
            _forceVelocity = Vector2.zero;

            _isBraking = false;
            _isAir = true;
            _snapGroundY = float.NaN;

            Velocity = Vector2.zero;
        }


        // 1フレーム分の変位計算（純粋・副作用は内部状態の更新のみ）
        public MoveStep Step(float deltaTime, Vector2 position, float? pushTargetX)
        {
            //****移動****
            Vector2 movePoint = position;

            //ブレーキ処理
            if (_isBraking)
            {
                if (_movingVelocity > 0.0f)
                {
                    _movingVelocity = Mathf.Max(0.0f,_movingVelocity-_settings.Friction*deltaTime);
                }
                else if (_movingVelocity < 0.0f)
                {
                    _movingVelocity = Mathf.Min(0.0f,_movingVelocity+_settings.Friction*deltaTime);
                }

                if (_movingVelocity == 0.0f)
                {
                    _isBraking = false;
                }
            }

            //移動処理
            movePoint += _movingVelocity *deltaTime* Vector2.right;

            //空中での処理
            if (_isAir)
            {
                //重力による上方向減衰
                _forceVelocity += _settings.Gravity * deltaTime * Vector2.down;
            }
            //地面についているときの処理
            else
            {
                if (_forceVelocity.x > 0.0f)
                {
                    _forceVelocity.x = Mathf.Max(0.0f, _forceVelocity.x - _settings.Friction * deltaTime);
                }
                else if (_forceVelocity.x < 0.0f)
                {
                    _forceVelocity.x = Mathf.Min(0.0f, _forceVelocity.x + _settings.Friction * deltaTime);
                }
            }

            //無理矢理掛かる力による移動
            //質量が軽いほどよく飛ぶ
            //接地中はY成分を無視してめり込みを防ぐ
            var appliedForce = _isAir ? _forceVelocity : new Vector2(_forceVelocity.x, 0.0f);
            movePoint += appliedForce / _settings.Weight * deltaTime;

            //キャラクター押しあたり判定
            if (pushTargetX.HasValue)
            {
                //TODO: 現状1.5がハードコーディングされているが、修正する
                if (position.x > pushTargetX.Value)
                {
                    movePoint += 1.5f * deltaTime * Vector2.right;
                }
                else
                {
                    movePoint -= 1.5f * deltaTime * Vector2.right;
                }
            }

            //着地スナップ補正
            if (!float.IsNaN(_snapGroundY))
            {
                movePoint.y = _snapGroundY;
                _snapGroundY = float.NaN;
            }

            //最終処理
            Velocity = (movePoint - position) / deltaTime;
            return new MoveStep(movePoint, Velocity);
        }
        

        // Unity 側からの接地情報の注入
        
        /// <summary>
        /// 接地レイの結果を反映する
        /// 接地→空中に変化した瞬間だけ OnForceAir を通知する
        /// (接地の通知は TryLand が担当する)
        /// </summary>
        /// <param name="grounded">接地したか？</param>
        public void ReportGroundProbe(bool grounded)
        {
            bool wasAir = _isAir;
            _isAir = !grounded;

            if (!wasAir && _isAir)
            {
                OnForceAir?.Invoke();
            }
        }
        
        
        /// <summary>
        /// 接地を試みる処理
        /// 足場にあたったときに呼び出す
        /// </summary>
        /// <param name="groundTopY">地面表面のy座標</param>
        /// <param name="selfHalfHeight">自身の足から中心までの高さ</param>
        /// <returns></returns>
        public bool TryLand(float groundTopY, float selfHalfHeight)
        {
            //上方向に動いているときは足場無視
            if(Velocity.y > 0.0f) return false;
                
            float groundTop = groundTopY;
            _isAir = false;
            //※コレが使われるのは1フレーム後であることに注意
            _snapGroundY = groundTop + selfHalfHeight;

            //地面についた場合、上下方向にかかっている速度は0にする
            _forceVelocity.y = 0.0f;

            //接地通知
            OnGround?.Invoke();
            return true;

        }
    }
}