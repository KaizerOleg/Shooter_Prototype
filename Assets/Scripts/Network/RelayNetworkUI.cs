
using System;
using System.Text;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Networking.Transport.Relay;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEngine;

public sealed class RelayNetworkUI : MonoBehaviour
{
    #region Inspector Settings

    [Header("Relay")]
    [SerializeField, Min(2)]
    private int maxPlayers = 4;

    [Header("Network")]
    [SerializeField]
    private NetworkObject lobbyManagerPrefab;

    #endregion

    #region Private Fields

    private string joinCode = "";
    private string codeToJoin = "";
    private string status = "Ініціалізація...";

    private bool busy;
    private bool servicesReady;

    private NetworkManager manager;

    #endregion

    #region Unity Lifecycle

    private void Start()
    {
        manager = NetworkManager.Singleton;

        if (manager == null)
        {
            status = "У сцені немає NetworkManager.";

            Debug.LogError(
                "[RelayNetworkUI] NetworkManager не знайдено.");

            return;
        }

        _ = InitializeServices();
    }

    #endregion

    #region Unity Services Initialization

    private async Task InitializeServices()
    {
        try
        {
            status = "Підключення до Unity Services...";

            if (UnityServices.State != ServicesInitializationState.Initialized)
            {
                await UnityServices.InitializeAsync();
            }

            if (!AuthenticationService.Instance.IsSignedIn)
            {
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
            }

            servicesReady = true;
            status = "Готово. Створи кімнату або введи код.";

            Debug.Log(
                "[RelayNetworkUI] Unity Services готові. PlayerId: " +
                AuthenticationService.Instance.PlayerId);
        }
        catch (Exception e)
        {
            status = "Помилка ініціалізації: " + e.Message;
            Debug.LogException(e);
        }
    }

    #endregion

    #region Connection Approval

    private void ConfigureConnectionApproval()
    {
        if (manager == null)
        {
            return;
        }

        manager.NetworkConfig.ConnectionApproval = true;
        manager.ConnectionApprovalCallback = ApprovalCheck;
    }

    private void ApprovalCheck(
        NetworkManager.ConnectionApprovalRequest request,
        NetworkManager.ConnectionApprovalResponse response)
    {
        string playerId = string.Empty;

        try
        {
            playerId = Encoding.UTF8.GetString(request.Payload);
        }
        catch
        {
            playerId = string.Empty;
        }

        if (string.IsNullOrWhiteSpace(playerId) || playerId.Length > 64)
        {
            response.Approved = false;
            response.CreatePlayerObject = false;
            response.Pending = false;

            return;
        }

        LobbySessionManager.SetPendingIdentity(
            request.ClientNetworkId,
            playerId);

        response.Approved = true;
        response.CreatePlayerObject = true;
        response.Pending = false;
    }

    private byte[] CreateConnectionPayload()
    {
        string playerId = AuthenticationService.Instance.PlayerId;

        if (string.IsNullOrWhiteSpace(playerId))
        {
            throw new Exception(
                "Unity Authentication PlayerId порожній.");
        }

        return Encoding.UTF8.GetBytes(playerId);
    }

    #endregion

    #region Host Management

    private async Task HostGame()
    {
        if (busy || !servicesReady)
        {
            return;
        }

        if (lobbyManagerPrefab == null)
        {
            status = "Не призначено Lobby Manager Prefab.";
            return;
        }

        busy = true;
        status = "Створюємо Relay-кімнату...";

        try
        {
            if (manager == null)
            {
                throw new Exception("NetworkManager не знайдено.");
            }

            if (manager.IsListening)
            {
                throw new Exception("NetworkManager вже запущений.");
            }

            UnityTransport transport =
                manager.GetComponent<UnityTransport>();

            if (transport == null)
            {
                throw new Exception("Не знайдено UnityTransport.");
            }

            Allocation allocation =
                await RelayService.Instance.CreateAllocationAsync(
                    maxPlayers - 1);

            transport.SetRelayServerData(
                AllocationUtils.ToRelayServerData(allocation, "dtls"));

            joinCode = await RelayService.Instance.GetJoinCodeAsync(
                allocation.AllocationId);

            manager.NetworkConfig.ConnectionData =
                CreateConnectionPayload();

            ConfigureConnectionApproval();

            if (!manager.StartHost())
            {
                throw new Exception("Не вдалося запустити Host.");
            }

            NetworkObject lobbyObject = Instantiate(lobbyManagerPrefab);
            lobbyObject.Spawn();

            status = "Кімната створена. Очікуємо гравців.";

            Debug.Log(
                $"[RelayNetworkUI] Host запущений. Код: {joinCode}");
        }
        catch (Exception e)
        {
            status = "Помилка створення кімнати: " + e.Message;
            Debug.LogException(e);

            if (manager != null && manager.IsListening)
            {
                manager.Shutdown();
            }
        }
        finally
        {
            busy = false;
        }
    }

