using System;
using UnityEngine;

namespace Systems
{
    
    
    
    /// <summary>
    /// シングルトン
    /// 効果音を鳴らすシステム
    /// </summary>
    public class SoundManager : MonoBehaviour
    {
        public static SoundManager Instance;
        
        [SerializeField] private AudioSource seSource;
        [SerializeField] private AudioClip[] seClips;

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
        }

        public void PlaySe(string clipName)
        {
            //TODO: 重いのでマップを作成する
            AudioClip clip = System.Array.Find(seClips, c => c.name == clipName);
            if (clip != null) seSource.PlayOneShot(clip);
        }
    }

}
