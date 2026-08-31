using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using JadeMahjong.Core;
using UnityEngine;

namespace JadeMahjong.Runtime
{
    public sealed class MahjongBoardView : MonoBehaviour
    {
        private readonly Dictionary<int, TileView> _views = new();
        private MahjongBoardModel _model;
        private int _selectedId = -1;
        private Coroutine _hintRoutine;
        private bool _inputEnabled;

        public int Remaining => _model?.Remaining ?? 144;
        public bool IsBuilt => _model != null;
        public bool InputEnabled
        {
            get => _inputEnabled;
            set
            {
                _inputEnabled = value;
                RefreshAll();
            }
        }

        public event Action<int> RemainingChanged;
        public event Action<string, int> CommentaryRequested;
        public event Action PairRemoved;
        public event Action Mismatch;
        public event Action BoardCleared;

        public void Build(int seed)
        {
            foreach (Transform child in transform)
                Destroy(child.gameObject);
            _views.Clear();
            _selectedId = -1;
            _model = new MahjongBoardModel(ShanghaiBoardFactory.Create(seed));

            foreach (var slot in _model.Slots.OrderBy(tile => tile.Layer).ThenByDescending(tile => tile.Y2))
            {
                var gameObject = new GameObject($"Tile_{slot.Id:000}_{slot.Kind:00}");
                gameObject.transform.SetParent(transform, false);
                gameObject.transform.localPosition = PositionFor(slot);
                var view = gameObject.AddComponent<TileView>();
                view.Initialize(slot);
                _views[slot.Id] = view;
            }

            RefreshAll();
            RemainingChanged?.Invoke(Remaining);
        }

        public void SelectAt(Vector2 worldPoint)
        {
            if (!_inputEnabled || _model == null)
                return;
            var colliders = Physics2D.OverlapPointAll(worldPoint);
            var view = colliders
                .Select(collider => collider.GetComponent<TileView>())
                .Where(candidate => candidate != null && candidate.Slot.Active)
                .OrderByDescending(candidate => candidate.Slot.Layer)
                .ThenByDescending(candidate => candidate.Renderer.sortingOrder)
                .FirstOrDefault();
            if (view != null)
                Select(view.Slot.Id);
        }

        public bool ShowHint()
        {
            if (!_inputEnabled || _model == null ||
                !_model.TryGetAvailablePair(out var first, out var second))
            {
                CommentaryRequested?.Invoke("Nem mesmo o céu encontrou um par. Embaralhe as peças.", 3);
                return false;
            }

            if (_hintRoutine != null)
                StopCoroutine(_hintRoutine);
            _hintRoutine = StartCoroutine(HintAnimation(_views[first], _views[second]));
            CommentaryRequested?.Invoke("Observe o brilho dourado. A corte lhe concede uma pista.", 2);
            return true;
        }

        public bool HasAvailablePair()
        {
            return _model != null && _model.HasAvailablePair();
        }

        public void Redeal(int seed)
        {
            if (_model == null || _model.IsCleared)
                return;
            _selectedId = -1;
            _model.RedealActive(seed);
            foreach (var slot in _model.Slots.Where(tile => tile.Active))
                _views[slot.Id].SetKind(slot.Kind);
            RefreshAll();
            CommentaryRequested?.Invoke("Os ventos do palácio reorganizam o destino.", 1);
        }

        private void Select(int id)
        {
            if (!_model.IsFree(id))
            {
                _views[id].Shake();
                CommentaryRequested?.Invoke("Essa peça ainda está presa sob o peso da corte.", 3);
                return;
            }

            if (_selectedId < 0)
            {
                _selectedId = id;
                RefreshAll();
                return;
            }

            if (_selectedId == id)
            {
                _selectedId = -1;
                RefreshAll();
                return;
            }

            var first = _selectedId;
            var result = _model.TryRemove(first, id);
            switch (result)
            {
                case RemoveResult.Removed:
                    _selectedId = -1;
                    _views[first].RemoveAnimated();
                    _views[id].RemoveAnimated();
                    PairRemoved?.Invoke();
                    RemainingChanged?.Invoke(Remaining);
                    CommentaryRequested?.Invoke(PairLine(Remaining), Remaining <= 24 ? 2 : 1);
                    RefreshAll();
                    if (_model.IsCleared)
                        BoardCleared?.Invoke();
                    else if (!_model.HasAvailablePair())
                        CommentaryRequested?.Invoke("O tabuleiro silenciou. Use REORDENAR para prosseguir.", 3);
                    break;

                case RemoveResult.NotMatching:
                    _views[first].Shake();
                    _views[id].Shake();
                    Mismatch?.Invoke();
                    _selectedId = id;
                    CommentaryRequested?.Invoke("Parecidas não basta. A harmonia exige um par verdadeiro.", 0);
                    RefreshAll();
                    break;

                default:
                    _selectedId = -1;
                    RefreshAll();
                    break;
            }
        }

