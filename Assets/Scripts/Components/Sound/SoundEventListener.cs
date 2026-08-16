using System;
using System.Linq;
using Components.Detection;
using Components.Identity;
using Components.Movement;
using Core.Constants;
using Core.Exceptions;
using Systems;
using UnityEngine;

namespace Components.Sound
{
    /// <summary>
    /// コンポーネント
    /// 壁や床への衝突・バウンド、ダメージ時など、共通で鳴らす音を登録する
    /// </summary>
    public class SoundEventListener : MonoBehaviour
    {
        
        [SerializeField] private string damageSoundFromCharacter;
        [SerializeField] private string damageSoundFromProp;
        
        //レイヤー
        LayerMask _characterLayer;
        LayerMask _propLayer;

        //キャッシュ
        private PhysicsMover _physicsMover_Cache;
        private DamageHitNotifier _damageHitNotifier_Cache;

        private void Awake()
        {
            //接地・壁衝突判定
            if (!TryGetComponent(out _physicsMover_Cache))
                throw new MissingComponentException($"[{GetType().Name}] PhysicsMover が {gameObject.name} に見つかりません");

            var damagedCollider = GetComponentsInChildren<Transform>()
                                      .FirstOrDefault(obj => obj.gameObject.CompareTag(GameTags.DamageChannel))
                                  ?? throw new MissingChannelException(GameTags.DamageChannel, gameObject.name);

            //ダメージ通知
            _damageHitNotifier_Cache = damagedCollider.GetComponent<DamageHitNotifier>();
            if(_damageHitNotifier_Cache == null)
                throw new MissingComponentException($"[{GetType().Name}] DamagedNotifier が {damagedCollider.gameObject.name} に見つかりません");

            //レイヤー
            _characterLayer = LayerMask.GetMask(GameLayers.Character);
            _propLayer = LayerMask.GetMask(GameLayers.Prop);
        }

        private void OnEnable()
        {
            //イベント登録
            _physicsMover_Cache.OnBounce += OnBounce;
            _damageHitNotifier_Cache.OnHit += OnDamage;
        }

        private void OnDisable()
        {
            //イベント解除
            _physicsMover_Cache.OnBounce -= OnBounce;
            _damageHitNotifier_Cache.OnHit -= OnDamage;
        }


        private void OnBounce(float bounceAmount)
        {
            if (bounceAmount < 0.5f) return;
            SoundManager.Instance.PlaySe(SoundNames.SePropBounce);
        }
        
        //ダメージの時の音
        private void OnDamage(Collider2D other)
        {
            var otherRoot = EntityRoot.Require(other);
            
            if (damageSoundFromCharacter.Length > 0 && 
                (_characterLayer & (1<<otherRoot.gameObject.layer))>0
               )
            {
                SoundManager.Instance.PlaySe(damageSoundFromCharacter);
            }

            if ((_propLayer & (1 << otherRoot.gameObject.layer)) > 0)
            {
                if (damageSoundFromProp.Length > 0)
                {
                    SoundManager.Instance.PlaySe(damageSoundFromProp);
                }
                else
                {
                    //デフォルトSE
                    SoundManager.Instance.PlaySe(SoundNames.SePropDamageFromProp);
                }
            }
        }
    }
}

