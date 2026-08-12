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

    /// <summary>
    /// 着地対象の足場の情報
    /// Unity 側の形状クエリ結果をソルバへ渡すための入れ物
    /// </summary>
    public readonly struct GroundHit
    {
        public readonly float GroundTopY;  // 足場表面の y 座標
        public readonly float FootOffset;  // 中心から足元までのオフセット(通常は負)

        public GroundHit(float groundTopY, float footOffset)
        {
            GroundTopY = groundTopY;
            FootOffset = footOffset;
        }

        // 着地後の中心 y
        // 足元(中心 + FootOffset)が足場表面に一致する位置
        public float SnappedCenterY => GroundTopY - FootOffset;
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

            Velocity = Vector2.zero;
        }


        /// <summary>
        /// 1フレーム分の移動先を予測する
        /// 接地解決を含まないので、この結果をそのまま確定させてはいけない
        /// 必ず ResolveGround に通すこと
        /// </summary>
        /// <param name="deltaTime">経過時間</param>
        /// <param name="position">現在位置</param>
        /// <param name="pushTargetX">押し合う相手のx座標</param>
        /// <returns>接地解決前の移動先</returns>
        public Vector2 Predict(float deltaTime, Vector2 position, float? pushTargetX)
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
                if (position.x > pushTargetX.Value)
                {
                    movePoint += _settings.PushSpeed * deltaTime * Vector2.right;
                }
                else
                {
                    movePoint -= _settings.PushSpeed * deltaTime * Vector2.right;
                }
            }

            return movePoint;
        }


        // Unity 側からの接地情報の注入

        /// <summary>
        /// Predict の結果に接地判定を反映し、最終的な移動先を確定する
        /// スナップは同じフレーム内で適用されるので、着地の反映は遅れない
        /// </summary>
        /// <param name="deltaTime">経過時間</param>
        /// <param name="from">移動前の位置</param>
        /// <param name="candidate">Predict が返した移動先</param>
        /// <param name="hit">移動区間で見つかった足場。無ければ null</param>
        /// <returns>確定した移動先と速度</returns>
        public MoveStep ResolveGround(float deltaTime, Vector2 from, Vector2 candidate, GroundHit? hit)
        {
            bool wasAir = _isAir;
            Vector2 result = candidate;

            if (hit.HasValue)
            {
                _isAir = false;

                //足場表面に吸着させる
                result.y = hit.Value.SnappedCenterY;

                //地面についた場合、上下方向にかかっている速度は0にする
                _forceVelocity.y = 0.0f;
            }
            else
            {
                _isAir = true;
            }

            //吸着を反映してから確定する
            //ここより前で確定させると補正前の速度が外に漏れる
            Velocity = (result - from) / deltaTime;

            //状態が変化した瞬間だけ通知する
            if (wasAir && !_isAir)
            {
                OnGround?.Invoke();
            }
            else if (!wasAir && _isAir)
            {
                OnForceAir?.Invoke();
            }

            return new MoveStep(result, Velocity);
        }
    }
}