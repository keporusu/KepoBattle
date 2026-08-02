using System.Collections.Generic;
using UnityEngine;
using Core.Constants;

namespace Battle.Character
{
    public enum AttackName
    {
        Attack1,
        Attack2,
        Attack3
    }
    
    [CreateAssetMenu(menuName="ScriptableObjects/AttackData")]
    public class AttackData : ScriptableObject
    {
        public AttackName attackName;
        public List<AttackCollisionSettingForAction> collisionSettings;
    }
}