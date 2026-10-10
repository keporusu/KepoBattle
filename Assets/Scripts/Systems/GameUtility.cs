using System.Collections.Generic;
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


        //プレイヤーはレベル読み込みで生成・破棄され、複数いることもあるため
        //Start で一度だけ探すのではなく、プレイヤー側から登録してもらう
        private readonly List<PlayerController> _players = new List<PlayerController>();

        /// <summary>
        /// 1P(playerNumber が最も小さいプレイヤー)
        /// カメラの追従やデバッグ用の生成位置の基準にする
        /// やり直し中の旧プレイヤーは非アクティブになっているので除く
        /// </summary>
        public PlayerController MainPlayer
        {
            get
            {
                PlayerController mainPlayer = null;
                foreach (var player in _players)
                {
                    if (player == null || !player.isActiveAndEnabled) continue;
                    if (mainPlayer == null || player.PlayerNumber < mainPlayer.PlayerNumber)
                    {
                        mainPlayer = player;
                    }
                }

                return mainPlayer;
            }
        }

        /// <summary>
        /// プレイヤーを登録する
        /// </summary>
        public void RegisterPlayer(PlayerController player)
        {
            if (_players.Contains(player)) return;
            _players.Add(player);
        }

        /// <summary>
        /// プレイヤーの登録を解除する
        /// </summary>
        public void UnregisterPlayer(PlayerController player)
        {
            _players.Remove(player);
        }
        
        /// <summary>
        /// レベルをやり直す
        /// レベルを読み込んでいない場合は、各プレイヤーを初期位置に戻すだけにする
        /// </summary>
        public void RestartLevel()
        {
            if (LevelManager.Instance != null && LevelManager.Instance.CurrentLevel != null)
            {
                LevelManager.Instance.RequestRestart();
                return;
            }

            foreach (var player in _players)
            {
                if (player != null) player.Respawn();
            }
        }
        
        /// <summary>
        /// カメラを揺らす処理
        /// </summary>
        /// <param name="size">揺らす大きさ</param>
        /// <param name="duration">揺らす時間</param>
        public void SetCameraShake(Vector2 size, float duration)
        {
            if (CameraController.Main == null) return;
            CameraController.Main.SetCameraShake(size, duration);
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
            //編集中に生成すると、レベルデータに無いオブジェクトが紛れ込むため無効にする
            if (LevelManager.IsEditMode) return;

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
            SpawnNearMainPlayer(ballPrefab, 2.0f);
        }

        private void SpawnEnemy()
        {
            SpawnNearMainPlayer(enemyPrefab, 5.0f);
        }

        private void SpawnBomb()
        {
            SpawnNearMainPlayer(bombPrefab, 2.0f);
        }

        /// <summary>
        /// 1P の前方上空に生成する
        /// レベルのやり直しで一緒に消えるよう、レベルのオブジェクトの下に入れる
        /// </summary>
        /// <param name="prefab">生成するもの</param>
        /// <param name="forwardDistance">前方への距離</param>
        private void SpawnNearMainPlayer(GameObject prefab, float forwardDistance)
        {
            var mainPlayer = MainPlayer;
            if (mainPlayer == null) return;

            Vector3 spawnDir = mainPlayer.IsFacingRight ? Vector3.right : -Vector3.right;
            Vector3 spawnPos = mainPlayer.Position;
            spawnPos += Vector3.up * 3.0f + spawnDir * forwardDistance;

            var parent = LevelManager.Instance != null ? LevelManager.Instance.ObjectRoot : null;
            Instantiate(prefab, spawnPos, Quaternion.identity, parent);
        }

    }
}
