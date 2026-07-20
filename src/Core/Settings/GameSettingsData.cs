namespace Velocitex.Core.Settings;

public sealed class GameSettingsData
{
    public int FpsLimit { get; set; } = 60;
    public bool VSyncEnabled { get; set; } = true;
    public bool Fullscreen { get; set; }
    public int ResolutionWidth { get; set; } = 1280;
    public int ResolutionHeight { get; set; } = 720;
    public int GraphicsPreset { get; set; } = 2;
    public float RenderScale { get; set; } = 1.0f;
    public int MsaaLevel { get; set; } = 2;
    public bool ShadowsEnabled { get; set; } = true;
    public float MouseSensitivity { get; set; } = 0.0025f;
    public bool InvertY { get; set; }
    public bool DefaultFirstPerson { get; set; }
    public float CameraShakeAmount { get; set; } = 1.0f;
    public bool InteractionPrompts { get; set; } = true;
    public float MasterVolume { get; set; } = 1.0f;
    public float MusicVolume { get; set; } = 0.8f;
    public float SfxVolume { get; set; } = 0.9f;
    public float VoiceVolume { get; set; } = 1.0f;
    public bool SubtitlesEnabled { get; set; } = true;
    public int SubtitleScalePercent { get; set; } = 100;
    public bool SubtitleBackground { get; set; } = true;
    public bool ReducedMotion { get; set; }
    public bool DisableFlashes { get; set; }
    public bool HighContrastPrompts { get; set; }
    public bool TrailEnabled { get; set; } = true;

    public long MoveForwardKey { get; set; } = (long)Godot.Key.W;
    public long MoveBackKey { get; set; } = (long)Godot.Key.S;
    public long MoveLeftKey { get; set; } = (long)Godot.Key.A;
    public long MoveRightKey { get; set; } = (long)Godot.Key.D;
    public long ToggleCameraKey { get; set; } = (long)Godot.Key.C;
    public long InteractKey { get; set; } = (long)Godot.Key.E;
}
