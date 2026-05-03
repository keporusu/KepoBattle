using System;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Serialization;

public class PhysicsMover : MonoBehaviour
{
    [SerializeField] private float gravity = 1.0f;
    [SerializeField] private float friction = 1.0f;
    [SerializeField] private Collider2D geometryCollider;
    //押し判定をする相手
    [SerializeField] private LayerMask characterLayer;
    
    private Rigidbody2D rigidbody_cache;
    
    //相手キャラクター
    private PhysicsMover otherMover_cache;
    private Collider2D otherCollider_cache;
    private Rigidbody2D otherRigidbody_cache;
    private bool hasOtherCharacter=false;
    
    private float accumulatedTime=0.0f;
    private float jumpingPower = 0.0f;
    private float movingPower = 0.0f;
    
    private bool isAir = true;
    private bool isBraking = false;
    private float snapGroundY = float.NaN;
    
    public bool IsAir => isAir;
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        rigidbody_cache = GetComponent<Rigidbody2D>();
        if (rigidbody_cache == null || geometryCollider == null)
        {
            Debug.LogError("Rigidbody or Collider component is missing");
            enabled = false;
            return;
        }
    }
    
    private void FixedUpdate()
    {
        Vector2 movePoint = rigidbody_cache.position;
        
        //ブレーキ処理
        if (isBraking)
        {
            if (movingPower > 0.0f)
            {
                movingPower = Mathf.Max(0.0f,movingPower-friction*Time.deltaTime);
            }
            else if (movingPower < 0.0f)
            {
                movingPower = Mathf.Min(0.0f,movingPower+friction*Time.deltaTime);
            }
            
            if (movingPower == 0.0f)
            {
                isBraking = false;
            }
        }
        
        //移動処理
        movePoint += movingPower *Time.fixedDeltaTime* Vector2.right;
        
        //着地スナップ補正
        if (!float.IsNaN(snapGroundY))
        {
            movePoint.y = snapGroundY;
            snapGroundY = float.NaN;
        }

        //空中での処理
        if (isAir)
        {
            if (jumpingPower > 0.0f)
            {
                //ジャンプ処理
                movePoint += jumpingPower * Vector2.up;
                jumpingPower = Mathf.Max(0.0f, jumpingPower-Time.fixedDeltaTime);
            }
            else
            {
                //落下処理
                movePoint += accumulatedTime * gravity * Vector2.down;
                accumulatedTime+=Time.fixedDeltaTime;
            }
        }
        
        //キャラクター押しあたり判定
        if (hasOtherCharacter)
        {
            if (rigidbody_cache.position.x > otherRigidbody_cache.position.x)
            {
                movePoint += 0.5f*Time.fixedDeltaTime * Vector2.right;
            }
            else
            {
                movePoint -= 0.5f*Time.fixedDeltaTime * Vector2.right;
            }
        }
        
        //最終処理
        rigidbody_cache.MovePosition(movePoint);
    }
    
    
    
    private void OnTriggerEnter2D(Collider2D other)
    {
        
        if (!other.CompareTag("Geometry Channel")) return;
        
        //相手がキャラクターの場合はキャッシュする
        if ((characterLayer.value & (1 << other.gameObject.layer)) > 0)
        {
            var otherParent=other.transform.parent.gameObject;
            otherMover_cache = otherParent.GetComponent<PhysicsMover>();
            otherCollider_cache = other;
            otherRigidbody_cache=otherParent.GetComponent<Rigidbody2D>();
            hasOtherCharacter = true;
            Debug.Log("Catch Character");
            return;
        }
        
        //足場の時
        if (other.bounds.max.y < geometryCollider.bounds.max.y)
        {
            //ジャンプ中は足場無視
            if(jumpingPower > 0.0f) return;

            float groundTop = other.bounds.max.y;
            isAir = false;
            snapGroundY = groundTop + geometryCollider.bounds.extents.y;
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!other.CompareTag("Geometry Channel")) return;
            
        //相手がキャラクターの場合はキャッシュ解除
        if ((characterLayer.value & (1 << other.gameObject.layer)) > 0)
        {
            otherMover_cache = null;
            otherCollider_cache = null;
            otherRigidbody_cache = null;
            hasOtherCharacter = false;
            Debug.Log("Lost Character");
            return;
        }
    }

    public void StartJump(float power)
    {
        isAir = true;
        jumpingPower = power;
        accumulatedTime = 0.0f;
    }

    public void StopJump()
    {
        jumpingPower *= 0.3f;
    }

    public void Move(float power)
    {
        isBraking = false;
        movingPower = power;
    }

    public void StopMove()
    {
        isBraking = true;
    }

}
