using System;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using Unity.Collections;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

namespace JadeMahjong.Networking
{
    public sealed class LanSession : MonoBehaviour
    {
        public const ushort Port = 7777;
        private const string StartMessage = "jade.start";
        private const string ProgressMessage = "jade.progress";
        private const string ResultMessage = "jade.result";

        private NetworkManager _network;
        private UnityTransport _transport;
        private bool _handlersRegistered;
        private int _hostRemaining = 144;
        private int _guestRemaining = 144;
        private bool _finished;

        public bool IsHost => _network != null && _network.IsHost;
        public bool IsClient => _network != null && _network.IsClient && !_network.IsHost;
        public bool IsRunning => _network != null && (_network.IsHost || _network.IsClient);
        public bool RivalConnected { get; private set; }
        public string LocalAddress { get; private set; } = "127.0.0.1";
        public string RoomCode => LanRoomCode.Encode(LocalAddress);

        public event Action<bool> RivalConnectionChanged;
        public event Action<int, double> MatchStarted;
        public event Action<int> RivalProgressChanged;
        public event Action<bool> MatchFinished;
        public event Action<string> SessionError;

        private void Awake()
        {
            _network = GetComponent<NetworkManager>();
            _transport = GetComponent<UnityTransport>();
            LocalAddress = FindPrivateIpv4();
            _network.NetworkConfig.EnableSceneManagement = false;
            _network.NetworkConfig.ConnectionApproval = true;
            _network.NetworkConfig.TickRate = 30;
            _network.ConnectionApprovalCallback = ApproveConnection;
            _network.OnClientConnectedCallback += OnClientConnected;
            _network.OnClientDisconnectCallback += OnClientDisconnected;
        }

        public bool StartHostSession()
        {
            StopSession();
            LocalAddress = FindPrivateIpv4();
            _transport.SetConnectionData(LocalAddress, Port, "0.0.0.0");
            ResetScore();
            if (!_network.StartHost())
            {
                SessionError?.Invoke("Não foi possível abrir a sala neste Wi-Fi.");
                return false;
            }

            RegisterHandlers();
            RivalConnected = false;
            RivalConnectionChanged?.Invoke(false);
            return true;
        }

        public bool StartClientSession(string codeOrAddress)
        {
            StopSession();
            if (!LanRoomCode.TryResolve(codeOrAddress, out var address))
            {
                SessionError?.Invoke("Código ou IP inválido.");
                return false;
            }

            _transport.SetConnectionData(address, Port);
            ResetScore();
            if (!_network.StartClient())
            {
                SessionError?.Invoke("Não foi possível iniciar a conexão.");
                return false;
            }

            RegisterHandlers();
            return true;
        }

        public void StartCompetitiveMatch()
        {
            if (!IsHost || !RivalConnected)
                return;

            ResetScore();
            var seed = unchecked(Environment.TickCount * 397 ^ DateTime.UtcNow.Millisecond);
            var startTime = _network.ServerTime.Time + 3.0d;
            foreach (var clientId in _network.ConnectedClientsIds.Where(id => id != NetworkManager.ServerClientId))
            {
                using var writer = new FastBufferWriter(sizeof(int) + sizeof(double), Allocator.Temp);
                writer.WriteValueSafe(seed);
                writer.WriteValueSafe(startTime);
                _network.CustomMessagingManager.SendNamedMessage(
                    StartMessage, clientId, writer, NetworkDelivery.ReliableSequenced);
            }

            MatchStarted?.Invoke(seed, startTime);
        }

        public void ReportProgress(int remaining)
        {
            if (!IsRunning || _finished)
                return;
            remaining = Mathf.Clamp(remaining, 0, 144);
            if ((remaining & 1) != 0)
                return;

            if (IsHost)
            {
                if (remaining > _hostRemaining)
                    return;
                _hostRemaining = remaining;
                BroadcastHostProgress(remaining);
                TryFinish(NetworkManager.ServerClientId, remaining);
                return;
            }

            using var writer = new FastBufferWriter(sizeof(int), Allocator.Temp);
            writer.WriteValueSafe(remaining);
            _network.CustomMessagingManager.SendNamedMessage(
                ProgressMessage, NetworkManager.ServerClientId, writer,
                NetworkDelivery.ReliableSequenced);
        }

        public double NetworkTime()
        {
            return IsRunning ? _network.ServerTime.Time : Time.unscaledTimeAsDouble;
        }

        public void StopSession()
        {
            UnregisterHandlers();
            if (_network != null && (_network.IsClient || _network.IsServer))
                _network.Shutdown();
            RivalConnected = false;
        }

        private void RegisterHandlers()
        {
            if (_handlersRegistered || _network.CustomMessagingManager == null)
                return;
            _network.CustomMessagingManager.RegisterNamedMessageHandler(StartMessage, ReceiveStart);
            _network.CustomMessagingManager.RegisterNamedMessageHandler(ProgressMessage, ReceiveProgress);
            _network.CustomMessagingManager.RegisterNamedMessageHandler(ResultMessage, ReceiveResult);
            _handlersRegistered = true;
        }

        private void UnregisterHandlers()
        {
            if (!_handlersRegistered || _network == null || _network.CustomMessagingManager == null)
                return;
            _network.CustomMessagingManager.UnregisterNamedMessageHandler(StartMessage);
            _network.CustomMessagingManager.UnregisterNamedMessageHandler(ProgressMessage);
            _network.CustomMessagingManager.UnregisterNamedMessageHandler(ResultMessage);
            _handlersRegistered = false;
        }

