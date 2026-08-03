using System;
using System.Linq;
using Components.Combat.Attack;
using UnityEngine;
using Core.Exceptions;
using Core.Constants;
using Components.Detection;
using Core.Movement;

namespace Components.Movement
{
    public class PhysicsMover : MonoBehaviour
    {
        // 移動ロジック
        private MovementSolver _movementSolver;
        
        //公開プロパティ
        [SerializeField] private float gravity = 1.0f;
        [SerializeField] private float weight = 1.0f;
        [SerializeField] private float friction = 1.0f;
        [SerializeField] private float pushedSpeed = 1.5f;

        //押し判定をする相手
        private LayerMask _characterLayer;
        private LayerMask _propLayer;
        private LayerMask _groundLayer;

        //キャッシュ
        private Rigidbody2D _rigidbody_Cache;
        private Collider2D _geometryCollider_Cache;
        private Rigidbody2D _otherRigidbody_Cache;
        

        //状態
        private bool _hasOtherCharacter = false;

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
        public event System.Action OnGround
        {
            add => _movementSolver.OnGround += value;
            remove => _movementSolver.OnGround -= value;
        }
        public event System.Action OnForceAir
        {
            add => _movementSolver.OnForceAir += value;
            remove => _movementSolver.OnForceAir -= value;
        }
        
        
        //公開プロパティ
        public bool IsAir => _movementSolver.IsAir;
        public Vector2 Velocity =>_movementSolver.Velocity;
        public Vector2 Position => _rigidbody_Cache.position;
        
        private Vector2 GetVelocity() => _movementSolver.Velocity;
        

        void Awake()
        {
            // ロジック生成
            var settings = new MovementSettings(gravity, weight, friction, pushedSpeed);
            _movementSolver = new MovementSolver(settings);
        }
        
        void Start()
        {
            if (!TryGetComponent(out _rigidbody_Cache))
                throw new MissingComponentException($"[{GetType().Name}] Rigidbody2D が {gameObject.name} に見つかりません");

            var geometryCollider = GetComponentsInChildren<Transform>()
                .FirstOrDefault(obj => obj.gameObject.CompareTag(GameTags.GeometryChannel))
                ?? throw new MissingChannelException(GameTags.GeometryChannel, gameObject.name);

            if(!geometryCollider.TryGetComponent(out _geometryCollider_Cache))
                throw new MissingComponentException($"[{GetType().Name}] Collider2D が {geometryCollider.gameObject.name} に見つかりません");

            //レイヤー取得
            _characterLayer=LayerMask.GetMask(GameLayers.Character);
            _propLayer=LayerMask.GetMask(GameLayers.Prop);
            _groundLayer=LayerMask.GetMask(GameLayers.Ground);
        }

        private void OnEnable()
        {
            var geometryCollider = GetComponentsInChildren<Transform>()
                                       .FirstOrDefault(obj => obj.gameObject.CompareTag(GameTags.GeometryChannel))
                                   ?? throw new MissingChannelException(GameTags.GeometryChannel, gameObject.name);
            if (!geometryCollider.TryGetComponent(out GeometryHitNotifier geometryHitNotifier))
                throw new MissingComponentException($"[{GetType().Name}] GeometryNotifier が {geometryCollider.gameObject.name} に見つかりません");

            //イベント登録
            geometryHitNotifier.OnHit += OnHitGeometry;
            geometryHitNotifier.OnRelease += OnReleaseGeometry;
            
            
            //PropAttackCollisionControllerに攻撃速度を渡す
            foreach (var controller in GetComponentsInChildren<PropAttackCollisionController>())
            {
                controller.OnGetAttackInfo += GetVelocity;
            }
        }

        void OnDisable()
        {
            var geometryCollider = GetComponentsInChildren<Transform>()
                                       .FirstOrDefault(obj => obj.gameObject.CompareTag(GameTags.GeometryChannel))
                                   ?? throw new MissingChannelException(GameTags.GeometryChannel, gameObject.name);
            if (!geometryCollider.TryGetComponent(out GeometryHitNotifier geometryHitNotifier))
                throw new MissingComponentException($"[{GetType().Name}] GeometryNotifier が {geometryCollider.gameObject.name} に見つかりません");

            //イベント解除
            geometryHitNotifier.OnHit -= OnHitGeometry;
            geometryHitNotifier.OnRelease -= OnReleaseGeometry;
            
            //イベント解除
            foreach (var controller in GetComponentsInChildren<PropAttackCollisionController>())
            {
                controller.OnGetAttackInfo -= GetVelocity;
            }
        }
        
        

        /// <summary>
        /// 移動用
        /// </summary>
        /// <param name="velocity">速度</param>
        protected void InputMove(float velocity)
        {
            _movementSolver.InputMove(velocity);
        }
        
        /// <summary>
        /// ブレーキを掛ける
        /// </summary>
        protected void CutMove()
        {
            _movementSolver.CutMove();
        }

        /// <summary>
        /// 速度を直接0にする
        /// </summary>
        /// <param name="cutX">x方向を切るか？</param>
        /// <param name="cutY">y方向を切るか？</param>
        protected void CutVelocity(bool cutX = true, bool cutY = true)
        {
            _movementSolver.CutVelocity(cutX, cutY);
        }

        protected virtual void FixedUpdate()
        {
            //押し合いする相手のx座標
            float? pushTargetX= _hasOtherCharacter ? _otherRigidbody_Cache.position.x : null;
            
            //移動処理
            MoveStep step =_movementSolver.Step(
                Time.fixedDeltaTime,
                _rigidbody_Cache.position,
                pushTargetX);

            _rigidbody_Cache.MovePosition(step.NextPosition);

            //衝突判定関連
            //地面端から落ちるか？
            var bottomOffset = _geometryCollider_Cache.bounds.min.y - _rigidbody_Cache.position.y;
            var groundOrigin = new Vector2(step.NextPosition.x, step.NextPosition.y + bottomOffset + 0.05f);
            
           _movementSolver.ReportGroundProbe(
                   Physics2D.Raycast(groundOrigin, Vector2.down, 0.08f, _groundLayer)
               );
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
            
            
            var groundTopY = other.bounds.max.y;//相手の頭
            var selfTopY = _geometryCollider_Cache.bounds.max.y;//自分の頭
            var selfHalfHeight = _geometryCollider_Cache.bounds.extents.y;//自分の高さ(半分)
            //足場の時、着地を試みる
            if (groundTopY < selfTopY)
            {
                _movementSolver.TryLand(groundTopY, selfHalfHeight);
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
            _movementSolver.AddForceVelocity(velocity, forceMode);
        }

        public void ResetAll(Vector2 position)
        {
            //移動処理を全てリセット
            _movementSolver.Reset();

            //位置
            _rigidbody_Cache.position = position;
        }

    }
}
