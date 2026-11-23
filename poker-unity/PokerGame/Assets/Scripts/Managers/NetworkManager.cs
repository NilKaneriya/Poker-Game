using System;
using System.Collections.Generic;
using Firesplash.GameDevAssets.SocketIO;
using Newtonsoft.Json;
using Poker.Core;
using UnityEngine;
using static Poker.Core.Constants;
using static Poker.Network.DataModel;

namespace Poker.Network
{
    /// <summary>
    /// The real-time connection to poker-server (Socket.IO).
    /// Created once by AppServices and kept across scenes. Scenes subscribe to the
    /// events below and call the Send methods; they never touch Socket.IO directly.
    /// All callbacks run on the Unity main thread.
    /// </summary>
    [RequireComponent(typeof(SocketIOCommunicator))]
    public class NetworkManager : MonoBehaviour
    {
        public static NetworkManager Instance { get; private set; }

        public event Action<SessionInfo> SessionStarted;
        public event Action<string> Disconnected;      // reason
        public event Action<string> ConnectFailed;     // reason
        public event Action AuthRejected;              // login token no longer valid
        public event Action<List<TableSummary>> TableListReceived;
        public event Action<TableState> TableStateReceived;
        public event Action<string> ActionRejected;
        public event Action<string> ServerErrorReceived;
        public event Action<PlayerStats> StatsReceived;
        public event Action<HandHistoryList> HandHistoryReceived;
        /// <summary>
        /// We were at a table, but the server says we aren't any more. The argument is true when
        /// the server restarted, false when our seat was released (away longer than the grace period).
        /// </summary>
        public event Action<bool> TableLost;

        public bool IsConnected => sio != null && sio.Instance.IsConnected();
        public bool HasConnectedOnce { get; private set; }
        public TableState CurrentTable { get; private set; }

        private SocketIOCommunicator sio;
        private string serverId; // from the last session message
        private ServerConfig config;

