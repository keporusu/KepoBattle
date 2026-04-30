using UnityEngine;

public class Attack : MonoBehaviour
{
    [SerializeField] private GameObject damageCollider;

    public void StartAttack()
    {
        damageCollider.SetActive(true);
    }
}