        private void RefreshAll()
        {
            if (_model == null)
                return;
            foreach (var slot in _model.Slots)
            {
                if (!_views.TryGetValue(slot.Id, out var view) || !slot.Active)
                    continue;
                view.SetState(_inputEnabled && _model.IsFree(slot.Id), slot.Id == _selectedId);
            }
        }

        private IEnumerator HintAnimation(TileView first, TileView second)
        {
            var elapsed = 0f;
            while (elapsed < 1.6f && first.Slot.Active && second.Slot.Active)
            {
                elapsed += Time.unscaledDeltaTime;
                var pulse = 0.5f + Mathf.Sin(elapsed * 14f) * 0.5f;
                first.SetHint(pulse);
                second.SetHint(pulse);
                yield return null;
            }
            first.SetHint(0f);
            second.SetHint(0f);
            RefreshAll();
            _hintRoutine = null;
        }

        private static Vector3 PositionFor(TileSlot slot)
        {
            return new Vector3(
                -1.65f + slot.X2 * 0.36f + slot.Layer * 0.055f,
                -0.28f + slot.Y2 * 0.255f + slot.Layer * 0.12f,
                0f);
        }

        private static string PairLine(int remaining)
        {
            if (remaining == 0)
                return "O último selo se rompeu. Magnífico!";
            if (remaining <= 24)
                return "A vitória já pode ser ouvida nos sinos celestiais.";
            if (remaining <= 72)
                return "Seu ritmo começa a impressionar a corte.";
            return "Um par correto. Continue antes que seu rival desperte.";
        }
    }

    public sealed class TileView : MonoBehaviour
    {
        private Vector3 _restPosition;
        private Coroutine _motion;
        private bool _free;
        private bool _selected;

        public TileSlot Slot { get; private set; }
        public SpriteRenderer Renderer { get; private set; }

        public void Initialize(TileSlot slot)
        {
            Slot = slot;
            Renderer = gameObject.AddComponent<SpriteRenderer>();
            Renderer.sprite = PixelArt.Tile(slot.Kind);
            Renderer.sortingOrder = slot.Layer * 1000 - slot.Y2 * 10 + slot.Id % 10;
            var collider = gameObject.AddComponent<BoxCollider2D>();
            collider.size = new Vector2(0.62f, 0.77f);
            collider.offset = new Vector2(0f, 0.04f);
            _restPosition = transform.localPosition;
        }

        public void SetKind(int kind)
        {
            Renderer.sprite = PixelArt.Tile(kind);
        }

        public void SetState(bool free, bool selected)
        {
            _free = free;
            _selected = selected;
            transform.localPosition = _restPosition + (selected ? Vector3.up * 0.09f : Vector3.zero);
            transform.localScale = selected ? Vector3.one * 1.08f : Vector3.one;
            Renderer.color = selected
                ? new Color(1f, 0.92f, 0.48f, 1f)
                : free
                    ? Color.white
                    : new Color(0.56f, 0.64f, 0.61f, 0.96f);
        }

        public void SetHint(float strength)
        {
            if (_selected)
                return;
            Renderer.color = Color.Lerp(_free ? Color.white : new Color(0.65f, 0.7f, 0.67f),
                new Color(1f, 0.72f, 0.16f), strength);
            transform.localScale = Vector3.one * Mathf.Lerp(1f, 1.1f, strength);
        }

        public void Shake()
        {
            if (_motion != null)
                StopCoroutine(_motion);
            _motion = StartCoroutine(ShakeRoutine());
        }

        public void RemoveAnimated()
        {
            if (_motion != null)
                StopCoroutine(_motion);
            GetComponent<Collider2D>().enabled = false;
            _motion = StartCoroutine(RemoveRoutine());
        }

        private IEnumerator ShakeRoutine()
        {
            var elapsed = 0f;
            while (elapsed < 0.28f)
            {
                elapsed += Time.unscaledDeltaTime;
                transform.localPosition = _restPosition +
                    Vector3.right * Mathf.Sin(elapsed * 70f) * 0.055f;
                yield return null;
            }
            transform.localPosition = _restPosition;
            _motion = null;
        }

        private IEnumerator RemoveRoutine()
        {
            var elapsed = 0f;
            var initialScale = transform.localScale;
            while (elapsed < 0.24f)
            {
                elapsed += Time.unscaledDeltaTime;
                var amount = Mathf.Clamp01(elapsed / 0.24f);
                transform.localPosition = _restPosition + Vector3.up * amount * 0.28f;
                transform.localScale = Vector3.Lerp(initialScale, Vector3.zero, amount);
                Renderer.color = new Color(1f, 0.8f, 0.3f, 1f - amount);
                yield return null;
            }
            gameObject.SetActive(false);
        }
    }
}
