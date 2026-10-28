using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(NetworkObject))]
public sealed class RespawnManager : NetworkBehaviour
{
    [SerializeField] private Transform competitionStart;
    [SerializeField] private float fallRecoveryHeight = -25f;
    private readonly Dictionary<ulong, Pose> checkpoints = new Dictionary<ulong, Pose>();
    private readonly Dictionary<ulong, float> lastRespawnTimes = new Dictionary<ulong, float>();
    private GameStateManager gameStateManager;

    private void Update()
    {
        if (!IsServer || !IsSpawned) return;
        foreach (NetworkClient client in NetworkManager.ConnectedClientsList)
        {
            if (client.PlayerObject != null && client.PlayerObject.transform.position.y < fallRecoveryHeight)
                RespawnPlayer(client.PlayerObject.GetComponent<PlayerMovement>());
        }
    }

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

        if (lastRespawnTimes.TryGetValue(player.OwnerClientId, out float lastTime) && Time.time - lastTime < 0.75f)
            return;
        lastRespawnTimes[player.OwnerClientId] = Time.time;

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
            lastRespawnTimes.Clear();
            gameStateManager = GetComponent<GameStateManager>();
            if (gameStateManager != null) gameStateManager.StateChanged += HandleGameStateChanged;
        }

        if (IsServer && NetworkManager != null)
        {
            NetworkManager.OnClientDisconnectCallback += HandleClientDisconnected;
        }
    }

    public override void OnNetworkDespawn()
    {
        if (gameStateManager != null) gameStateManager.StateChanged -= HandleGameStateChanged;
        if (IsServer && NetworkManager != null)
        {
            NetworkManager.OnClientDisconnectCallback -= HandleClientDisconnected;
        }
    }

    private void HandleClientDisconnected(ulong clientId)
    {
        checkpoints.Remove(clientId);
        lastRespawnTimes.Remove(clientId);
    }

    private void HandleGameStateChanged(GameState previous, GameState next)
    {
        if (!IsServer || next != GameState.Competition || competitionStart == null) return;
        int index = 0;
        foreach (NetworkClient client in NetworkManager.ConnectedClientsList)
        {
            Vector3 offset = competitionStart.right * (index++ == 0 ? -1f : 1f);
            checkpoints[client.ClientId] = new Pose(competitionStart.position + offset, competitionStart.rotation);
        }
    }
}
