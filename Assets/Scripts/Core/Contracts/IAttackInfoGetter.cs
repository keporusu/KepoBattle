using UnityEngine;
using Data;

namespace Core.Contracts
{
    
    //AttackInfoを決定するために必要な相手の情報
    public struct AdditionalInfoForAttackCalculation
    {
        public Vector2 OtherPosition; //相手の位置
        public float OtherBlowAlpha; //相手の吹き飛び補正
    }
    
    
    //TODO:削除予定
    public interface IAttackInfoGetter
    {
        EntityId AttackerID { get; }
        public AttackInfo? CalculateAttackInfo(AdditionalInfoForAttackCalculation additionalInfo);
    }
}