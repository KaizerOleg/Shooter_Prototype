
using System;
using Unity.Collections;
using Unity.Netcode;

public struct LobbyPlayerState :
    INetworkSerializable,
    IEquatable<LobbyPlayerState>
{
    public FixedString64Bytes PlayerId;
    public FixedString64Bytes ClassId;
    public ulong ClientId;
    public bool IsConnected;
    public bool IsReady;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer)
        where T : IReaderWriter
    {
        serializer.SerializeValue(ref PlayerId);
        serializer.SerializeValue(ref ClassId);
        serializer.SerializeValue(ref ClientId);
        serializer.SerializeValue(ref IsConnected);
        serializer.SerializeValue(ref IsReady);
    }

    public bool Equals(LobbyPlayerState other)
    {
        return PlayerId.Equals(other.PlayerId) &&
               ClassId.Equals(other.ClassId) &&
               ClientId == other.ClientId &&
               IsConnected == other.IsConnected &&
               IsReady == other.IsReady;
    }
}
