using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(NetworkObject))]
public sealed class RespawnManager : NetworkBehaviour
{
    private readonly Dictionary<ulong, Pose> checkpoints = new Dictionary<ulong, Pose>();

    public void RegisterSpawn(PlayerMovement player)
    {
        if (IsServer && player != null && player.IsSpawned)
        {
            checkpoints[player.OwnerClientId] = new Pose(player.transform.position, player.transform.rotation);
        }
    }

    public void SetCheckpoint(PlayerMovement player, Vector3 position, Quaternion rotation)
    {
        if (!IsServer || player == null || !player.IsSpawned)
        {
            return;
        }

        checkpoints[player.OwnerClientId] = new Pose(position, rotation);
        Debug.Log($"Checkpoint set for client {player.OwnerClientId}.", player);
    }

    public void RespawnPlayer(PlayerMovement player)
    {
        if (!IsServer || player == null || !player.IsSpawned)
        {
            return;
        }

        player.GetComponent<PlayerCombat>()?.ForceEndGrabServer();

        if (!checkpoints.TryGetValue(player.OwnerClientId, out Pose checkpoint))
        {
            checkpoint = player.InitialSpawnPose;
            checkpoints[player.OwnerClientId] = checkpoint;
        }

        player.RespawnAtOwnerClientRpc(checkpoint.position, checkpoint.rotation,
            RpcTarget.Single(player.OwnerClientId, RpcTargetUse.Temp));
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            checkpoints.Clear();
        }

        if (IsServer && NetworkManager != null)
        {
            NetworkManager.OnClientDisconnectCallback += HandleClientDisconnected;
        }
    }

    public override void OnNetworkDespawn()
    {
        if (IsServer && NetworkManager != null)
        {
            NetworkManager.OnClientDisconnectCallback -= HandleClientDisconnected;
        }
    }

    private void HandleClientDisconnected(ulong clientId)
    {
        checkpoints.Remove(clientId);
    }
}
