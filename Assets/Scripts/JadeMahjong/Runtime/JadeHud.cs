using System;
using JadeMahjong.Networking;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace JadeMahjong.Runtime
{
    public sealed class JadeHud : MonoBehaviour
    {
        private JadeGameApp _app;
        private Font _font;
        private RectTransform _safeRoot;
        private Rect _lastSafeArea;
        private GameObject _menuPanel;
        private GameObject _lobbyPanel;
        private GameObject _countdownPanel;
        private GameObject _resultPanel;
        private GameObject _actions;
        private Text _timer;
        private Text _localLabel;
        private Text _rivalLabel;
        private Image _localFill;
        private Image _rivalFill;
        private Text _speech;
        private Image _emperor;
        private Sprite[] _emperorPoses;
        private InputField _joinInput;
        private Text _room;
        private Text _lobbyStatus;
        private Button _startButton;
        private Text _countdown;
        private Text _hintLabel;
        private Button _redealButton;
        private Text _resultTitle;
        private Text _resultBody;
        private bool _muted;

        private static readonly Color Ink = new(0.04f, 0.11f, 0.11f, 1f);
        private static readonly Color Ivory = new(0.98f, 0.91f, 0.71f, 1f);
        private static readonly Color Gold = new(0.94f, 0.72f, 0.2f, 1f);
        private static readonly Color Jade = new(0.12f, 0.48f, 0.34f, 1f);
        private static readonly Color Vermilion = new(0.78f, 0.2f, 0.14f, 1f);

        private void Start()
        {
            _app = GetComponent<JadeGameApp>();
            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            EnsureEventSystem();
            Build();
            _app.StateChanged += Refresh;
            _app.DataChanged += Refresh;
            _app.CommentaryChanged += OnCommentary;
            Refresh();
        }

        private void Update()
        {
            ApplySafeArea();
        }

        private void Build()
        {
            var canvasObject = new GameObject("Jade HUD", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            _safeRoot = Rect("Safe Area", canvasObject.transform, Vector2.zero, Vector2.one);
            BuildTopBar();
            BuildRightRail();
            BuildActions();
            BuildMenu();
            BuildLobby();
            BuildCountdown();
            BuildResult();
            ApplySafeArea();
        }

        private void BuildTopBar()
        {
            var panel = Panel("Status Bar", _safeRoot, new Vector2(0.015f, 0.865f), new Vector2(0.755f, 0.985f));
            Text("JADE MAHJONG", panel.transform, new Vector2(0.025f, 0.55f), new Vector2(0.28f, 0.93f),
                25, Gold, TextAnchor.MiddleLeft);
            _timer = Text("00:00.0", panel.transform, new Vector2(0.42f, 0.54f), new Vector2(0.59f, 0.94f),
                27, Ivory, TextAnchor.MiddleCenter);
            _localLabel = Text("VOCÊ 144", panel.transform, new Vector2(0.025f, 0.13f), new Vector2(0.18f, 0.48f),
                17, Ivory, TextAnchor.MiddleLeft);
            _localFill = Progress(panel.transform, new Vector2(0.18f, 0.18f), new Vector2(0.45f, 0.43f), Jade);
            _rivalLabel = Text("RIVAL 144", panel.transform, new Vector2(0.52f, 0.13f), new Vector2(0.67f, 0.48f),
                17, Ivory, TextAnchor.MiddleLeft);
            _rivalFill = Progress(panel.transform, new Vector2(0.67f, 0.18f), new Vector2(0.96f, 0.43f), Vermilion);
        }

        private void BuildRightRail()
        {
            var rail = Panel("Imperial Rail", _safeRoot, new Vector2(0.765f, 0.025f), new Vector2(0.985f, 0.985f));
            Text("A CORTE DE JADE", rail.transform, new Vector2(0.08f, 0.92f), new Vector2(0.92f, 0.985f),
                20, Gold, TextAnchor.MiddleCenter);

            _emperor = Image("Jade Emperor", rail.transform, new Vector2(0.06f, 0.40f), new Vector2(0.94f, 0.91f),
                Color.white);
            _emperor.preserveAspect = true;
            _emperorPoses = new Sprite[6];
            for (var pose = 0; pose < _emperorPoses.Length; pose++)
                _emperorPoses[pose] = PixelArt.EmperorPose(pose);
            _emperor.sprite = _emperorPoses[0];

            var speechPanel = Panel("Speech", rail.transform, new Vector2(0.055f, 0.09f), new Vector2(0.945f, 0.41f));
            _speech = Text("", speechPanel.transform, new Vector2(0.07f, 0.12f), new Vector2(0.93f, 0.88f),
                18, Ivory, TextAnchor.MiddleCenter);
            _speech.horizontalOverflow = HorizontalWrapMode.Wrap;
            _speech.verticalOverflow = VerticalWrapMode.Truncate;

            Button("SOM", rail.transform, new Vector2(0.30f, 0.015f), new Vector2(0.70f, 0.085f), () =>
            {
                _muted = !_muted;
                _app.ToggleMute(_muted);
            });
        }

        private void BuildActions()
        {
            _actions = new GameObject("Actions", typeof(RectTransform));
            _actions.transform.SetParent(_safeRoot, false);
            SetAnchors(_actions.GetComponent<RectTransform>(), new Vector2(0.025f, 0.025f), new Vector2(0.745f, 0.14f));
            var hintButton = Button("DICA", _actions.transform, new Vector2(0.03f, 0.08f), new Vector2(0.34f, 0.92f),
                _app.UseHint);
            _hintLabel = hintButton.GetComponentInChildren<Text>();
            _redealButton = Button("REORDENAR", _actions.transform, new Vector2(0.37f, 0.08f), new Vector2(0.70f, 0.92f),
                _app.Redeal);
            Button("SAIR", _actions.transform, new Vector2(0.73f, 0.08f), new Vector2(0.97f, 0.92f),
                _app.ReturnToMenu);
        }

        private void BuildMenu()
        {
            _menuPanel = Panel("Main Menu", _safeRoot, new Vector2(0.12f, 0.15f), new Vector2(0.67f, 0.82f));
            var root = _menuPanel.transform;
            Text("DUELO CELESTIAL", root, new Vector2(0.08f, 0.82f), new Vector2(0.92f, 0.96f),
                34, Gold, TextAnchor.MiddleCenter);
            Text("SHANGHAI MAHJONG • 2 JOGADORES", root, new Vector2(0.08f, 0.72f), new Vector2(0.92f, 0.82f),
                17, Ivory, TextAnchor.MiddleCenter);

            Button("CRIAR SALA", root, new Vector2(0.12f, 0.56f), new Vector2(0.88f, 0.69f), _app.Host);
            _joinInput = Input(root, new Vector2(0.12f, 0.37f), new Vector2(0.58f, 0.51f),
                "CÓDIGO OU IP");
            Button("ENTRAR", root, new Vector2(0.61f, 0.37f), new Vector2(0.88f, 0.51f),
                () => _app.Join(_joinInput.text));
            Button("TREINO SOLO", root, new Vector2(0.12f, 0.18f), new Vector2(0.88f, 0.31f),
                _app.StartTraining);
            Text("Mesmo Wi-Fi • sem servidor • sem mensalidade", root,
                new Vector2(0.10f, 0.04f), new Vector2(0.90f, 0.14f),
                15, new Color(0.65f, 0.83f, 0.72f), TextAnchor.MiddleCenter);
        }

        private void BuildLobby()
        {
            _lobbyPanel = Panel("Lobby", _safeRoot, new Vector2(0.13f, 0.17f), new Vector2(0.66f, 0.81f));
            var root = _lobbyPanel.transform;
            Text("SALÃO DOS DESAFIANTES", root, new Vector2(0.07f, 0.82f), new Vector2(0.93f, 0.96f),
                28, Gold, TextAnchor.MiddleCenter);
            _room = Text("", root, new Vector2(0.08f, 0.56f), new Vector2(0.92f, 0.80f),
                24, Ivory, TextAnchor.MiddleCenter);
            _lobbyStatus = Text("", root, new Vector2(0.08f, 0.38f), new Vector2(0.92f, 0.55f),
                17, Ivory, TextAnchor.MiddleCenter);
            Button("COPIAR CÓDIGO", root, new Vector2(0.10f, 0.24f), new Vector2(0.48f, 0.36f), () =>
            {
                if (_app.Session.IsHost)
                    GUIUtility.systemCopyBuffer = _app.Session.RoomCode;
            });
            _startButton = Button("INICIAR DUELO", root, new Vector2(0.52f, 0.24f), new Vector2(0.90f, 0.36f),
                _app.StartHostMatch);
            Button("VOLTAR", root, new Vector2(0.30f, 0.07f), new Vector2(0.70f, 0.19f),
                _app.ReturnToMenu);
        }

        private void BuildCountdown()
        {
            _countdownPanel = Panel("Countdown", _safeRoot, new Vector2(0.29f, 0.35f), new Vector2(0.56f, 0.69f));
            _countdown = Text("3", _countdownPanel.transform, new Vector2(0.05f, 0.15f), new Vector2(0.95f, 0.85f),
                72, Gold, TextAnchor.MiddleCenter);
        }

        private void BuildResult()
        {
            _resultPanel = Panel("Result", _safeRoot, new Vector2(0.15f, 0.20f), new Vector2(0.64f, 0.79f));
            var root = _resultPanel.transform;
            _resultTitle = Text("", root, new Vector2(0.08f, 0.68f), new Vector2(0.92f, 0.91f),
                37, Gold, TextAnchor.MiddleCenter);
            _resultBody = Text("", root, new Vector2(0.10f, 0.35f), new Vector2(0.90f, 0.67f),
                20, Ivory, TextAnchor.MiddleCenter);
            Button("REVANCHE", root, new Vector2(0.12f, 0.14f), new Vector2(0.48f, 0.30f), () =>
            {
                if (_app.IsTraining)
                    _app.StartTraining();
                else if (_app.Session.IsHost)
                    _app.StartHostMatch();
                else
                    _app.ReturnToMenu();
            });
            Button("MENU", root, new Vector2(0.52f, 0.14f), new Vector2(0.88f, 0.30f),
                _app.ReturnToMenu);
        }

        private void Refresh()
        {
            if (_app == null || _menuPanel == null)
                return;

            _menuPanel.SetActive(_app.Phase == JadePhase.Menu);
            _lobbyPanel.SetActive(_app.Phase == JadePhase.Lobby);
            _countdownPanel.SetActive(_app.Phase == JadePhase.Countdown);
            _resultPanel.SetActive(_app.Phase == JadePhase.Finished);
            _actions.SetActive(_app.Phase == JadePhase.Playing);

            _timer.text = FormatTime(_app.ElapsedSeconds);
            _localLabel.text = $"VOCÊ  {_app.LocalRemaining:000}";
            _rivalLabel.text = _app.IsTraining ? "TREINO" : $"RIVAL {_app.RivalRemaining:000}";
            _localFill.fillAmount = 1f - _app.LocalRemaining / 144f;
            _rivalFill.fillAmount = _app.IsTraining ? 0f : 1f - _app.RivalRemaining / 144f;
            _speech.text = _app.Commentary;
            _emperor.sprite = _emperorPoses[Mathf.Clamp(_app.EmperorPose, 0, 5)];

            _hintLabel.text = $"DICA  ×{_app.HintsRemaining}";
            _redealButton.interactable = _app.CanRedeal;

            if (_app.Phase == JadePhase.Lobby)
            {
                var host = _app.Session.IsHost;
                _room.text = host
                    ? $"SALA  {_app.Session.RoomCode}\nIP  {_app.Session.LocalAddress}:{LanSession.Port}"
                    : "ENTRANDO NA SALA...";
                _lobbyStatus.text = _app.Session.RivalConnected
                    ? (host ? "DESAFIANTE CONECTADO" : "CONECTADO • AGUARDANDO O GONGO")
                    : (host ? "AGUARDANDO O SEGUNDO CELULAR" : "PROCURANDO ANFITRIÃO");
                _startButton.gameObject.SetActive(host);
                _startButton.interactable = _app.Session.RivalConnected;
            }

            if (_app.Phase == JadePhase.Countdown)
                _countdown.text = Mathf.Max(1, Mathf.CeilToInt((float)_app.CountdownRemaining)).ToString();

            if (_app.Phase == JadePhase.Finished)
            {
                _resultTitle.text = _app.LocalWon ? "VITÓRIA CELESTIAL" : "A CORTE DECIDIU";
                _resultBody.text = _app.LocalWon
                    ? $"Você libertou as 144 peças em {FormatTime(_app.ElapsedSeconds)}."
                    : $"O rival terminou primeiro.\nSeu tempo: {FormatTime(_app.ElapsedSeconds)}.";
            }
        }

        private void OnCommentary(string line, int pose)
        {
            if (_speech != null)
                _speech.text = line;
            if (_emperor != null && _emperorPoses != null)
                _emperor.sprite = _emperorPoses[Mathf.Clamp(pose, 0, 5)];
        }

        private static string FormatTime(double seconds)
        {
            var span = TimeSpan.FromSeconds(Math.Max(0d, seconds));
            return $"{(int)span.TotalMinutes:00}:{span.Seconds:00}.{span.Milliseconds / 100}";
        }

        private void ApplySafeArea()
        {
            if (_safeRoot == null || Screen.safeArea == _lastSafeArea)
                return;
            _lastSafeArea = Screen.safeArea;
            var minimum = _lastSafeArea.position;
            var maximum = _lastSafeArea.position + _lastSafeArea.size;
            minimum.x /= Screen.width;
            minimum.y /= Screen.height;
            maximum.x /= Screen.width;
            maximum.y /= Screen.height;
            _safeRoot.anchorMin = minimum;
            _safeRoot.anchorMax = maximum;
            _safeRoot.offsetMin = Vector2.zero;
            _safeRoot.offsetMax = Vector2.zero;
        }

        private static void EnsureEventSystem()
        {
            if (EventSystem.current != null)
                return;
            var system = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            system.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
        }

        private GameObject Panel(string name, Transform parent, Vector2 min, Vector2 max)
        {
            var image = Image(name, parent, min, max, Color.white);
            image.sprite = PixelArt.Panel();
            image.type = Image.Type.Sliced;
            AddShadow(image.gameObject);
            return image.gameObject;
        }

        private Image Image(string name, Transform parent, Vector2 min, Vector2 max, Color color)
        {
            var rectangle = Rect(name, parent, min, max);
            var image = rectangle.gameObject.AddComponent<Image>();
            image.color = color;
            return image;
        }

        private Text Text(string value, Transform parent, Vector2 min, Vector2 max,
            int fontSize, Color color, TextAnchor alignment)
        {
            var rectangle = Rect("Text", parent, min, max);
            var text = rectangle.gameObject.AddComponent<Text>();
            text.text = value;
            text.font = _font;
            text.fontSize = fontSize;
            text.color = color;
            text.alignment = alignment;
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = Mathf.Max(10, fontSize - 6);
            text.resizeTextMaxSize = fontSize;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            AddShadow(text.gameObject);
            return text;
        }

        private Button Button(string label, Transform parent, Vector2 min, Vector2 max, UnityEngine.Events.UnityAction action)
        {
            var image = Image(label, parent, min, max, Color.white);
            image.sprite = PixelArt.Button(false);
            image.type = Image.Type.Sliced;
            var button = image.gameObject.AddComponent<Button>();
            var state = button.spriteState;
            state.pressedSprite = PixelArt.Button(true);
            state.selectedSprite = PixelArt.Button(true);
            button.spriteState = state;
            button.transition = Selectable.Transition.SpriteSwap;
            button.onClick.AddListener(action);
            Text(label, button.transform, new Vector2(0.05f, 0.08f), new Vector2(0.95f, 0.92f),
                20, Ivory, TextAnchor.MiddleCenter);
            AddShadow(button.gameObject);
            return button;
        }

        private InputField Input(Transform parent, Vector2 min, Vector2 max, string placeholder)
        {
            var image = Image("Room Input", parent, min, max, new Color(1f, 1f, 1f, 0.96f));
            image.sprite = PixelArt.Panel();
            image.type = Image.Type.Sliced;
            var input = image.gameObject.AddComponent<InputField>();
            var value = Text("", input.transform, new Vector2(0.06f, 0.08f), new Vector2(0.94f, 0.92f),
                20, Ink, TextAnchor.MiddleLeft);
            var hint = Text(placeholder, input.transform, new Vector2(0.06f, 0.08f), new Vector2(0.94f, 0.92f),
                17, new Color(0.25f, 0.38f, 0.34f, 0.7f), TextAnchor.MiddleLeft);
            input.textComponent = value;
            input.placeholder = hint;
            input.characterLimit = 15;
            input.lineType = InputField.LineType.SingleLine;
            input.contentType = InputField.ContentType.Standard;
            return input;
        }

        private Image Progress(Transform parent, Vector2 min, Vector2 max, Color color)
        {
            var frame = Image("Progress Frame", parent, min, max, new Color(0.02f, 0.08f, 0.08f, 0.95f));
            frame.sprite = PixelArt.Panel();
            frame.type = Image.Type.Sliced;
            var fill = Image("Progress Fill", frame.transform, new Vector2(0.03f, 0.18f), new Vector2(0.97f, 0.82f), color);
            fill.sprite = PixelArt.ProgressFill();
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = 0;
            fill.fillAmount = 0f;
            return fill;
        }

        private static RectTransform Rect(string name, Transform parent, Vector2 min, Vector2 max)
        {
            var gameObject = new GameObject(name, typeof(RectTransform));
            gameObject.transform.SetParent(parent, false);
            var rectangle = gameObject.GetComponent<RectTransform>();
            SetAnchors(rectangle, min, max);
            return rectangle;
        }

        private static void SetAnchors(RectTransform rectangle, Vector2 min, Vector2 max)
        {
            rectangle.anchorMin = min;
            rectangle.anchorMax = max;
            rectangle.offsetMin = Vector2.zero;
            rectangle.offsetMax = Vector2.zero;
            rectangle.localScale = Vector3.one;
        }

        private static void AddShadow(GameObject gameObject)
        {
            var shadow = gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.72f);
            shadow.effectDistance = new Vector2(3f, -3f);
            shadow.useGraphicAlpha = true;
        }

        private void OnDestroy()
        {
            if (_app == null)
                return;
            _app.StateChanged -= Refresh;
            _app.DataChanged -= Refresh;
            _app.CommentaryChanged -= OnCommentary;
        }
    }
}
