using Godot;

namespace Velocitex.UI.Visual;

public partial class MenuBackground : Control
{
    [Export] public float PanoramaSeconds { get; set; } = 10.5f;
    [Export] public float CrossFadeSeconds { get; set; } = 1.35f;
    [Export] public float OverscanPixels { get; set; } = 190.0f;

    public bool MotionEnabled { get; set; } = true;
    public int PanoramaCount => _entries.Count;
    public string CurrentRoomKey => _currentRoomKey;

    private sealed record PanoramaEntry(string Path, string RoomKey);

    private readonly List<PanoramaEntry> _entries = new();
    private readonly RandomNumberGenerator _random = new();
    private TextureRect _front = null!;
    private TextureRect _back = null!;
    private float _holdElapsed;
    private float _fadeElapsed;
    private float _frontDirection = 1.0f;
    private float _backDirection = -1.0f;
    private string _currentRoomKey = string.Empty;
    private string _pendingRoomKey = string.Empty;
    private bool _transitioning;

    public override void _Ready()
    {
        _front = GetNode<TextureRect>("Current");
        _back = GetNode<TextureRect>("Next");
        _random.Randomize();
        LoadPanoramaCatalog();
        if (_entries.Count == 0)
        {
            GD.PushWarning("No menu panoramas were found under res://assets/panoramas.");
            return;
        }

        ShowEntry(_front, _entries[_random.RandiRange(0, _entries.Count - 1)]);
        _currentRoomKey = EntryForTexture(_front.Texture)?.RoomKey ?? string.Empty;
        _front.Modulate = Colors.White;
        _front.Show();
        _back.Hide();
        UpdatePaneLayout(_front, 0.08f, _frontDirection);
    }

    public override void _Process(double delta)
    {
        if (!Visible || !MotionEnabled || _entries.Count == 0)
        {
            return;
        }

        float seconds = (float)delta;
        if (!_transitioning)
        {
            _holdElapsed += seconds;
            float progress = Mathf.Clamp(_holdElapsed / Mathf.Max(PanoramaSeconds, 0.1f), 0.0f, 1.0f);
            UpdatePaneLayout(_front, progress, _frontDirection);
            if (_holdElapsed >= PanoramaSeconds)
            {
                BeginNextPanorama();
            }
            return;
        }

        _fadeElapsed += seconds;
        float fade = Mathf.Clamp(_fadeElapsed / Mathf.Max(CrossFadeSeconds, 0.1f), 0.0f, 1.0f);
        float eased = fade * fade * (3.0f - (2.0f * fade));
        _front.Modulate = new Color(1.0f, 1.0f, 1.0f, 1.0f - eased);
        _back.Modulate = new Color(1.0f, 1.0f, 1.0f, eased);
        UpdatePaneLayout(_front, 1.0f, _frontDirection);
        UpdatePaneLayout(_back, fade * 0.22f, _backDirection);
        if (fade >= 1.0f)
        {
            FinishTransition();
        }
    }

    public void SetActive(bool active)
    {
        Visible = active;
        SetProcess(active);
    }

    public bool AdvancePanoramaForTesting()
    {
        if (_entries.Count < 2)
        {
            return false;
        }

        string before = _currentRoomKey;
        BeginNextPanorama();
        _fadeElapsed = CrossFadeSeconds;
        FinishTransition();
        return !string.Equals(before, _currentRoomKey, StringComparison.Ordinal);
    }

    private void LoadPanoramaCatalog()
    {
        const string root = "res://assets/panoramas";
        for (int room = 1; room <= 28; room++)
        {
            foreach (string view in new[] { "a", "b", "c" })
            {
                string roomKey = $"room{room:00}";
                string path = $"{root}/{roomKey}_{view}.png";
                if (ResourceLoader.Exists(path))
                {
                    _entries.Add(new PanoramaEntry(path, roomKey));
                }
            }
        }
    }

    private void BeginNextPanorama()
    {
        if (_transitioning || _entries.Count == 0)
        {
            return;
        }

        PanoramaEntry[] candidates = _entries
            .Where(entry => !entry.RoomKey.Equals(_currentRoomKey, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        if (candidates.Length == 0)
        {
            candidates = _entries.ToArray();
        }

        PanoramaEntry selected = candidates[_random.RandiRange(0, candidates.Length - 1)];
        ShowEntry(_back, selected);
        _pendingRoomKey = selected.RoomKey;
        _backDirection = _random.Randf() < 0.5f ? -1.0f : 1.0f;
        _back.Modulate = new Color(1.0f, 1.0f, 1.0f, 0.0f);
        _back.Show();
        _fadeElapsed = 0.0f;
        _transitioning = true;
        UpdatePaneLayout(_back, 0.0f, _backDirection);
    }

    private void FinishTransition()
    {
        _front.Hide();
        (_front, _back) = (_back, _front);
        _front.Modulate = Colors.White;
        _back.Hide();
        _back.Modulate = new Color(1.0f, 1.0f, 1.0f, 0.0f);
        _currentRoomKey = _pendingRoomKey;
        _pendingRoomKey = string.Empty;
        _frontDirection = _backDirection;
        _holdElapsed = 0.0f;
        _fadeElapsed = 0.0f;
        _transitioning = false;
    }

    private static void ShowEntry(TextureRect pane, PanoramaEntry entry)
    {
        pane.Texture = GD.Load<Texture2D>(entry.Path);
        pane.SetMeta("panorama_path", entry.Path);
    }

    private PanoramaEntry? EntryForTexture(Texture2D? texture)
    {
        if (texture is null || !_front.HasMeta("panorama_path"))
        {
            return null;
        }

        string path = _front.GetMeta("panorama_path").AsString();
        return _entries.FirstOrDefault(entry => entry.Path == path);
    }

    private void UpdatePaneLayout(TextureRect pane, float progress, float direction)
    {
        float overscan = Mathf.Min(OverscanPixels, Mathf.Max(80.0f, Size.X * 0.16f));
        pane.Size = new Vector2(Size.X + (overscan * 2.0f), Size.Y);
        float leftToRight = Mathf.Lerp(-overscan * 2.0f, 0.0f, Mathf.Clamp(progress, 0.0f, 1.0f));
        pane.Position = new Vector2(direction > 0.0f ? leftToRight : (-overscan * 2.0f) - leftToRight, 0.0f);
    }
}
