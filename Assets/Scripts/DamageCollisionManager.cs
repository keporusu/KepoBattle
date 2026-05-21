using System;
using JetBrains.Annotations;
using UnityEngine;
using UnityEngine.WSA;



public class DamageCollisionManager : MonoBehaviour
{
    private CircleCollider2D circleCollider;
    private CapsuleCollider2D capsuleCollider;
    private BoxCollider2D boxCollider;

    private ColliderShape lastactiveShape;
    private bool isActive = false;
    private int ownerID=-1;
    private AttackInfo attackInfo;
    
    public bool IsActive => isActive;
    public int OwnerID => ownerID;

    private void Start()
    {
        circleCollider = GetComponent<CircleCollider2D>();
        capsuleCollider = GetComponent<CapsuleCollider2D>();
        boxCollider = GetComponent<BoxCollider2D>();
        circleCollider.enabled = false;
        capsuleCollider.enabled = false;
        boxCollider.enabled = false;
        gameObject.SetActive(false);
    }

    [CanBeNull]
    public Collider2D Activate(AttackCollisionSetting collisionSetting, int id)
    {
        //すでにアクティブなら何もしない
        if(gameObject.activeSelf)return null ;

        lastactiveShape = collisionSetting.shape;
        gameObject.SetActive(true);
        isActive = true;
        ownerID = id;
        
        //コリジョンの攻撃情報
        attackInfo.attackPower = collisionSetting.attackPower;
        
        //コリジョン形状の設定
        switch (collisionSetting.shape)
        {
            case ColliderShape.Circle:
                circleCollider.radius=collisionSetting.circleRadius;
                circleCollider.offset = collisionSetting.offset;
                circleCollider.enabled = true;
                return circleCollider;
            case ColliderShape.Capsule:
                capsuleCollider.size = new Vector2(collisionSetting.capsuleRadius * 2, collisionSetting.capsuleHeight);
                capsuleCollider.direction = collisionSetting.capsuleDirection == CapsuleDirection.X
                    ? CapsuleDirection2D.Horizontal
                    : CapsuleDirection2D.Vertical;
                capsuleCollider.offset = collisionSetting.offset;
                capsuleCollider.enabled = true;
                return capsuleCollider;
            case ColliderShape.Box:
                boxCollider.size = new Vector2(collisionSetting.boxSize.x, collisionSetting.boxSize.y);
                boxCollider.offset = collisionSetting.offset;
                boxCollider.enabled = true;
                return boxCollider;
            default:
                return null;
        }
    }

    public void Deactivate()
    {
        switch (lastactiveShape)
        {
            case ColliderShape.Circle:
                circleCollider.enabled = false;
                break;
            case ColliderShape.Capsule:
                capsuleCollider.enabled = false;
                break;
            case ColliderShape.Box:
                boxCollider.enabled = false;
                break;
        }
        gameObject.SetActive(false);
        isActive = false;
    }
    
    
    public AttackInfo GetAttackInfo()
    {
        return attackInfo;
    }

}
