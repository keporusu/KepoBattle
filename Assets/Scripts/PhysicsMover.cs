using System;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Serialization;

[RequireComponent(typeof(Rigidbody2D))]
public class PhysicsMover : MonoBehaviour
{
    [SerializeField] private float gravity = 1.0f;
    [SerializeField] private float weight = 1.0f;
    [SerializeField] private float friction = 1.0f;
    [SerializeField] private GameObject geometryCollider;
    //押し判定をする相手
    private LayerMask _characterLayer;
    private LayerMask _propLayer;
    private LayerMask _groundLayer;

    public Vector2 Velocity { get; private set; }
    
    //キャッシュ
    private Rigidbody2D _rigidbody_Cache;
    private Collider2D _geometryCollider_Cache;
    private Rigidbody2D _otherRigidbody_Cache;
    
    //常にUpdateする値
    protected float MovingVelocity = 0.0f; //移動時の速度
    protected Vector2 ForceVelocity = Vector2.zero; //無理に掛かる速度
    
    //状態
    private bool _hasOtherCharacter=false;
    private bool _isAir = true;
    protected bool IsBraking = false;
    private float _snapGroundY = float.NaN;
    
    public bool IsAir => _isAir;
    private bool CanPushObject(Collider2D other)
    {
        return (_characterLayer.value & (1 << other.gameObject.layer)) > 0 ||
               (_propLayer.value & (1 << other.gameObject.layer)) > 0;
    }
    
    //通知
    public event System.Action OnGround;
    public event System.Action OnForceAir;
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        _rigidbody_Cache = GetComponent<Rigidbody2D>();

        if (geometryCollider == null)
        {
            Debug.LogError("geometryCollider is not set", this);
            enabled = false;
            return;
        }

        _geometryCollider_Cache = geometryCollider.GetComponent<Collider2D>();
        if (_geometryCollider_Cache == null)
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
        _characterLayer=LayerMask.GetMask("Character");
        _propLayer=LayerMask.GetMask("Prop");
        _groundLayer=LayerMask.GetMask("Ground");
    }
    
    private void FixedUpdate()
    {

        //****移動****
        Vector2 movePoint = _rigidbody_Cache.position;
        
        //ブレーキ処理
        if (IsBraking)
        {
            if (MovingVelocity > 0.0f)
            {
                MovingVelocity = Mathf.Max(0.0f,MovingVelocity-friction*Time.deltaTime);
            }
            else if (MovingVelocity < 0.0f)
            {
                MovingVelocity = Mathf.Min(0.0f,MovingVelocity+friction*Time.deltaTime);
            }
            
            if (MovingVelocity == 0.0f)
            {
                IsBraking = false;
            }
        }
        
        //移動処理
        movePoint += MovingVelocity *Time.fixedDeltaTime* Vector2.right;

        //空中での処理
        if (_isAir)
        {
            //重力による上方向減衰
            ForceVelocity += gravity * Time.fixedDeltaTime * Vector2.down;
        }
        //地面についているときの処理
        else
        {
            if (ForceVelocity.x > 0.0f)
            {
                ForceVelocity.x = Mathf.Max(0.0f, ForceVelocity.x - friction * Time.deltaTime);
            }
            else if (ForceVelocity.x < 0.0f)
            {
                ForceVelocity.x = Mathf.Min(0.0f, ForceVelocity.x + friction * Time.deltaTime);
            }
        }
        
        //無理矢理掛かる力による移動
        //質量が軽いほどよく飛ぶ
        movePoint += ForceVelocity * (Vector2.right + Vector2.up) / Mathf.Max(0.0f, weight) * Time.fixedDeltaTime;
        
        //キャラクター押しあたり判定
        if (_hasOtherCharacter)
        {
            if (_rigidbody_Cache.position.x > _otherRigidbody_Cache.position.x)
            {
                movePoint += 1.5f*Time.fixedDeltaTime * Vector2.right;
            }
            else
            {
                movePoint -= 1.5f*Time.fixedDeltaTime * Vector2.right;
            }
        }
        
        //着地スナップ補正
        if (!float.IsNaN(_snapGroundY))
        {
            movePoint.y = _snapGroundY;
            _snapGroundY = float.NaN;
        }
        
        //最終処理
        Velocity = (movePoint - _rigidbody_Cache.position) / Time.fixedDeltaTime;
        _rigidbody_Cache.MovePosition(movePoint);
        
        
        
        //**********衝突判定関連****************
        
        //地面端から落ちるか？
        var bottomOffset = _geometryCollider_Cache.bounds.min.y - _rigidbody_Cache.position.y;
        var groundOrigin = new Vector2(movePoint.x, movePoint.y + bottomOffset + 0.05f);
        bool wasAir = _isAir;
        _isAir = !Physics2D.Raycast(groundOrigin, Vector2.down, 0.15f, _groundLayer);
        if (!wasAir && _isAir)
        {
            OnForceAir?.Invoke();
        }
    }

    private void OnHitGeometry(Collider2D other)
    {
        if (!other.CompareTag("Geometry Channel")) return;
        
        //相手がキャラクターの場合はキャッシュする
        if (CanPushObject(other))
        {
            var otherParent=other.transform.parent.gameObject;
            _otherRigidbody_Cache=otherParent.GetComponent<Rigidbody2D>();
            _hasOtherCharacter = true;
            Debug.Log(gameObject.name+": Catch Character");
            return;
        }
        
        //足場の時
        if (other.bounds.max.y < _geometryCollider_Cache.bounds.max.y)
        {
            //上方向に動いているときは足場無視
            if(Velocity.y > 0.0f) return;

            float groundTop = other.bounds.max.y;
            _isAir = false;
            _snapGroundY = groundTop + _geometryCollider_Cache.bounds.extents.y;
            
            //地面についた場合、上下方向にかかっている速度は0にする
            ForceVelocity.y = 0.0f;
            
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
            _otherRigidbody_Cache = null;
            _hasOtherCharacter = false;
            Debug.Log(gameObject.name+": Lost Character");
            return;
        }
    }

    /// <summary>
    /// 自分に特定の方向に速度を加える
    /// </summary>
    /// <param name="velocity">加える速度</param>
    /// <param name="forceMode">一回停止させてから力を加えるか？</param>
    public void AddForceVelocity(Vector2 velocity, bool forceMode)
    {
        if (forceMode)
        {
            MovingVelocity = 0.0f;
            ForceVelocity = new Vector2();
        }
        
        ForceVelocity += velocity;
        if (velocity.y > 0.0f)
        {
            _isAir = true;
        }
    }

    public void ResetAll()
    {
        MovingVelocity = 0.0f; //移動時の力
        ForceVelocity = new Vector2(); //攻撃などで無理にかかる速度
    
        //状態
        _hasOtherCharacter=false;
        _isAir = true;
        IsBraking = false;
    }

}
