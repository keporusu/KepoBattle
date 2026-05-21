using System;
using UnityEngine;


//コリジョン形状
public enum ColliderShape
{
    Circle,
    Capsule,
    Box,
}
public enum CapsuleDirection { X, Y, Z }


// 攻撃コリジョンの形状・攻撃情報の設定
[Serializable]
public struct AttackCollisionSetting
{
    public ColliderShape shape;

    // Circle
    public float circleRadius;

    // Capsule
    public float capsuleRadius;
    public float capsuleHeight;
    public CapsuleDirection capsuleDirection;

    // Box
    public Vector2 boxSize;

    // 共通
    public Vector2 offset;
    [Range(0f, 1f)] public float spanStart;
    [Range(0f, 1f)] public float spanEnd;
    public Vector2 attackPower;
}


//攻撃情報のみを抽出したもの
public struct AttackInfo
{
    /// <summary>
    /// 原則として0以上の値を入れること
    /// </summary>
    public Vector2 attackPower;
}

//ヒット情報
// struct HitInfo
// {
//     public float 
// }