    #endregion

    #region Client Management

    private async Task JoinGame()
    {
        if (busy || !servicesReady)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(codeToJoin))
        {
            status = "Введи код кімнати.";
            return;
        }

        busy = true;
        status = "Підключаємося до Relay...";

        try
        {
            if (manager == null)
            {
                throw new Exception("NetworkManager не знайдено.");
            }

            if (manager.IsListening)
            {
                throw new Exception("NetworkManager вже запущений.");
            }

            UnityTransport transport =
                manager.GetComponent<UnityTransport>();

            if (transport == null)
            {
                throw new Exception("Не знайдено UnityTransport.");
            }

            JoinAllocation allocation =
                await RelayService.Instance.JoinAllocationAsync(
                    codeToJoin.Trim().ToUpperInvariant());

            transport.SetRelayServerData(
                AllocationUtils.ToRelayServerData(allocation, "dtls"));

            manager.NetworkConfig.ConnectionData =
                CreateConnectionPayload();

            ConfigureConnectionApproval();

            if (!manager.StartClient())
            {
                throw new Exception("Не вдалося запустити Client.");
            }

            status = "Запит на підключення відправлено.";
        }
        catch (Exception e)
        {
            status = "Помилка підключення: " + e.Message;
            Debug.LogException(e);

            if (manager != null && manager.IsListening)
            {
                manager.Shutdown();
            }
        }
        finally
        {
            busy = false;
        }
    }

    #endregion

    #region Lobby Player State

    private bool TryGetLocalPlayerState(
        LobbySessionManager lobby,
        out LobbyPlayerState localState)
    {
        localState = default;

        if (lobby == null || manager == null)
        {
            return false;
        }

        if (!lobby.TryGetLocalPlayerId(out string playerId))
        {
            return false;
        }

        return lobby.TryGetPlayerState(
            playerId,
            out localState);
    }

    private bool AreAllConnectedPlayersReady(
        LobbySessionManager lobby,
        out int connectedCount,
        out int readyCount)
    {
        connectedCount = 0;
        readyCount = 0;

        if (lobby == null)
        {
            return false;
        }

        foreach (LobbyPlayerState player in lobby.Players)
        {
            if (!player.IsConnected)
            {
                continue;
            }

            connectedCount++;

            if (player.IsReady && player.ClassId.Length > 0)
            {
                readyCount++;
            }
        }

        return connectedCount > 0 && readyCount == connectedCount;
    }

    #endregion

    #region Lobby UI

    private void DrawLobby(LobbySessionManager lobby)
    {
        GUILayout.Space(10);
        GUILayout.Label("ГРАВЦІ:");

        DrawPlayerList(lobby);

        GUILayout.Space(10);

        if (lobby.MatchStarted)
        {
            GUILayout.Label("МАТЧ РОЗПОЧАТО!");
            return;
        }

        DrawReadyControls(lobby);
        DrawMatchStartControls(lobby);
    }

    private void DrawPlayerList(LobbySessionManager lobby)
    {
        foreach (LobbyPlayerState player in lobby.Players)
        {
            string className = player.ClassId.Length > 0
                ? player.ClassId.ToString()
                : "Клас не вибрано";

            string connectionStatus = player.IsConnected
                ? "Online"
                : "Offline";

            string readyStatus = player.IsReady
                ? "ГОТОВИЙ"
                : "НЕ ГОТОВИЙ";

            GUILayout.BeginVertical(GUI.skin.box);

            GUILayout.Label(player.PlayerId.ToString());
            GUILayout.Label("Клас: " + className);
            GUILayout.Label("З'єднання: " + connectionStatus);
            GUILayout.Label("Статус: " + readyStatus);

            GUILayout.EndVertical();
        }
    }

    private void DrawReadyControls(LobbySessionManager lobby)
    {
        if (TryGetLocalPlayerState(lobby, out LobbyPlayerState localState))
        {
            bool hasClass = localState.ClassId.Length > 0;

            GUI.enabled = hasClass;

            string readyButtonText = localState.IsReady
                ? "СКАСУВАТИ ГОТОВНІСТЬ"
                : "ГОТОВИЙ ДО МАТЧУ";

            if (GUILayout.Button(
                    readyButtonText,
                    GUILayout.Height(42)))
            {
                lobby.RequestReady(!localState.IsReady);
            }

            GUI.enabled = true;

            if (!hasClass)
            {
                GUILayout.Label("Спочатку обери клас персонажа.");
            }
        }
        else
        {
            GUILayout.Label(
                "Очікуємо реєстрацію твого гравця в Lobby...");
        }
    }

    private void DrawMatchStartControls(LobbySessionManager lobby)
    {
        GUILayout.Space(10);

        bool allReady = AreAllConnectedPlayersReady(
            lobby,
            out int connectedCount,
            out int readyCount);

        GUILayout.Label(
            $"Готовність: {readyCount}/{connectedCount}");

        if (manager.IsHost)
        {
            DrawHostControls(lobby, allReady);
        }
        else
        {
            GUILayout.Label(
                "Очікуємо, поки хост розпочне матч...");
        }
    }

    private void DrawHostControls(
        LobbySessionManager lobby,
        bool allReady)
    {
        GUILayout.Space(5);
        GUILayout.Label("КЕРУВАННЯ МАТЧЕМ (ХОСТ)");

        GUI.enabled = allReady;

        if (GUILayout.Button(
                "ПОЧАТИ МАТЧ",
                GUILayout.Height(46)))
        {
            Debug.Log(
                "[RelayNetworkUI] Хост натиснув ПОЧАТИ МАТЧ.");

            lobby.RequestStartMatch();
        }

        GUI.enabled = true;

        if (!allReady)
        {
            GUILayout.Label(
                "Кнопка стане активною, коли всі підключені " +
                "гравці виберуть клас і будуть готові.");
        }
    }

    #endregion

    #region Main UI

    private void OnGUI()
    {
        GUILayout.BeginArea(
            new Rect(20, 20, 460, 1000),
            GUI.skin.box);

        DrawHeader();
        DrawConnectionControls();

        if (manager != null && manager.IsListening)
        {
            DrawConnectedSession();
        }

        GUILayout.EndArea();
    }

    private void DrawHeader()
    {
        GUILayout.Label("NEXUS DEFENSE — LOBBY");
        GUILayout.Space(8);
        GUILayout.Label(status);
    }

    private void DrawConnectionControls()
    {
        bool canConnect =
            servicesReady &&
            !busy &&
            manager != null &&
            !manager.IsListening;

        GUI.enabled = canConnect;

        if (GUILayout.Button(
                "Створити кімнату (Host)",
                GUILayout.Height(36)))
        {
            _ = HostGame();
        }

        GUILayout.Space(10);
        GUILayout.Label("Код кімнати:");

        codeToJoin = GUILayout.TextField(codeToJoin, 16);

        if (GUILayout.Button(
                "Приєднатися (Client)",
                GUILayout.Height(36)))
        {
            _ = JoinGame();
        }

        GUI.enabled = true;
    }

    #endregion

    #region Connected Session UI

    private void DrawConnectedSession()
    {
        GUILayout.Space(15);

        GUILayout.Label(
            manager.IsHost ? "Роль: Host" : "Роль: Client");

        DrawJoinCode();

        LobbySessionManager lobby = LobbySessionManager.Instance;

        if (lobby != null)
        {
            DrawLobby(lobby);
        }
        else
        {
            GUILayout.Label("Підключення до Lobby Manager...");
        }

        DrawDisconnectButton();
    }

    private void DrawJoinCode()
    {
        if (string.IsNullOrEmpty(joinCode))
        {
            return;
        }

        GUILayout.Label("Код для друзів:");
        GUILayout.TextField(joinCode);

        if (GUILayout.Button("Скопіювати код"))
        {
            GUIUtility.systemCopyBuffer = joinCode;
        }
    }

    private void DrawDisconnectButton()
    {
        GUILayout.Space(10);

        if (GUILayout.Button(
                "ВІД'ЄДНАТИСЯ",
                GUILayout.Height(34)))
        {
            manager.Shutdown();

            LobbySessionManager.ClearPendingIdentities();

            joinCode = "";
            status = "Від'єднано.";
        }
    }

    #endregion
}
