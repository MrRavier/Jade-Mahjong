using System;
using JadeMahjong.Networking;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace JadeMahjong.Runtime
{
    public enum JadePhase
    {
        Menu,
        Lobby,
        Countdown,
        Playing,
        Finished
    }

    [RequireComponent(typeof(LanSession))]
    public sealed class JadeGameApp : MonoBehaviour
    {
        private MahjongBoardView _board;
        private LanSession _session;
        private CelestialAudio _audio;
        private double _startAt;
        private double _finishAt;
        private float _timePenalty;
        private bool _training;
        private bool _localWon;
        private int _rivalRemaining = 144;
        private int _hints = 3;
        private string _commentary = "Dois competidores, um palácio e cento e quarenta e quatro destinos.";
        private int _emperorPose;

        public JadePhase Phase { get; private set; } = JadePhase.Menu;
        public MahjongBoardView Board => _board;
        public LanSession Session => _session;
        public int LocalRemaining => _board?.Remaining ?? 144;
        public int RivalRemaining => _rivalRemaining;
        public int HintsRemaining => _hints;
        public bool IsTraining => _training;
        public bool LocalWon => _localWon;
        public string Commentary => _commentary;
        public int EmperorPose => _emperorPose;
        public bool CanRedeal => Phase == JadePhase.Playing && _board != null && !_board.HasAvailablePair();
        public double CountdownRemaining => Mathf.Max(0f, (float)(_startAt - CurrentClock));
        public double ElapsedSeconds
        {
            get
            {
                if (Phase is JadePhase.Menu or JadePhase.Lobby)
                    return 0d;
                var end = Phase == JadePhase.Finished ? _finishAt : CurrentClock;
                return Math.Max(0d, end - _startAt) + _timePenalty;
            }
        }

        private double CurrentClock => _training || !_session.IsRunning
            ? Time.unscaledTimeAsDouble
            : _session.NetworkTime();

        public event Action StateChanged;
        public event Action DataChanged;
        public event Action<string, int> CommentaryChanged;

        private void Awake()
        {
            Application.targetFrameRate = 60;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            _session = GetComponent<LanSession>();
            _audio = gameObject.AddComponent<CelestialAudio>();
            BuildWorld();
        }

        private void Start()
        {
            _board.RemainingChanged += OnRemainingChanged;
            _board.PairRemoved += _audio.Pair;
            _board.Mismatch += _audio.Error;
            _board.BoardCleared += OnBoardCleared;
            _board.CommentaryRequested += Say;

            _session.RivalConnectionChanged += OnRivalConnection;
            _session.MatchStarted += OnMatchStarted;
            _session.RivalProgressChanged += OnRivalProgress;
            _session.MatchFinished += OnMatchFinished;
            _session.SessionError += OnSessionError;

            SetPhase(JadePhase.Menu);
            Say(_commentary, 0);
        }

        private void Update()
        {
            if (Phase == JadePhase.Countdown && CurrentClock >= _startAt)
            {
                _board.InputEnabled = true;
                SetPhase(JadePhase.Playing);
                _audio.Gong();
                Say("Comecem! Que a mão mais veloz honre o Palácio de Jade.", 2);
            }

            if (Phase == JadePhase.Playing)
                ReadBoardInput();

            if (Keyboard.current?.escapeKey.wasPressedThisFrame == true)
                ReturnToMenu();

            DataChanged?.Invoke();
        }

        public void Host()
        {
            _training = false;
            if (!_session.StartHostSession())
                return;
            SetPhase(JadePhase.Lobby);
            Say("Sala aberta. Mostre o código ao segundo competidor.", 0);
        }

        public void Join(string codeOrAddress)
        {
            _training = false;
            if (!_session.StartClientSession(codeOrAddress))
                return;
            SetPhase(JadePhase.Lobby);
            Say("Buscando o anfitrião na rede local...", 0);
        }

        public void StartHostMatch()
        {
            if (_session.IsHost && _session.RivalConnected)
                _session.StartCompetitiveMatch();
        }

        public void StartTraining()
        {
            _session.StopSession();
            _training = true;
            _rivalRemaining = 144;
            PrepareMatch(unchecked(Environment.TickCount * 31), Time.unscaledTimeAsDouble + 1.5d);
            Say("Treino imperial. Limpe o tabuleiro sem a pressão de um rival.", 1);
        }

        public void UseHint()
        {
            if (Phase != JadePhase.Playing || _hints <= 0)
                return;
            if (!_board.ShowHint())
                return;
            _hints--;
            _timePenalty += 2f;
            DataChanged?.Invoke();
        }

        public void Redeal()
        {
            if (!CanRedeal)
                return;
            _timePenalty += 5f;
            _board.Redeal(unchecked(Environment.TickCount ^ LocalRemaining * 7919));
            DataChanged?.Invoke();
        }

        public void ToggleMute(bool muted)
        {
            _audio.SetMuted(muted);
        }

        public void ReturnToMenu()
        {
            _board.InputEnabled = false;
            _session.StopSession();
            _training = false;
            _rivalRemaining = 144;
            SetPhase(JadePhase.Menu);
            Say("A corte aguarda um novo desafio.", 0);
        }

        private void BuildWorld()
        {
            var backgroundObject = new GameObject("Celestial Palace Background");
            backgroundObject.transform.SetParent(transform, false);
            var background = backgroundObject.AddComponent<SpriteRenderer>();
            background.sprite = PixelArt.Background();
            background.sortingOrder = -100;
            if (background.sprite != null)
            {
                var camera = Camera.main;
                var targetHeight = camera != null ? camera.orthographicSize * 2f : 7.2f;
                var targetWidth = targetHeight * (camera != null ? camera.aspect : 16f / 9f);
                var bounds = background.sprite.bounds.size;
                var scale = Mathf.Max(targetWidth / bounds.x, targetHeight / bounds.y);
                backgroundObject.transform.localScale = Vector3.one * scale;
            }

            var matObject = new GameObject("Carved Jade Table");
            matObject.transform.SetParent(transform, false);
            matObject.transform.localPosition = new Vector3(-1.65f, -0.18f, 0f);
            var mat = matObject.AddComponent<SpriteRenderer>();
            mat.sprite = PixelArt.Panel();
            mat.drawMode = SpriteDrawMode.Sliced;
            mat.size = new Vector2(8.9f, 5.55f);
            mat.color = new Color(0.02f, 0.18f, 0.17f, 0.92f);
            mat.sortingOrder = -50;

            var boardObject = new GameObject("Shanghai Board");
            boardObject.transform.SetParent(transform, false);
            _board = boardObject.AddComponent<MahjongBoardView>();
            _board.InputEnabled = false;
        }

        private void PrepareMatch(int seed, double startAt)
        {
            _startAt = startAt;
            _finishAt = 0d;
            _timePenalty = 0f;
            _localWon = false;
            _rivalRemaining = 144;
            _hints = 3;
            _board.InputEnabled = false;
            _board.Build(seed);
            SetPhase(JadePhase.Countdown);
        }

        private void OnMatchStarted(int seed, double startAt)
        {
            PrepareMatch(seed, startAt);
            Say("O mesmo destino foi servido aos dois competidores.", 1);
        }

        private void OnRemainingChanged(int remaining)
        {
            if (Phase is JadePhase.Playing or JadePhase.Countdown)
                _session.ReportProgress(remaining);
            DataChanged?.Invoke();
        }

        private void OnRivalProgress(int remaining)
        {
            _rivalRemaining = remaining;
            if (Phase == JadePhase.Playing && remaining < LocalRemaining)
                Say("Seu rival tomou a dianteira. Serenidade e velocidade.", 3);
            DataChanged?.Invoke();
        }

        private void OnRivalConnection(bool connected)
        {
            if (connected)
                Say(_session.IsHost
                    ? "O desafiante entrou. Quando estiver pronto, toque em INICIAR."
                    : "Conexão aceita. Aguarde o anfitrião tocar o gongo.", 1);
            DataChanged?.Invoke();
        }

        private void OnBoardCleared()
        {
            if (_training)
                Finish(true);
        }

        private void OnMatchFinished(bool localWon)
        {
            Finish(localWon);
        }

        private void Finish(bool localWon)
        {
            if (Phase == JadePhase.Finished)
                return;
            _localWon = localWon;
            _finishAt = CurrentClock;
            _board.InputEnabled = false;
            SetPhase(JadePhase.Finished);
            if (localWon)
            {
                _audio.Victory();
                Say("Vitória! O Imperador de Jade reconhece sua mão celestial.", 4);
            }
            else
            {
                Say("Seu rival limpou o destino primeiro. A corte espera a revanche.", 5);
            }
        }

        private void OnSessionError(string message)
        {
            if (Phase is JadePhase.Playing or JadePhase.Countdown)
            {
                _localWon = false;
                _finishAt = CurrentClock;
                _board.InputEnabled = false;
                SetPhase(JadePhase.Finished);
            }
            Say(message, 3);
        }

        private void SetPhase(JadePhase phase)
        {
            Phase = phase;
            StateChanged?.Invoke();
            DataChanged?.Invoke();
        }

        private void Say(string line, int pose)
        {
            _commentary = line;
            _emperorPose = Mathf.Clamp(pose, 0, 5);
            CommentaryChanged?.Invoke(_commentary, _emperorPose);
            DataChanged?.Invoke();
        }

        private void ReadBoardInput()
        {
            if (Touchscreen.current != null &&
                Touchscreen.current.primaryTouch.press.wasPressedThisFrame)
            {
                var touchId = Touchscreen.current.primaryTouch.touchId.ReadValue();
                if (EventSystem.current == null || !EventSystem.current.IsPointerOverGameObject(touchId))
                    SelectScreenPoint(Touchscreen.current.primaryTouch.position.ReadValue());
                return;
            }

            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            {
                if (EventSystem.current == null || !EventSystem.current.IsPointerOverGameObject())
                    SelectScreenPoint(Mouse.current.position.ReadValue());
            }
        }

        private void SelectScreenPoint(Vector2 screenPoint)
        {
            var camera = Camera.main;
            if (camera == null)
                return;
            var world = camera.ScreenToWorldPoint(new Vector3(screenPoint.x, screenPoint.y, -camera.transform.position.z));
            _board.SelectAt(world);
        }

        private void OnDestroy()
        {
            if (_board != null)
            {
                _board.RemainingChanged -= OnRemainingChanged;
                _board.PairRemoved -= _audio.Pair;
                _board.Mismatch -= _audio.Error;
                _board.BoardCleared -= OnBoardCleared;
                _board.CommentaryRequested -= Say;
            }
            if (_session != null)
            {
                _session.RivalConnectionChanged -= OnRivalConnection;
                _session.MatchStarted -= OnMatchStarted;
                _session.RivalProgressChanged -= OnRivalProgress;
                _session.MatchFinished -= OnMatchFinished;
                _session.SessionError -= OnSessionError;
            }
        }
    }
}
