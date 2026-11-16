
using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(NetworkObject))]
public sealed class LobbySessionManager : NetworkBehaviour
{
    #region Singleton

    public static LobbySessionManager Instance { get; private set; }

    #endregion

    #region Network State

    private readonly NetworkList<LobbyPlayerState> players = new();

    private readonly NetworkVariable<bool> matchStarted = new(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    #endregion

    #region Static Data

    private static readonly Dictionary<ulong, string> pendingIdentities = new();

    #endregion

    #region Properties

    public bool MatchStarted => matchStarted.Value;

    public IReadOnlyList<LobbyPlayerState> Players
    {
        get
        {
            List<LobbyPlayerState> result = new();

            for (int i = 0; i < players.Count; i++)
            {
                result.Add(players[i]);
            }

            return result;
        }
    }

    #endregion

    #region Events

    public event Action PlayersChanged;
    public event Action<bool> MatchStartedChanged;

    #endregion

    #region Static Methods

    public static void SetPendingIdentity(
        ulong clientId,
        string playerId)
    {
        if (string.IsNullOrWhiteSpace(playerId) ||
            playerId.Length > 64)
        {
            return;
        }

        pendingIdentities[clientId] = playerId;
    }

    public static void ClearPendingIdentities()
    {
        pendingIdentities.Clear();
    }

    #endregion

    #region Network Lifecycle

    public override void OnNetworkSpawn()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogError(
                "[LobbySessionManager] Знайдено другий екземпляр.",
                this);

            return;
        }

        Instance = this;

        players.OnListChanged += HandlePlayersChanged;
        matchStarted.OnValueChanged += HandleMatchStartedChanged;

        if (!IsServer)
        {
            return;
        }

        NetworkManager.OnClientConnectedCallback += HandleClientConnected;
        NetworkManager.OnClientDisconnectCallback += HandleClientDisconnected;

        RegisterPlayer(
            NetworkManager.ServerClientId,
            GetLocalPlayerId());

