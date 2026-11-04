using Components.Identity;
using Components.Movement;
using Core.Constants;
using DG.Tweening;
using Systems;
using UnityEngine;

namespace Components.Controller
{
    public class BellController : PropBehaviourController
    {
        //弾き返し
        [SerializeField] private float reflectRatio = 1.0f; //当たってきた速度に対する弾き返す速度の割合
        [SerializeField] private float minReflectSpeed = 5.0f; //弾き返す速度の下限

        //アニメーション関連
        [SerializeField] private SpriteRenderer spRenderer;
        [SerializeField] private float swingAngle = 15.0f; //揺れの最大角度(度)
        [SerializeField] private float swingDuration = 0.4f; //揺れ始めてから元に戻るまでの時間

        private void Start()
        {
            //自身
            SetIdentity(ObjectIdentity.Bell);
        }

        protected override void OnDamageHit(Collider2D other)
        {
            base.OnDamageHit(other);

            var otherRoot = EntityRoot.Require(other);

            //自身には何も起こさない
            if (otherRoot == EntityRoot.Require(this)) return;

            if (otherRoot.TryGetComponent(out PhysicsMover mover))
            {
                Reflect(mover);
            }

            //アニメーション再生
            SwingEffect();
        }

        //当たってきたときの音
        //基底の Prop 用の音と重ならないように、どちらもベルの音に置き換える
        protected override void PlayDamageSoundFromCharacter()
        {
            SoundManager.Instance.PlaySe("BellRinging");
        }
        protected override void PlayDamageSoundFromProp()
        {
            SoundManager.Instance.PlaySe("BellRinging");
        }

        /// <summary>
        /// 当たってきた相手を、その場で弾き返す
        /// </summary>
        /// <param name="mover">弾き返す相手</param>
        private void Reflect(PhysicsMover mover)
        {
            //Velocity は実際の移動速度で、BlowAlpha が掛かった後の値
            //AddForceVelocity に渡す値は BlowAlpha を掛ける前の値なので、単位を揃える
            //揃えないと、弾き返すたびに BlowAlpha 倍に加速してしまう
            var velocity = mover.BlowAlpha > 0.0f ? mover.Velocity / mover.BlowAlpha : mover.Velocity;

            //ベルの中心から相手へ向かう向きを法線とする
            var normal = (mover.Position - (Vector2)transform.position).normalized;
            //中心が重なっている場合は、進んできた向きの逆を法線とする
            if (normal == Vector2.zero)
            {
                normal = velocity == Vector2.zero ? Vector2.up : -velocity.normalized;
            }

            //近づいてきている場合は反射させる
            //そうでない場合(止まっている、離れている)は、法線の向きへ弾き出す
            var reflectVelocity = Vector2.Dot(velocity, normal) < 0.0f
                ? Vector2.Reflect(velocity, normal)
                : normal * velocity.magnitude;

            reflectVelocity *= reflectRatio;
            //遅く当たっても下限の速度では弾き返す
            if (reflectVelocity.magnitude < minReflectSpeed)
            {
                reflectVelocity = (reflectVelocity == Vector2.zero ? normal : reflectVelocity.normalized) * minReflectSpeed;
            }

            mover.AddForceVelocity(reflectVelocity, true);
        }

        /// <summary>
        /// 時計回り→反時計回り→時計回り と小さく揺れて、元の角度に戻る
        /// </summary>
        private void SwingEffect()
        {
            //前の揺れアニメーションは停止する
            var animationId = "Swing" + gameObject.GetEntityId();
            DOTween.Kill(animationId);

            var swingTransform = spRenderer.transform;
            var initialRotation = swingTransform.localRotation;
            var initialAngle = swingTransform.localEulerAngles.z;

            //時計回りはz軸のマイナス方向
            //揺れるごとに角度を小さくして、元の角度へ収まるようにする
            float[] angles = { -swingAngle, swingAngle * 0.6f, -swingAngle * 0.3f, 0.0f };
            var stepDuration = swingDuration / angles.Length;

            Sequence seq = DOTween.Sequence();
            foreach (var angle in angles)
            {
                seq.Append(
                    swingTransform.DOLocalRotate(new Vector3(0.0f, 0.0f, initialAngle + angle), stepDuration)
                        .SetEase(Ease.InOutSine)
                );
            }
            seq.OnKill(() =>
            {
                //終了時などでrendererが先に破棄されている場合がある
                if (spRenderer == null) return;
                spRenderer.transform.localRotation = initialRotation;
            });
            seq.SetLink(spRenderer.gameObject);
            seq.SetId(animationId);
            seq.Play();
        }
    }
}
