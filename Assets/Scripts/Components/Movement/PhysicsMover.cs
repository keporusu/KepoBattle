using System.Linq;
using UnityEngine;
using Core.Exceptions;
using Core.Constants;
using Components.Detection;

namespace Components.Movement
{
    public class PhysicsMover : MonoBehaviour
    {
        [SerializeField] private float gravity = 1.0f;
        [SerializeField] private float weight = 1.0f;
        [SerializeField] private float friction = 1.0f;

        //押し判定をする相手
        private LayerMask _characterLayer;
        private LayerMask _propLayer;
        private LayerMask _groundLayer;

        //キャッシュ
        private Rigidbody2D _rigidbody_Cache;
        private Collider2D _geometryCollider_Cache;
        private Rigidbody2D _otherRigidbody_Cache;

        //常にUpdateする値
        private float _movingVelocity = 0.0f; //移動時の速度
        private Vector2 _forceVelocity = Vector2.zero; //無理に掛かる速度

        //状態
        private bool _hasOtherCharacter=false;
        private bool _isAir = true;
        private bool _isBraking = false;
        private float _snapGroundY = float.NaN;

        private bool CanPushObject(Collider2D other)
        {
            return (_characterLayer.value & (1 << other.gameObject.layer)) > 0 ||
                   (_propLayer.value & (1 << other.gameObject.layer)) > 0;
        }
        private bool IsGround(Collider2D other)
        {
            return (_groundLayer.value & (1 << other.gameObject.layer)) > 0;
        }

        //通知
        public event System.Action OnGround;
        public event System.Action OnForceAir;

        //公開プロパティ
        public bool IsAir => _isAir;
        public Vector2 Velocity { get; private set; }
        public Vector2 Position => _rigidbody_Cache.position;

        void Start()
        {
            if (!TryGetComponent(out _rigidbody_Cache))
                throw new MissingComponentException($"[{GetType().Name}] Rigidbody2D が {gameObject.name} に見つかりません");

            var geometryCollider = GetComponentsInChildren<Transform>()
                .FirstOrDefault(obj => obj.gameObject.CompareTag(GameTags.GeometryChannel))
                ?? throw new MissingChannelException(GameTags.GeometryChannel, gameObject.name);

            if(!geometryCollider.TryGetComponent(out _geometryCollider_Cache))
                throw new MissingComponentException($"[{GetType().Name}] Collider2D が {geometryCollider.gameObject.name} に見つかりません");

            if (!geometryCollider.TryGetComponent(out GeometryHitNotifier geometryHitNotifier))
                throw new MissingComponentException($"[{GetType().Name}] GeometryNotifier が {geometryCollider.gameObject.name} に見つかりません");

            //イベント登録
            geometryHitNotifier.OnHit += OnHitGeometry;
            geometryHitNotifier.OnRelease += OnReleaseGeometry;

            //レイヤー取得
            _characterLayer=LayerMask.GetMask(GameLayers.Character);
            _propLayer=LayerMask.GetMask(GameLayers.Prop);
            _groundLayer=LayerMask.GetMask(GameLayers.Ground);
        }
        
        /// <summary>
        /// 移動用
        /// </summary>
        /// <param name="velocity">速度</param>
        protected void InputMove(float velocity)
        {
            _isBraking = false;
            _movingVelocity = velocity;
        }
        
        /// <summary>
        /// ブレーキを掛ける
        /// </summary>
        protected void CutMove()
        {
            _isBraking = true;
        }

        /// <summary>
        /// 速度を直接0にする
        /// </summary>
        /// <param name="cutX">x方向を切るか？</param>
        /// <param name="cutY">y方向を切るか？</param>
        protected void CutVelocity(bool cutX = true, bool cutY = true)
        {
            if (cutX) _forceVelocity.x = 0.0f;
            if (cutY) _forceVelocity.y = 0.0f;
        }

