using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Scripting;

namespace Dreamy.Debugger.Sample
{
    /// <summary>Add to a demo object, then inspect it through the Scene tab. Values exist only in memory.</summary>
    [Preserve]
    public sealed class SceneInspectionExample : MonoBehaviour
    {
        [Serializable] [Preserve] public struct Stats { public long Score; public Vector3 Spawn; }
        public bool Invincible;
        public float Speed = 5;
        public string Note = "Select me in the Scene tab";
        public Color Tint = new Color(.8f, .3f, .4f);
        public Stats Player = new Stats { Score = 9007199254740993L, Spawn = Vector3.up };
        public List<Stats> Checkpoints = new List<Stats> { new Stats { Score = 10 }, new Stats { Score = 20 } };
        public GameObject Reference;
        private void Awake() { if (Reference == null) Reference = gameObject; }
    }
}