        private void ApproveConnection(NetworkManager.ConnectionApprovalRequest request,
            NetworkManager.ConnectionApprovalResponse response)
        {
            var roomHasSpace = _network.ConnectedClientsIds.Count < 2;
            response.Approved = roomHasSpace;
            response.CreatePlayerObject = false;
            response.PlayerPrefabHash = null;
            response.Position = null;
            response.Rotation = null;
            response.Pending = false;
            response.Reason = roomHasSpace ? string.Empty : "A sala já tem dois jogadores.";
        }

        private void OnClientConnected(ulong clientId)
        {
            if (IsHost && clientId != NetworkManager.ServerClientId)
            {
                RivalConnected = true;
                RivalConnectionChanged?.Invoke(true);
            }
            else if (IsClient && clientId == _network.LocalClientId)
            {
                RivalConnected = true;
                RivalConnectionChanged?.Invoke(true);
            }
        }

        private void OnClientDisconnected(ulong clientId)
        {
            if (IsHost && clientId == NetworkManager.ServerClientId)
                return;
            if (IsHost && clientId != NetworkManager.ServerClientId)
            {
                RivalConnected = false;
                RivalConnectionChanged?.Invoke(false);
                SessionError?.Invoke("O outro jogador saiu da sala.");
            }
            else if (IsClient)
            {
                RivalConnected = false;
                RivalConnectionChanged?.Invoke(false);
                SessionError?.Invoke("A conexão com o anfitrião foi encerrada.");
            }
        }

        private void ReceiveStart(ulong sender, FastBufferReader reader)
        {
            if (!IsClient || sender != NetworkManager.ServerClientId)
                return;
            reader.ReadValueSafe(out int seed);
            reader.ReadValueSafe(out double startTime);
            ResetScore();
            MatchStarted?.Invoke(seed, startTime);
        }

        private void ReceiveProgress(ulong sender, FastBufferReader reader)
        {
            reader.ReadValueSafe(out int remaining);
            if (remaining < 0 || remaining > 144 || (remaining & 1) != 0)
                return;

            if (IsHost)
            {
                if (sender == NetworkManager.ServerClientId || remaining > _guestRemaining)
                    return;
                _guestRemaining = remaining;
                RivalProgressChanged?.Invoke(remaining);
                TryFinish(sender, remaining);
            }
            else if (sender == NetworkManager.ServerClientId)
            {
                if (remaining > _hostRemaining)
                    return;
                _hostRemaining = remaining;
                RivalProgressChanged?.Invoke(remaining);
            }
        }

        private void ReceiveResult(ulong sender, FastBufferReader reader)
        {
            if (!IsClient || sender != NetworkManager.ServerClientId)
                return;
            reader.ReadValueSafe(out ulong winner);
            _finished = true;
            MatchFinished?.Invoke(winner == _network.LocalClientId);
        }

        private void BroadcastHostProgress(int remaining)
        {
            foreach (var clientId in _network.ConnectedClientsIds.Where(id => id != NetworkManager.ServerClientId))
            {
                using var writer = new FastBufferWriter(sizeof(int), Allocator.Temp);
                writer.WriteValueSafe(remaining);
                _network.CustomMessagingManager.SendNamedMessage(
                    ProgressMessage, clientId, writer, NetworkDelivery.ReliableSequenced);
            }
        }

        private void TryFinish(ulong reporter, int remaining)
        {
            if (_finished || remaining != 0)
                return;
            _finished = true;
            foreach (var clientId in _network.ConnectedClientsIds.Where(id => id != NetworkManager.ServerClientId))
            {
                using var writer = new FastBufferWriter(sizeof(ulong), Allocator.Temp);
                writer.WriteValueSafe(reporter);
                _network.CustomMessagingManager.SendNamedMessage(
                    ResultMessage, clientId, writer, NetworkDelivery.ReliableSequenced);
            }
            MatchFinished?.Invoke(reporter == NetworkManager.ServerClientId);
        }

        private void ResetScore()
        {
            _hostRemaining = 144;
            _guestRemaining = 144;
            _finished = false;
        }

        private static string FindPrivateIpv4()
        {
            try
            {
                var candidates = NetworkInterface.GetAllNetworkInterfaces()
                    .Where(network => network.OperationalStatus == OperationalStatus.Up)
                    .SelectMany(network => network.GetIPProperties().UnicastAddresses
                        .Select(info => new
                        {
                            network.Name,
                            Address = info.Address
                        }))
                    .Where(item => item.Address.AddressFamily == AddressFamily.InterNetwork &&
                                   !IPAddress.IsLoopback(item.Address))
                    .OrderByDescending(item =>
                        item.Name.Contains("wlan", StringComparison.OrdinalIgnoreCase) ||
                        item.Name.Contains("wifi", StringComparison.OrdinalIgnoreCase))
                    .Select(item => item.Address.ToString())
                    .ToList();

                return candidates.FirstOrDefault(IsPrivate) ?? candidates.FirstOrDefault() ?? "127.0.0.1";
            }
            catch
            {
                return "127.0.0.1";
            }
        }

        private static bool IsPrivate(string address)
        {
            var bytes = IPAddress.Parse(address).GetAddressBytes();
            return bytes[0] == 10 ||
                   (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31) ||
                   (bytes[0] == 192 && bytes[1] == 168);
        }

        private void OnDestroy()
        {
            if (_network == null)
                return;
            _network.OnClientConnectedCallback -= OnClientConnected;
            _network.OnClientDisconnectCallback -= OnClientDisconnected;
            UnregisterHandlers();
        }
    }
}
