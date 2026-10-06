using System;
using UnityEngine;
using UnityEngine.Serialization;

namespace Data
{
    //コリジョン形状
    public enum ColliderShape
    {
        Circle,
        Capsule,
        Box,
    }
    public enum CapsuleDirection { X, Y, Z }
    
    
    /// <summary>
    /// Fixed: 設定されたAttackPowerをそのまま用いる
    /// Velocity: 自身のルートオブジェクトの速度を用いる
    /// Radial: 自身のルートオブジェクトから相手への向きを用いる
    /// </summary>
    public enum AttackPowerType
    {
        Fixed,
        Velocity,
        Radial,
    }

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
        public float damage;
        public bool ignoreDuration;
        
        public AttackPowerType attackPowerType;
        public float velocityAlpha; //Velocityのみ
        public float power; //RadialとFixedのみ
        [FormerlySerializedAs("attackPower")] public Vector2 direction; //Fixedのみ

    }

    // AttackCollisionSetting に、アクション中のどの区間で発生させるかを加えたもの
    [Serializable]
    public struct AttackCollisionSettingForAction
    {
        public AttackCollisionSetting collision;

        [Range(0f, 1f)] public float spanStart;
        [Range(0f, 1f)] public float spanEnd;
    }


    //攻撃情報のみを抽出したもの
    public struct AttackInfo
    {
        /// <summary>
        /// Character: 向き付きの吹き飛びパワー
        /// Prop: スカラーの吹き飛びパワー（xのみ。yは使用しない）
        /// </summary>
        public Vector2 attackPower;

        public float damage;
        
        public bool ignoreDuration;
    }
    
    
    //トリガ用
    [Serializable]
    public struct TriggerShapeSetting
    {
        //形状
        public ColliderShape shape;

        // Circle
        public float circleRadius;

        // Capsule
        public float capsuleRadius;
        public float capsuleHeight;
        public CapsuleDirection capsuleDirection;

        // Box
        public Vector2 boxSize;
    }
}
