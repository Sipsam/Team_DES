using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public sealed class NetworkSpawnCoordinator : MonoBehaviour
{
    [SerializeField] private NetworkManager networkManager;
    [SerializeField] private Transform[] spawnPoints;

    private readonly Dictionary<ulong, int> clientSpawnSlots = new Dictionary<ulong, int>();

    private void Awake()
    {
        if (networkManager == null)
        {
            networkManager = GetComponent<NetworkManager>();
        }

        if (networkManager == null)
        {
            Debug.LogError("NetworkSpawnCoordinator requires a NetworkManager.", this);
            enabled = false;
            return;
        }

        networkManager.NetworkConfig.ConnectionApproval = true;
        networkManager.ConnectionApprovalCallback = ApproveConnection;
        networkManager.OnClientConnectedCallback += PlaceConnectedPlayer;
        networkManager.OnClientDisconnectCallback += HandleClientDisconnected;
    }

    private void OnDestroy()
    {
        if (networkManager == null)
        {
            return;
        }

        networkManager.OnClientConnectedCallback -= PlaceConnectedPlayer;
        networkManager.OnClientDisconnectCallback -= HandleClientDisconnected;
        networkManager.ConnectionApprovalCallback = null;
    }

    private void ApproveConnection(
        NetworkManager.ConnectionApprovalRequest request,
        NetworkManager.ConnectionApprovalResponse response)
    {
        int spawnIndex = FindAvailableSpawnIndex(request.ClientNetworkId);
        if (spawnIndex < 0)
        {
            response.Approved = false;
            response.CreatePlayerObject = false;
            response.Reason = "This prototype supports two players.";
            return;
        }

        Transform spawnPoint = spawnPoints[spawnIndex];
        Pose pose = GetSpawnPose(spawnPoint, out _);
        clientSpawnSlots[request.ClientNetworkId] = spawnIndex;

        response.Approved = true;
        response.CreatePlayerObject = true;
        response.Position = pose.position;
        response.Rotation = pose.rotation;
    }

    private void PlaceConnectedPlayer(ulong clientId)
    {
        // V3 uses the server approval pose; client-ID modulo is invalid after reconnects.
        if (!networkManager.IsServer && networkManager.NetworkConfig.PlayerPrefab.GetComponent<TwinEdge.Gameplay.GameplayCharacter>() != null) return;
        if (spawnPoints == null || spawnPoints.Length == 0)
        {
            Debug.LogError("NetworkSpawnCoordinator has no PlayerSpawnPoints assigned.", this);
            return;
        }

        int spawnIndex;
        if (!clientSpawnSlots.TryGetValue(clientId, out spawnIndex))
        {
            // The connecting client's local coordinator does not run the server approval callback.
            // Client IDs 0 and 1 map to the same two initial slots as the host's allocation.
            spawnIndex = (int)(clientId % (ulong)spawnPoints.Length);
        }

        Transform spawnPoint = spawnPoints[spawnIndex];
        if (spawnPoint == null)
        {
            Debug.LogError($"PlayerSpawnPoints[{spawnIndex}] is not assigned.", this);
            return;
        }

        NetworkObject playerObject = null;
        if (networkManager.IsServer && networkManager.ConnectedClients.TryGetValue(clientId, out NetworkClient client))
        {
            playerObject = client.PlayerObject;
        }
        else if (clientId == networkManager.LocalClientId && networkManager.LocalClient != null)
        {
            playerObject = networkManager.LocalClient.PlayerObject;
        }

        if (playerObject == null)
        {
            Debug.LogWarning($"PlayerObject for client {clientId} was not available at connect callback.", this);
            return;
        }

        Pose pose = GetSpawnPose(spawnPoint, out string label);
        playerObject.transform.SetPositionAndRotation(pose.position, pose.rotation);
        playerObject.GetComponent<PlayerMovement>()?.ResetVerticalVelocity();
        if (networkManager.IsServer)
        {
            RespawnManager respawnManager = FindFirstObjectByType<RespawnManager>();
            var movement = playerObject.GetComponent<PlayerMovement>();
            respawnManager?.RegisterSpawn(movement);
            if (label != null) respawnManager?.SetCheckpoint(movement, pose.position, pose.rotation, label);
        }
        Debug.Log($"Placed client {clientId} at {label ?? spawnPoint.name}: {pose.position}.", playerObject);
    }

    private static Pose GetSpawnPose(Transform spawnPoint, out string label)
    {
        var bridge = TwinEdge.Gameplay.BridgeGameplay.Current;
        if (bridge != null && bridge.TryGetResumeSpawn(out Pose pose, out label)) return pose;
        label = null;
        return new Pose(spawnPoint.position, spawnPoint.rotation);
    }

    private int FindAvailableSpawnIndex(ulong clientId)
    {
        if (clientSpawnSlots.TryGetValue(clientId, out int existingIndex))
        {
            return existingIndex;
        }

        if (spawnPoints == null)
        {
            return -1;
        }

        for (int i = 0; i < spawnPoints.Length; i++)
        {
            if (spawnPoints[i] == null)
            {
                continue;
            }

            bool slotInUse = false;
            foreach (int assignedIndex in clientSpawnSlots.Values)
            {
                if (assignedIndex == i)
                {
                    slotInUse = true;
                    break;
                }
            }

            if (!slotInUse)
            {
                return i;
            }
        }

        return -1;
    }

    private void HandleClientDisconnected(ulong clientId)
    {
        clientSpawnSlots.Remove(clientId);
    }
}
