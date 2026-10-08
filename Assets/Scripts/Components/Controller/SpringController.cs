using System;
using Components.Detection;
using Components.Identity;
using Components.Movement;
using Core.Constants;
using UnityEngine;
using Data;
using DG.Tweening;
using Systems;
using UnityEngine.Rendering;
using UnityEngine.Serialization;
using UnityEngine.UIElements;

namespace Components.Controller
{
    public class SpringController : MonoBehaviour
    {
        //基本パラメータ
        [SerializeField] private float springForce;
        [SerializeField] private float angleOffset;
        [SerializeField] private float jumpExtraForce = 8.0f;
        [SerializeField] private float jumpVectorRatio = 0.8f; //0~1 どのくらいを上向きと許容するか
        
        //アニメーション関連
        [SerializeField] private float stretchScale;
        [SerializeField] private float stretchDuration;
        [SerializeField] private GameObject renderer;

        private void OnValidate()
        {
            //回転がエディタ上で反映されるようにする
            var eulerAngles = transform.localEulerAngles;
            eulerAngles.z = angleOffset;
            transform.localEulerAngles = eulerAngles;
        }

        //イベントのId
        private int? stretchEventId;
        
        //レイヤー
        private LayerMask _characterLayer;
        private LayerMask _propLayer;


        private void Awake()
        {
            _characterLayer = LayerMask.GetMask(GameLayers.Character);
            _propLayer=LayerMask.GetMask(GameLayers.Prop);
        }
        private void OnEnable()
        {

            if (TryGetComponent(out EventTriggersManager eventTriggersManager))
            {
                var setting = new TriggerShapeSetting
                {
                    shape = ColliderShape.Box,
                    boxSize = new Vector2(0.5f, 0.5f),
                };
            
                //イベントを追加
                stretchEventId 
                    = eventTriggersManager.Subscribe(setting, TriggerType.Enter, Vector2.up * 0.2f, Stretch);
            }

            
        }

        private void OnDisable()
        {
            if (TryGetComponent(out EventTriggersManager eventTriggersManager))
            {
                if (stretchEventId.HasValue)
                {
                    eventTriggersManager.Unsubscribe(stretchEventId.Value);
                }
            }
            
        }


        void Stretch(Collider2D other)
        {
            var otherRoot = EntityRoot.Require(other);
            
            //自身には何も起こさない
            if (otherRoot == EntityRoot.Require(this))return;
            
            //相手が prop or Characterか？
            var manipulatable = (_characterLayer.value & (1 << other.gameObject.layer)) > 0 ||
                                (_propLayer.value & (1 << other.gameObject.layer)) > 0;
            if (!manipulatable)return;
            
            
            if (!otherRoot.TryGetComponent(out PhysicsMover mover)) return;
            
            //飛ばす処理
            var fixedSpringForce = springForce;
            var forceDirection = Quaternion.Euler(new Vector3(0.0f, 0.0f, angleOffset)) * Vector3.up;
            //力の向きが上向きなら、バネジャンプを認める（横向きを許容するとジャンプじゃなくなる）
            if (mover.IsRequestedSpringForce && Vector2.Dot(Vector2.up, (Vector2)forceDirection) > jumpVectorRatio)
                fixedSpringForce += jumpExtraForce;
            mover.AddForceVelocity((Vector2)forceDirection * fixedSpringForce, true);
            
            //アニメーション再生
            StretchEffect();
        }

        void StretchEffect()
        {
            //前のバネアニメーションは停止する
            DOTween.Kill("Stretch" + gameObject.GetEntityId());
            
            Sequence seq = DOTween.Sequence();
            
            var initialScale = renderer.transform.localScale;

            seq.Append(renderer.transform.DOScaleY(initialScale.y*stretchScale, 0.05f));
            seq.Append(renderer.transform.DOScaleY(initialScale.y, stretchDuration).SetEase(Ease.OutElastic));
            seq.OnKill(()=>
            {
                renderer.transform.localScale = initialScale;
            });
            seq.SetLink(renderer.gameObject);
            seq.SetId("Stretch" + gameObject.GetEntityId());
            seq.Play();
            
            //音
            SoundManager.Instance.PlaySe("SpringStretch");
        }
        
    }
}