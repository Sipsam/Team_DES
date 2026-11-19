using System;
using Unity.Netcode;
using UnityEngine;

namespace TwinEdge.Gameplay
{
    internal enum RiverRaftStatus : byte { Hidden, Docked, Sailing, Destroyed, Arrived }
    internal enum RiverContact : byte { None, Bank, Dock, Rock, Driftwood, Fish, Crocodile, RiderLost, Disconnect, PhaseChanged }

    internal struct RiverRaftSnapshot : INetworkSerializable, IEquatable<RiverRaftSnapshot>
    {
        public const ulong Empty = ulong.MaxValue;
        public Vector3 Position, Velocity;
        public float Heading, TurnRate, LeftStamina, RightStamina, Drive, Steer, Flow, LeftDrive, RightDrive;
        public ulong Left, Right, Finisher;
        public uint Voyage;
        public byte Hits, FinishPlace;
        public RiverRaftStatus Status;
        public RiverContact Contact, Recovery;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Position);
            serializer.SerializeValue(ref Velocity);
            serializer.SerializeValue(ref Heading);
            serializer.SerializeValue(ref TurnRate);
            serializer.SerializeValue(ref LeftStamina);
            serializer.SerializeValue(ref RightStamina);
            serializer.SerializeValue(ref Drive);
            serializer.SerializeValue(ref Steer);
            serializer.SerializeValue(ref Flow);
            serializer.SerializeValue(ref LeftDrive);
            serializer.SerializeValue(ref RightDrive);
            serializer.SerializeValue(ref Left);
            serializer.SerializeValue(ref Right);
            serializer.SerializeValue(ref Finisher);
            serializer.SerializeValue(ref Voyage);
            serializer.SerializeValue(ref Hits);
            serializer.SerializeValue(ref FinishPlace);
            serializer.SerializeValue(ref Status);
            serializer.SerializeValue(ref Contact);
            serializer.SerializeValue(ref Recovery);
        }

        public bool Has(ulong id) => Left == id || Right == id;
        public bool Equals(RiverRaftSnapshot other) =>
            Position.Equals(other.Position) && Velocity.Equals(other.Velocity) && Heading == other.Heading &&
            TurnRate == other.TurnRate && LeftStamina == other.LeftStamina && RightStamina == other.RightStamina &&
            LeftDrive == other.LeftDrive && RightDrive == other.RightDrive && Drive == other.Drive && Steer == other.Steer && Flow == other.Flow && Left == other.Left && Right == other.Right && Finisher == other.Finisher &&
            Voyage == other.Voyage && Hits == other.Hits && FinishPlace == other.FinishPlace &&
            Status == other.Status && Contact == other.Contact && Recovery == other.Recovery;
    }
}
