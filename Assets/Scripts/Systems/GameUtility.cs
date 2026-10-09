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


        //プレイヤーはレベル読み込みで生成・破棄されるため、Start で一度だけ探すのではなく
        //プレイヤー側から登録してもらう。破棄されていれば Unity の null 判定で null になる
        private PlayerController playerController_Cache;

        /// <summary>
        /// 操作対象のプレイヤーを登録する
        /// </summary>
        public void RegisterPlayer(PlayerController player)
        {
            if (playerController_Cache != null && playerController_Cache != player)
            {
                Debug.LogWarning($"[{GetType().Name}] プレイヤーが既に登録されているため、{player.gameObject.name} で上書きします");
            }

            playerController_Cache = player;
        }

        /// <summary>
        /// プレイヤーの登録を解除する
        /// </summary>
        public void UnregisterPlayer(PlayerController player)
        {
            if (playerController_Cache == player)
            {
                playerController_Cache = null;
            }
        }
        
        public void RespawnPlayer()
        {
            if (playerController_Cache == null) return;
            playerController_Cache.Respawn();
        }
        
        /// <summary>
        /// カメラを揺らす処理
        /// </summary>
        /// <param name="size">揺らす大きさ</param>
        /// <param name="duration">揺らす時間</param>
        public void SetCameraShake(Vector2 size, float duration)
        {
            if (playerController_Cache == null) return;
            if (playerController_Cache.gameObject.TryGetComponent(out CameraController controller))
            {
                controller.SetCameraShake(size, duration);
            }
        }
        /// <summary>
        /// マウスのワールド座標の取得
        /// プレイヤーがいない状態(レベル編集中など)でも使えるよう、メインカメラから直接求める
        /// </summary>
        public Vector2 GetMouseWorldPos()
        {
            var mainCamera = UnityEngine.Camera.main;
            if (mainCamera == null || Mouse.current == null) return Vector2.zero;

            Vector2 screenPos = Mouse.current.position.ReadValue();
            Vector3 world = mainCamera.ScreenToWorldPoint(screenPos);
            return world; // Vector2への暗黙変換でzは捨てられる
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
            if (playerController_Cache == null) return;
            Vector3 spawnDir = playerController_Cache.IsFacingRight ? Vector3.right : -Vector3.right;
            Vector3 spawnPos = playerController_Cache.Position;
            spawnPos += Vector3.up * 3.0f + spawnDir * 2.0f;
            Instantiate(ballPrefab, spawnPos, Quaternion.identity);
        }

        private void SpawnEnemy()
        {
            if (playerController_Cache == null) return;
            Vector3 spawnDir = playerController_Cache.IsFacingRight ? Vector3.right : -Vector3.right;
            Vector3 spawnPos = playerController_Cache.Position;
            spawnPos += Vector3.up * 3.0f + spawnDir * 5.0f;
            Instantiate(enemyPrefab, spawnPos, Quaternion.identity);
        }

        private void SpawnBomb()
        {
            if (playerController_Cache == null) return;
            Vector3 spawnDir = playerController_Cache.IsFacingRight ? Vector3.right : -Vector3.right;
            Vector3 spawnPos = playerController_Cache.Position;
            spawnPos += Vector3.up * 3.0f + spawnDir * 2.0f;
            Instantiate(bombPrefab, spawnPos, Quaternion.identity);
        }

    }
}
