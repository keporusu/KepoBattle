using UnityEngine;

namespace Interfaces
{
    public interface IAttackInfoGetter
    {
        EntityId AttackerID { get; }
        public AttackInfo GetAttackInfo();
    }
}