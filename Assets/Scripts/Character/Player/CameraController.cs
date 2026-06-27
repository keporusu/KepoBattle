using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Character.Player
{
    public class CameraController : MonoBehaviour
    {
        [SerializeField] private Transform camera;
        [SerializeField] private Vector3 offset;
        [SerializeField] private float maxHorizontal=1e5f;
        [SerializeField] private float minHorizontal=-1e5f;
        [SerializeField] private float maxVertical=1e5f;
        [SerializeField] private float minVertical=-1e5f;


        /// <summary>
        /// カメラの位置を合わせる
        /// </summary>
        /// <param name="position">プレイヤーの位置</param>
        public void AdjustCameraPosition(Vector2 position)
        {
            float x = Mathf.Clamp(position.x, minHorizontal, maxHorizontal);
            float y = Mathf.Clamp(position.y, minVertical, maxVertical);
            camera.position = new Vector3(x, y, camera.position.z) + offset;
        }


    }
}

