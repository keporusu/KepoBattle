using Components.Camera;
using Components.Controller;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Systems
{
    public class GameUtility : MonoBehaviour
    {
        public static GameUtility Instance{ get; private set; }
        
        //[SerializeField] private GameObject player;
        [SerializeField] private GameObject enemyPrefab;
        [SerializeField] private GameObject ballPrefab;
        [SerializeField] private GameObject bombPrefab;


        private PlayerController playerController_Cache;
        
        public void RespawnPlayer()
        {
            playerController_Cache.Respawn();
        }
        
        /// <summary>
        /// カメラを揺らす処理
        /// </summary>
        /// <param name="size">揺らす大きさ</param>
        /// <param name="duration">揺らす時間</param>
        public void SetCameraShake(Vector2 size, float duration)
        {
            if (playerController_Cache.gameObject.TryGetComponent(out CameraController controller))
            {
                controller.SetCameraShake(size, duration);
            }
        }
        
        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("GameUtilityの重複を破棄します");
                Destroy(gameObject);
                return;
            }

            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }
        
        private void Start()
        {
            playerController_Cache = FindAnyObjectByType<PlayerController>();
            if (playerController_Cache == null)
            {
                throw new MissingComponentException($"[{GetType().Name}] PlayerController がシーンに存在しません");
            }
        }

        private void Update()
        {
            if (Keyboard.current[Key.Digit1].wasPressedThisFrame)
            {
                SpawnEnemy();
            }

            if (Keyboard.current[Key.Digit2].wasPressedThisFrame)
            {
                SpawnBall();
            }
            
            if (Keyboard.current[Key.Digit3].wasPressedThisFrame)
            {
                SpawnBomb();
            }
        }

        private void SpawnBall()
        {
            Vector3 spawnDir = playerController_Cache.IsForward ? Vector3.right : -Vector3.right;
            Vector3 spawnPos = playerController_Cache.Position;
            spawnPos += Vector3.up * 3.0f + spawnDir * 2.0f;
            Instantiate(ballPrefab, spawnPos, Quaternion.identity);
        }

        private void SpawnEnemy()
        {
            Vector3 spawnDir = playerController_Cache.IsForward ? Vector3.right : -Vector3.right;
            Vector3 spawnPos = playerController_Cache.Position;
            spawnPos += Vector3.up * 3.0f + spawnDir * 5.0f;
            Instantiate(enemyPrefab, spawnPos, Quaternion.identity);
        }

        private void SpawnBomb()
        {
            Vector3 spawnDir = playerController_Cache.IsForward ? Vector3.right : -Vector3.right;
            Vector3 spawnPos = playerController_Cache.Position;
            spawnPos += Vector3.up * 3.0f + spawnDir * 2.0f;
            Instantiate(bombPrefab, spawnPos, Quaternion.identity);
        }

    }
}
