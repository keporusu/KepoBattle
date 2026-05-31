using UnityEngine;

[RequireComponent(typeof(AnimatorTrigger))]
[RequireComponent(typeof(PhysicsMover))]
public class DamageProcessor : MonoBehaviour
{
    [SerializeField] private GameObject damagedCollider;
    private AnimatorTrigger animatorTrigger_Cache;
    private PhysicsMover physicsMover_Cache;
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        var damagedNotifier=damagedCollider.GetComponent<DamagedNotifier>();
        if (damagedNotifier != null)
        {
            damagedNotifier.OnHit += DamagedHit;
        }
        
        animatorTrigger_Cache = GetComponent<AnimatorTrigger>();
        physicsMover_Cache = GetComponent<PhysicsMover>();
        
    }

    void DamagedHit(Collider2D other)
    {
        //コリジョン処理
        var damageCollisionManager = other.GetComponent<DamageCollisionManager>();
        if (damageCollisionManager != null)
        {
            var attackInfo = damageCollisionManager.GetAttackInfo();
            
            //キャラクターの位置関係で、どちら向きに吹き飛ばすか決める
            if (transform.root.position.x < other.transform.root.position.x)
            {
                attackInfo.attackPower.x = -attackInfo.attackPower.x;
            }
            physicsMover_Cache.ForcePower(attackInfo.attackPower,true);
        }
        //ダメージアニメーションに遷移
        animatorTrigger_Cache.TriggerDamage();
        Debug.Log("DamagedHit");
    }
    
}
