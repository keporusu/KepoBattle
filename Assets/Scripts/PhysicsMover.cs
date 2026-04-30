using System;
using Unity.VisualScripting;
using UnityEngine;

public class PhysicsMover : MonoBehaviour
{
    [SerializeField] private float gravity = 1.0f;
    private Rigidbody2D rigidbody_cache;
    private Collider2D collider_cache;
    
    private float accumulatedTime=0.0f;
    private bool isAir = true;
    private float jumpingPower = 0.0f;
    
    public bool IsAir => isAir;
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        rigidbody_cache = GetComponent<Rigidbody2D>();
        collider_cache = GetComponent<Collider2D>();
        if (rigidbody_cache == null || collider_cache == null)
        {
            Debug.LogError("Rigidbody or Collider component is missing");
            enabled = false;
            return;
        }
    }
    
    private void FixedUpdate()
    {
        Vector2 movePoint = rigidbody_cache.position;
        
        //空中ではないなら以下の処理をしない
        if (!isAir) return;
        
        
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
        
        
        
        //最終処理
        rigidbody_cache.MovePosition(movePoint);
    }

    
    private void OnTriggerEnter2D(Collider2D other)
    {
        //ジャンプ中は足場無視
        if(jumpingPower > 0.0f)return;
        
        Vector2 footPosition = rigidbody_cache.position - new Vector2(0.0f, collider_cache.bounds.extents.y)-Vector2.up*0.01f;
        //RaycastHit2D hit = Physics2D.Raycast(footPosition, Vector2.down, 0.1f);
        RaycastHit2D hit = Physics2D.Raycast(footPosition, Vector2.up, 0.5f);
        if (hit.collider != null)
        {
            isAir = false;
            rigidbody_cache.MovePosition(hit.point + new Vector2(0.0f, collider_cache.bounds.extents.y));
            Debug.Log($"floor: hit = {hit.point}");
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

}
