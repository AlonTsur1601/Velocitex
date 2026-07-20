using Godot;

namespace Velocitex.Core.Settings;

public static class SettingsStore
{
    private const string SettingsPath = "user://settings.cfg";

    public static GameSettingsData Load(string path = SettingsPath)
    {
        GameSettingsData settings = new();
        ConfigFile config = new();
        if (config.Load(path) != Error.Ok)
        {
            return settings;
        }

        settings.FpsLimit = config.GetValue("video", "fps_limit", settings.FpsLimit).AsInt32();
        settings.VSyncEnabled = config.GetValue("video", "vsync", settings.VSyncEnabled).AsBool();
        settings.Fullscreen = config.GetValue("video", "fullscreen", settings.Fullscreen).AsBool();
        settings.ResolutionWidth = config.GetValue("video", "resolution_width", settings.ResolutionWidth).AsInt32();
        settings.ResolutionHeight = config.GetValue("video", "resolution_height", settings.ResolutionHeight).AsInt32();
        settings.GraphicsPreset = config.GetValue("video", "graphics_preset", settings.GraphicsPreset).AsInt32();
        settings.RenderScale = config.GetValue("video", "render_scale", settings.RenderScale).AsSingle();
        settings.MsaaLevel = config.GetValue("video", "msaa", settings.MsaaLevel).AsInt32();
        settings.ShadowsEnabled = config.GetValue("video", "shadows", settings.ShadowsEnabled).AsBool();
        settings.MouseSensitivity = config.GetValue("gameplay", "mouse_sensitivity", settings.MouseSensitivity).AsSingle();
        settings.InvertY = config.GetValue("gameplay", "invert_y", settings.InvertY).AsBool();
        settings.DefaultFirstPerson = config.GetValue("gameplay", "default_first_person", settings.DefaultFirstPerson).AsBool();
        settings.CameraShakeAmount = config.GetValue("gameplay", "camera_shake", settings.CameraShakeAmount).AsSingle();
        settings.InteractionPrompts = config.GetValue("gameplay", "interaction_prompts", settings.InteractionPrompts).AsBool();
        settings.MasterVolume = config.GetValue("audio", "master", settings.MasterVolume).AsSingle();
        settings.MusicVolume = config.GetValue("audio", "music", settings.MusicVolume).AsSingle();
        settings.SfxVolume = config.GetValue("audio", "sfx", settings.SfxVolume).AsSingle();
        settings.VoiceVolume = config.GetValue("audio", "voice", settings.VoiceVolume).AsSingle();
        settings.SubtitlesEnabled = config.GetValue("accessibility", "subtitles", settings.SubtitlesEnabled).AsBool();
        settings.SubtitleScalePercent = config.GetValue("accessibility", "subtitle_scale", settings.SubtitleScalePercent).AsInt32();
        settings.SubtitleBackground = config.GetValue("accessibility", "subtitle_background", settings.SubtitleBackground).AsBool();
        settings.ReducedMotion = config.GetValue("accessibility", "reduced_motion", settings.ReducedMotion).AsBool();
        settings.DisableFlashes = config.GetValue("accessibility", "disable_flashes", settings.DisableFlashes).AsBool();
        settings.HighContrastPrompts = config.GetValue("accessibility", "high_contrast_prompts", settings.HighContrastPrompts).AsBool();
        settings.TrailEnabled = config.GetValue("accessibility", "trail_enabled", settings.TrailEnabled).AsBool();
        settings.MoveForwardKey = config.GetValue("controls", "move_forward", settings.MoveForwardKey).AsInt64();
        settings.MoveBackKey = config.GetValue("controls", "move_back", settings.MoveBackKey).AsInt64();
        settings.MoveLeftKey = config.GetValue("controls", "move_left", settings.MoveLeftKey).AsInt64();
        settings.MoveRightKey = config.GetValue("controls", "move_right", settings.MoveRightKey).AsInt64();
        settings.ToggleCameraKey = config.GetValue("controls", "toggle_camera", settings.ToggleCameraKey).AsInt64();
        settings.InteractKey = config.GetValue("controls", "interact", settings.InteractKey).AsInt64();
        return settings;
    }

    public static Error Save(GameSettingsData settings, string path = SettingsPath)
    {
        ConfigFile config = new();
        config.SetValue("video", "fps_limit", settings.FpsLimit);
        config.SetValue("video", "vsync", settings.VSyncEnabled);
        config.SetValue("video", "fullscreen", settings.Fullscreen);
        config.SetValue("video", "resolution_width", settings.ResolutionWidth);
        config.SetValue("video", "resolution_height", settings.ResolutionHeight);
        config.SetValue("video", "graphics_preset", settings.GraphicsPreset);
        config.SetValue("video", "render_scale", settings.RenderScale);
        config.SetValue("video", "msaa", settings.MsaaLevel);
        config.SetValue("video", "shadows", settings.ShadowsEnabled);
        config.SetValue("gameplay", "mouse_sensitivity", settings.MouseSensitivity);
        config.SetValue("gameplay", "invert_y", settings.InvertY);
        config.SetValue("gameplay", "default_first_person", settings.DefaultFirstPerson);
        config.SetValue("gameplay", "camera_shake", settings.CameraShakeAmount);
        config.SetValue("gameplay", "interaction_prompts", settings.InteractionPrompts);
        config.SetValue("audio", "master", settings.MasterVolume);
        config.SetValue("audio", "music", settings.MusicVolume);
        config.SetValue("audio", "sfx", settings.SfxVolume);
        config.SetValue("audio", "voice", settings.VoiceVolume);
        config.SetValue("accessibility", "subtitles", settings.SubtitlesEnabled);
        config.SetValue("accessibility", "subtitle_scale", settings.SubtitleScalePercent);
        config.SetValue("accessibility", "subtitle_background", settings.SubtitleBackground);
        config.SetValue("accessibility", "reduced_motion", settings.ReducedMotion);
        config.SetValue("accessibility", "disable_flashes", settings.DisableFlashes);
        config.SetValue("accessibility", "high_contrast_prompts", settings.HighContrastPrompts);
        config.SetValue("accessibility", "trail_enabled", settings.TrailEnabled);
        config.SetValue("controls", "move_forward", settings.MoveForwardKey);
        config.SetValue("controls", "move_back", settings.MoveBackKey);
        config.SetValue("controls", "move_left", settings.MoveLeftKey);
        config.SetValue("controls", "move_right", settings.MoveRightKey);
        config.SetValue("controls", "toggle_camera", settings.ToggleCameraKey);
        config.SetValue("controls", "interact", settings.InteractKey);
        return config.Save(path);
    }
}
