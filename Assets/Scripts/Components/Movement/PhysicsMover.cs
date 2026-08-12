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

        //接地判定の余白
        //足場に吸着し続けるための厚み
        private const float SkinWidth = 0.04f;

        //キャッシュ
        private Rigidbody2D _rigidbody_Cache;
        private Collider2D _geometryCollider_Cache;
        private Rigidbody2D _otherRigidbody_Cache;

        //接地判定用
        private ContactFilter2D _groundFilter;
        private readonly RaycastHit2D[] _groundHitBuffer = new RaycastHit2D[8];

        //自分の形状(Startで確定させる)
        private float _selfWidth;
        private float _footOffset;


        //状態
        private bool _hasOtherCharacter = false;

        private bool CanPushObject(Collider2D other)
        {
            return (_characterLayer.value & (1 << other.gameObject.layer)) > 0 ||
                   (_propLayer.value & (1 << other.gameObject.layer)) > 0;
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

            //接地判定用フィルタ
            //足場のCollider2Dは GeometryHitNotifier によって isTrigger にされるので
            //useTriggers を有効にしないと一切ヒットしない
            _groundFilter = new ContactFilter2D { useTriggers = true };
            _groundFilter.SetLayerMask(_groundLayer);

            //自分の形状をキャッシュする
            //Physics2D の AutoSyncTransforms が無効なため、Rigidbody2D.position へ直接代入した
            //直後は bounds が古い値を返す。毎フレーム読まずにここで確定させる
            var geometryBounds = _geometryCollider_Cache.bounds;
            _selfWidth = geometryBounds.size.x;
            _footOffset = geometryBounds.min.y - _rigidbody_Cache.position.y;
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
            var from = _rigidbody_Cache.position;

            //押し合いする相手のx座標
            float? pushTargetX= _hasOtherCharacter ? _otherRigidbody_Cache.position.x : null;

            //移動先の予測
            Vector2 candidate = _movementSolver.Predict(
                Time.fixedDeltaTime,
                from,
                pushTargetX);

            //移動区間を掃いて足場を探す
            GroundHit? groundHit = QueryGround(from, candidate);

            //接地を反映してから確定する
            //吸着が同じ MovePosition に乗るので、着地は1フレームも遅れない
            MoveStep step = _movementSolver.ResolveGround(
                Time.fixedDeltaTime,
                from,
                candidate,
                groundHit);

            _rigidbody_Cache.MovePosition(step.NextPosition);
        }

        /// <summary>
        /// from → to の移動区間を掃いて、着地できる足場を探す
        /// 落下量ぶんを掃くのでトンネリングしない
        /// </summary>
        /// <param name="from">移動前の位置</param>
        /// <param name="to">移動先の予測位置</param>
        /// <returns>着地対象が見つかればその情報、無ければ null</returns>
        private GroundHit? QueryGround(Vector2 from, Vector2 to)
        {
            var fallDistance = from.y - to.y;

            //明確に上昇しているときだけ足場を無視する
            //接地中の落下量はちょうど0になるので、ここで弾くと毎フレーム落下と着地を繰り返す
            if (fallDistance < 0.0f) return null;

            //移動前の足元の高さ
            var footY = from.y + _footOffset;

            //足裏センサ
            //上端を足元に合わせた厚み SkinWidth の箱を、落下量 + 余白ぶんだけ下に掃く
            //x は移動先を使う。移動前の x で掃くと足場の端から出たことを検出できない
            var sensorSize = new Vector2(_selfWidth, SkinWidth);
            var sensorOrigin = new Vector2(to.x, footY - SkinWidth * 0.5f);

            //静止接地中は落下量が0になる
            //掃引距離を0にすると検出が不安定なので、余白を必ず足しておく
            var distance = fallDistance + SkinWidth;

            var count = Physics2D.BoxCast(
                sensorOrigin,
                sensorSize,
                0.0f,
                Vector2.down,
                _groundFilter,
                _groundHitBuffer,
                distance);

#if UNITY_EDITOR
            Debug.DrawLine(
                new Vector2(to.x, footY),
                new Vector2(to.x, footY - fallDistance - 2.0f * SkinWidth),
                count > 0 ? Color.green : Color.red,
                Time.fixedDeltaTime);
#endif

            if (count == 0) return null;

            var bestTopY = float.NegativeInfinity;
            var found = false;

            for (var i = 0; i < count; i++)
            {
                //Physics2D の QueriesStartInColliders が有効なため、めり込んだ状態から掃くと
                //hit.point と hit.normal が退化する。相手の bounds から表面を取る
                var topY = _groundHitBuffer[i].collider.bounds.max.y;

                //足元より上にある面は足場ではない(横腹や天井への接触)
                if (topY > footY + SkinWidth) continue;

                //複数の候補があれば一番高い面に乗る
                if (topY > bestTopY)
                {
                    bestTopY = topY;
                    found = true;
                }
            }

            return found ? new GroundHit(bestTopY, _footOffset) : (GroundHit?)null;
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
            }

            //接地判定は FixedUpdate 内の QueryGround が担当する
            //トリガは物理ステップの後に発火するため、ここで着地させると反映が1フレーム遅れる
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