        private void Awake()
        {
            Instance = this;
            config = ServerConfig.Load();
            sio = GetComponent<SocketIOCommunicator>();
            RegisterHandlers();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // ---------- Connection ----------

        /// <summary>Open the socket using the current login token (log in first).</summary>
        public void Connect()
        {
            var auth = AuthManager.Instance;
            if (auth == null || !auth.IsLoggedIn)
            {
                Debug.LogWarning("[Network] Connect called before login.");
                return;
            }

            var payload = new SIOAuthPayload();
            payload.AddElement("token", auth.Token); // read by the server as socket.handshake.auth.token
            Log($"Connecting to {config.ServerAddress}");
            sio.Instance.Connect(config.ServerAddress, true, payload);
        }

        public void Disconnect()
        {
            CurrentTable = null;
            HasConnectedOnce = false;
            sio.Instance.Close();
        }

        // ---------- Client -> Server ----------

        public void RequestTableList() => Emit(PokerEvents.ListTables);

        public void RequestStats() => Emit(PokerEvents.GetStats);

        public void RequestHandHistory() => Emit(PokerEvents.GetHandHistory);

        public void CreateTable(string tableName, string mode, int maxSeats) =>
            Emit(PokerEvents.CreateTable, new CreateTableRequest { tableName = tableName, mode = mode, maxSeats = maxSeats });

        /// <summary>Host only: start a friends table before every seat is filled.</summary>
        public void StartGame() => Emit(PokerEvents.StartGame);

        public void JoinTable(string code) =>
            Emit(PokerEvents.JoinTable, new JoinTableRequest { code = code });

        public void LeaveTable()
        {
            Emit(PokerEvents.LeaveTable);
            CurrentTable = null;
        }

        /// <summary>For raises, amount is the total bet to raise to on this street.</summary>
        public void SendAction(string action, int amount = 0) =>
            Emit(PokerEvents.PlayerAction, new PlayerActionRequest { action = action, amount = amount });

        public void Rebuy() => Emit(PokerEvents.Rebuy);

        // ---------- Transport for feature modules ----------
        // Modules such as Chat have their own events and payloads. They use the socket
        // through these two methods, so this class never needs to know about them.

        /// <summary>Subscribe to a server event; the JSON payload is parsed into T.</summary>
        public void Listen<T>(string eventName, Action<T> handler) => On(eventName, handler);

        /// <summary>Send an event, with or without a payload (serialized to JSON).</summary>
        public void Send(string eventName, object payload = null)
        {
            if (payload == null) Emit(eventName);
            else Emit(eventName, payload);
        }

        // ---------- Server -> Client ----------

        private void RegisterHandlers()
        {
            var io = sio.Instance;

            io.On(PokerEvents.SocketConnect, _ =>
            {
                HasConnectedOnce = true;
                Log("Socket connected");
            });
            io.On(PokerEvents.SocketConnectError, reason =>
            {
                Debug.LogWarning($"[Network] Connect error: {reason}");
                if (reason != null && reason.IndexOf("unauthorized", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    // Token expired or revoked: stop retrying and send the player to log in again.
                    sio.Instance.Close();
                    AuthRejected?.Invoke();
                    return;
                }
                ConnectFailed?.Invoke(reason);
            });
            io.On(PokerEvents.SocketDisconnect, reason =>
            {
                Log($"Disconnected: {reason}");
                Disconnected?.Invoke(reason);
            });

            // The same account signed in somewhere else (e.g. another tab): this one is closed.
            On<ServerError>(PokerEvents.SignedInElsewhere, info =>
            {
                SceneNotice.Set(string.IsNullOrEmpty(info?.message) ? "You signed in from another tab or device." : info.message);
                sio.Instance.Close();
                AuthRejected?.Invoke();
            });
            On<SessionInfo>(PokerEvents.Session, session =>
            {
                bool lostTable = CurrentTable != null && session.tableCode != CurrentTable.code;
                bool serverRestarted = serverId != null && session.serverId != serverId;
                serverId = session.serverId;
                if (lostTable) CurrentTable = null;
                SessionStarted?.Invoke(session);
                if (lostTable) TableLost?.Invoke(serverRestarted);
            });
            On<PlayerStats>(PokerEvents.Stats, stats => StatsReceived?.Invoke(stats));
            On<HandHistoryList>(PokerEvents.HandHistory, list => HandHistoryReceived?.Invoke(list));
            On<List<TableSummary>>(PokerEvents.TableList, list => TableListReceived?.Invoke(list));
            On<TableState>(PokerEvents.TableState, table =>
            {
                CurrentTable = table;
                TableStateReceived?.Invoke(table);
            });
            On<ActionRejectedInfo>(PokerEvents.ActionRejected, info => ActionRejected?.Invoke(info.reason));
            On<ServerError>(PokerEvents.Error, err => ServerErrorReceived?.Invoke(err.message));
        }

        // ---------- Helpers ----------

        /// <summary>Subscribe to an event and parse its JSON payload into T.</summary>
        private void On<T>(string eventName, Action<T> handler)
        {
            sio.Instance.On(eventName, payload =>
            {
                Log($"<- {eventName} {payload}");
                T data;
                try
                {
                    data = string.IsNullOrEmpty(payload) ? default : JsonConvert.DeserializeObject<T>(payload);
                }
                catch (Exception e)
                {
                    Debug.LogError($"[Network] Failed to parse '{eventName}': {e.Message}\n{payload}");
                    return;
                }
                handler(data);
            });
        }

        private void Emit(string eventName)
        {
            if (!EnsureConnected(eventName)) return;
            Log($"-> {eventName}");
            sio.Instance.Emit(eventName);
        }

        private void Emit(string eventName, object payload)
        {
            if (!EnsureConnected(eventName)) return;
            string json = JsonConvert.SerializeObject(payload);
            Log($"-> {eventName} {json}");
            sio.Instance.Emit(eventName, json, false); // false = payload is JSON, not plain text
        }

        private bool EnsureConnected(string eventName)
        {
            if (IsConnected) return true;
            Debug.LogWarning($"[Network] Not connected; dropped '{eventName}'");
            return false;
        }

        private void Log(string msg)
        {
            if (config.logNetworkEvents) Debug.Log($"[Network] {msg}");
        }
    }
}