        foreach (ulong clientId in NetworkManager.ConnectedClientsIds)
        {
            if (clientId == NetworkManager.ServerClientId)
            {
                continue;
            }

            if (pendingIdentities.TryGetValue(
                    clientId,
                    out string playerId))
            {
                RegisterPlayer(clientId, playerId);
            }
        }
    }

    public override void OnNetworkDespawn()
    {
        players.OnListChanged -= HandlePlayersChanged;
        matchStarted.OnValueChanged -= HandleMatchStartedChanged;

        if (NetworkManager != null && IsServer)
        {
            NetworkManager.OnClientConnectedCallback -= HandleClientConnected;
            NetworkManager.OnClientDisconnectCallback -= HandleClientDisconnected;
        }

        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    #endregion

    #region Player Identity

    public bool TryGetPlayerId(
        ulong clientId,
        out string playerId)
    {
        playerId = null;

        for (int i = 0; i < players.Count; i++)
        {
            LobbyPlayerState state = players[i];

            if (state.ClientId != clientId || !state.IsConnected)
            {
                continue;
            }

            playerId = state.PlayerId.ToString();

            return !string.IsNullOrWhiteSpace(playerId);
        }

        return false;
    }

    public bool TryGetLocalPlayerId(out string playerId)
    {
        playerId = null;

        if (NetworkManager == null ||
            !NetworkManager.IsListening)
        {
            return false;
        }

        return TryGetPlayerId(
            NetworkManager.LocalClientId,
            out playerId);
    }

    public bool TryGetPlayerState(
        string playerId,
        out LobbyPlayerState result)
    {
        result = default;

        if (string.IsNullOrWhiteSpace(playerId))
        {
            return false;
        }

        for (int i = 0; i < players.Count; i++)
        {
            LobbyPlayerState state = players[i];

            if (state.PlayerId.ToString() != playerId)
            {
                continue;
            }

            result = state;
            return true;
        }

        return false;
    }

    private string GetLocalPlayerId()
    {
        try
        {
            return Unity.Services.Authentication.AuthenticationService
                .Instance.PlayerId;
        }
        catch (Exception e)
        {
            Debug.LogError(
                "[LobbySessionManager] Не вдалося отримати PlayerId: " +
                e.Message,
                this);

            return string.Empty;
        }
    }

    #endregion

    #region Player Registration

    private void HandleClientConnected(ulong clientId)
    {
        if (!IsServer)
        {
            return;
        }

        if (clientId == NetworkManager.ServerClientId)
        {
            RegisterPlayer(clientId, GetLocalPlayerId());
            return;
        }

        if (!pendingIdentities.TryGetValue(
                clientId,
                out string playerId))
        {
            Debug.LogWarning(
                $"[LobbySessionManager] Немає PlayerId для ClientId {clientId}.",
                this);

            return;
        }

        RegisterPlayer(clientId, playerId);
    }

    private void RegisterPlayer(
        ulong clientId,
        string playerId)
    {
        if (!IsServer ||
            string.IsNullOrWhiteSpace(playerId) ||
            playerId.Length > 64)
        {
            return;
        }

        FixedString64Bytes fixedId = new(playerId);

        for (int i = 0; i < players.Count; i++)
        {
            LobbyPlayerState state = players[i];

            if (!state.PlayerId.Equals(fixedId))
            {
                continue;
            }

            state.ClientId = clientId;
            state.ClassId = default;
            state.IsConnected = true;
            state.IsReady = false;

            players[i] = state;

            Debug.Log(
                $"[LobbySessionManager] Гравець повернувся: {playerId}.",
                this);

            return;
        }

        players.Add(new LobbyPlayerState
        {
            PlayerId = fixedId,
            ClassId = default,
            ClientId = clientId,
            IsConnected = true,
            IsReady = false
        });

        Debug.Log(
            $"[LobbySessionManager] Новий гравець: {playerId}.",
            this);
    }

    private void HandleClientDisconnected(ulong clientId)
    {
        if (!IsServer)
        {
            return;
        }

        pendingIdentities.Remove(clientId);

        for (int i = 0; i < players.Count; i++)
        {
            LobbyPlayerState state = players[i];

            if (state.ClientId != clientId)
            {
                continue;
            }

            state.IsConnected = false;
            state.IsReady = false;

            players[i] = state;

            Debug.Log(
                $"[LobbySessionManager] Відключився: {state.PlayerId}.",
                this);

            return;
        }
    }

    #endregion

    #region Class Selection

    public void SetPlayerClassFromCharacter(
        ulong clientId,
        string selectedClassId)
    {
        if (!IsServer ||
            matchStarted.Value ||
            string.IsNullOrWhiteSpace(selectedClassId) ||
            selectedClassId.Length > 64)
        {
            return;
        }

        int index = FindPlayerByClientId(clientId);

        if (index < 0)
        {
            return;
        }

        LobbyPlayerState state = players[index];

        state.ClassId = new FixedString64Bytes(
            selectedClassId.Trim().ToLowerInvariant());

        state.IsReady = false;

        players[index] = state;

        Debug.Log(
            $"[LobbySessionManager] Гравець {clientId} вибрав клас " +
            $"{state.ClassId}.",
            this);
    }

    #endregion

    #region Ready System

    public void RequestReady(bool ready)
    {
        if (!IsSpawned || !IsClient)
        {
            return;
        }

        RequestReadyServerRpc(ready);
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestReadyServerRpc(
        bool ready,
        ServerRpcParams rpcParams = default)
    {
        if (!IsServer || matchStarted.Value)
        {
            return;
        }

        int index = FindPlayerByClientId(
            rpcParams.Receive.SenderClientId);

        if (index < 0)
        {
            return;
        }

        LobbyPlayerState state = players[index];

        if (state.ClassId.Length == 0 && ready)
        {
            return;
        }

        state.IsReady = ready;
        players[index] = state;

        Debug.Log(
            $"[LobbySessionManager] Гравець {state.ClientId}: " +
            $"{(ready ? "Ready" : "Not Ready")}.",
            this);
    }

    #endregion

    #region Match Management

    public void RequestStartMatch()
    {
        if (!IsSpawned || !IsHost || matchStarted.Value)
        {
            return;
        }

        RequestStartMatchServerRpc();
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestStartMatchServerRpc(
        ServerRpcParams rpcParams = default)
    {
        if (!IsServer || matchStarted.Value)
        {
            return;
        }

        if (rpcParams.Receive.SenderClientId !=
            NetworkManager.ServerClientId)
        {
            return;
        }

        bool hasConnectedPlayers = false;

        for (int i = 0; i < players.Count; i++)
        {
            LobbyPlayerState state = players[i];

            if (!state.IsConnected)
            {
                continue;
            }

            hasConnectedPlayers = true;

            if (!state.IsReady || state.ClassId.Length == 0)
            {
                Debug.LogWarning(
                    "[LobbySessionManager] Не всі підключені гравці " +
                    "вибрали клас і підтвердили готовність.",
                    this);

                return;
            }
        }

        if (!hasConnectedPlayers)
        {
            Debug.LogWarning(
                "[LobbySessionManager] Немає підключених гравців.",
                this);

            return;
        }

        WaveManager waveManager =
            FindFirstObjectByType<WaveManager>();

        if (waveManager == null || !waveManager.IsSpawned)
        {
            Debug.LogError(
                "[LobbySessionManager] Не знайдено активний WaveManager.",
                this);

            return;
        }

        if (!waveManager.TryStartMatch())
        {
            Debug.LogError(
                "[LobbySessionManager] WaveManager не зміг запустити матч.",
                this);

            return;
        }

        matchStarted.Value = true;

        Debug.Log(
            "[LobbySessionManager] Хост успішно запустив матч!",
            this);
    }

    #endregion

    #region Player Lookup

    private int FindPlayerByClientId(ulong clientId)
    {
        for (int i = 0; i < players.Count; i++)
        {
            LobbyPlayerState state = players[i];

            if (state.ClientId == clientId && state.IsConnected)
            {
                return i;
            }
        }

        return -1;
    }

    #endregion

    #region Network Event Handlers

    private void HandlePlayersChanged(
        NetworkListEvent<LobbyPlayerState> change)
    {
        PlayersChanged?.Invoke();
    }

    private void HandleMatchStartedChanged(
        bool previous,
        bool current)
    {
        MatchStartedChanged?.Invoke(current);
    }

    #endregion
}
