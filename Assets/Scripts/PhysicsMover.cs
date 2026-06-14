using System;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Serialization;

[RequireComponent(typeof(Rigidbody2D))]
public class PhysicsMover : MonoBehaviour
{
    [SerializeField] private float gravity = 1.0f;
    [SerializeField] private float mass = 1.0f;
    [SerializeField] private float friction = 1.0f;
    [SerializeField] private GameObject geometryCollider;
    //押し判定をする相手
    private LayerMask characterLayer;
    private LayerMask propLayer;

    public Vector2 Velocity { get; private set; }
    
    //キャッシュ
    private Rigidbody2D rigidbody_Cache;
    private Collider2D geometryCollider_Cache;
    private Rigidbody2D otherRigidbody_Cache;
    
    //常にUpdateする値
    protected float movingVelocity = 0.0f; //移動時の速度
    protected Vector2 forceVelocity = Vector2.zero; //無理に掛かる速度
    
    //状態
    private bool hasOtherCharacter=false;
    private bool isAir = true;
    protected bool isBraking = false;
    private bool isForcing = false;
    private float snapGroundY = float.NaN;
    
    public bool IsAir => isAir;
    private bool CanPushObject(Collider2D other)
    {
        return (characterLayer.value & (1 << other.gameObject.layer)) > 0 ||
               (propLayer.value & (1 << other.gameObject.layer)) > 0;
    }
    
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
        
        //レイヤー取得
        characterLayer=LayerMask.GetMask("Character");
        propLayer=LayerMask.GetMask("Prop");
    }
    
    private void FixedUpdate()
    {
        Vector2 movePoint = rigidbody_Cache.position;
        
        //ブレーキ処理
        if (isBraking)
        {
            if (movingVelocity > 0.0f)
            {
                movingVelocity = Mathf.Max(0.0f,movingVelocity-friction*Time.deltaTime);
            }
            else if (movingVelocity < 0.0f)
            {
                movingVelocity = Mathf.Min(0.0f,movingVelocity+friction*Time.deltaTime);
            }
            
            if (movingVelocity == 0.0f)
            {
                isBraking = false;
            }
        }
        
        //移動処理
        movePoint += movingVelocity *Time.fixedDeltaTime* Vector2.right;
        
        //着地スナップ補正
        if (!float.IsNaN(snapGroundY))
        {
            movePoint.y = snapGroundY;
            snapGroundY = float.NaN;
        }

        //空中での処理
        if (isAir)
        {
            //重力による上方向減衰
            forceVelocity += gravity * Time.fixedDeltaTime * Vector2.down;
        }
        //地面についているときの処理
        else
        {
            if (forceVelocity.x > 0.0f)
            {
                forceVelocity.x = Mathf.Max(0.0f, forceVelocity.x - friction * Time.deltaTime);
            }
            else if (forceVelocity.x < 0.0f)
            {
                forceVelocity.x = Mathf.Min(0.0f, forceVelocity.x + friction * Time.deltaTime);
            }
        }
        
        //無理矢理掛かる力による移動
        //質量が軽いほどよく飛ぶ
        movePoint += forceVelocity * (Vector2.right + Vector2.up) / Mathf.Max(0.0f, mass) * Time.fixedDeltaTime;
        
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
        if (CanPushObject(other))
        {
            var otherParent=other.transform.parent.gameObject;
            otherRigidbody_Cache=otherParent.GetComponent<Rigidbody2D>();
            hasOtherCharacter = true;
            Debug.Log(gameObject.name+": Catch Character");
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
            
            //地面についた場合、上下方向にかかっている速度は0にする
            forceVelocity.y = 0.0f;
            
            //接地通知
            OnGround?.Invoke();
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!other.CompareTag("Geometry Channel")) return;
            
        //相手がキャラクターの場合はキャッシュ解除
        if (CanPushObject(other))
        {
            otherRigidbody_Cache = null;
            hasOtherCharacter = false;
            Debug.Log(gameObject.name+": Lost Character");
            return;
        }
    }

    /// <summary>
    /// 自分に特定の方向に速度を加える
    /// </summary>
    /// <param name="velocity">加える速度</param>
    /// <param name="forceMode">一回停止させてから力を加えるか？</param>
    public void ForceVelocity(Vector2 velocity, bool forceMode)
    {
        if (forceMode)
        {
            movingVelocity = 0.0f;
            forceVelocity = new Vector2();
        }
        
        forceVelocity += velocity;
        isForcing = true;
        if (velocity.y > 0.0f)
        {
            isAir = true;
        }
    }

    public void ResetAll()
    {
        movingVelocity = 0.0f; //移動時の力
        forceVelocity = new Vector2(); //攻撃などで無理にかかる速度
    
        //状態
        hasOtherCharacter=false;
        isAir = true;
        isBraking = false;
        isForcing = false;
    }

}
