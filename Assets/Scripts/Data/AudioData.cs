using System.Collections.Generic;
using UnityEngine;

namespace Data
{
    [CreateAssetMenu(menuName="ScriptableObjects/AudioData")]
    public class AudioData : ScriptableObject
    {
        public string audioName;
        public AudioClip audioClip;
        public float volume;
    }
}