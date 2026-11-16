using UnityEngine;

namespace TwinEdge.Levels
{
    // Ownership is deliberately distinct from legacy generation. Manual lives outside Generated.
    public sealed class MountainSectionV3 : MonoBehaviour
    {
        public MountainConfigV3 config;
        public GameObject generated;
        public string generationAssets;
    }
}
