using System.Linq;
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
    private readonly Dictionary<ulong, float> lastSupportedHeight = new();
    private readonly Dictionary<ulong, Vector3> lastBridgeFeet = new();
    private readonly Dictionary<ulong, Vector3> lastSupportedFeet = new();
    public void SetSharedCheckpointServer(Vector3 position, Quaternion rotation, string label, bool resolveGround = false)
    {
        if (!IsServer) return;
        foreach (var client in NetworkManager.ConnectedClientsList)
        {
            if (client.PlayerObject == null) continue;
            Vector3 point = position + Vector3.right * (client.ClientId == 0 ? -1.5f : 1.5f);
            if (resolveGround) point = TwinEdge.Gameplay.V3GameplayBootstrap.SafeGround(point);
            SetCheckpoint(client.PlayerObject.GetComponent<PlayerMovement>(), point, rotation, label);
        }
    }
    public void RespawnGroupServer(bool useAssignedCheckpoint = false)
    {
        if (!IsServer) return;
        foreach (var client in NetworkManager.ConnectedClientsList)
            if (client.PlayerObject != null) RespawnPlayer(client.PlayerObject.GetComponent<PlayerMovement>(), useAssignedCheckpoint);
    }
    public void ConfigureFallRecovery(float height) => fallRecoveryHeight = height;

    private void Update()
    {
        if (!IsServer || !IsSpawned || (gameStateManager != null && gameStateManager.foundationMode && !gameStateManager.CanPlay))
            return;
        foreach (NetworkClient client in NetworkManager.ConnectedClientsList)
        {
            if (client.PlayerObject == null) continue;
            if (TwinEdge.Gameplay.RiverGameplay.Current?.IsSeated(client.ClientId) == true) continue;
            if (client.PlayerObject.GetComponent<TwinEdge.Gameplay.MountainReturnLaunch>()?.InFlight == true) continue;
            var underground = TwinEdge.Gameplay.UndergroundGameplay.Current;
            if (underground != null && underground.HandleFall(client.PlayerObject.GetComponent<PlayerMovement>())) continue;
            var position = client.PlayerObject.transform.position;
            bool fallen = position.y < fallRecoveryHeight;
            if (client.PlayerObject.GetComponent<TwinEdge.Gameplay.GameplayCharacter>() != null)
            {
                bool supported = Physics.RaycastAll(position + Vector3.up * .15f, Vector3.down, .5f, ~0, QueryTriggerInteraction.Ignore)
                    .Any(hit => !hit.transform.IsChildOf(client.PlayerObject.transform) && hit.normal.y > .4f);
                var bridge = TwinEdge.Gameplay.BridgeGameplay.Current;
                if (bridge != null && lastBridgeFeet.ContainsKey(client.ClientId) && bridge.IsBelowSpan(position)) fallen = true;
                if (bridge != null && bridge.PlankCount > 0 &&
                    bridge.InBridgeAirspace(position, 1f) && Mathf.Abs(position.y - bridge.StartPoint.y) < 1.2f)
                    lastBridgeFeet[client.ClientId] = position;
                else if (supported && !fallen) lastBridgeFeet.Remove(client.ClientId);
                if (supported)
                {
                    lastSupportedHeight[client.ClientId] = position.y;
                    lastSupportedFeet[client.ClientId] = position;
                }
                else if (lastSupportedHeight.TryGetValue(client.ClientId, out float height)) fallen |= position.y < height - 18f;
            }
            if (fallen) RespawnPlayer(client.PlayerObject.GetComponent<PlayerMovement>());
        }
    }

    public void RegisterSpawn(PlayerMovement player)
    {
        if (IsServer && player != null && player.IsSpawned)
        {
            checkpoints[player.OwnerClientId] = new Pose(player.transform.position, player.transform.rotation);
            var character = player.GetComponent<TwinEdge.Gameplay.GameplayCharacter>();
            if (character != null) character.CheckpointName.Value = "Mountain start";
        }
    }

    public void SetCheckpoint(PlayerMovement player, Vector3 position, Quaternion rotation, string label = "Checkpoint")
    {
        if (!IsServer || player == null || !player.IsSpawned)
        {
            return;
        }

        checkpoints[player.OwnerClientId] = new Pose(position, rotation);
        var character = player.GetComponent<TwinEdge.Gameplay.GameplayCharacter>();
        if (character != null) character.CheckpointName.Value = label;
        Debug.Log($"Checkpoint set for client {player.OwnerClientId}.", player);
    }

    public void RespawnPlayer(PlayerMovement player, bool useAssignedCheckpoint = false)
    {
        if (!IsServer || player == null || !player.IsSpawned)
        {
            return;
        }

        if (!useAssignedCheckpoint && TwinEdge.Gameplay.RiverGameplay.Current?.TryRecoverRiderServer(player) == true) return;
        if (!useAssignedCheckpoint && lastRespawnTimes.TryGetValue(player.OwnerClientId, out float lastTime) && Time.time - lastTime < 0.75f)
            return;
        lastRespawnTimes[player.OwnerClientId] = Time.time;

        lastBridgeFeet.TryGetValue(player.OwnerClientId, out Vector3 bridgeFeet);
        lastSupportedFeet.TryGetValue(player.OwnerClientId, out Vector3 supportedFeet);
        TwinEdge.Gameplay.GemOwnership.Current?.BeforeRespawnServer(player,
            lastBridgeFeet.ContainsKey(player.OwnerClientId) ? bridgeFeet : null,
            lastSupportedFeet.ContainsKey(player.OwnerClientId) ? supportedFeet : null);
        var character = player.GetComponent<TwinEdge.Gameplay.GameplayCharacter>();
        character?.BeforeContestRespawnServer();
        bool regionalCheckpoint = !useAssignedCheckpoint &&
            (TwinEdge.Gameplay.UndergroundGameplay.Current?.AssignDeathCheckpoint(player) == true ||
             TwinEdge.Gameplay.CastleGameplay.Current?.AssignDeathCheckpoint(player) == true);
        var bridge = TwinEdge.Gameplay.BridgeGameplay.Current;
        bridge?.BeforeRespawnServer(player);
        if (lastBridgeFeet.Remove(player.OwnerClientId, out Vector3 feet) && !useAssignedCheckpoint && !regionalCheckpoint && bridge != null &&
            bridge.TryFallCheckpoint(player, feet, out Pose bridgeCheckpoint, out string bridgeLabel))
            SetCheckpoint(player, bridgeCheckpoint.position, bridgeCheckpoint.rotation, bridgeLabel);
        TwinEdge.Gameplay.CooperativePole.Current?.BeforeRespawnServer(player.OwnerClientId);
        player.GetComponent<TwinEdge.Gameplay.MountainReturnLaunch>()?.BeforeRespawnServer();

        player.GetComponent<PlayerCombat>()?.ForceEndGrabServer();

        if (!checkpoints.TryGetValue(player.OwnerClientId, out Pose checkpoint))
        {
            checkpoint = player.InitialSpawnPose;
            checkpoints[player.OwnerClientId] = checkpoint;
        }

        if (character != null) { character.RespawnCount.Value++; character.SetCarryingServer(false); }
        lastSupportedHeight[player.OwnerClientId] = checkpoint.position.y;
        lastSupportedFeet[player.OwnerClientId] = checkpoint.position;
        player.SetGrabbedServer(false);
        player.RespawnAtOwnerClientRpc(checkpoint.position, checkpoint.rotation, character?.RespawnCount.Value ?? 0,
            RpcTarget.Single(player.OwnerClientId, RpcTargetUse.Temp));
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            checkpoints.Clear();
            lastRespawnTimes.Clear();
            lastSupportedHeight.Clear();
            lastBridgeFeet.Clear();
            lastSupportedFeet.Clear();
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
        lastSupportedHeight.Remove(clientId);
        lastBridgeFeet.Remove(clientId);
        lastSupportedFeet.Remove(clientId);
    }

    private void HandleGameStateChanged(GameState previous, GameState next)
    {
        if (!IsServer || next != GameState.Competition) return;
        if (gameStateManager.foundationMode)
        {
            TwinEdge.Gameplay.UndergroundGameplay.Current?.AssignReturnStartCheckpoints();
            return;
        }
        if (competitionStart == null) return;
        int index = 0;
        foreach (NetworkClient client in NetworkManager.ConnectedClientsList)
        {
            Vector3 offset = competitionStart.right * (index++ == 0 ? -1f : 1f);
            checkpoints[client.ClientId] = new Pose(competitionStart.position + offset, competitionStart.rotation);
        }
    }
}
