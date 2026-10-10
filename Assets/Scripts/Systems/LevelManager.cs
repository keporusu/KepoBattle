using System;
using System.IO;
using System.Threading;
using Components.Camera;
using Components.Identity;
using Core.Contracts;
using Cysharp.Threading.Tasks;
using Data;
using UnityEngine;
using UnityEngine.Networking;

namespace Systems
{
    /// <summary>
    /// レベルデータの読み込みと、レベルデータからのオブジェクト生成を行う
    /// レベルデータを正とし、やり直しは「全破棄 → 再生成」で行う
    /// 状態を巻き戻す方式は、Start でキャッシュするコンポーネントが多く漏れが出るため採らない
    /// </summary>
    public class LevelManager : MonoBehaviour
    {
        public static LevelManager Instance { get; private set; }

        //StreamingAssets 以下のレベル置き場
        public const string LevelDirectory = "Levels";

        [SerializeField] private LevelObjectRegistry registry;
        [SerializeField] private string levelFileName = "SampleLevel.json";
        [SerializeField] private bool loadOnStart = true;

        //状態
        private LevelData _levelData;
        private Transform _objectRoot;
        private bool _isRestartRequested;

        //現在のレベルデータ(読み込み前は null)
        public LevelData CurrentLevel => _levelData;

        //落下で消える高さ
        public float KillHeight => _levelData?.settings.killHeight ?? LevelSettings.DefaultKillHeight;

        //レベルから生成したオブジェクトの親
        //やり直し時にまとめて破棄されるので、プレイ中に生成するものもここに入れる
        public Transform ObjectRoot => _objectRoot;

        //レベルを生成し終えた時の通知
        public event Action OnLevelBuilt;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("LevelManagerの重複を破棄します");
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

        private async UniTaskVoid Start()
        {
            if (!loadOnStart) return;

            var levelData = await LoadFromStreamingAssetsAsync(levelFileName, destroyCancellationToken);
            if (levelData != null)
            {
                SetLevel(levelData);
            }
        }

        private void LateUpdate()
        {
            //同じフレームに複数のプレイヤーが落ちても、やり直しは1回にまとめる
            if (!_isRestartRequested) return;
            _isRestartRequested = false;

            Rebuild();
        }

        /// <summary>
        /// StreamingAssets/Levels からレベルデータを読み込む
        /// WebGL では StreamingAssets が HTTP 経由になるため、UnityWebRequest で非同期に読む
        /// </summary>
        /// <param name="fileName">レベルファイル名</param>
        /// <param name="cancellationToken">キャンセル</param>
        /// <returns>読み込めなければ null</returns>
        public static async UniTask<LevelData> LoadFromStreamingAssetsAsync(string fileName, CancellationToken cancellationToken)
        {
            var path = Path.Combine(Application.streamingAssetsPath, LevelDirectory, fileName);

            //WebGL では URL、それ以外ではファイルパスになるため、ファイルパスなら file:// を付ける
            var url = path.Contains("://") ? path : "file://" + path;

            using var request = UnityWebRequest.Get(url);
            try
            {
                await request.SendWebRequest().WithCancellation(cancellationToken);
            }
            catch (UnityWebRequestException e)
            {
                Debug.LogError($"[{nameof(LevelManager)}] レベルを読み込めませんでした: {url}\n{e.Message}");
                return null;
            }

            var levelData = JsonUtility.FromJson<LevelData>(request.downloadHandler.text);
            if (levelData == null)
            {
                Debug.LogError($"[{nameof(LevelManager)}] レベルの形式が不正です: {url}");
                return null;
            }

            if (levelData.version > LevelData.CurrentVersion)
            {
                Debug.LogWarning($"[{nameof(LevelManager)}] 未対応の新しい形式です(version {levelData.version}): {url}");
            }

            return levelData;
        }

        /// <summary>
        /// レベルデータを差し替え、オブジェクトを生成し直す
        /// </summary>
        public void SetLevel(LevelData levelData)
        {
            _levelData = levelData;
            Rebuild();
        }

        /// <summary>
        /// レベルのやり直しを要求する
        /// 実際の再生成はこのフレームの LateUpdate で行う
        /// </summary>
        public void RequestRestart()
        {
            if (_levelData == null) return;
            _isRestartRequested = true;
        }

        /// <summary>
        /// 現在のオブジェクトを全て破棄し、レベルデータから生成し直す
        /// </summary>
        private void Rebuild()
        {
            ClearObjects();

            _objectRoot = new GameObject("LevelObjects").transform;

            ApplySettings(_levelData.settings);

            foreach (var objectData in _levelData.objects)
            {
                Spawn(objectData);
            }

            OnLevelBuilt?.Invoke();
        }

        private void ClearObjects()
        {
            if (_objectRoot == null) return;

            //Destroy はフレームの最後まで遅れるため、先に非アクティブにして
            //新しいオブジェクトと同じフレームに動いたり当たったりしないようにする
            _objectRoot.gameObject.SetActive(false);
            Destroy(_objectRoot.gameObject);
            _objectRoot = null;
        }

        private void ApplySettings(LevelSettings settings)
        {
            if (CameraController.Main != null)
            {
                CameraController.Main.SetBounds(settings.cameraMin, settings.cameraMax);
            }
        }

        private void Spawn(LevelObjectData objectData)
        {
            if (!registry.TryGetPrefab(objectData.type, out var prefab))
            {
                Debug.LogWarning($"[{GetType().Name}] 種別 {objectData.type} が LevelObjectRegistry に登録されていません");
                return;
            }

            //回転・奥行きは Prefab の値を使う
            var prefabTransform = prefab.transform;
            var position = new Vector3(objectData.position.x, objectData.position.y, prefabTransform.position.z);
            var instance = Instantiate(prefab, position, prefabTransform.rotation, _objectRoot);

            instance.AddComponent<LevelObjectIdentity>().Initialize(objectData.type, objectData.id);

            //Instantiate 直後は Awake / OnEnable のみ実行済みで、Start はまだ
            //Start で参照される値もここで反映すれば間に合う
            foreach (var levelObject in instance.GetComponentsInChildren<ILevelObject>(true))
            {
                levelObject.ApplyLevelParameters(objectData.parameters);
            }
        }
    }
}
