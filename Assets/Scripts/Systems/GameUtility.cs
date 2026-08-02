using Battle.Character.Player;
using UnityEngine;
using UnityEngine.InputSystem;

public class GameUtility : MonoBehaviour
{
    [SerializeField] private GameObject player;
    [SerializeField] private GameObject enemyPrefab;
    [SerializeField] private GameObject ballPrefab;
    
    
    private PlayerController playerController_Cache;
    private void Start()
    {
        if (!player.TryGetComponent(out playerController_Cache))
        {
            throw new MissingComponentException($"[{GetType().Name}] PlayerController が {player.gameObject.name} に見つかりません");
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
    }

    public void RespawnPlayer()
    {
        playerController_Cache.Respawn();
    }

    private void SpawnBall()
    {
        Vector3 spawnDir = playerController_Cache.IsForward ? Vector3.right : -Vector3.right;
        Vector3 spawnPos = player.transform.position;
        spawnPos += Vector3.up * 3.0f + spawnDir * 2.0f;
        Instantiate(ballPrefab, spawnPos, Quaternion.identity);
    }
    
    private void SpawnEnemy()
    {
        Vector3 spawnDir = playerController_Cache.IsForward ? Vector3.right : -Vector3.right;
        Vector3 spawnPos = player.transform.position;
        spawnPos += Vector3.up * 3.0f + spawnDir * 5.0f;
        Instantiate(enemyPrefab, spawnPos, Quaternion.identity);
    }
}
