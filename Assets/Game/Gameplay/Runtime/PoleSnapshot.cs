using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace TwinEdge.Gameplay
{
    public enum PolePhase : byte { Idle, OneAttached, Ready, Rotating, Landed, Failed }

    // One atomic server snapshot: roles, endpoints and revision never arrive separately.
    public struct PoleSnapshot : INetworkSerializable, System.IEquatable<PoleSnapshot>
    {
        public uint revision;
        public PolePhase phase;
        public ulong left, right;
        public bool pivotRight;
        public Vector3 a, b;
        public float yaw, pitch;
        public FixedString64Bytes surface, failure;
        public bool Has(ulong id) => id != ulong.MaxValue && (left == id || right == id);
        public bool Paired => left != ulong.MaxValue && right != ulong.MaxValue;
        public ulong Pivot => pivotRight ? right : left;
        public ulong Orbit => pivotRight ? left : right;
        public Vector3 PivotPoint => pivotRight ? b : a;
        public Vector3 OrbitPoint => pivotRight ? a : b;
        public void NetworkSerialize<T>(BufferSerializer<T> s) where T : IReaderWriter
        {
            s.SerializeValue(ref revision); s.SerializeValue(ref phase);
            s.SerializeValue(ref left); s.SerializeValue(ref right); s.SerializeValue(ref pivotRight);
            s.SerializeValue(ref a); s.SerializeValue(ref b); s.SerializeValue(ref yaw); s.SerializeValue(ref pitch);
            s.SerializeValue(ref surface); s.SerializeValue(ref failure);
        }
        public bool Equals(PoleSnapshot other) => revision == other.revision && phase == other.phase && left == other.left && right == other.right && pivotRight == other.pivotRight && a.Equals(other.a) && b.Equals(other.b) && yaw.Equals(other.yaw) && pitch.Equals(other.pitch) && surface.Equals(other.surface) && failure.Equals(other.failure);
    }
}
