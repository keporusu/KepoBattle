using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
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
        private bool _isEditMode;

        //生成したオブジェクト(永続ID → オブジェクト)
        private readonly Dictionary<string, LevelObjectIdentity> _spawnedObjects = new Dictionary<string, LevelObjectIdentity>();

        //現在のレベルデータ(読み込み前は null)
        //レベル作成システムではこれを編集中のデータとして書き換え、保存していなくてもそのままプレイできる
        public LevelData CurrentLevel => _levelData;

        //編集モード中か？
        //編集中は Time.timeScale = 0 でゲームを止める。入力など止まらない処理は各コンポーネントがこれを見て止める
        //レベル作成システムが無い(ビルドしたゲーム等)場合は常に false
        public static bool IsEditMode => Instance != null && Instance._isEditMode;

        //落下で消える高さ
        public float KillHeight => _levelData?.settings.killHeight ?? LevelSettings.DefaultKillHeight;

        //レベルから生成したオブジェクトの親
        //やり直し時にまとめて破棄されるので、プレイ中に生成するものもここに入れる
        public Transform ObjectRoot => _objectRoot;

        //種別キーと Prefab の対応表
        public LevelObjectRegistry Registry => registry;

        //起動時に読み込むレベルのファイル名(StreamingAssets/Levels 以下)
        public string LevelFileName => levelFileName;

        //レベルデータから生成したオブジェクト
        //プレイ中に破棄されたものは含まれない(Unity の null 判定で除く)
        public IEnumerable<LevelObjectIdentity> SpawnedObjects
        {
            get
            {
                foreach (var spawned in _spawnedObjects.Values)
                {
                    if (spawned != null) yield return spawned;
                }
            }
        }

        //レベルデータに保存していない変更があるか？
        public bool IsDirty { get; private set; }

        //読み込んだ・保存したレベルのファイル名(StreamingAssets/Levels 以下)
        public string CurrentFileName { get; private set; }

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

                //止めたまま残さない
                Time.timeScale = 1.0f;
            }
        }

        private async UniTaskVoid Start()
        {
            if (!loadOnStart) return;

            var levelData = await LoadFromStreamingAssetsAsync(levelFileName, destroyCancellationToken);
            if (levelData != null)
            {
                CurrentFileName = levelFileName;
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
        /// 現在のレベルデータを StreamingAssets/Levels に保存する
        /// StreamingAssets は実行時に書き込めないため、Unity エディタ上でのみ保存できる
        /// プレイ中の状態(爆発・落下等)はレベルデータに影響しないので、どちらのモードで保存しても初期配置が保存される
        /// </summary>
        /// <param name="fileName">ファイル名(拡張子 .json を含む)</param>
        /// <param name="errorMessage">失敗した理由</param>
        /// <returns>保存できたか？</returns>
        public bool SaveToStreamingAssets(string fileName, out string errorMessage)
        {
            if (_levelData == null)
            {
                errorMessage = "レベルが読み込まれていません";
                return false;
            }

            if (!Application.isEditor)
            {
                errorMessage = "StreamingAssets には Unity エディタ上でのみ保存できます";
                return false;
            }

            var directory = Path.Combine(Application.streamingAssetsPath, LevelDirectory);
            var path = Path.Combine(directory, fileName);

            try
            {
                Directory.CreateDirectory(directory);

                _levelData.version = LevelData.CurrentVersion;
                var json = JsonUtility.ToJson(_levelData, true);

                //BOM 付きにすると、他のツールで扱いにくくなるため付けない
                File.WriteAllText(path, json + "\n", new UTF8Encoding(false));
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                Debug.LogError($"[{GetType().Name}] レベルを保存できませんでした: {path}\n{e.Message}");
                errorMessage = e.Message;
                return false;
            }

#if UNITY_EDITOR
            //Project ウィンドウにすぐ反映する
            UnityEditor.AssetDatabase.ImportAsset($"Assets/StreamingAssets/{LevelDirectory}/{fileName}");
#endif

            CurrentFileName = fileName;
            IsDirty = false;
            errorMessage = null;
            return true;
        }

        /// <summary>
        /// レベルデータを差し替え、オブジェクトを生成し直す
        /// </summary>
        public void SetLevel(LevelData levelData)
        {
            _levelData = levelData;
            IsDirty = false;
            Rebuild();
        }

        //****編集用の操作****
        //レベルデータを書き換え、生成済みのオブジェクトにも同じ変更を反映する
        //作り直さないので、編集中に選択しているオブジェクトはそのまま残る

        /// <summary>
        /// 永続IDから生成済みのオブジェクトを探す
        /// </summary>
        public bool TryGetSpawnedObject(string id, out LevelObjectIdentity spawned)
        {
            return _spawnedObjects.TryGetValue(id, out spawned) && spawned != null;
        }

        /// <summary>
        /// 永続IDからレベルデータを探す
        /// </summary>
        public LevelObjectData FindObjectData(string id)
        {
            return _levelData?.objects.Find(objectData => objectData.id == id);
        }

        /// <summary>
        /// オブジェクトを追加する
        /// </summary>
        /// <param name="type">種別キー</param>
        /// <param name="position">位置</param>
        /// <returns>生成したオブジェクト。生成できなければ null</returns>
        public LevelObjectIdentity AddObject(string type, Vector2 position)
        {
            if (_levelData == null) return null;

            var objectData = new LevelObjectData
            {
                type = type,
                id = Guid.NewGuid().ToString(),
                position = position,
            };

            var spawned = Spawn(objectData);
            if (spawned == null) return null;

            //パラメータは Prefab の既定値で埋めておく
            objectData.parameters = spawned.Export().parameters;

            _levelData.objects.Add(objectData);
            IsDirty = true;
            return spawned;
        }

        /// <summary>
        /// オブジェクトを削除する
        /// </summary>
        public void RemoveObject(string id)
        {
            if (_levelData == null) return;

            _levelData.objects.RemoveAll(objectData => objectData.id == id);

            if (_spawnedObjects.TryGetValue(id, out var spawned))
            {
                _spawnedObjects.Remove(id);
                if (spawned != null)
                {
                    spawned.gameObject.SetActive(false);
                    Destroy(spawned.gameObject);
                }
            }

            IsDirty = true;
        }

        /// <summary>
        /// オブジェクトを動かす
        /// </summary>
        public void MoveObject(string id, Vector2 position)
        {
            var objectData = FindObjectData(id);
            if (objectData == null) return;

            objectData.position = position;

            if (_spawnedObjects.TryGetValue(id, out var spawned) && spawned != null)
            {
                var spawnedTransform = spawned.transform;
                spawnedTransform.position = new Vector3(position.x, position.y, spawnedTransform.position.z);
            }

            IsDirty = true;
        }

        /// <summary>
        /// レベルデータに書かれていないパラメータを、生成済みオブジェクトの現在値で補う
        /// 編集画面に全てのパラメータを並べるために使う
        /// </summary>
        public void FillMissingParameters(string id)
        {
            var objectData = FindObjectData(id);
            if (objectData == null) return;
            if (!_spawnedObjects.TryGetValue(id, out var spawned) || spawned == null) return;

            objectData.parameters.MergeMissing(spawned.Export().parameters);
        }

        /// <summary>
        /// レベルデータのパラメータを書き換えた後に呼び、生成済みオブジェクトに反映する
        /// </summary>
        public void ApplyParameters(string id)
        {
            var objectData = FindObjectData(id);
            if (objectData == null) return;

            if (_spawnedObjects.TryGetValue(id, out var spawned) && spawned != null)
            {
                foreach (var levelObject in spawned.GetComponentsInChildren<ILevelObject>(true))
                {
                    levelObject.ApplyLevelParameters(objectData.parameters);
                }
            }

            IsDirty = true;
        }

        /// <summary>
        /// 編集モードとプレイモードを切り替える
        /// どちらに切り替えても、レベルデータから全て作り直して初期配置に戻す
        /// レベルの読み込み前に呼んだ場合は、読み込み時にそのモードで生成される
        /// </summary>
        /// <param name="editMode">編集モードにするか？</param>
        public void SetEditMode(bool editMode)
        {
            _isEditMode = editMode;
            Time.timeScale = editMode ? 0.0f : 1.0f;

            //モードを切り替えた時点で、プレイ中のやり直し要求は不要になる
            _isRestartRequested = false;

            if (_levelData != null)
            {
                Rebuild();
            }
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
            _spawnedObjects.Clear();
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

        private LevelObjectIdentity Spawn(LevelObjectData objectData)
        {
            if (!registry.TryGetPrefab(objectData.type, out var prefab))
            {
                Debug.LogWarning($"[{GetType().Name}] 種別 {objectData.type} が LevelObjectRegistry に登録されていません");
                return null;
            }

            //ID が無いデータ(手書きの JSON 等)には割り当てる
            if (string.IsNullOrEmpty(objectData.id))
            {
                objectData.id = Guid.NewGuid().ToString();
            }

            //回転・奥行きは Prefab の値を使う
            var prefabTransform = prefab.transform;
            var position = new Vector3(objectData.position.x, objectData.position.y, prefabTransform.position.z);
            var instance = Instantiate(prefab, position, prefabTransform.rotation, _objectRoot);

            var identity = instance.AddComponent<LevelObjectIdentity>();
            identity.Initialize(objectData.type, objectData.id);
            _spawnedObjects[objectData.id] = identity;

            //Instantiate 直後は Awake / OnEnable のみ実行済みで、Start はまだ
            //Start で参照される値もここで反映すれば間に合う
            foreach (var levelObject in instance.GetComponentsInChildren<ILevelObject>(true))
            {
                levelObject.ApplyLevelParameters(objectData.parameters);
            }

            return identity;
        }
    }
}
