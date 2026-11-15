using UnityEngine;
namespace TwinEdge.Levels
{
    public sealed class LevelSection : MonoBehaviour
    {
        public const string Owner = "TwinEdge.Blockout.v1";
        public string owner = Owner;
        public SectionId section;
        public LevelLayoutConfig config;
        public string generationId;
    }
}
