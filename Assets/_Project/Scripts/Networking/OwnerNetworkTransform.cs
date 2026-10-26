using Unity.Netcode.Components;
using UnityEngine;

[RequireComponent(typeof(Unity.Netcode.NetworkObject))]
public sealed class OwnerNetworkTransform : NetworkTransform
{
    private void Reset()
    {
        AuthorityMode = AuthorityModes.Owner;
    }

    private void OnValidate()
    {
        AuthorityMode = AuthorityModes.Owner;
    }

    protected override bool OnIsServerAuthoritative()
    {
        return false;
    }
}
