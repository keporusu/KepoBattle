using System;
using UnityEngine;
using DG.Tweening;

namespace Components.Camera
{
    public class CameraController : MonoBehaviour
    {
        [SerializeField] private Transform camera;
        [SerializeField] private Vector3 offset;
        [SerializeField] private float maxHorizontal=1e5f;
        [SerializeField] private float minHorizontal=-1e5f;
        [SerializeField] private float maxVertical=1e5f;
        [SerializeField] private float minVertical=-1e5f;
        
        //状態
        private Vector3 cameraShakeOffset;
        
        //キャッシュ
        private UnityEngine.Camera _camera_Cache;

        void Awake()
        {
            //Prefab から生成した場合はシーン上のカメラを参照できないので、メインカメラを使う
            if (camera == null)
            {
                var mainCamera = UnityEngine.Camera.main;
                if (mainCamera == null)
                    throw new MissingReferenceException($"[{GetType().Name}] カメラが未設定で、MainCamera タグのカメラもシーンに存在しません");
                camera = mainCamera.transform;
            }

            _camera_Cache = camera.GetComponent<UnityEngine.Camera>();
        }

        /// <summary>
        /// カメラの位置を合わせる
        /// </summary>
        /// <param name="position">プレイヤーの位置</param>
        public void AdjustCameraPosition(Vector2 position)
        {
            float x = Mathf.Clamp(position.x, minHorizontal, maxHorizontal);
            float y = Mathf.Clamp(position.y, minVertical, maxVertical);
            camera.position = new Vector3(x, y, camera.position.z) + offset + cameraShakeOffset;
        }

        public void SetCameraShake(Vector2 size, float duration)
        {
            float xSize = size.x;
            float ySize = size.y;

            DOVirtual.Float(xSize, 0.0f, duration, (x) =>
            {
                cameraShakeOffset = new Vector3(x, cameraShakeOffset.y, 0.0f);
            }).SetEase(Ease.OutElastic).SetLink(gameObject);
            DOVirtual.Float(ySize, 0.0f, duration, (y) =>
            {
                cameraShakeOffset = new Vector3(cameraShakeOffset.x, y, 0.0f);
            }).SetEase(Ease.OutElastic).SetLink(gameObject);
        }

        public Vector3 ScreenToWorldPoint(Vector2 screenPos)
        {
            return _camera_Cache.ScreenToWorldPoint(screenPos);
        }

    }
}

