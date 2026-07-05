using UnityEngine;

namespace Battle.Interfaces
{
    public interface IAttackInfoGetter
    {
        EntityId AttackerID { get; }
        public AttackInfo GetAttackInfo();
    }
}