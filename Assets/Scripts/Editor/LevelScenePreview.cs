using System.IO;
using System.Linq;
using Core.Contracts;
using Data;
using Systems;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Editor
{
    /// <summary>
    /// 再生していない時に、LevelManager が読み込むレベルをシーン上にプレビュー表示する
    /// レベルは実行時に StreamingAssets から生成されるため、何もしないとシーンを開いただけでは見た目が分からない
    ///
    /// プレビューはシーンに保存されず(DontSave)、編集もできない(NotEditable)一時的なオブジェクト
    /// 再生を始めると消し、再生を止めた時・シーンを開いた時・レベルの JSON が更新された時に作り直す
    /// </summary>
    [InitializeOnLoad]
    public static class LevelScenePreview
    {
        private const string RootName = "[Level Preview]";
        private const string MenuPath = "KepoBattle/レベルのプレビューを表示";
        private const string RefreshMenuPath = "KepoBattle/レベルのプレビューを更新";
        private const string EnabledPrefKey = "KepoBattle.LevelScenePreview.Enabled";

        //プレビューは保存せず、Inspector からも編集させない
        private const HideFlags PreviewHideFlags = HideFlags.DontSave | HideFlags.NotEditable;

        private static bool IsEnabled
        {
            get => EditorPrefs.GetBool(EnabledPrefKey, true);
            set => EditorPrefs.SetBool(EnabledPrefKey, value);
        }

        static LevelScenePreview()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            EditorSceneManager.sceneOpened += (_, _) => RequestRefresh();

            //スクリプトの再読み込み後も表示し直す
            RequestRefresh();
        }

        [MenuItem(MenuPath)]
        private static void ToggleEnabled()
        {
            IsEnabled = !IsEnabled;
            RequestRefresh();
        }

        [MenuItem(MenuPath, true)]
        private static bool ToggleEnabledValidate()
        {
            Menu.SetChecked(MenuPath, IsEnabled);
            return true;
        }

        [MenuItem(RefreshMenuPath)]
        private static void RefreshFromMenu()
        {
            RequestRefresh();
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            switch (state)
            {
                //再生中は LevelManager が本物を生成するので、プレビューを残さない
                case PlayModeStateChange.ExitingEditMode:
                    Clear();
                    break;
                case PlayModeStateChange.EnteredEditMode:
                    RequestRefresh();
                    break;
            }
        }

        /// <summary>
        /// プレビューを作り直す
        /// シーンの読み込みやアセットの取り込みの途中で呼ばれることがあるため、処理が落ち着いてから行う
        /// </summary>
        public static void RequestRefresh()
        {
            EditorApplication.delayCall -= Refresh;
            EditorApplication.delayCall += Refresh;
        }

        private static void Refresh()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;

            Clear();
            if (!IsEnabled) return;

            var levelManager = Object.FindAnyObjectByType<LevelManager>();
            if (levelManager == null || levelManager.Registry == null) return;

            var levelData = LoadLevel(levelManager.LevelFileName);
            if (levelData == null) return;

            var root = new GameObject(RootName) { hideFlags = PreviewHideFlags };
            SceneManager.MoveGameObjectToScene(root, levelManager.gameObject.scene);

            foreach (var objectData in levelData.objects)
            {
                Spawn(levelManager.Registry, objectData, root.transform);
            }
        }

        private static LevelData LoadLevel(string fileName)
        {
            if (string.IsNullOrEmpty(fileName)) return null;

            //エディタ上の StreamingAssets はローカルのフォルダなので、そのまま読める
            var path = Path.Combine(Application.streamingAssetsPath, LevelManager.LevelDirectory, fileName);
            if (!File.Exists(path))
            {
                Debug.LogWarning($"[{nameof(LevelScenePreview)}] レベルが見つかりません: {path}");
                return null;
            }

            return JsonUtility.FromJson<LevelData>(File.ReadAllText(path));
        }

        private static void Spawn(LevelObjectRegistry registry, LevelObjectData objectData, Transform parent)
        {
            if (!registry.TryGetPrefab(objectData.type, out var prefab)) return;

            //実行時と同じく、回転・奥行きは Prefab の値を使う
            var prefabTransform = prefab.transform;
            var position = new Vector3(objectData.position.x, objectData.position.y, prefabTransform.position.z);
            var instance = Object.Instantiate(prefab, position, prefabTransform.rotation, parent);

            //向き・角度などを見た目に反映する
            //再生していないので Awake / Start は呼ばれず、見た目に関わる値だけが変わる
            foreach (var levelObject in instance.GetComponentsInChildren<ILevelObject>(true))
            {
                levelObject.ApplyLevelParameters(objectData.parameters);
            }

            foreach (var child in instance.GetComponentsInChildren<Transform>(true))
            {
                child.gameObject.hideFlags = PreviewHideFlags;
            }
        }

        /// <summary>
        /// プレビューを消す
        /// スクリプトの再読み込みで参照を失っても消せるよう、名前とフラグで探す
        /// </summary>
        private static void Clear()
        {
            var previews = Resources.FindObjectsOfTypeAll<GameObject>()
                .Where(go => go.name == RootName && go.transform.parent == null
                             && (go.hideFlags & HideFlags.DontSave) != 0
                             && !EditorUtility.IsPersistent(go))
                .ToList();

            foreach (var preview in previews)
            {
                Object.DestroyImmediate(preview);
            }
        }

        /// <summary>
        /// レベルの JSON が保存・変更されたらプレビューを作り直す
        /// </summary>
        private class LevelFilePostprocessor : AssetPostprocessor
        {
            private static void OnPostprocessAllAssets(
                string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
            {
                var levelFolder = $"Assets/StreamingAssets/{LevelManager.LevelDirectory}/";
                if (importedAssets.Concat(deletedAssets).Any(path => path.StartsWith(levelFolder)))
                {
                    RequestRefresh();
                }
            }
        }
    }
}
