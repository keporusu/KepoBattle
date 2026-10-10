using UnityEngine;

namespace Systems
{
    /// <summary>
    /// レベル作成システム
    /// 現在は編集モードとプレイモードの切り替えのみ
    /// StreamingAssets にはエディタからしか書き込めないため、Unity エディタ上でのみ動かす
    /// </summary>
    public class LevelEditor : MonoBehaviour
    {
        [SerializeField] private bool startInEditMode = false;

        //ユーティリティ画面の位置と大きさ
        private static readonly Rect UtilityRect = new Rect(10.0f, 10.0f, 220.0f, 70.0f);

        private void Awake()
        {
            //ビルドしたゲームでは無効にする
            if (!Application.isEditor)
            {
                enabled = false;
            }
        }

        private void Start()
        {
            if (startInEditMode && LevelManager.Instance != null)
            {
                LevelManager.Instance.SetEditMode(true);
            }
        }

        private void OnGUI()
        {
            var levelManager = LevelManager.Instance;
            if (levelManager == null) return;

            var isEditMode = LevelManager.IsEditMode;

            GUILayout.BeginArea(UtilityRect, GUI.skin.box);
            GUILayout.Label(isEditMode ? "モード: 編集" : "モード: プレイ");

            GUILayout.BeginHorizontal();

            //今のモードのボタンは押せないようにする
            GUI.enabled = !isEditMode;
            if (GUILayout.Button("編集"))
            {
                levelManager.SetEditMode(true);
            }

            GUI.enabled = isEditMode;
            if (GUILayout.Button("再生"))
            {
                levelManager.SetEditMode(false);
            }

            GUI.enabled = true;
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }
    }
}
