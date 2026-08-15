using System;
using System.Collections.Generic;
using Data;
using UnityEngine;
using UnityEngine.Serialization;

namespace Systems
{

    [Serializable]
    public struct ClipData
    {
        public string name;
        public AudioClip clip;
    }
    
    /// <summary>
    /// シングルトン
    /// 効果音を鳴らすシステム
    /// </summary>
    public class SoundManager : MonoBehaviour
    {
        public static SoundManager Instance;

        [SerializeField] private AudioSource seSourcePrefab;
        [SerializeField] private int poolSize = 16;
        [SerializeField] private AudioData[] seDataList;
        
        //カウンター
        private int _sourceCounter = 0;
        
        //プール
        private readonly List<AudioSource> _seSourcesPool = new List<AudioSource>();
        
        //マッピング
        private readonly Dictionary<string, AudioData> _seClipsMap = new Dictionary<string, AudioData>();
        private readonly Dictionary<int, AudioSource> _activeSources = new Dictionary<int, AudioSource>();

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(this);
            }
            else
            {
                Destroy(gameObject);
            }
            
            //マッピング
            foreach (var data in seDataList)
            {
                _seClipsMap.Add(data.name, data);
            }
            
            //プール
            for (int i = 0; i < poolSize; i++)
            {
                var source = Instantiate(seSourcePrefab, transform);
                _seSourcesPool.Add(source);
            }
        }
        
        /// <summary>
        /// 空いてるAudioSourceを探してSEを再生する
        /// </summary>
        /// <param name="clipName">クリップの名前</param>
        /// <returns>今回の再生ID</returns>
        public int PlaySe(string clipName)
        {
            //TODO: 重いのでマップを作成する
            var data = _seClipsMap[clipName];
            if (data != null)
            {
                _sourceCounter++;
                var source = GetFreeSource();
                source.clip = data.audioClip;
                source.volume = data.volume;
                source.Play();
                _activeSources[_sourceCounter] = source;
            }
            return _sourceCounter;
        }
        
        /// <summary>
        /// 再生IDを指定して、SEを停止
        /// </summary>
        /// <param name="id">再生ID</param>
        public void StopSe(int id)
        {
            if (_activeSources.TryGetValue(id, out var source))
            {
                source.Stop();
                _activeSources.Remove(id);
            }
        }
        
        
        private AudioSource GetFreeSource()
        {
            //空いているAudioSourceを探す
            foreach(var source in _seSourcesPool)
            {
                if (!source.isPlaying)
                {
                    return source;
                }
            }
            return _seSourcesPool[0];
        }
    }

}
