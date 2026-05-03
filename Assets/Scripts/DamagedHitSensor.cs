using UnityEngine;

public class DamagedHitSensor : MonoBehaviour
{
    [SerializeField] private GameObject damagedCollider;
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        var damagedNotifier=damagedCollider.GetComponent<DamagedNotifier>();
        if (damagedNotifier != null)
        {
            damagedNotifier.OnHit += DamagedHit;
        }
    }

    void DamagedHit(Collider2D other)
    {
        Debug.Log("DamagedHit");
    }
    
}