        protected virtual void FixedUpdate()
        {

            //****移動****
            Vector2 movePoint = _rigidbody_Cache.position;

            //ブレーキ処理
            if (_isBraking)
            {
                if (_movingVelocity > 0.0f)
                {
                    _movingVelocity = Mathf.Max(0.0f,_movingVelocity-friction*Time.deltaTime);
                }
                else if (_movingVelocity < 0.0f)
                {
                    _movingVelocity = Mathf.Min(0.0f,_movingVelocity+friction*Time.deltaTime);
                }

                if (_movingVelocity == 0.0f)
                {
                    _isBraking = false;
                }
            }

            //移動処理
            movePoint += _movingVelocity *Time.fixedDeltaTime* Vector2.right;

            //空中での処理
            if (_isAir)
            {
                //重力による上方向減衰
                _forceVelocity += gravity * Time.fixedDeltaTime * Vector2.down;
            }
            //地面についているときの処理
            else
            {
                if (_forceVelocity.x > 0.0f)
                {
                    _forceVelocity.x = Mathf.Max(0.0f, _forceVelocity.x - friction * Time.deltaTime);
                }
                else if (_forceVelocity.x < 0.0f)
                {
                    _forceVelocity.x = Mathf.Min(0.0f, _forceVelocity.x + friction * Time.deltaTime);
                }
            }

            //無理矢理掛かる力による移動
            //質量が軽いほどよく飛ぶ
            //接地中はY成分を無視してめり込みを防ぐ
            var appliedForce = _isAir ? _forceVelocity : new Vector2(_forceVelocity.x, 0.0f);
            movePoint += appliedForce / Mathf.Max(0.001f, weight) * Time.fixedDeltaTime;
            //movePoint += ForceVelocity / Mathf.Max(0.001f, weight) * Time.fixedDeltaTime;

            //キャラクター押しあたり判定
            if (_hasOtherCharacter)
            {
                if (_rigidbody_Cache.position.x > _otherRigidbody_Cache.position.x)
                {
                    movePoint += 1.5f*Time.fixedDeltaTime * Vector2.right;
                }
                else
                {
                    movePoint -= 1.5f*Time.fixedDeltaTime * Vector2.right;
                }
            }

            //着地スナップ補正
            if (!float.IsNaN(_snapGroundY))
            {
                movePoint.y = _snapGroundY;
                _snapGroundY = float.NaN;
            }

            //最終処理
            Velocity = (movePoint - _rigidbody_Cache.position) / Time.fixedDeltaTime;
            _rigidbody_Cache.MovePosition(movePoint);



            //**********衝突判定関連****************

            //地面端から落ちるか？
            var bottomOffset = _geometryCollider_Cache.bounds.min.y - _rigidbody_Cache.position.y;
            var groundOrigin = new Vector2(movePoint.x, movePoint.y + bottomOffset + 0.05f);
            bool wasAir = _isAir;
            _isAir = !Physics2D.Raycast(groundOrigin, Vector2.down, 0.08f, _groundLayer);
            if (!wasAir && _isAir)
            {
                OnForceAir?.Invoke();
            }
        }

        private void OnHitGeometry(Collider2D other)
        {
            if (!other.CompareTag(GameTags.GeometryChannel)) return;

            //相手がCharacter or Propの場合はキャッシュする
            if (CanPushObject(other))
            {
                _otherRigidbody_Cache=other.GetComponentInParent<Rigidbody2D>();
                _hasOtherCharacter = true;
                Debug.Log(gameObject.name+": Catch Character");
                return;
            }

            if (!IsGround(other)) return;
            
            //足場の時
            if (other.bounds.max.y < _geometryCollider_Cache.bounds.max.y)
            {
                //上方向に動いているときは足場無視
                if(Velocity.y > 0.0f) return;

                float groundTop = other.bounds.max.y;
                _isAir = false;
                _snapGroundY = groundTop + _geometryCollider_Cache.bounds.extents.y;

                //地面についた場合、上下方向にかかっている速度は0にする
                _forceVelocity.y = 0.0f;

                //接地通知
                OnGround?.Invoke();
            }
        }

        private void OnReleaseGeometry(Collider2D other)
        {
            if (!other.CompareTag(GameTags.GeometryChannel)) return;

            //相手がキャラクターの場合はキャッシュ解除
            if (CanPushObject(other))
            {
                _otherRigidbody_Cache = null;
                _hasOtherCharacter = false;
                Debug.Log(gameObject.name+": Lost Character");
                return;
            }
        }

        /// <summary>
        /// 自分に特定の方向に速度を加える
        /// </summary>
        /// <param name="velocity">加える速度</param>
        /// <param name="forceMode">一回停止させてから力を加えるか？</param>
        /// <param name="instigator">攻撃者のオブジェクト</param>>
        public void AddForceVelocity(Vector2 velocity, bool forceMode, GameObject instigator=null)
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

        public void ResetAll(Vector2 position)
        {
            _movingVelocity = 0.0f; //移動時の力
            _forceVelocity = new Vector2(); //攻撃などで無理にかかる速度

            //状態
            _hasOtherCharacter=false;
            _isAir = true;
            _isBraking = false;
            _snapGroundY = float.NaN;

            //位置
            _rigidbody_Cache.position = position;
        }

    }
}
