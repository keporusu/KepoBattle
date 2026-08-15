using System;
using System.Collections.Generic;
using UnityEngine;

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
        
        [SerializeField] private AudioSource seSource;
        [SerializeField] private ClipData[] seClips;
        
        //マッピング
        private readonly Dictionary<string, AudioClip> _seClipsMap = new Dictionary<string, AudioClip>();

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
            foreach (var clip in seClips)
            {
                _seClipsMap.Add(clip.name, clip.clip);
            }
        }

        public void PlaySe(string clipName)
        {
            //TODO: 重いのでマップを作成する
            var clip = _seClipsMap[clipName];
            if (clip != null)
            {
                seSource.PlayOneShot(clip);
            }
        }

        public void StopSe(string clipName)
        {
            var clip = _seClipsMap[clipName];
            if (clip != null)
            {
                seSource.Stop();
            }
        }
    }

}
