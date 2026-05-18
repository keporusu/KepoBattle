using UnityEngine;

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
        if (animatorTrigger_Cache == null)
        {
            Debug.LogError("animatorTrigger component not found");
        }
        
        physicsMover_Cache = GetComponent<PhysicsMover>();
        if (physicsMover_Cache == null)
        {
            Debug.LogError("physicsMover component not found");
        }
        
    }

    void DamagedHit(Collider2D other)
    {
        var damageCollisionManager = other.GetComponent<DamageCollisionManager>();
        if (damageCollisionManager != null)
        {
            
        }
        animatorTrigger_Cache.TriggerDamage();
        Debug.Log("DamagedHit");
    }
    
}
