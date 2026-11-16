
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class WaveManager : NetworkBehaviour
{
    #region Enums

    public enum WaveState
    {
        WaitingToStart,
        InProgress,
        WaitingForVotes
    }

    #endregion

    #region Inspector Settings

    [Header("Enemy Spawning")]
    [SerializeField] private NetworkObject enemyPrefab;
    [SerializeField] private Transform[] spawnPoints;
    [SerializeField, Min(1)] private int enemiesInFirstWave = 5;
    [SerializeField, Min(0)] private int extraEnemiesPerWave = 2;
    [SerializeField, Min(0f)] private float spawnInterval = 1f;

    [Header("Voting")]
    [SerializeField, Min(0f)] private float voteRetryDelay = 3f;

    [Header("Wave Rewards")]
    [SerializeField, Min(0)] private int upgradePointsPerWave = 1;

    #endregion

    #region Network State

    public NetworkVariable<int> CurrentWave = new(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    public NetworkVariable<int> AliveEnemies = new(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    public NetworkVariable<int> YesVotes = new(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    public NetworkVariable<int> EligibleVoters = new(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    public NetworkVariable<WaveState> State = new(
        WaveState.WaitingToStart,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    public NetworkVariable<int> VoteRound = new(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    #endregion

    #region Runtime State

    private readonly List<NetworkObject> waveEnemies = new();
    private readonly Dictionary<ulong, bool> votes = new();

    private float nextSpawnTime;
    private float nextVoteRetryTime;

    private int enemiesToSpawn;
    private int spawnedThisWave;

    private bool spawnFinished;
    private bool waveRewardGranted;
    private bool matchStarted;

    #endregion

    #region Properties

    public bool MatchStarted => matchStarted;

    #endregion

    #region Network Lifecycle

    public override void OnNetworkSpawn()
    {
        if (!IsServer)
            return;

        if (enemyPrefab == null)
        {
            Debug.LogError(
                "[WaveManager] Не призначено Enemy Prefab.",
                this);
            return;
        }

        if (spawnPoints == null || spawnPoints.Length == 0)
        {
            Debug.LogError(
                "[WaveManager] Не призначено Spawn Points.",
                this);
            return;
        }

        NetworkManager.OnClientDisconnectCallback +=
            OnClientDisconnected;

        LobbySessionManager lobby = LobbySessionManager.Instance;

        if (lobby != null && lobby.MatchStarted)
            TryStartMatch();
    }

    public override void OnNetworkDespawn()
    {
        if (NetworkManager != null && IsServer)
        {
            NetworkManager.OnClientDisconnectCallback -=
                OnClientDisconnected;
        }
    }

    #endregion

    #region Match Management

    public bool TryStartMatch()
    {
        if (!IsServer || !IsSpawned || matchStarted)
            return false;

        if (enemyPrefab == null ||
            spawnPoints == null ||
            spawnPoints.Length == 0)
        {
            Debug.LogError(
                "[WaveManager] Неможливо почати матч: " +
                "не налаштований спавн.",
                this);

            return false;
        }

        matchStarted = true;

        CurrentWave.Value = 0;
        AliveEnemies.Value = 0;
        YesVotes.Value = 0;
        EligibleVoters.Value = 0;
        VoteRound.Value = 0;

        votes.Clear();
        waveEnemies.Clear();

        Debug.Log("[WaveManager] Матч розпочато!", this);

        StartNextWave();

        return State.Value == WaveState.InProgress &&
               CurrentWave.Value == 1;
    }

    private void StartNextWave()
    {
        if (!IsServer || !matchStarted)
            return;

        CurrentWave.Value++;

        waveRewardGranted = false;
        State.Value = WaveState.InProgress;

        waveEnemies.Clear();
        votes.Clear();

        AliveEnemies.Value = 0;
        YesVotes.Value = 0;
        EligibleVoters.Value =
            NetworkManager.ConnectedClientsIds.Count;

        spawnedThisWave = 0;
        spawnFinished = false;
        nextSpawnTime = Time.time;

        enemiesToSpawn =
            enemiesInFirstWave +
            (CurrentWave.Value - 1) * extraEnemiesPerWave;

        Debug.Log(
            $"[WaveManager] Починається хвиля {CurrentWave.Value}. " +
            $"Ворогів: {enemiesToSpawn}.",
            this);
    }

    #endregion

    #region Unity Lifecycle

    private void Update()
    {
        if (!IsServer || !matchStarted)
            return;

        if (State.Value != WaveState.InProgress)
            return;

        UpdateEnemyCount();
        SpawnEnemiesOverTime();

        if (spawnFinished &&
            AliveEnemies.Value == 0 &&
            !waveRewardGranted)
        {
            waveRewardGranted = true;

            AwardWaveRewards();

            State.Value = WaveState.WaitingForVotes;
            VoteRound.Value++;

            ResetVotes();

            Debug.Log(
                $"[WaveManager] Хвиля {CurrentWave.Value} завершена. " +
                "Нагороду видано. Чекаємо голосування.",
                this);
        }
    }

    #endregion

    #region Enemy Spawning

    private void SpawnEnemiesOverTime()
    {
        if (spawnFinished || Time.time < nextSpawnTime)
            return;

        if (spawnedThisWave >= enemiesToSpawn)
        {
            spawnFinished = true;
            return;
        }

        Transform point =
            spawnPoints[Random.Range(0, spawnPoints.Length)];

        if (point == null)
        {
            Debug.LogError(
                "[WaveManager] Один зі Spawn Points не призначений.",
                this);

            spawnFinished = true;
            return;
        }

        NetworkObject enemy = Instantiate(
            enemyPrefab,
            point.position,
            point.rotation);

        enemy.Spawn();

        waveEnemies.Add(enemy);
        spawnedThisWave++;

        AliveEnemies.Value = CountAliveEnemies();
        nextSpawnTime = Time.time + spawnInterval;

        if (spawnedThisWave >= enemiesToSpawn)
            spawnFinished = true;
    }

    #endregion

    #region Enemy Tracking

    private void UpdateEnemyCount()
    {
        AliveEnemies.Value = CountAliveEnemies();
    }

    private int CountAliveEnemies()
    {
        int count = 0;

        foreach (NetworkObject enemy in waveEnemies)
        {
            if (enemy == null || !enemy.IsSpawned)
                continue;

            Health health = enemy.GetComponent<Health>();

            if (health != null && !health.IsDead.Value)
                count++;
        }

        return count;
    }

    #endregion

    #region Wave Rewards

    private void AwardWaveRewards()
    {
        if (!IsServer || upgradePointsPerWave <= 0)
            return;

        foreach (ulong clientId in NetworkManager.ConnectedClientsIds)
        {
            if (!NetworkManager.ConnectedClients.TryGetValue(
                    clientId,
                    out NetworkClient client))
                continue;

            if (client.PlayerObject == null)
                continue;

            PlayerUpgradeWallet wallet =
                client.PlayerObject.GetComponent<PlayerUpgradeWallet>();

            if (wallet == null || !wallet.IsSpawned)
            {
                Debug.LogWarning(
                    $"[WaveManager] У гравця {clientId} немає " +
                    "PlayerUpgradeWallet.",
                    client.PlayerObject);
                continue;
            }

            wallet.AddPoints(upgradePointsPerWave);

            Debug.Log(
                $"[WaveManager] Гравець {clientId} отримав " +
                $"{upgradePointsPerWave} спільних очок. " +
                $"Баланс: {wallet.Points}.",
                client.PlayerObject);
        }
    }

    #endregion

    #region Voting API

    public void SubmitVote(bool voteYes)
    {
        if (!IsSpawned || !IsClient || !matchStarted)
            return;

        SubmitVoteServerRpc(voteYes);
    }

    #endregion

    #region Voting RPC

    [ServerRpc(RequireOwnership = false)]
    private void SubmitVoteServerRpc(
        bool voteYes,
        ServerRpcParams rpcParams = default)
    {
        if (!IsServer ||
            !matchStarted ||
            State.Value != WaveState.WaitingForVotes)
            return;

        if (Time.time < nextVoteRetryTime)
            return;

        ulong clientId = rpcParams.Receive.SenderClientId;

        if (!NetworkManager.ConnectedClients.ContainsKey(clientId))
            return;

        votes[clientId] = voteYes;

        ResolveVotes();
    }

    #endregion

    #region Voting Logic

    private void ResolveVotes()
    {
        int connectedCount =
            NetworkManager.ConnectedClientsIds.Count;

        RefreshVoteCounts();

        if (connectedCount == 0 || votes.Count < connectedCount)
            return;

        bool everyoneVotedYes = true;

        foreach (ulong clientId in NetworkManager.ConnectedClientsIds)
        {
            if (!votes.TryGetValue(clientId, out bool yes) || !yes)
            {
                everyoneVotedYes = false;
                break;
            }
        }

        if (everyoneVotedYes)
        {
            Debug.Log(
                "[WaveManager] Усі проголосували ЗА. Наступна хвиля!",
                this);

            StartNextWave();
        }
        else
        {
            Debug.Log(
                "[WaveManager] Голосування не пройшло. " +
                "Можна проголосувати повторно.",
                this);

            votes.Clear();
            RefreshVoteCounts();

            VoteRound.Value++;
            nextVoteRetryTime = Time.time + voteRetryDelay;
        }
    }

    private void ResetVotes()
    {
        votes.Clear();
        RefreshVoteCounts();
        nextVoteRetryTime = 0f;
    }

    private void RefreshVoteCounts()
    {
        int yes = 0;

        foreach (KeyValuePair<ulong, bool> vote in votes)
        {
            if (vote.Value)
                yes++;
        }

        YesVotes.Value = yes;
        EligibleVoters.Value =
            NetworkManager.ConnectedClientsIds.Count;
    }

    #endregion

    #region Network Event Handlers

    private void OnClientDisconnected(ulong clientId)
    {
        if (!IsServer)
            return;

        votes.Remove(clientId);
        RefreshVoteCounts();

        if (matchStarted &&
            State.Value == WaveState.WaitingForVotes)
        {
            ResolveVotes();
        }
    }

    #endregion
}
