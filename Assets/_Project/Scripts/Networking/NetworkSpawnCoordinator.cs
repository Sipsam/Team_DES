using System.Collections;
using System.Collections.Generic;
using TwinEdge.Gameplay;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class NetworkSpawnCoordinator : MonoBehaviour
{
    internal const string RoomFullReason = "This prototype supports two players.";
    internal const string InvalidNicknameReason = "Invalid player nickname.";
    internal const string RebuildingReason = "A new match is loading.";
    [SerializeField] private NetworkManager networkManager;
    [SerializeField] private Transform[] spawnPoints;

    private readonly Dictionary<ulong, int> clientSpawnSlots = new Dictionary<ulong, int>();
    private readonly Dictionary<ulong, (Pose Pose, string Checkpoint)> approvedV3Spawns = new();
    private readonly Dictionary<ulong, string> approvedNicknames = new();
    private bool rebuilding;

    internal void BindFoundationSpawns(Transform[] points) => spawnPoints = points;

    internal bool TryRebuildMatch(V3GameplayBootstrap game)
    {
        if (rebuilding || !game || !networkManager.IsServer || networkManager.ShutdownInProgress) return false;

        var scenes = networkManager.SceneManager;
        string matchScene = game.gameObject.scene.name;
        string levelScene = game.levelScene;
        bool matchLoaded = false, levelLoaded = false;
        void Completed(string scene, LoadSceneMode mode, List<ulong> completed, List<ulong> timedOut)
        {
            if (scene != matchScene && scene != levelScene) return;
            foreach (ulong client in timedOut)
                if (client != NetworkManager.ServerClientId && networkManager.ConnectedClients.ContainsKey(client))
                    networkManager.DisconnectClient(client);
            if (!completed.Contains(NetworkManager.ServerClientId)) return;
            if (scene == matchScene) matchLoaded = true;
            if (scene == levelScene) levelLoaded = true;
        }

        scenes.OnLoadEventCompleted += Completed;
        rebuilding = true;
        var previousPlayers = new List<NetworkObject>(2);
        foreach (var client in networkManager.ConnectedClientsList) previousPlayers.Add(client.PlayerObject);
        var status = scenes.LoadScene(matchScene, LoadSceneMode.Single);
        if (status != SceneEventProgressStatus.Started)
        {
            rebuilding = false;
            scenes.OnLoadEventCompleted -= Completed;
            Debug.LogWarning($"Rematch scene load rejected: {status}");
            return false;
        }

        // NGO preserves ordinary dynamically spawned players across a single-mode scene load.
        // Retire them explicitly, including their player-owned movement and interaction streams.
        foreach (var player in previousPlayers)
            if (player && player.IsSpawned) player.Despawn();
        StartCoroutine(Rebuild());
        return true;

        IEnumerator Rebuild()
        {
            bool Succeeded() => networkManager.IsServer && !networkManager.ShutdownInProgress;
            try
            {
                float deadline = Time.realtimeSinceStartup + networkManager.NetworkConfig.LoadSceneTimeOut + 5;
                while (Succeeded() && !matchLoaded && Time.realtimeSinceStartup < deadline) yield return null;
                if (!Succeeded()) yield break;
                if (matchLoaded && scenes.LoadScene(levelScene, LoadSceneMode.Additive) == SceneEventProgressStatus.Started)
                {
                    deadline = Time.realtimeSinceStartup + networkManager.NetworkConfig.LoadSceneTimeOut + 5;
                    while (Succeeded() && Time.realtimeSinceStartup < deadline &&
                        (!levelLoaded || V3GameplayBootstrap.Current?.ReadyForNetworkPlay != true))
                        yield return null;
                    if (!Succeeded()) yield break;
                    if (levelLoaded && V3GameplayBootstrap.Current?.ReadyForNetworkPlay == true)
                    {
                        foreach (var client in networkManager.ConnectedClientsList)
                        {
                            if (!TryGetSlot(client.ClientId, out int slot)) continue;
                            Transform point = spawnPoints[slot];
                            var player = Instantiate(networkManager.NetworkConfig.PlayerPrefab, point.position, point.rotation);
                            player.GetComponent<NetworkObject>().SpawnAsPlayerObject(client.ClientId, destroyWithScene: true);
                            PlaceConnectedPlayer(client.ClientId);
                        }
                        yield break;
                    }
                }

                Debug.LogError("Rematch reconstruction failed. Closing the room.");
                _ = GetComponent<MultiplayerSessionManager>().LeaveRoomAsync();
            }
            finally
            {
                scenes.OnLoadEventCompleted -= Completed;
                rebuilding = false;
            }
        }
    }

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
        if (rebuilding)
        {
            response.Reason = RebuildingReason;
            return;
        }
        int spawnIndex = FindAvailableSpawnIndex(request.ClientNetworkId);
        if (spawnIndex < 0)
        {
            response.Approved = false;
            response.CreatePlayerObject = false;
            response.Reason = RoomFullReason;
            return;
        }

        bool isV3 = networkManager.NetworkConfig.PlayerPrefab.GetComponent<TwinEdge.Gameplay.GameplayCharacter>() != null;
        if (isV3)
        {
            if (!TwinEdge.Gameplay.PlayerNickname.TryDecode(request.Payload, out string nickname))
            {
                response.Reason = InvalidNicknameReason;
                return;
            }
            approvedNicknames[request.ClientNetworkId] = nickname;
        }
        Transform spawnPoint = spawnPoints[spawnIndex];
        Pose pose = GetSpawnPose(spawnPoint, spawnIndex, out string checkpoint);
        clientSpawnSlots[request.ClientNetworkId] = spawnIndex;
        if (isV3)
            approvedV3Spawns[request.ClientNetworkId] = (pose, checkpoint);

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

        var character = playerObject.GetComponent<TwinEdge.Gameplay.GameplayCharacter>();
        bool hasApprovedPose = approvedV3Spawns.Remove(clientId, out var approved);
        // River recovery can assign a dock and start the owner's respawn while NGO
        // is still synchronizing the connecting player. Preserve that placement.
        if (networkManager.IsServer && character != null && character.RespawnCount.Value > 0)
        {
            character.ConfirmFoundationSpawnRpc();
            return;
        }

        // Region presence may change during sync as the new player appears at the
        // reconnect point. Keep the server's decision from connection approval.
        string label = approved.Checkpoint;
        Pose pose = hasApprovedPose ? approved.Pose : GetSpawnPose(spawnPoint, spawnIndex, out label);
        playerObject.transform.SetPositionAndRotation(pose.position, pose.rotation);
        var movement = playerObject.GetComponent<PlayerMovement>();
        movement?.ResetVerticalVelocity();
        if (networkManager.IsServer)
        {
            RespawnManager respawnManager = FindFirstObjectByType<RespawnManager>();
            respawnManager?.RegisterSpawn(movement);
            if (label != null) respawnManager?.SetCheckpoint(movement, pose.position, pose.rotation, label);
            // Commit the approved pose through the owner after NGO scene synchronization.
            // Moving just the server replica leaves the owner transform's spawn state in control.
            if (movement != null && character != null)
            {
                movement.ApplyApprovedSpawnRpc(pose.position, pose.rotation);
                character.ConfirmFoundationSpawnRpc();
            }
        }
        Debug.Log($"Placed client {clientId} at {label ?? spawnPoint.name}: {pose.position}.", playerObject);
    }

    public bool TryGetSlot(ulong clientId, out int slot) => clientSpawnSlots.TryGetValue(clientId, out slot);
    internal string GetApprovedNickname(ulong clientId) => approvedNicknames[clientId];

    private static Pose GetSpawnPose(Transform spawnPoint, int slot, out string label)
    {
        var underground = TwinEdge.Gameplay.UndergroundGameplay.Current;
        if (underground != null && underground.TryResume(slot, out Pose undergroundPose, out label)) return undergroundPose;
        var castle = TwinEdge.Gameplay.CastleGameplay.Current;
        if (castle != null && castle.TryResume(slot, out Pose castlePose, out label)) return castlePose;
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
        approvedV3Spawns.Remove(clientId);
        approvedNicknames.Remove(clientId);
    }
}
