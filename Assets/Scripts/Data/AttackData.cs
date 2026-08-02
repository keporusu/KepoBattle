using System.Collections.Generic;
using UnityEngine;
using Core.Constants;

namespace Data
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