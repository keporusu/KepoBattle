using System;
using UnityEngine;
using DG.Tweening;
using Systems;

namespace Components.Camera
{
    /// <summary>
    /// メインカメラを 1P に追従させる
    /// プレイヤーが複数いても奪い合いにならないよう、プレイヤーではなくカメラ側に1つだけ置く
    /// </summary>
    public class CameraController : MonoBehaviour
    {
        public static CameraController Main { get; private set; }

        [SerializeField] private Transform camera;
        [SerializeField] private Vector3 offset;

        //移動範囲。レベルを読み込むとレベルデータの値で上書きされる
        [SerializeField] private float maxHorizontal=1e5f;
        [SerializeField] private float minHorizontal=-1e5f;
        [SerializeField] private float maxVertical=1e5f;
        [SerializeField] private float minVertical=-1e5f;

        //状態
        private Vector3 cameraShakeOffset;

        void Awake()
        {
            if (Main != null && Main != this)
            {
                Debug.LogWarning($"[{GetType().Name}] CameraController が複数あります。{gameObject.name} は追従を行いません");
                enabled = false;
                return;
            }

            Main = this;

            //未設定なら、自身か MainCamera タグのカメラを動かす
            if (camera == null)
            {
                if (TryGetComponent(out UnityEngine.Camera ownCamera))
                {
                    camera = ownCamera.transform;
                }
                else
                {
                    var mainCamera = UnityEngine.Camera.main;
                    if (mainCamera == null)
                        throw new MissingReferenceException($"[{GetType().Name}] カメラが未設定で、MainCamera タグのカメラもシーンに存在しません");
                    camera = mainCamera.transform;
                }
            }
        }

        private void OnDestroy()
        {
            if (Main == this)
            {
                Main = null;
            }
        }

        private void LateUpdate()
        {
            //編集中はレベル作成システムがカメラを動かすので追従しない
            if (LevelManager.IsEditMode) return;

            //プレイヤーの移動を反映した後に追従する
            var gameUtility = GameUtility.Instance;
            if (gameUtility == null) return;

            var mainPlayer = gameUtility.MainPlayer;
            if (mainPlayer == null) return;

            AdjustCameraPosition(mainPlayer.Position);
        }

        /// <summary>
        /// 移動範囲を設定する
        /// </summary>
        /// <param name="min">左下</param>
        /// <param name="max">右上</param>
        public void SetBounds(Vector2 min, Vector2 max)
        {
            minHorizontal = min.x;
            minVertical = min.y;
            maxHorizontal = max.x;
            maxVertical = max.y;
        }

        /// <summary>
        /// カメラの位置を合わせる
        /// </summary>
        /// <param name="position">追従する位置</param>
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

    }
}
