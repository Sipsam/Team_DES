#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using UnityEngine;

namespace TwinEdge.Gameplay
{
    internal static class RelayMatchStart
    {
        internal static IEnumerator Run(GameStateManager state)
        {
            float until = Time.realtimeSinceStartup + 90;
            bool sent = false;
            while (Time.realtimeSinceStartup < until)
            {
                if (state.IsSpawned && state.Phase >= FoundationPhase.Outbound &&
                    !Presentation.PresentationRoot.BlocksGameplay) yield break;
                if (!sent && state.IsSpawned && state.Phase == FoundationPhase.WaitingForPlayers && state.ConnectedPlayerCount == 2)
                {
                    if (!state.IsHost)
                    {
                        state.SetLobbyReadyRpc(true);
                        sent = true;
                    }
                    else if (state.HostCanStart)
                    {
                        state.RequestMatchStartRpc();
                        sent = true;
                    }
                }
                yield return null;
            }
            throw new TimeoutException("Timed out waiting for Client Ready and Host Start.");
        }
    }
}
#endif
