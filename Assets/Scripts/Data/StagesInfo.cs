using System;
using System.Collections.Generic;
using UnityEngine;

namespace Data
{
    
    [Serializable]
    public struct StageInfo
    {
        public string Id;
        public string Name;
        public string Description;
        public string SceneName;
    }
    
    [CreateAssetMenu(menuName="ScriptableObjects/StagesInfo")]
    public class StagesInfo : ScriptableObject
    {
        public List<StageInfo> stages;
    }
}