using UnityEngine;

public class DamageProcessor : MonoBehaviour
{
    [SerializeField] private GameObject damagedCollider;
    private AnimatorTrigger animatorTrigger_Cache;
    
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
        
    }

    void DamagedHit(Collider2D other)
    {
        Debug.Log("DamagedHit");
        animatorTrigger_Cache.TriggerDamage();
    }
    
}
