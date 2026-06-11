using System;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Serialization;

[RequireComponent(typeof(Rigidbody2D))]
public class PhysicsMover : MonoBehaviour
{
    [SerializeField] private float gravity = 1.0f;
    [SerializeField] private float friction = 1.0f;
    [SerializeField] private GameObject geometryCollider;
    //押し判定をする相手
    [SerializeField] private LayerMask characterLayer;

    public Vector2 Velocity { get; private set; }
    
    //キャッシュ
    private Rigidbody2D rigidbody_Cache;
    private Collider2D geometryCollider_Cache;
    private PhysicsMover otherMover_Cache;
    private Collider2D otherCollider_Cache;
    private Rigidbody2D otherRigidbody_Cache;
    
    //常にUpdateする値
    private float accumulatedTime=0.0f;
    protected float movingPower = 0.0f; //移動時の力
    protected Vector2 forcePower = new Vector2(); //攻撃などで無理にかかる力
    
    //状態
    private bool hasOtherCharacter=false;
    private bool isAir = true;
    protected bool isBraking = false;
    private bool isForcing = false;
    private float snapGroundY = float.NaN;
    
    public bool IsAir => isAir;
    
    //通知
    public event System.Action OnGround;
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        rigidbody_Cache = GetComponent<Rigidbody2D>();

        if (geometryCollider == null)
        {
            Debug.LogError("geometryCollider is not set", this);
            enabled = false;
            return;
        }

        geometryCollider_Cache = geometryCollider.GetComponent<Collider2D>();
        if (geometryCollider_Cache == null)
        {
            Debug.LogError("Collider2D not found on geometryCollider", this);
            enabled = false;
            return;
        }

        var geometryHitNotifier = geometryCollider.GetComponent<GeometryHitNotifier>();
        if (geometryHitNotifier == null)
        {
            Debug.LogError("GeometryHitNotifier is missing", this);
            enabled = false;
            return;
        }

        geometryHitNotifier.OnHit += OnHitGeometry;

    }
    
    private void FixedUpdate()
    {
        Vector2 movePoint = rigidbody_Cache.position;
        
        //無理矢理掛かる力の減衰(横)
        if (isForcing)
        {
            if (forcePower.x > 0.0f)
            {
                forcePower.x = Mathf.Max(0.0f,forcePower.x-20.0f*Time.deltaTime);
            }
            else if (forcePower.x < 0.0f)
            {
                forcePower.x = Mathf.Min(0.0f,forcePower.x+20.0f*Time.deltaTime);
            }
            //無理矢理掛かる力の減衰(縦)
            if (forcePower.y > 0.0f)
            {
                forcePower.y = Mathf.Max(0.0f,forcePower.y-20.0f*Time.fixedDeltaTime);
            }
            else if (forcePower.y < 0.0f)
            {
                forcePower.y = Mathf.Min(0.0f,forcePower.y+20.0f*Time.fixedDeltaTime);
            }

            if (forcePower == Vector2.zero)
            {
                isForcing = false;
            }
        }
        
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
            //落下処理
            movePoint += accumulatedTime * gravity * Vector2.down;
            accumulatedTime+=Time.fixedDeltaTime;
        }
        
        //無理矢理掛かる力による移動
        movePoint += forcePower.x *Time.fixedDeltaTime* Vector2.right;
        movePoint += forcePower.y *Time.fixedDeltaTime* Vector2.up;
        
        //キャラクター押しあたり判定
        if (hasOtherCharacter)
        {
            if (rigidbody_Cache.position.x > otherRigidbody_Cache.position.x)
            {
                movePoint += 1.5f*Time.fixedDeltaTime * Vector2.right;
            }
            else
            {
                movePoint -= 1.5f*Time.fixedDeltaTime * Vector2.right;
            }
        }
        
        //最終処理
        Velocity = (movePoint - rigidbody_Cache.position) / Time.fixedDeltaTime;
        rigidbody_Cache.MovePosition(movePoint);
    }

    private void OnHitGeometry(Collider2D other)
    {
        if (!other.CompareTag("Geometry Channel")) return;
        
        //相手がキャラクターの場合はキャッシュする
        if ((characterLayer.value & (1 << other.gameObject.layer)) > 0)
        {
            var otherParent=other.transform.parent.gameObject;
            otherMover_Cache = otherParent.GetComponent<PhysicsMover>();
            otherCollider_Cache = other;
            otherRigidbody_Cache=otherParent.GetComponent<Rigidbody2D>();
            hasOtherCharacter = true;
            Debug.Log("Catch Character");
            return;
        }
        
        //足場の時
        if (other.bounds.max.y < geometryCollider_Cache.bounds.max.y)
        {
            //上方向に動いているときは足場無視
            if(Velocity.y > 0.0f) return;

            float groundTop = other.bounds.max.y;
            isAir = false;
            snapGroundY = groundTop + geometryCollider_Cache.bounds.extents.y;
            
            //地面についた場合、上下方向にかかっている力は0にする
            forcePower.y = 0.0f;
            
            //接地通知
            OnGround?.Invoke();
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!other.CompareTag("Geometry Channel")) return;
            
        //相手がキャラクターの場合はキャッシュ解除
        if ((characterLayer.value & (1 << other.gameObject.layer)) > 0)
        {
            otherMover_Cache = null;
            otherCollider_Cache = null;
            otherRigidbody_Cache = null;
            hasOtherCharacter = false;
            Debug.Log("Lost Character");
            return;
        }
    }

    /// <summary>
    /// 自分に特定の方向に力を加える
    /// </summary>
    /// <param name="force">加える力</param>
    /// <param name="forceMode">一回停止させてから力を加えるか？</param>
    public void ForcePower(Vector2 force, bool forceMode)
    {
        if (forceMode)
        {
            accumulatedTime=0.0f;
            movingPower = 0.0f;
            forcePower = new Vector2();
        }
        
        forcePower += force;
        isForcing = true;
        if (force.y > 0.0f)
        {
            isAir = true;
            accumulatedTime = 0.0f;
        }
    }

    public void ResetAll()
    {
        movingPower = 0.0f; //移動時の力
        forcePower = new Vector2(); //攻撃などで無理にかかる力
    
        //状態
        hasOtherCharacter=false;
        isAir = true;
        isBraking = false;
        isForcing = false;
        accumulatedTime = 0.0f;
    }

}
