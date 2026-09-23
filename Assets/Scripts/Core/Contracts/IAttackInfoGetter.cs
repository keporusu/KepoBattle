using UnityEngine;
using Data;

namespace Core.Contracts
{
    public interface IAttackInfoGetter
    {
        EntityId AttackerID { get; }
        public AttackInfo CalculateAttackInfo(Vector2 otherPosition);
    }
}