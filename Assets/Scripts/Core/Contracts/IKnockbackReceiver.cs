using UnityEngine;

namespace Core.Contracts
{
    public interface IKnockbackReceiver
    {
        /// <summary>
        /// 自分に特定の方向に速度を加える
        /// </summary>
        /// <param name="velocity">加える速度</param>
        /// <param name="instigator">攻撃者のオブジェクト</param>>
        public  void ForceKnockback(Vector2 velocity, GameObject instigator = null);
    }
}