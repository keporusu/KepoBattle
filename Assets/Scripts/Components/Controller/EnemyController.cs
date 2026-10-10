using System;
using UnityEngine;
using Components.Combat.Attack;
using Components.Identity;
using Components.Movement;
using Core.Constants;
using Core.Contracts;
using Data;

namespace Components.Controller
{

    public class EnemyController : MonoBehaviour, ILevelObject
    {
        //レベルデータのキー
        private const string FacingRightKey = "facingRight";

        //AnimatorでSpriteを動かすGameObjectを期待する
        [SerializeField] private GameObject animSprite;

        //キャッシュ
        private PhysicsMover _physicsMover_Cache;
        private AttackExecutor _attackExecutor_Cache;
        private Animator _animator_Cache;

        //右を向いているか？
        //絵は右向きなので、ワールド上のスケールが正なら右向き
        //ルートが反転されていても正しく判定できるよう、localScale ではなく lossyScale で見る
        public bool IsFacingRight => animSprite.transform.lossyScale.x > 0;


        private void Start()
        {
            if (!TryGetComponent(out _physicsMover_Cache))
                throw new MissingComponentException($"[{GetType().Name}] PhysicsMover が {gameObject.name} に見つかりません");

            if (!TryGetComponent(out _attackExecutor_Cache))
                throw new MissingComponentException($"[{GetType().Name}] AttackExecutor が {gameObject.name} に見つかりません");

            if (!animSprite.TryGetComponent(out _animator_Cache))
                throw new MissingComponentException($"[{GetType().Name}] Animator が animSprite ({animSprite.name}) に見つかりません");

            _physicsMover_Cache.OnGround += OnGround;

            //落下したら消える
            _physicsMover_Cache.OnFallOut += OnFallOut;

            //最初のスプライトの向きによって最初の向きを決める
            _physicsMover_Cache.SetRight(IsFacingRight);
        }

        /// <summary>
        /// 向きを設定する
        /// スプライトのみを反転させ、ルートの Transform は反転させない
        /// </summary>
        /// <param name="right">右向きにするか？</param>
        public void SetFacingRight(bool right)
        {
            //親(ルート)が反転されていても、ワールド上で狙った向きになるよう親の符号を掛ける
            var spriteTransform = animSprite.transform;
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

        public void ApplyLevelParameters(LevelObjectParameters parameters)
        {
            if (parameters.TryGetBool(FacingRightKey, out var facingRight))
            {
                SetFacingRight(facingRight);
            }
        }

        public void ExportLevelParameters(LevelObjectParameters parameters)
        {
            parameters.SetBool(FacingRightKey, IsFacingRight);
        }

        private void OnFallOut()
        {
            Destroy(EntityRoot.Require(this).gameObject);
        }

        private void OnGround()
        {
            //接地状態遷移（AnimController）
            _animator_Cache.SetTrigger(AnimatorParams.Ground);
            Debug.Log("Grounded");
        }
    }
}
