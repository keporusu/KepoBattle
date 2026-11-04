using System;
using System.Collections.Generic;
using Components.Movement;
using Core.Constants;
using DG.Tweening;
using Systems;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Components.Controller
{
    /// <summary>
    /// 発射するオブジェクトの比率
    /// </summary>
    [Serializable]
    public class LaunchWeight
    {
        public ObjectIdentity identity;
        public float weight = 1.0f; //足して1である必要はない。発射時に確率へ変換する
    }

    /// <summary>
    /// 残り時間ごとの発射の設定
    /// </summary>
    [Serializable]
    public class LauncherRun
    {
        public float remainingTime; //残り時間がこの値になると、このRunで発射する(秒)
        [Min(0.01f)] public float launchInterval = 1.0f; //発射する間隔(秒)
        public List<LaunchWeight> launchWeights = new List<LaunchWeight>();
    }

    /// <summary>
    /// ObjectIdentity と発射する prefab の対応
    /// Identity は各 Controller の Start で設定されるので、生成前の prefab からは読めない
    /// </summary>
    [Serializable]
    public class LaunchPrefab
    {
        public ObjectIdentity identity;
        public GameObject prefab;
    }

    /// <summary>
    /// ステージの残り時間に応じて、オブジェクトを発射する
    /// 残り時間で動くので、時間制限のないステージでは発射しない
    /// </summary>
    public class LauncherController : PropBehaviourController
    {
        //発射の内容
        [SerializeField] private List<LauncherRun> launcherSequence = new List<LauncherRun>();
        [SerializeField] private List<LaunchPrefab> launchPrefabs = new List<LaunchPrefab>();

        //発射の向きと速さ
        [SerializeField] private float minLaunchAngle = -30.0f; //上向きを0とした角度(度)。反時計回りが正
        [SerializeField] private float maxLaunchAngle = 30.0f;
        [SerializeField] private float minLaunchSpeed = 10.0f; //画面上で実際に動く速度
        [SerializeField] private float maxLaunchSpeed = 20.0f;
        [SerializeField] private float spawnDistance = 1.0f; //中心から発射方向へ離して生成する距離

        //アニメーション関連
        [SerializeField] private GameObject rendererObject;
        [SerializeField] private float stretchScale = 1.3f; //発射した瞬間のy方向の伸び(元の大きさに対する倍率)
        [SerializeField] private float stretchDuration = 0.3f; //伸び切った状態から元に戻るまでの時間

        //状態
        private LauncherRun _currentRun;
        private float _launchTimer; //次の発射までの時間

        //キャッシュ
        private Vector3 _rendererScale; //Rendererの元の大きさ

        private void Start()
        {
            //自身
            SetIdentity(ObjectIdentity.Launcher);

            _rendererScale = rendererObject.transform.localScale;

            if (StageManager.Current != null)
            {
                StageManager.Current.OnRestarted += ResetLaunch;
            }
        }

        private void OnDestroy()
        {
            if (StageManager.Current != null)
            {
                StageManager.Current.OnRestarted -= ResetLaunch;
            }
        }

        private void Update()
        {
            //編集中は発射しない
            if (LevelManager.IsEditMode) return;

            //クリア後やステージを出る間は発射しない
            var stage = StageManager.Current;
            if (stage == null || !stage.IsPlaying || !stage.HasTimeLimit) return;

            //まだどのRunの残り時間にも達していなければ発射しない
            var run = FindCurrentRun(stage.RemainingTime);
            if (run == null) return;

            //Runが切り替わったら、すぐに1発撃つ
            if (run != _currentRun)
            {
                _currentRun = run;
                _launchTimer = 0.0f;
            }

            _launchTimer -= Time.deltaTime;
            if (_launchTimer > 0.0f) return;

            _launchTimer += run.launchInterval;
            Launch(run);
        }

        /// <summary>
        /// ステージをやり直したときに、発射の状態を最初に戻す
        /// </summary>
        private void ResetLaunch()
        {
            _currentRun = null;
            _launchTimer = 0.0f;
        }

        /// <summary>
        /// 今の残り時間で使うRunを探す
        /// すでに到達したRunのうち、最後に到達したものを選ぶ。リストの並び順には依存しない
        /// </summary>
        /// <param name="remainingTime">ステージの残り時間</param>
        /// <returns>まだどのRunにも到達していなければ null</returns>
        private LauncherRun FindCurrentRun(float remainingTime)
        {
            LauncherRun currentRun = null;
            foreach (var run in launcherSequence)
            {
                if (remainingTime > run.remainingTime) continue;
                if (currentRun == null || run.remainingTime < currentRun.remainingTime)
                {
                    currentRun = run;
                }
            }

            return currentRun;
        }

        /// <summary>
        /// Runの比率に沿って選んだオブジェクトを、ランダムな角度と速度で発射する
        /// </summary>
        private void Launch(LauncherRun run)
        {
            if (!TryPickIdentity(run, out var identity))
            {
                Debug.LogWarning($"[{GetType().Name}] 比率の合計が0のRunがあるため発射できません : {gameObject.name}");
                return;
            }

            var prefab = launchPrefabs.Find(x => x.identity == identity)?.prefab;
            if (prefab == null)
            {
                Debug.LogWarning($"[{GetType().Name}] {identity} の prefab が登録されていません : {gameObject.name}");
                return;
            }

            var angle = Random.Range(minLaunchAngle, maxLaunchAngle);
            var direction = (Vector2)(Quaternion.Euler(0.0f, 0.0f, angle) * Vector3.up);
            var position = transform.position + (Vector3)(direction * spawnDistance);

            //レベルと一緒に破棄されるようにする
            var parent = LevelManager.Instance != null ? LevelManager.Instance.ObjectRoot : null;
            var instance = Instantiate(prefab, position, prefab.transform.rotation, parent);

            //アニメーション再生
            StretchEffect();

            if (!instance.TryGetComponent(out PhysicsMover mover))
            {
                Debug.LogWarning($"[{GetType().Name}] PhysicsMover が {instance.name} に見つからないため、速度を与えられません");
                return;
            }

            //AddForceVelocity には BlowAlpha を掛ける前の値を渡す
            //割っておくと、BlowAlpha の違うオブジェクトも指定した速度で飛ぶ
            var speed = Random.Range(minLaunchSpeed, maxLaunchSpeed);
            var force = direction * speed;
            if (mover.BlowAlpha > 0.0f)
            {
                force /= mover.BlowAlpha;
            }
            mover.AddForceVelocity(force, true);
        }

        /// <summary>
        /// 伸び切った状態から、y方向にバネのように揺れて元の大きさに戻る
        /// </summary>
        private void StretchEffect()
        {
            //前のアニメーションは停止する
            var animationId = "LauncherStretch" + gameObject.GetEntityId();
            DOTween.Kill(animationId);

            var rendererTransform = rendererObject.transform;
            var stretchedScale = _rendererScale;
            stretchedScale.y *= stretchScale;
            rendererTransform.localScale = stretchedScale;

            rendererTransform.DOScaleY(_rendererScale.y, stretchDuration)
                .SetEase(Ease.OutElastic)
                .OnKill(() =>
                {
                    //終了時などでrendererが先に破棄されている場合がある
                    if (rendererObject == null) return;
                    rendererObject.transform.localScale = _rendererScale;
                })
                .SetLink(rendererObject)
                .SetId(animationId);
        }

        /// <summary>
        /// Runの比率を確率として、発射するオブジェクトを選ぶ
        /// </summary>
        /// <param name="run">今のRun</param>
        /// <param name="identity">選ばれたオブジェクト</param>
        /// <returns>選べなかった場合(比率の合計が0)は false</returns>
        private static bool TryPickIdentity(LauncherRun run, out ObjectIdentity identity)
        {
            identity = default;

            var totalWeight = 0.0f;
            foreach (var launchWeight in run.launchWeights)
            {
                totalWeight += Mathf.Max(0.0f, launchWeight.weight);
            }
            if (totalWeight <= 0.0f) return false;

            var value = Random.Range(0.0f, totalWeight);
            foreach (var launchWeight in run.launchWeights)
            {
                if (launchWeight.weight <= 0.0f) continue;

                //誤差で最後まで抜けても、比率のあるものが選ばれるように毎回入れておく
                identity = launchWeight.identity;
                value -= launchWeight.weight;
                if (value <= 0.0f) break;
            }

            return true;
        }
    }

}
