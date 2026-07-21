using Godot;
using Velocitex.Core.Input;
using Velocitex.Core.Profile;
using Velocitex.Core.Rooms;
using Velocitex.Core.Save;
using Velocitex.Core.Settings;
using Velocitex.Gameplay.Camera;
using Velocitex.Gameplay.Player;
using Velocitex.Story;
using Velocitex.UI.Visual;

namespace Velocitex.UI;

public partial class AppRoot : Node
{
    private static readonly StringName ShadowLightGroup = "quality_shadow_light";
    private const double LoadingCandyOnlySeconds = 0.80;
    private const float RoomTransferCandyHalfSize = 128.0f;

    [Export] public PackedScene? GameplayScene { get; set; }
    [Export] public PackedScene? OpeningScene { get; set; }
    [Export] public PackedScene? EndingScene { get; set; }

    private Node _sceneContainer = null!;
    private MenuBackground _menuBackground = null!;
    private Control _mainMenu = null!;
    private Control _playMenu = null!;
    private Control _pauseMenu = null!;
    private Control _settingsMenu = null!;
    private Control _browserMenu = null!;
    private Control _customizeMenu = null!;
    private Control _advancementsMenu = null!;
    private TextureRect _mainMenuLogoBall = null!;
    private Label _advancementsProgress = null!;
    private VBoxContainer _advancementsList = null!;
    private AdvancementNotificationPresenter _advancementNotifications = null!;
    private Control _loadingOverlay = null!;
    private ColorRect _loadingDim = null!;
    private Control _loadingLogo = null!;
    private Label _loadingLogoLeft = null!;
    private TextureRect _loadingLogoBall = null!;
    private Label _loadingLogoRight = null!;
    private Label _loadingRoomLabel = null!;
    private ProgressBar _loadingProgress = null!;
    private Control _roomIntroCard = null!;
    private Label _roomIntroNumber = null!;
    private Label _roomIntroName = null!;
    private Control _roomTransferOverlay = null!;
    private Control _roomTransferTube = null!;
    private TextureRect _roomTransferCandy = null!;
    private PanelContainer _roomTransferSubtitlePanel = null!;
    private Label _roomTransferSubtitle = null!;
    private AudioStreamPlayer _roomTransferSfx = null!;
    private GridContainer _primaryColorGrid = null!;
    private GridContainer _secondaryColorGrid = null!;
    private GridContainer _patternGrid = null!;
    private GridContainer _trailGrid = null!;
    private ScrollContainer _customizeScroll = null!;
    private readonly List<CosmeticSwatchButton> _primaryColorButtons = new();
    private readonly List<CosmeticSwatchButton> _secondaryColorButtons = new();
    private readonly List<CosmeticSwatchButton> _patternButtons = new();
    private readonly List<CosmeticSwatchButton> _trailButtons = new();
    private string _selectedPrimaryColorId = "cherry";
    private string _selectedSecondaryColorId = "vanilla";
    private string _selectedPatternId = "none";
    private string _selectedTrailId = "off";
    private Label _customizeStatus = null!;
    private CandyPreview3D _candyPreview = null!;
    private Label _browserHeader = null!;
    private Label _browserEmptyLabel = null!;
    private VBoxContainer _browserList = null!;
    private Button _continueButton = null!;
    private Button _loadButton = null!;
    private Button _roomSelectButton = null!;
    private Label _settingsHeader = null!;
    private OptionButton _fpsOption = null!;
    private OptionButton _resolutionOption = null!;
    private OptionButton _windowOption = null!;
    private OptionButton _presetOption = null!;
    private OptionButton _msaaOption = null!;
    private CheckButton _vsyncCheck = null!;
    private CheckButton _shadowsCheck = null!;
    private HSlider _renderScaleSlider = null!;
    private HSlider _sensitivitySlider = null!;
    private CheckButton _invertYCheck = null!;
    private OptionButton _defaultCameraOption = null!;
    private HSlider _cameraShakeSlider = null!;
    private CheckButton _interactionPromptsCheck = null!;
    private HSlider _masterSlider = null!;
    private HSlider _musicSlider = null!;
    private HSlider _sfxSlider = null!;
    private HSlider _voiceSlider = null!;
    private CheckButton _subtitlesCheck = null!;
    private OptionButton _subtitleScaleOption = null!;
    private CheckButton _subtitleBackgroundCheck = null!;
    private CheckButton _reducedMotionCheck = null!;
    private CheckButton _disableFlashesCheck = null!;
    private CheckButton _highContrastCheck = null!;
    private CheckButton _trailCheck = null!;
    private readonly Dictionary<StringName, Button> _bindingButtons = new();
    private ConfirmationDialog _quitConfirmation = null!;
    private ConfirmationDialog _newGameConfirmation = null!;
    private AcceptDialog _roomCompleteDialog = null!;
    private AcceptDialog _unavailableDialog = null!;
    private AudioStreamPlayer _roomDialogueVoice = null!;
    private AudioStreamPlayer _menuMusic = null!;
    private Tween? _menuMusicTween;
    private Tween? _loadingTween;
    private int _menuMusicGeneration;
    private bool _pauseSfxOverrideActive;
    private bool _sfxMutedBeforePause;
    private bool _roomTransferSfxOverrideActive;
    private bool _sfxMutedBeforeRoomTransfer;
    private Node? _gameplayInstance;
    private OpeningSequence? _openingInstance;
    private EndingSequence? _endingInstance;
    private RoomRuntime? _currentRoom;
    private PlayerBall? _currentPlayer;
    private CampaignSnapshot? _pendingCompletionSnapshot;
    private GameSettingsData _settings = new();
    private PlayerProfile _profile = ProfileStore.CreateDefault();
    private MenuOrigin _settingsOrigin;
    private MenuOrigin _browserOrigin;
    private MenuOrigin _customizeOrigin;
    private MenuOrigin _advancementsOrigin;
    private double _campaignElapsedSeconds;
    private bool _roomCompletionHandled;
    private bool _runUiSmoke;
    private bool _runSaveSmoke;
    private bool _runCampaignFlowSmoke;
    private bool _runCustomizePreview;
    private bool _runCustomizeTrailPreview;
    private string _customizePatternPreviewId = string.Empty;
    private bool _runSettingsPreview;
    private bool _runMenuPreview;
    private bool _runTransitionPreview;
    private bool _runRoomTransferPreview;
    private bool _runRoomTransferSmoke;
    private bool _runPerformanceSmoke;
    private bool _runOpeningSmoke;
    private bool _runEndingSmoke;
    private bool _endingReturnSmokePending;
    private bool _endingCreditsContractPassed;
    private bool _runStoryAudioSmoke;
    private bool _runStartupLoadingSmoke;
    private bool _uiSmokePending;
    private bool _saveSmokePending;
    private bool _campaignFlowSmokePending;
    private bool _loadingRoom;
    private bool _populatingCustomize;
    private float _loadingVisualTime;
    private double _lastLoadingRevealToExitSeconds;
    private float _startupWordCenterError;
    private float _startupLogoLandingError;
    private int _campaignFlowSmokeStep;
    private int _campaignFlowWaitFrames;
    private int _customizePreviewFrames;
    private int _settingsPreviewFrames;
    private int _menuPreviewFrames;
    private int _transitionPreviewFrames;
    private int _roomTransferPreviewFrames;
    private int _roomTransferSmokeFrames;
    private int _roomTransferSmokeStage;
    private bool _roomTransferLoadingScreenSeen;
    private bool _roomTransferCandyHandoffSeen;
    private int _performanceWarmupFrames;
    private readonly List<double> _performanceFrameMilliseconds = new();
    private float _roomTransferVisualTime;
    private ulong _roomTransferShownAtMilliseconds;
    private int _roomTransferGeneration;
    private bool _roomTransferRunning;
    private readonly List<AdvancementDefinition> _deferredRoomStartNotifications = new();
    private string _campaignRoot = CampaignSaveService.DefaultRoot;
    private string _profilePath = ProfileStore.DefaultPath;
    private bool _waitingForKey;
    private StringName _bindingAction = string.Empty;

    private enum MenuOrigin
    {
        Main,
        Pause,
    }

    private enum CosmeticSlot
    {
        PrimaryColor,
        SecondaryColor,
        Pattern,
        Trail,
    }

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        string[] userArguments = OS.GetCmdlineUserArgs();
        InputDefaults.EnsureActions();
        CacheNodes();
        PopulateOptions();
        ConnectSignals();

        _runUiSmoke = Array.Exists(userArguments, argument => argument == "--ui-smoke");
        _runSaveSmoke = Array.Exists(userArguments, argument => argument == "--save-smoke");
        _runCampaignFlowSmoke = Array.Exists(userArguments, argument => argument == "--campaign-flow-smoke");
        string? customizePatternArgument = Array.Find(
            userArguments,
            argument => argument.StartsWith("--customize-pattern-preview=", StringComparison.Ordinal));
        if (customizePatternArgument is not null)
        {
            _customizePatternPreviewId = customizePatternArgument["--customize-pattern-preview=".Length..];
        }
        _runCustomizePreview = Array.Exists(userArguments, argument => argument == "--customize-preview") ||
            !string.IsNullOrWhiteSpace(_customizePatternPreviewId);
        _runCustomizeTrailPreview = Array.Exists(userArguments, argument => argument == "--customize-trail-preview");
        _runSettingsPreview = Array.Exists(userArguments, argument => argument == "--settings-preview");
        _runMenuPreview = Array.Exists(userArguments, argument => argument == "--menu-preview");
        _runTransitionPreview = Array.Exists(userArguments, argument => argument == "--transition-preview");
        _runRoomTransferPreview = Array.Exists(userArguments, argument => argument == "--room-transfer-preview");
        _runRoomTransferSmoke = Array.Exists(userArguments, argument => argument == "--room-transfer-smoke");
        _runPerformanceSmoke = Array.Exists(userArguments, argument => argument == "--performance-smoke");
        _runOpeningSmoke = Array.Exists(userArguments, argument => argument == "--opening-smoke");
        _runEndingSmoke = Array.Exists(userArguments, argument => argument == "--ending-smoke");
        _runStoryAudioSmoke = Array.Exists(userArguments, argument => argument == "--story-audio-smoke");
        _runStartupLoadingSmoke = Array.Exists(userArguments, argument => argument == "--startup-loading-smoke");
        if (_runCampaignFlowSmoke)
        {
            _campaignRoot = "user://campaign-flow-smoke";
        }
        else if (_runUiSmoke)
        {
            _campaignRoot = "user://campaign-ui-smoke";
            _profilePath = "user://profile-ui-smoke.json";
            ProfileStore.DeleteTestFiles(_profilePath);
        }
        else if (_runRoomTransferSmoke)
        {
            _campaignRoot = "user://continue-transfer-smoke";
        }

        _profile = ProfileStore.Load(out string? profileWarning, _profilePath);
        if (!string.IsNullOrWhiteSpace(profileWarning))
        {
            GD.PushWarning(profileWarning);
        }

        _settings = _runUiSmoke || _runSaveSmoke || _runCampaignFlowSmoke || _runPerformanceSmoke || _runSettingsPreview
            ? new GameSettingsData()
            : SettingsStore.Load();
        if (_runPerformanceSmoke)
        {
            _settings.FpsLimit = 0;
            _settings.VSyncEnabled = false;
            _settings.Fullscreen = false;
            _settings.ResolutionWidth = 1280;
            _settings.ResolutionHeight = 720;
            _settings.GraphicsPreset = 2;
            _settings.RenderScale = 1.0f;
            _settings.MsaaLevel = 2;
            _settings.ShadowsEnabled = true;
        }
        ApplyInputBindings();
        SyncControlsFromSettings();
        ApplySettings(save: false, applyDefaultCamera: false);
        ShowMainMenu();
        if (_runStoryAudioSmoke)
        {
            RunStoryAudioSmoke();
        }
        else if (_runEndingSmoke)
        {
            StartEndingSequence();
        }
        else if (_runOpeningSmoke)
        {
            StartOpeningSequence();
        }
        else if (_runTransitionPreview)
        {
            StartRoom(roomNumber: 2, saveRoomStart: false, elapsedSeconds: 0.0);
        }
        else if (_runRoomTransferPreview || _runRoomTransferSmoke)
        {
            BeginRoomTransferHarness();
        }
        else if (_runPerformanceSmoke)
        {
            StartRoom(roomNumber: 20, saveRoomStart: false, elapsedSeconds: 0.0);
        }
        else if (_runCustomizePreview || _runCustomizeTrailPreview)
        {
            OpenCustomize(MenuOrigin.Main);
            SelectCosmetic(CosmeticSlot.PrimaryColor, "blueberry");
            SelectCosmetic(CosmeticSlot.SecondaryColor, "vanilla");
            SelectCosmetic(
                CosmeticSlot.Pattern,
                string.IsNullOrWhiteSpace(_customizePatternPreviewId) ? "spiral" : _customizePatternPreviewId);
            SelectCosmetic(CosmeticSlot.Trail, "trail-cyan");
            OnCosmeticSwatchPressed(
                _runCustomizeTrailPreview ? CosmeticSlot.Trail : CosmeticSlot.PrimaryColor,
                _runCustomizeTrailPreview ? "trail-cloud" : "mint");
        }
        else if (_runSettingsPreview)
        {
            OpenSettings(MenuOrigin.Main);
            _vsyncCheck.ButtonPressed = true;
            _shadowsCheck.ButtonPressed = false;
            _invertYCheck.ButtonPressed = false;
            _interactionPromptsCheck.ButtonPressed = true;
        }
        else if (_runStartupLoadingSmoke || userArguments.Length == 0)
        {
            PlayStartupLoadingSequenceAsync();
        }
        _uiSmokePending = _runUiSmoke;
        _saveSmokePending = _runSaveSmoke;
        _campaignFlowSmokePending = _runCampaignFlowSmoke;
    }

    public override void _ExitTree()
    {
        _menuMusicGeneration++;
        _menuMusicTween?.Kill();
        _menuMusicTween = null;
        if (_menuMusic is not null && IsInstanceValid(_menuMusic))
        {
            _menuMusic.Stop();
            _menuMusic.Stream = null;
        }
    }

    public override void _Process(double delta)
    {
        if (_runPerformanceSmoke &&
            _currentRoom?.RoomNumber == 20 &&
            !_loadingRoom &&
            !_roomIntroCard.Visible)
        {
            RunPerformanceSmokeFrame(delta);
        }

        if (_runRoomTransferSmoke)
        {
            _roomTransferSmokeFrames++;
            _roomTransferLoadingScreenSeen |= _loadingRoom && _loadingOverlay.Visible;
            _roomTransferCandyHandoffSeen |= _loadingRoom && _roomTransferOverlay.Visible;
            if (_roomTransferSmokeStage == 0 && _currentRoom?.RoomNumber == 1 && !_loadingRoom)
            {
                if (_roomTransferLoadingScreenSeen || !_roomTransferCandyHandoffSeen)
                {
                    GD.PushError($"ROOM_TRANSFER_SMOKE_FAIL: Continue used the wrong presentation (full={_roomTransferLoadingScreenSeen}, candy={_roomTransferCandyHandoffSeen}).");
                    GetTree().Quit(1);
                    return;
                }

                PlayerCameraRig? roomOneCamera = _currentRoom.GetNodeOrNull<PlayerCameraRig>("CameraRig");
                roomOneCamera?.SetFirstPerson(true);
                if (roomOneCamera?.IsFirstPerson != true)
                {
                    GD.PushError("ROOM_TRANSFER_SMOKE_FAIL: could not select first person before the Room 01 handoff.");
                    GetTree().Quit(1);
                    return;
                }

                RoomCatalogEntry? room = RoomCatalog.Find(1);
                _pendingCompletionSnapshot = new CampaignSnapshot
                {
                    RoomId = room?.Id ?? "room-01",
                    RoomName = room?.DisplayName ?? "First Drop",
                    RoomNumber = 1,
                    Kind = SnapshotKind.RoomComplete,
                    SavedAtUtc = DateTimeOffset.UtcNow,
                    CampaignElapsedSeconds = 0.0,
                };
                _roomTransferSmokeStage = 1;
                StartRoomCompletionTransition(1);
                return;
            }

            if (_roomTransferSmokeStage == 1 && _currentRoom?.RoomNumber == 2 && !_roomTransferRunning && !_loadingRoom)
            {
                if (_roomTransferLoadingScreenSeen)
                {
                    GD.PushError("ROOM_TRANSFER_SMOKE_FAIL: the full loading screen appeared during the candy-only room handoff.");
                    GetTree().Quit(1);
                    return;
                }

                int sfxBus = AudioServer.GetBusIndex("SFX");
                if (_roomTransferSfxOverrideActive || (sfxBus >= 0 && AudioServer.IsBusMute(sfxBus)))
                {
                    GD.PushError(
                        $"ROOM_TRANSFER_SMOKE_FAIL: gameplay SFX remained muted after Room 02 became playable " +
                        $"(override={_roomTransferSfxOverrideActive}, bus_muted={(sfxBus >= 0 && AudioServer.IsBusMute(sfxBus))}, " +
                        $"stored_before={_sfxMutedBeforeRoomTransfer}).");
                    GetTree().Quit(1);
                    return;
                }

                if (_currentRoom.GetNodeOrNull<PlayerCameraRig>("CameraRig")?.IsFirstPerson != true)
                {
                    GD.PushError("ROOM_TRANSFER_SMOKE_FAIL: Room 02 did not preserve the first-person mode selected in Room 01.");
                    GetTree().Quit(1);
                    return;
                }

                _roomTransferSmokeStage = 2;
                ShowMainMenu();
                ShowRoomTransferPresentation();
                StartRoom(roomNumber: 1, saveRoomStart: false, elapsedSeconds: 0.0);
                return;
            }

            if (_roomTransferSmokeStage == 2 && _currentRoom?.RoomNumber == 1 && !_loadingRoom)
            {
                if (_currentRoom.GetNodeOrNull<PlayerCameraRig>("CameraRig")?.IsFirstPerson != _settings.DefaultFirstPerson)
                {
                    GD.PushError("ROOM_TRANSFER_SMOKE_FAIL: returning through the main menu did not restore the configured default camera mode.");
                    GetTree().Quit(1);
                    return;
                }

                CampaignSaveService.DeleteAll(out _, _campaignRoot);
                GD.Print("ROOM_TRANSFER_SMOKE_PASS: room handoff preserved first-person mode, while a main-menu restart restored the configured default; the full loading screen remained startup-only.");
                GetTree().Quit(0);
                return;
            }

            if (_roomTransferSmokeFrames >= 1100)
            {
                GD.PushError($"ROOM_TRANSFER_SMOKE_FAIL: camera persistence/reset flow did not finish within 1100 fixed frames (stage={_roomTransferSmokeStage}, room={_currentRoom?.RoomNumber}).");
                GetTree().Quit(1);
                return;
            }
        }

        if (_runRoomTransferPreview && ++_roomTransferPreviewFrames >= 45)
        {
            string capturePath = ProjectSettings.GlobalizePath("user://room-transfer-preview.png");
            GetViewport().GetTexture().GetImage().SavePng(capturePath);
            GD.Print($"ROOM_TRANSFER_PREVIEW_CAPTURE: {capturePath}");
            GetTree().Quit(0);
            return;
        }

        if (_runTransitionPreview && ++_transitionPreviewFrames >= 76)
        {
            string capturePath = ProjectSettings.GlobalizePath("user://transition-preview.png");
            GetViewport().GetTexture().GetImage().SavePng(capturePath);
            GD.Print($"TRANSITION_PREVIEW_CAPTURE: {capturePath}");
            GetTree().Quit(0);
            return;
        }

        if (_runMenuPreview && ++_menuPreviewFrames >= 45)
        {
            string capturePath = ProjectSettings.GlobalizePath("user://menu-preview.png");
            GetViewport().GetTexture().GetImage().SavePng(capturePath);
            GD.Print($"MENU_PREVIEW_AUDIO: playing={_menuMusic.Playing}, position={_menuMusic.GetPlaybackPosition():F2}s, length={_menuMusic.Stream.GetLength():F2}s, loop={(_menuMusic.Stream as AudioStreamWav)?.LoopMode}.");
            GD.Print($"MENU_PREVIEW_CAPTURE: {capturePath}");
            GetTree().Quit(0);
            return;
        }

        if ((_runCustomizePreview || _runCustomizeTrailPreview) && ++_customizePreviewFrames == 5 && _runCustomizeTrailPreview)
        {
            _customizeScroll.ScrollVertical = Mathf.RoundToInt((float)_customizeScroll.GetVScrollBar().MaxValue);
        }

        if ((_runCustomizePreview || _runCustomizeTrailPreview) && _customizePreviewFrames >= 45)
        {
            string previewFileName = _runCustomizeTrailPreview
                ? "customize-trail-preview.png"
                : string.IsNullOrWhiteSpace(_customizePatternPreviewId)
                    ? "customize-preview.png"
                    : $"customize-{_customizePatternPreviewId}-preview.png";
            string capturePath = ProjectSettings.GlobalizePath($"user://{previewFileName}");
            GetViewport().GetTexture().GetImage().SavePng(capturePath);
            GD.Print($"CUSTOMIZE{(_runCustomizeTrailPreview ? "_TRAIL" : string.Empty)}_PREVIEW_CAPTURE: {capturePath}");
            GetTree().Quit(0);
            return;
        }

        if (_runSettingsPreview && ++_settingsPreviewFrames >= 90)
        {
            string capturePath = ProjectSettings.GlobalizePath("user://settings-preview.png");
            GetViewport().GetTexture().GetImage().SavePng(capturePath);
            GD.Print($"SETTINGS_PREVIEW_CAPTURE: {capturePath}");
            GetTree().Quit(0);
            return;
        }

        if (_loadingOverlay.Visible)
        {
            _loadingVisualTime += (float)delta;
            _loadingProgress.Value = _settings.ReducedMotion
                ? 55.0
                : 8.0 + (84.0 * Mathf.PosMod(_loadingVisualTime / 1.6f, 1.0f));
        }

        if (_roomTransferOverlay.Visible && !_settings.ReducedMotion)
        {
            _roomTransferVisualTime += (float)delta;
            _roomTransferCandy.Rotation = _roomTransferVisualTime * 2.4f;
        }

        if (_currentRoom is not null && !GetTree().Paused && !_roomCompletionHandled)
        {
            _campaignElapsedSeconds += delta;
            PollAdvancementTelemetry();
        }

        if (_saveSmokePending)
        {
            _saveSmokePending = false;
            RunSaveSmokeTest();
            return;
        }

        if (_campaignFlowSmokePending)
        {
            RunCampaignFlowSmokeStep();
            return;
        }

        if (!_uiSmokePending)
        {
            return;
        }

        _uiSmokePending = false;
        RunUiSmokeTest();
    }

    private void BeginRoomTransferHarness()
    {
        if (_roomTransferTube.GetChildCount() != 1 || _roomTransferTube.GetChild(0) != _roomTransferCandy ||
            !Mathf.IsEqualApprox(_roomTransferCandy.OffsetRight - _roomTransferCandy.OffsetLeft, 256.0f) ||
            !Mathf.IsEqualApprox(_roomTransferCandy.OffsetBottom - _roomTransferCandy.OffsetTop, 256.0f))
        {
            GD.PushError("ROOM_TRANSFER_SMOKE_FAIL: the transition is not a single boot-sized candy visual.");
            GetTree().Quit(1);
            return;
        }

        CampaignSaveService.DeleteAll(out _, _campaignRoot);
        RoomCatalogEntry? room = RoomCatalog.Find(1);
        CampaignSnapshot start = new()
        {
            RoomId = room?.Id ?? "room-01",
            RoomName = room?.DisplayName ?? "First Drop",
            RoomNumber = 1,
            Kind = SnapshotKind.RoomStart,
            SavedAtUtc = DateTimeOffset.UtcNow,
            CampaignElapsedSeconds = 0.0,
        };
        if (!CampaignSaveService.Save(start, null, out string? error, _campaignRoot))
        {
            GD.PushError($"ROOM_TRANSFER_SMOKE_FAIL: could not prepare Continue snapshot: {error}");
            GetTree().Quit(1);
            return;
        }

        _settings.DefaultFirstPerson = false;
        ContinueCampaign();
    }

    private void RunPerformanceSmokeFrame(double delta)
    {
        Godot.Input.MouseMode = Godot.Input.MouseModeEnum.Visible;
        if (_performanceWarmupFrames++ < 120 || delta <= 0.0)
        {
            return;
        }

        _performanceFrameMilliseconds.Add(delta * 1000.0);
        if (_performanceFrameMilliseconds.Count < 600)
        {
            return;
        }

        double totalMilliseconds = _performanceFrameMilliseconds.Sum();
        double averageFps = 600000.0 / totalMilliseconds;
        double[] sorted = _performanceFrameMilliseconds.OrderBy(value => value).ToArray();
        double medianMilliseconds = sorted[sorted.Length / 2];
        double percentile99Milliseconds = sorted[Math.Min(sorted.Length - 1, (int)Math.Ceiling(sorted.Length * 0.99) - 1)];
        double onePercentLowFps = 1000.0 / percentile99Milliseconds;
        if (averageFps < 60.0)
        {
            GD.PushError($"PERFORMANCE_SMOKE_FAIL: Room 20 Medium 720p averaged {averageFps:F1} FPS (median {medianMilliseconds:F2} ms, 1% low {onePercentLowFps:F1} FPS).");
            GetTree().Quit(1);
            return;
        }

        GD.Print($"PERFORMANCE_SMOKE_PASS: Room 20 Medium 720p averaged {averageFps:F1} FPS (median {medianMilliseconds:F2} ms, 1% low {onePercentLowFps:F1} FPS).");
        GetTree().Quit(0);
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (_waitingForKey && @event is InputEventKey keyEvent && keyEvent.Pressed && !keyEvent.Echo)
        {
            Key key = keyEvent.PhysicalKeycode != Key.None ? keyEvent.PhysicalKeycode : keyEvent.Keycode;
            if (key == Key.Escape)
            {
                CancelRebind();
            }
            else if (key != Key.None)
            {
                FinishRebind(key);
            }

            GetViewport().SetInputAsHandled();
            return;
        }

        if (!@event.IsActionPressed("ui_cancel"))
        {
            return;
        }

        if (_browserMenu.Visible)
        {
            CloseBrowser();
        }
        else if (_customizeMenu.Visible)
        {
            CloseCustomize();
        }
        else if (_advancementsMenu.Visible)
        {
            CloseAdvancements();
        }
        else if (_settingsMenu.Visible)
        {
            CloseSettings();
        }
        else if (_playMenu.Visible && _gameplayInstance is null)
        {
            ShowMainMenu();
        }
        else if (_gameplayInstance is null)
        {
            return;
        }
        else if (GetTree().Paused)
        {
            ResumeGame();
        }
        else
        {
            PauseGame();
        }

        GetViewport().SetInputAsHandled();
    }

    private void CacheNodes()
    {
        _sceneContainer = GetNode<Node>("SceneContainer");
        _menuBackground = GetNode<MenuBackground>("MenuBackground");
        _mainMenu = GetNode<Control>("Ui/MainMenu");
        _mainMenuLogoBall = GetNode<TextureRect>("Ui/MainMenu/Center/Panel/Layout/Logo/BallO");
        _playMenu = GetNode<Control>("Ui/PlayMenu");
        _pauseMenu = GetNode<Control>("Ui/PauseMenu");
        _settingsMenu = GetNode<Control>("Ui/SettingsMenu");
        _browserMenu = GetNode<Control>("Ui/BrowserMenu");
        _customizeMenu = GetNode<Control>("Ui/CustomizeMenu");
        _advancementsMenu = GetNode<Control>("Ui/AdvancementsMenu");
        _advancementsProgress = GetNode<Label>("Ui/AdvancementsMenu/Center/Panel/Layout/Progress");
        _advancementsList = GetNode<VBoxContainer>("Ui/AdvancementsMenu/Center/Panel/Layout/Scroll/List");
        _advancementNotifications = new AdvancementNotificationPresenter { Name = "AdvancementNotifications" };
        GetNode<CanvasLayer>("Ui").AddChild(_advancementNotifications);
        _loadingOverlay = GetNode<Control>("Ui/LoadingOverlay");
        _loadingDim = GetNode<ColorRect>("Ui/LoadingOverlay/Dim");
        _loadingLogo = GetNode<Control>("Ui/LoadingOverlay/Center/Layout/Logo");
        _loadingLogoLeft = GetNode<Label>("Ui/LoadingOverlay/Center/Layout/Logo/Vel");
        _loadingLogoBall = GetNode<TextureRect>("Ui/LoadingOverlay/Center/Layout/Logo/BallO");
        _loadingLogoRight = GetNode<Label>("Ui/LoadingOverlay/Center/Layout/Logo/Citex");
        _loadingRoomLabel = GetNode<Label>("Ui/LoadingOverlay/Center/Layout/RoomLabel");
        _loadingProgress = GetNode<ProgressBar>("Ui/LoadingOverlay/Center/Layout/Progress");
        _roomIntroCard = GetNode<Control>("Ui/RoomIntroCard");
        _roomIntroNumber = GetNode<Label>("Ui/RoomIntroCard/Panel/Content/Number");
        _roomIntroName = GetNode<Label>("Ui/RoomIntroCard/Panel/Content/Name");
        BuildRoomTransferOverlay();
        string customizeRoot = "Ui/CustomizeMenu/Center/Panel/Layout";
        string customizeControls = $"{customizeRoot}/Body/ControlsScroll/Controls";
        _customizeScroll = GetNode<ScrollContainer>($"{customizeRoot}/Body/ControlsScroll");
        _primaryColorGrid = GetNode<GridContainer>($"{customizeControls}/PrimaryGrid");
        _secondaryColorGrid = GetNode<GridContainer>($"{customizeControls}/SecondaryGrid");
        _patternGrid = GetNode<GridContainer>($"{customizeControls}/PatternGrid");
        _trailGrid = GetNode<GridContainer>($"{customizeControls}/TrailGrid");
        _customizeStatus = GetNode<Label>($"{customizeRoot}/Status");
        _candyPreview = GetNode<CandyPreview3D>($"{customizeRoot}/Body/PreviewFrame/Preview/Viewport/CandyPreview3D");
        _browserHeader = GetNode<Label>("Ui/BrowserMenu/Center/Panel/Layout/Header");
        _browserEmptyLabel = GetNode<Label>("Ui/BrowserMenu/Center/Panel/Layout/EmptyLabel");
        _browserList = GetNode<VBoxContainer>("Ui/BrowserMenu/Center/Panel/Layout/Scroll/List");
        _continueButton = GetNode<Button>("Ui/PlayMenu/Center/Panel/Layout/ContinueButton");
        _loadButton = GetNode<Button>("Ui/PlayMenu/Center/Panel/Layout/LoadButton");
        _roomSelectButton = GetNode<Button>("Ui/PlayMenu/Center/Panel/Layout/RoomSelectButton");
        string settingsRoot = "Ui/SettingsMenu/Center/Panel/Layout";
        _settingsHeader = GetNode<Label>($"{settingsRoot}/Header");
        _fpsOption = GetNode<OptionButton>($"{settingsRoot}/Tabs/VIDEO/Grid/FpsOption");
        _resolutionOption = GetNode<OptionButton>($"{settingsRoot}/Tabs/VIDEO/Grid/ResolutionOption");
        _windowOption = GetNode<OptionButton>($"{settingsRoot}/Tabs/VIDEO/Grid/WindowOption");
        _presetOption = GetNode<OptionButton>($"{settingsRoot}/Tabs/VIDEO/Grid/PresetOption");
        _renderScaleSlider = GetNode<HSlider>($"{settingsRoot}/Tabs/VIDEO/Grid/RenderScaleSlider");
        _msaaOption = GetNode<OptionButton>($"{settingsRoot}/Tabs/VIDEO/Grid/MsaaOption");
        _vsyncCheck = GetNode<CheckButton>($"{settingsRoot}/Tabs/VIDEO/Grid/VsyncCheck");
        _shadowsCheck = GetNode<CheckButton>($"{settingsRoot}/Tabs/VIDEO/Grid/ShadowsCheck");
        _sensitivitySlider = GetNode<HSlider>($"{settingsRoot}/Tabs/GAMEPLAY/Grid/SensitivitySlider");
        _invertYCheck = GetNode<CheckButton>($"{settingsRoot}/Tabs/GAMEPLAY/Grid/InvertYCheck");
        _defaultCameraOption = GetNode<OptionButton>($"{settingsRoot}/Tabs/GAMEPLAY/Grid/DefaultCameraOption");
        _cameraShakeSlider = GetNode<HSlider>($"{settingsRoot}/Tabs/GAMEPLAY/Grid/CameraShakeSlider");
        _interactionPromptsCheck = GetNode<CheckButton>($"{settingsRoot}/Tabs/GAMEPLAY/Grid/InteractionPromptsCheck");
        _masterSlider = GetNode<HSlider>($"{settingsRoot}/Tabs/AUDIO/Grid/MasterSlider");
        _musicSlider = GetNode<HSlider>($"{settingsRoot}/Tabs/AUDIO/Grid/MusicSlider");
        _sfxSlider = GetNode<HSlider>($"{settingsRoot}/Tabs/AUDIO/Grid/SfxSlider");
        _voiceSlider = GetNode<HSlider>($"{settingsRoot}/Tabs/AUDIO/Grid/VoiceSlider");
        _subtitlesCheck = GetNode<CheckButton>($"{settingsRoot}/Tabs/ACCESSIBILITY/Grid/SubtitlesCheck");
        _subtitleScaleOption = GetNode<OptionButton>($"{settingsRoot}/Tabs/ACCESSIBILITY/Grid/SubtitleScaleOption");
        _subtitleBackgroundCheck = GetNode<CheckButton>($"{settingsRoot}/Tabs/ACCESSIBILITY/Grid/SubtitleBackgroundCheck");
        _reducedMotionCheck = GetNode<CheckButton>($"{settingsRoot}/Tabs/ACCESSIBILITY/Grid/ReducedMotionCheck");
        _disableFlashesCheck = GetNode<CheckButton>($"{settingsRoot}/Tabs/ACCESSIBILITY/Grid/DisableFlashesCheck");
        _highContrastCheck = GetNode<CheckButton>($"{settingsRoot}/Tabs/ACCESSIBILITY/Grid/HighContrastCheck");
        _trailCheck = GetNode<CheckButton>($"{settingsRoot}/Tabs/ACCESSIBILITY/Grid/TrailCheck");
        foreach (CheckButton toggle in SettingsCheckButtons())
        {
            toggle.LayoutDirection = Control.LayoutDirectionEnum.Ltr;
        }
        string controlsRoot = $"{settingsRoot}/Tabs/CONTROLS/Layout/Grid";
        _bindingButtons[InputDefaults.MoveForward] = GetNode<Button>($"{controlsRoot}/ForwardButton");
        _bindingButtons[InputDefaults.MoveBack] = GetNode<Button>($"{controlsRoot}/BackButton");
        _bindingButtons[InputDefaults.MoveLeft] = GetNode<Button>($"{controlsRoot}/LeftButton");
        _bindingButtons[InputDefaults.MoveRight] = GetNode<Button>($"{controlsRoot}/RightButton");
        _bindingButtons[InputDefaults.ToggleCamera] = GetNode<Button>($"{controlsRoot}/CameraButton");
        _bindingButtons[InputDefaults.Interact] = GetNode<Button>($"{controlsRoot}/InteractButton");
        _quitConfirmation = GetNode<ConfirmationDialog>("Ui/QuitConfirmation");
        _newGameConfirmation = GetNode<ConfirmationDialog>("Ui/NewGameConfirmation");
        _roomCompleteDialog = GetNode<AcceptDialog>("Ui/RoomCompleteDialog");
        _unavailableDialog = GetNode<AcceptDialog>("Ui/UnavailableDialog");
        _roomDialogueVoice = GetNode<AudioStreamPlayer>("RoomDialogueVoice");
        _menuMusic = GetNode<AudioStreamPlayer>("MenuMusic");
        AudioStreamWav menuStream = (AudioStreamWav)_menuMusic.Stream.Duplicate();
        menuStream.LoopBegin = 0;
        menuStream.LoopEnd = Mathf.RoundToInt((float)(menuStream.GetLength() * menuStream.MixRate));
        menuStream.LoopMode = AudioStreamWav.LoopModeEnum.Forward;
        _menuMusic.Stream = menuStream;
    }

    private void BuildRoomTransferOverlay()
    {
        CanvasLayer ui = GetNode<CanvasLayer>("Ui");
        _roomTransferOverlay = new Control
        {
            Name = "RoomTransferOverlay",
            ProcessMode = ProcessModeEnum.Always,
            MouseFilter = Control.MouseFilterEnum.Stop,
            Visible = false,
        };
        _roomTransferOverlay.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        ui.AddChild(_roomTransferOverlay);

        ColorRect backdrop = new()
        {
            Color = new Color("091216"),
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        backdrop.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _roomTransferOverlay.AddChild(backdrop);

        _roomTransferTube = new Control
        {
            Name = "CandyContainer",
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        _roomTransferOverlay.AddChild(_roomTransferTube);
        _roomTransferTube.SetAnchorsPreset(Control.LayoutPreset.Center);
        _roomTransferTube.OffsetLeft = -RoomTransferCandyHalfSize;
        _roomTransferTube.OffsetRight = RoomTransferCandyHalfSize;
        _roomTransferTube.OffsetTop = -RoomTransferCandyHalfSize;
        _roomTransferTube.OffsetBottom = RoomTransferCandyHalfSize;
        _roomTransferTube.PivotOffset = Vector2.One * RoomTransferCandyHalfSize;

        _roomTransferCandy = new TextureRect
        {
            Texture = GD.Load<Texture2D>("res://assets/ui/logo_ball.png"),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        _roomTransferTube.AddChild(_roomTransferCandy);
        _roomTransferCandy.SetAnchorsPreset(Control.LayoutPreset.Center);
        _roomTransferCandy.OffsetLeft = -RoomTransferCandyHalfSize;
        _roomTransferCandy.OffsetTop = -RoomTransferCandyHalfSize;
        _roomTransferCandy.OffsetRight = RoomTransferCandyHalfSize;
        _roomTransferCandy.OffsetBottom = RoomTransferCandyHalfSize;
        _roomTransferCandy.PivotOffset = Vector2.One * RoomTransferCandyHalfSize;

        _roomTransferSubtitlePanel = new PanelContainer
        {
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        _roomTransferOverlay.AddChild(_roomTransferSubtitlePanel);
        _roomTransferSubtitlePanel.SetAnchorsPreset(Control.LayoutPreset.CenterBottom);
        _roomTransferSubtitlePanel.OffsetLeft = -420.0f;
        _roomTransferSubtitlePanel.OffsetRight = 420.0f;
        _roomTransferSubtitlePanel.OffsetTop = -112.0f;
        _roomTransferSubtitlePanel.OffsetBottom = -34.0f;
        _roomTransferSubtitlePanel.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color(0.02f, 0.04f, 0.05f, 0.88f),
            BorderColor = new Color("638e91"),
            BorderWidthLeft = 2,
            BorderWidthTop = 2,
            BorderWidthRight = 2,
            BorderWidthBottom = 2,
            CornerRadiusTopLeft = 10,
            CornerRadiusTopRight = 10,
            CornerRadiusBottomLeft = 10,
            CornerRadiusBottomRight = 10,
        });

        _roomTransferSubtitle = new Label
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        _roomTransferSubtitle.AddThemeColorOverride("font_color", new Color("f6ecd2"));
        _roomTransferSubtitle.AddThemeFontSizeOverride("font_size", 22);
        _roomTransferSubtitlePanel.AddChild(_roomTransferSubtitle);

        _roomTransferSfx = new AudioStreamPlayer
        {
            Name = "RoomTransferSfx",
            ProcessMode = ProcessModeEnum.Always,
            Bus = "SFX",
            Stream = GD.Load<AudioStream>("res://assets/audio/sfx/room_transfer.wav"),
            VolumeDb = -5.0f,
        };
        AddChild(_roomTransferSfx);
    }

    private void ShowRoomTransferPresentation(string speaker = "", string dialogue = "")
    {
        _roomTransferVisualTime = 0.0f;
        _roomTransferCandy.Rotation = 0.0f;
        _roomTransferCandy.OffsetLeft = -RoomTransferCandyHalfSize;
        _roomTransferCandy.OffsetTop = -RoomTransferCandyHalfSize;
        _roomTransferCandy.OffsetRight = RoomTransferCandyHalfSize;
        _roomTransferCandy.OffsetBottom = RoomTransferCandyHalfSize;
        _roomTransferSubtitle.Text = string.IsNullOrWhiteSpace(dialogue)
            ? string.Empty
            : $"{speaker.ToUpperInvariant()}  —  “{dialogue}”";
        _roomTransferSubtitle.AddThemeFontSizeOverride(
            "font_size",
            Math.Max(16, Mathf.RoundToInt(22.0f * _settings.SubtitleScalePercent / 100.0f)));
        _roomTransferSubtitlePanel.AddThemeStyleboxOverride(
            "panel",
            CreateRoomTransferSubtitleStyle(_settings.SubtitleBackground));
        _roomTransferSubtitlePanel.Visible = _settings.SubtitlesEnabled && !string.IsNullOrWhiteSpace(dialogue);
        _roomTransferOverlay.Modulate = Colors.White;
        _roomTransferTube.Modulate = Colors.White;
        _roomTransferTube.Scale = Vector2.One;
        _roomTransferSubtitlePanel.Modulate = Colors.White;
        _roomTransferOverlay.Show();
        _roomTransferOverlay.MoveToFront();
        _roomTransferShownAtMilliseconds = Time.GetTicksMsec();
    }

    private async Task FinishRoomTransferPresentationAsync()
    {
        double visibleSeconds = (Time.GetTicksMsec() - _roomTransferShownAtMilliseconds) / 1000.0;
        double remainingSeconds = LoadingCandyOnlySeconds - visibleSeconds;
        if (remainingSeconds > 0.0 && !IsAutomatedSmokeRun())
        {
            await ToSignal(
                GetTree().CreateTimer(remainingSeconds, processAlways: true),
                SceneTreeTimer.SignalName.Timeout);
        }

        if (!_settings.ReducedMotion && !IsAutomatedSmokeRun())
        {
            Tween exit = CreateTween().SetPauseMode(Tween.TweenPauseMode.Process).SetParallel();
            exit.TweenProperty(_roomTransferTube, "modulate:a", 0.0f, 0.24f)
                .SetTrans(Tween.TransitionType.Cubic)
                .SetEase(Tween.EaseType.In);
            exit.TweenProperty(_roomTransferSubtitlePanel, "modulate:a", 0.0f, 0.2f)
                .SetTrans(Tween.TransitionType.Cubic)
                .SetEase(Tween.EaseType.In);
            await ToSignal(exit, Tween.SignalName.Finished);
        }

        HideRoomTransferPresentation();
    }

    private void HideRoomTransferPresentation()
    {
        _roomTransferOverlay.Hide();
        _roomTransferOverlay.Modulate = Colors.White;
        _roomTransferTube.Modulate = Colors.White;
        _roomTransferTube.Scale = Vector2.One;
        _roomTransferSubtitlePanel.Modulate = Colors.White;
        _roomTransferSubtitlePanel.Hide();
        _roomTransferSfx.Stop();
        _roomDialogueVoice.Stop();
        _roomTransferShownAtMilliseconds = 0;
    }

    private void PopulateOptions()
    {
        AddOption(_fpsOption, "30 FPS", 30);
        AddOption(_fpsOption, "60 FPS", 60);
        AddOption(_fpsOption, "120 FPS", 120);
        AddOption(_fpsOption, "Unlimited", 0);
        AddOption(_resolutionOption, "1280 x 720", 0);
        AddOption(_resolutionOption, "1600 x 900", 1);
        AddOption(_resolutionOption, "1920 x 1080", 2);
        AddOption(_windowOption, "Windowed", 0);
        AddOption(_windowOption, "Fullscreen", 1);
        AddOption(_presetOption, "Potato", 0);
        AddOption(_presetOption, "Low", 1);
        AddOption(_presetOption, "Medium", 2);
        AddOption(_presetOption, "High", 3);
        AddOption(_msaaOption, "Off", 0);
        AddOption(_msaaOption, "2x", 2);
        AddOption(_msaaOption, "4x", 4);
        AddOption(_defaultCameraOption, "Third person", 0);
        AddOption(_defaultCameraOption, "First person", 1);
        AddOption(_subtitleScaleOption, "Small", 85);
        AddOption(_subtitleScaleOption, "Normal", 100);
        AddOption(_subtitleScaleOption, "Large", 125);
    }

    private void ConnectSignals()
    {
        GetNode<Button>("Ui/MainMenu/Center/Panel/Layout/PlayButton").Pressed += ShowPlayMenu;
        GetNode<Button>("Ui/MainMenu/Center/Panel/Layout/CustomizeButton").Pressed += () => OpenCustomize(MenuOrigin.Main);
        GetNode<Button>("Ui/MainMenu/Center/Panel/Layout/AdvancementsButton").Pressed += () => OpenAdvancements(MenuOrigin.Main);
        GetNode<Button>("Ui/MainMenu/Center/Panel/Layout/SettingsButton").Pressed += () => OpenSettings(MenuOrigin.Main);
        GetNode<Button>("Ui/MainMenu/Center/Panel/Layout/QuitButton").Pressed += () => _quitConfirmation.PopupCentered();
        GetNode<Button>("Ui/PauseMenu/Center/Panel/Layout/ResumeButton").Pressed += ResumeGame;
        GetNode<Button>("Ui/PauseMenu/Center/Panel/Layout/RestartButton").Pressed += RestartRoom;
        GetNode<Button>("Ui/PauseMenu/Center/Panel/Layout/AdvancementsButton").Pressed += () => OpenAdvancements(MenuOrigin.Pause);
        GetNode<Button>("Ui/PauseMenu/Center/Panel/Layout/SettingsButton").Pressed += () => OpenSettings(MenuOrigin.Pause);
        GetNode<Button>("Ui/PauseMenu/Center/Panel/Layout/MainMenuButton").Pressed += ShowMainMenu;
        _continueButton.Pressed += ContinueCampaign;
        _loadButton.Pressed += () => ShowLoadBrowser(MenuOrigin.Main);
        _roomSelectButton.Pressed += ShowRoomSelectBrowser;
        GetNode<Button>("Ui/PlayMenu/Center/Panel/Layout/NewGameButton").Pressed += () => _newGameConfirmation.PopupCentered();
        GetNode<Button>("Ui/PlayMenu/Center/Panel/Layout/BackButton").Pressed += ShowMainMenu;
        GetNode<Button>("Ui/BrowserMenu/Center/Panel/Layout/BackButton").Pressed += CloseBrowser;
        GetNode<Button>("Ui/CustomizeMenu/Center/Panel/Layout/Actions/BackButton").Pressed += CloseCustomize;
        GetNode<Button>("Ui/CustomizeMenu/Center/Panel/Layout/Actions/SaveButton").Pressed += SaveCustomization;
        GetNode<Button>("Ui/AdvancementsMenu/Center/Panel/Layout/BackButton").Pressed += CloseAdvancements;
        GetNode<Button>("Ui/SettingsMenu/Center/Panel/Layout/Actions/ApplyButton").Pressed += ReadApplyAndSaveSettings;
        GetNode<Button>("Ui/SettingsMenu/Center/Panel/Layout/Actions/DefaultsButton").Pressed += ResetSettingsControls;
        GetNode<Button>("Ui/SettingsMenu/Center/Panel/Layout/Actions/BackButton").Pressed += CloseSettings;
        _presetOption.ItemSelected += index => ApplyPresetToControls(_presetOption.GetItemId((int)index));
        foreach (CheckButton toggle in SettingsCheckButtons())
        {
            CheckButton capturedToggle = toggle;
            toggle.Toggled += _ => RefreshCheckButtonLabel(capturedToggle);
        }
        foreach ((StringName action, Button button) in _bindingButtons)
        {
            StringName capturedAction = action;
            button.Pressed += () => BeginRebind(capturedAction);
        }

        _quitConfirmation.Confirmed += () => GetTree().Quit();
        _newGameConfirmation.Confirmed += StartNewGame;
        _roomCompleteDialog.Confirmed += ContinueAfterRoomCompletion;
    }

    private void StartGame()
    {
        StartRoom(roomNumber: 1, saveRoomStart: !_runUiSmoke, elapsedSeconds: 0.0);
    }

    private void StartOpeningSequence()
    {
        CancelRoomCompletionTransition();
        StopMenuMusic();
        PackedScene? openingScene = OpeningScene ?? GD.Load<PackedScene>("res://scenes/OpeningSequence.tscn");
        if (openingScene is null)
        {
            ShowUnavailable("OPENING LOAD FAILED", "The opening sequence scene could not be loaded.");
            return;
        }

        GetTree().Paused = false;
        _gameplayInstance?.QueueFree();
        _gameplayInstance = null;
        _currentRoom = null;
        _currentPlayer = null;
        _openingInstance?.QueueFree();
        _openingInstance = openingScene.Instantiate<OpeningSequence>();
        _openingInstance.SmokeMode = _runOpeningSmoke;
        _openingInstance.CandyProfile = _profile;
        _openingInstance.SubtitlesEnabled = _settings.SubtitlesEnabled;
        _openingInstance.SubtitleScalePercent = _settings.SubtitleScalePercent;
        _openingInstance.SubtitleBackgroundEnabled = _settings.SubtitleBackground;
        _openingInstance.Finished += OnOpeningFinished;
        _sceneContainer.AddChild(_openingInstance);
        if (_runOpeningSmoke)
        {
            GD.Print("OPENING_SMOKE_START: opening scene instantiated.");
        }
        _mainMenu.Hide();
        _playMenu.Hide();
        _pauseMenu.Hide();
        _settingsMenu.Hide();
        _browserMenu.Hide();
        _customizeMenu.Hide();
        _advancementsMenu.Hide();
        _loadingOverlay.Hide();
        _menuBackground.SetActive(false);
        Godot.Input.MouseMode = Godot.Input.MouseModeEnum.Visible;
    }

    private void OnOpeningFinished()
    {
        _openingInstance?.QueueFree();
        _openingInstance = null;
        if (_runOpeningSmoke)
        {
            GD.Print("OPENING_SMOKE_PASS: opening sequence reached its handoff and emitted Finished.");
            GetTree().Quit(0);
            return;
        }

        ShowRoomTransferPresentation();
        _roomTransferSfx.Play();
        StartRoom(roomNumber: 1, saveRoomStart: true, elapsedSeconds: 0.0);
    }

    private void StartEndingSequence()
    {
        CancelRoomCompletionTransition();
        StopMenuMusic();
        PackedScene? endingScene = EndingScene ?? GD.Load<PackedScene>("res://scenes/EndingSequence.tscn");
        if (endingScene is null)
        {
            ShowUnavailable("ENDING LOAD FAILED", "The ending sequence scene could not be loaded.");
            return;
        }

        GetTree().Paused = false;
        _roomDialogueVoice.Stop();
        _gameplayInstance?.QueueFree();
        _gameplayInstance = null;
        _currentRoom = null;
        _currentPlayer = null;
        _openingInstance?.QueueFree();
        _openingInstance = null;
        _endingInstance?.QueueFree();
        _endingInstance = endingScene.Instantiate<EndingSequence>();
        _endingInstance.SmokeMode = _runEndingSmoke;
        _endingInstance.CandyProfile = _profile;
        _endingInstance.SubtitlesEnabled = _settings.SubtitlesEnabled;
        _endingInstance.SubtitleScalePercent = _settings.SubtitleScalePercent;
        _endingInstance.SubtitleBackgroundEnabled = _settings.SubtitleBackground;
        _endingInstance.Finished += OnEndingFinished;
        _sceneContainer.AddChild(_endingInstance);
        if (_runEndingSmoke)
        {
            GD.Print("ENDING_SMOKE_START: ending scene instantiated.");
        }

        _mainMenu.Hide();
        _playMenu.Hide();
        _pauseMenu.Hide();
        _settingsMenu.Hide();
        _browserMenu.Hide();
        _customizeMenu.Hide();
        _advancementsMenu.Hide();
        _loadingOverlay.Hide();
        _roomIntroCard.Hide();
        _menuBackground.SetActive(false);
        Godot.Input.MouseMode = Godot.Input.MouseModeEnum.Visible;
    }

    private void OnEndingFinished()
    {
        bool freezeReached = _endingInstance?.FreezeFrameReached == true;
        bool blackoutReached = _endingInstance?.BlackoutReached == true;
        bool creditsCompleted = _endingInstance?.CreditsSequenceCompleted == true;
        float creditsWidthRatio = _endingInstance?.CreditsWidthRatio ?? 0.0f;
        float creditsHeightRatio = _endingInstance?.CreditsHeightRatio ?? 0.0f;
        _endingInstance?.QueueFree();
        _endingInstance = null;
        if (_runEndingSmoke)
        {
            if (!freezeReached || !blackoutReached || !creditsCompleted || creditsWidthRatio < 0.72f || creditsHeightRatio < 0.60f)
            {
                GD.PushError($"ENDING_SMOKE_FAIL: finale contract failed (freeze={freezeReached}, black={blackoutReached}, credits={creditsCompleted}, coverage={creditsWidthRatio:F2}x{creditsHeightRatio:F2}).");
                GetTree().Quit(1);
                return;
            }
            _endingCreditsContractPassed = true;
            _endingReturnSmokePending = true;
        }

        ShowMainMenu();
        PlayStartupLoadingSequenceAsync();
    }

    private void RunStoryAudioSmoke()
    {
        List<string> paths = new()
        {
            "res://assets/audio/voice/opening_01_child.mp3",
            "res://assets/audio/voice/opening_02_mother.mp3",
            "res://assets/audio/voice/opening_03_child.mp3",
            "res://assets/audio/voice/opening_04_mother.mp3",
            "res://assets/audio/voice/ending_finally.mp3",
        };
        for (int room = 1; room <= CampaignSaveService.MaximumRoomCount; room++)
        {
            paths.Add($"res://assets/audio/voice/room{room:00}.mp3");
        }

        double shortest = double.MaxValue;
        double longest = 0.0;
        foreach (string path in paths)
        {
            AudioStream? stream = GD.Load<AudioStream>(path);
            double length = stream?.GetLength() ?? 0.0;
            if (length < 0.35 || length > 6.0)
            {
                GD.PushError($"STORY_AUDIO_SMOKE_FAIL: invalid or implausible clip {path} ({length:F2}s).");
                GetTree().Quit(1);
                return;
            }

            shortest = Math.Min(shortest, length);
            longest = Math.Max(longest, length);
            if (path.Contains("opening_", StringComparison.Ordinal) || path.Contains("ending_", StringComparison.Ordinal))
            {
                GD.Print($"STORY_AUDIO_CINEMATIC_CLIP: {path.GetFile()}={length:F2}s");
            }
        }

        GD.Print($"STORY_AUDIO_SMOKE_PASS: {paths.Count} neural clips load; duration range {shortest:F2}-{longest:F2}s.");
        GetTree().Quit(0);
    }

    private async void StartRoom(int roomNumber, bool saveRoomStart, double elapsedSeconds)
    {
        if (_loadingRoom)
        {
            return;
        }

        RoomCatalogEntry? entry = RoomCatalog.Find(roomNumber);
        if (entry is null)
        {
            ShowUnavailable("ROOM NOT AVAILABLE", $"Room {roomNumber:00} has not been built yet.");
            return;
        }

        bool firstPersonMode = _currentRoom?.GetNodeOrNull<PlayerCameraRig>("CameraRig")?.IsFirstPerson
            ?? _settings.DefaultFirstPerson;
        _loadingRoom = true;
        StopMenuMusic();
        bool useRoomTransferPresentation = _roomTransferOverlay.Visible;
        if (!useRoomTransferPresentation)
        {
            ShowLoadingScreen();
        }
        try
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            if (!useRoomTransferPresentation && !_runUiSmoke && !_runSaveSmoke && !_runCampaignFlowSmoke)
            {
                await ToSignal(GetTree().CreateTimer(LoadingCandyOnlySeconds, processAlways: true), SceneTreeTimer.SignalName.Timeout);
            }

            PackedScene? scene = GD.Load<PackedScene>(entry.ScenePath);
            if (scene is null)
            {
                ShowUnavailable("ROOM LOAD FAILED", $"The scene for Room {roomNumber:00} could not be loaded.");
                return;
            }

            GetTree().Paused = false;
            _gameplayInstance?.QueueFree();
            PackedScene selectedScene = roomNumber == 1 ? GameplayScene ?? scene : scene;
            _gameplayInstance = selectedScene.Instantiate();
            _currentRoom = _gameplayInstance as RoomRuntime;
            if (_currentRoom is null)
            {
                _gameplayInstance.QueueFree();
                _gameplayInstance = null;
                ShowUnavailable("ROOM LOAD FAILED", "The room scene does not provide a RoomRuntime controller.");
                return;
            }

            _currentRoom.RoomNumber = entry.Number;
            _currentRoom.RoomId = entry.Id;
            _currentRoom.RoomDisplayName = entry.DisplayName;
            _currentRoom.RoomCompleted += OnRoomCompleted;
            _sceneContainer.AddChild(_gameplayInstance);
            _currentPlayer = _currentRoom.GetNodeOrNull<PlayerBall>("Player");
            _campaignElapsedSeconds = Math.Max(0.0, elapsedSeconds);
            _roomCompletionHandled = false;
            _pendingCompletionSnapshot = null;
            _mainMenu.Hide();
            _playMenu.Hide();
            _pauseMenu.Hide();
            _settingsMenu.Hide();
            _browserMenu.Hide();
            _advancementsMenu.Hide();
            _customizeMenu.Hide();
            _menuBackground.SetActive(false);
            ApplyShadowSettings();
            ApplyProfileToPlayers();
            ApplyCameraSettings(inputEnabled: false, applyDefaultMode: false);
            _currentRoom.GetNodeOrNull<PlayerCameraRig>("CameraRig")?.SetFirstPerson(firstPersonMode);
            if (useRoomTransferPresentation)
            {
                await FinishRoomTransferPresentationAsync();
            }
            else
            {
                await FinishLoadingScreenAsync();
            }
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

            // The opaque handoff has ended. Restore gameplay audio before
            // returning input so the room-title card cannot overlap a silent
            // but already playable start interval.
            RestoreSfxAfterRoomTransfer();
            ApplyCameraSettings(inputEnabled: !_runPerformanceSmoke, applyDefaultMode: false);
            if (_runPerformanceSmoke)
            {
                Godot.Input.MouseMode = Godot.Input.MouseModeEnum.Visible;
            }
            await ShowRoomIntroCardAsync(entry);
            FlushDeferredRoomStartNotifications(roomNumber);
        }
        finally
        {
            if (useRoomTransferPresentation)
            {
                HideRoomTransferPresentation();
            }
            else
            {
                HideLoadingScreen();
            }
            RestoreSfxAfterRoomTransfer();
            _loadingRoom = false;
        }
    }

    private async void PlayStartupLoadingSequenceAsync()
    {
        ulong startedAtMilliseconds = Time.GetTicksMsec();
        _mainMenu.Modulate = new Color(1.0f, 1.0f, 1.0f, 0.0f);
        ShowLoadingScreen();
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

        Vector2 viewportCenter = GetViewport().GetVisibleRect().Size * 0.5f;
        Vector2 candyCenter = _loadingLogoBall.GetGlobalRect().GetCenter();
        if (_runStartupLoadingSmoke &&
            (!_loadingOverlay.Visible || candyCenter.DistanceTo(viewportCenter) > 1.5f ||
             _loadingLogoLeft.Modulate.A > 0.01f || _loadingLogoRight.Modulate.A > 0.01f))
        {
            GD.PushError($"STARTUP_LOADING_SMOKE_FAIL: initial candy stage is invalid (candy={candyCenter}, viewport={viewportCenter}).");
            GetTree().Quit(1);
            return;
        }

        await ToSignal(GetTree().CreateTimer(LoadingCandyOnlySeconds, processAlways: true), SceneTreeTimer.SignalName.Timeout);
        await FinishStartupLoadingScreenAsync();
        if (_endingReturnSmokePending)
        {
            _endingReturnSmokePending = false;
            if (!_endingCreditsContractPassed || _loadingOverlay.Visible || _mainMenu.Modulate.A < 0.99f || !_mainMenu.Visible)
            {
                GD.PushError($"ENDING_SMOKE_FAIL: startup loading did not return cleanly to the menu (credits={_endingCreditsContractPassed}, loading={_loadingOverlay.Visible}, menu={_mainMenu.Visible}, alpha={_mainMenu.Modulate.A:F2}).");
                GetTree().Quit(1);
                return;
            }
            GD.Print("ENDING_SMOKE_PASS: mouth blackout, large fading credits, startup loading screen and main-menu return all completed in order.");
            GetTree().Quit(0);
            return;
        }
        if (_runStartupLoadingSmoke)
        {
            double elapsedSeconds = (Time.GetTicksMsec() - startedAtMilliseconds) / 1000.0;
            if (_loadingOverlay.Visible || _loadingDim.Color.A > 0.01f || _loadingLogo.Modulate.A > 0.01f ||
                _mainMenu.Modulate.A < 0.99f ||
                _startupWordCenterError > 2.0f || _startupLogoLandingError > 2.0f ||
                _lastLoadingRevealToExitSeconds < 1.9 || _lastLoadingRevealToExitSeconds > 2.6)
            {
                GD.PushError($"STARTUP_LOADING_SMOKE_FAIL: fade state or duration is invalid " +
                    $"(word-center={_startupWordCenterError:F2}px, landing={_startupLogoLandingError:F2}px, " +
                    $"reveal-to-exit={_lastLoadingRevealToExitSeconds:F2}s, total={elapsedSeconds:F2}s).");
                GetTree().Quit(1);
                return;
            }

            GD.Print($"STARTUP_LOADING_SMOKE_PASS: reveal-to-exit took {_lastLoadingRevealToExitSeconds:F2}s " +
                $"and the complete candy-to-menu sequence took {elapsedSeconds:F2}s.");
            GetTree().Quit(0);
        }
    }

    private async Task FinishStartupLoadingScreenAsync()
    {
        _loadingProgress.Value = 100.0;
        ulong revealStartedAtMilliseconds = Time.GetTicksMsec();
        float revealSeconds = _settings.ReducedMotion ? 0.35f : 0.72f;

        Rect2 wordBounds = GetLoadingWordGlobalRect();
        Vector2 viewportCenter = GetViewport().GetVisibleRect().Size * 0.5f;
        Vector2 centeredLogoPosition = _loadingLogo.GlobalPosition + (viewportCenter - wordBounds.GetCenter());
        _loadingTween = CreateTween().SetPauseMode(Tween.TweenPauseMode.Process).SetParallel();
        _loadingTween.TweenProperty(_loadingLogoLeft, "modulate:a", 1.0f, revealSeconds)
            .SetTrans(Tween.TransitionType.Cubic)
            .SetEase(Tween.EaseType.Out);
        _loadingTween.TweenProperty(_loadingLogoRight, "modulate:a", 1.0f, revealSeconds)
            .SetTrans(Tween.TransitionType.Cubic)
            .SetEase(Tween.EaseType.Out);
        if (_settings.ReducedMotion)
        {
            _loadingLogo.GlobalPosition = centeredLogoPosition;
        }
        else
        {
            _loadingTween.TweenProperty(_loadingLogo, "global_position", centeredLogoPosition, revealSeconds)
                .SetTrans(Tween.TransitionType.Cubic)
                .SetEase(Tween.EaseType.InOut);
        }
        await ToSignal(_loadingTween, Tween.SignalName.Finished);
        _startupWordCenterError = GetLoadingWordGlobalRect().GetCenter().DistanceTo(viewportCenter);

        await ToSignal(
            GetTree().CreateTimer(_settings.ReducedMotion ? 1.0 : 0.32, processAlways: true),
            SceneTreeTimer.SignalName.Timeout);

        Vector2 loadingBallCenter = _loadingLogoBall.GetGlobalRect().GetCenter();
        Vector2 mainBallCenter = _mainMenuLogoBall.GetGlobalRect().GetCenter();
        Vector2 ballCenterInLogo = loadingBallCenter - _loadingLogo.GlobalPosition;
        float targetScale = _mainMenuLogoBall.Size.X / Mathf.Max(_loadingLogoBall.Size.X, 1.0f);
        _loadingLogo.PivotOffset = ballCenterInLogo;
        Vector2 landedLogoPosition = mainBallCenter - (ballCenterInLogo * targetScale);
        float travelSeconds = _settings.ReducedMotion ? 0.01f : 0.78f;
        _loadingTween = CreateTween().SetPauseMode(Tween.TweenPauseMode.Process).SetParallel();
        _loadingTween.TweenProperty(_loadingLogo, "global_position", landedLogoPosition, travelSeconds)
            .SetTrans(Tween.TransitionType.Quint)
            .SetEase(Tween.EaseType.InOut);
        _loadingTween.TweenProperty(_loadingLogo, "scale", Vector2.One * targetScale, travelSeconds)
            .SetTrans(Tween.TransitionType.Quint)
            .SetEase(Tween.EaseType.InOut);
        _loadingTween.TweenProperty(_loadingDim, "color:a", 0.0f, travelSeconds)
            .SetTrans(Tween.TransitionType.Cubic)
            .SetEase(Tween.EaseType.InOut);
        await ToSignal(_loadingTween, Tween.SignalName.Finished);
        _startupLogoLandingError = _loadingLogoBall.GetGlobalRect().GetCenter().DistanceTo(mainBallCenter);

        float menuRevealSeconds = _settings.ReducedMotion ? 0.60f : 0.34f;
        _loadingTween = CreateTween().SetPauseMode(Tween.TweenPauseMode.Process).SetParallel();
        _loadingTween.TweenProperty(_mainMenu, "modulate:a", 1.0f, menuRevealSeconds)
            .SetTrans(Tween.TransitionType.Cubic)
            .SetEase(Tween.EaseType.Out);
        _loadingTween.TweenProperty(_loadingLogo, "modulate:a", 0.0f, menuRevealSeconds)
            .SetTrans(Tween.TransitionType.Cubic)
            .SetEase(Tween.EaseType.Out);
        await ToSignal(_loadingTween, Tween.SignalName.Finished);

        _mainMenu.Modulate = Colors.White;
        _lastLoadingRevealToExitSeconds = (Time.GetTicksMsec() - revealStartedAtMilliseconds) / 1000.0;
        HideLoadingScreen();
    }

    private Rect2 GetLoadingWordGlobalRect()
    {
        Rect2 bounds = _loadingLogoLeft.GetGlobalRect();
        bounds = bounds.Merge(_loadingLogoBall.GetGlobalRect());
        return bounds.Merge(_loadingLogoRight.GetGlobalRect());
    }

    private void ShowLoadingScreen()
    {
        _loadingTween?.Kill();
        _loadingTween = null;
        _loadingVisualTime = 0.0f;
        _lastLoadingRevealToExitSeconds = 0.0;
        _loadingProgress.Value = 8.0;
        _loadingRoomLabel.Text = "ROUTING TO THE NEXT CHAMBER";
        _loadingOverlay.Modulate = Colors.White;
        _loadingDim.Color = new Color(0.004f, 0.005f, 0.007f, 1.0f);
        _loadingLogo.Modulate = Colors.White;
        _loadingLogo.Scale = Vector2.One;
        _loadingLogo.PivotOffset = Vector2.Zero;
        _loadingLogoBall.Modulate = Colors.White;
        _loadingLogoLeft.Modulate = new Color(1.0f, 1.0f, 1.0f, 0.0f);
        _loadingLogoRight.Modulate = new Color(1.0f, 1.0f, 1.0f, 0.0f);
        _loadingOverlay.Show();
        _loadingOverlay.MoveToFront();
        ApplyCameraSettings(inputEnabled: false, applyDefaultMode: false);
        Godot.Input.MouseMode = Godot.Input.MouseModeEnum.Visible;
    }

    private async Task FinishLoadingScreenAsync()
    {
        _loadingProgress.Value = 100.0;
        bool smoke = _runUiSmoke || _runSaveSmoke || _runCampaignFlowSmoke || _runPerformanceSmoke;
        if (smoke)
        {
            HideLoadingScreen();
            return;
        }

        ulong revealStartedAtMilliseconds = Time.GetTicksMsec();
        float revealSeconds = _settings.ReducedMotion ? 0.35f : 0.75f;
        _loadingTween = CreateTween().SetPauseMode(Tween.TweenPauseMode.Process).SetParallel();
        _loadingTween.TweenProperty(_loadingLogoLeft, "modulate:a", 1.0f, revealSeconds)
            .SetTrans(Tween.TransitionType.Cubic)
            .SetEase(Tween.EaseType.Out);
        _loadingTween.TweenProperty(_loadingLogoRight, "modulate:a", 1.0f, revealSeconds)
            .SetTrans(Tween.TransitionType.Cubic)
            .SetEase(Tween.EaseType.Out);
        await ToSignal(_loadingTween, Tween.SignalName.Finished);

        await ToSignal(
            GetTree().CreateTimer(_settings.ReducedMotion ? 1.10 : 0.45, processAlways: true),
            SceneTreeTimer.SignalName.Timeout);

        float logoFadeSeconds = _settings.ReducedMotion ? 0.35f : 0.55f;
        float blackFadeSeconds = _settings.ReducedMotion ? 0.70f : 0.95f;
        _loadingTween = CreateTween().SetPauseMode(Tween.TweenPauseMode.Process).SetParallel();
        _loadingTween.TweenProperty(_loadingLogo, "modulate:a", 0.0f, logoFadeSeconds)
            .SetTrans(Tween.TransitionType.Cubic)
            .SetEase(Tween.EaseType.In);
        _loadingTween.TweenProperty(_loadingDim, "color:a", 0.0f, blackFadeSeconds)
            .SetTrans(Tween.TransitionType.Cubic)
            .SetEase(Tween.EaseType.InOut);
        await ToSignal(_loadingTween, Tween.SignalName.Finished);
        _lastLoadingRevealToExitSeconds = (Time.GetTicksMsec() - revealStartedAtMilliseconds) / 1000.0;
        HideLoadingScreen();
    }

    private void HideLoadingScreen()
    {
        _loadingTween?.Kill();
        _loadingTween = null;
        _loadingProgress.Value = 100.0;
        _loadingOverlay.Hide();
        _loadingOverlay.Modulate = Colors.White;
    }

    private async Task ShowRoomIntroCardAsync(RoomCatalogEntry room)
    {
        CanvasLayer? roomHud = _currentRoom?.GetNodeOrNull<CanvasLayer>("Hud");
        roomHud?.Hide();
        _roomIntroNumber.Text = $"ROOM {room.Number:00} / 28";
        _roomIntroName.Text = room.DisplayName.ToUpperInvariant();
        _roomIntroCard.Position = new Vector2(0.0f, -150.0f);
        _roomIntroCard.Modulate = new Color(1.0f, 1.0f, 1.0f, 0.0f);
        _roomIntroCard.Show();
        _roomIntroCard.MoveToFront();

        bool smoke = _runUiSmoke || _runSaveSmoke || _runCampaignFlowSmoke;
        if (smoke)
        {
            _roomIntroCard.Position = Vector2.Zero;
            _roomIntroCard.Modulate = Colors.White;
            _roomIntroCard.Hide();
            roomHud?.Show();
            return;
        }

        if (_settings.ReducedMotion)
        {
            _roomIntroCard.Position = Vector2.Zero;
            _roomIntroCard.Modulate = Colors.White;
        }
        else
        {
            Tween entrance = CreateTween().SetPauseMode(Tween.TweenPauseMode.Process).SetParallel();
            entrance.TweenProperty(_roomIntroCard, "position:y", 0.0f, 0.48f)
                .SetTrans(Tween.TransitionType.Quint)
                .SetEase(Tween.EaseType.Out);
            entrance.TweenProperty(_roomIntroCard, "modulate:a", 1.0f, 0.34f)
                .SetTrans(Tween.TransitionType.Cubic)
                .SetEase(Tween.EaseType.Out);
            await ToSignal(entrance, Tween.SignalName.Finished);
        }

        await ToSignal(GetTree().CreateTimer(1.15, processAlways: true), SceneTreeTimer.SignalName.Timeout);
        if (!_settings.ReducedMotion)
        {
            Tween exit = CreateTween().SetPauseMode(Tween.TweenPauseMode.Process).SetParallel();
            exit.TweenProperty(_roomIntroCard, "position:y", -32.0f, 0.32f)
                .SetTrans(Tween.TransitionType.Cubic)
                .SetEase(Tween.EaseType.In);
            exit.TweenProperty(_roomIntroCard, "modulate:a", 0.0f, 0.28f)
                .SetTrans(Tween.TransitionType.Cubic)
                .SetEase(Tween.EaseType.In);
            await ToSignal(exit, Tween.SignalName.Finished);
        }

        _roomIntroCard.Hide();
        roomHud?.Show();
    }

    private void ShowMainMenu()
    {
        CancelRoomCompletionTransition();
        GetTree().Paused = false;
        RestoreSfxAfterPause();
        if (_gameplayInstance is not null)
        {
            _gameplayInstance.QueueFree();
            _gameplayInstance = null;
            _currentRoom = null;
            _currentPlayer = null;
        }

        if (_openingInstance is not null)
        {
            _openingInstance.QueueFree();
            _openingInstance = null;
        }

        if (_endingInstance is not null)
        {
            _endingInstance.QueueFree();
            _endingInstance = null;
        }

        _roomDialogueVoice.Stop();

        _mainMenu.Show();
        _playMenu.Hide();
        _pauseMenu.Hide();
        _settingsMenu.Hide();
        _browserMenu.Hide();
        _advancementsMenu.Hide();
        _customizeMenu.Hide();
        _loadingOverlay.Hide();
        _menuBackground.SetActive(true);
        StartMenuMusic();
        Godot.Input.MouseMode = Godot.Input.MouseModeEnum.Visible;
    }

    private async void StartMenuMusic()
    {
        int generation = ++_menuMusicGeneration;
        _menuMusicTween?.Kill();
        if (DisplayServer.GetName() == "headless")
        {
            _menuMusic.Stop();
            return;
        }

        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        if (generation != _menuMusicGeneration || !IsInstanceValid(_menuMusic))
        {
            return;
        }

        if (!_menuMusic.Playing)
        {
            _menuMusic.VolumeDb = -40.0f;
            _menuMusic.Play();
            if (_runMenuPreview)
            {
                GD.Print($"MENU_MUSIC_START: playing={_menuMusic.Playing}, length={_menuMusic.Stream.GetLength():F2}s.");
            }
        }

        _menuMusicTween = CreateTween().SetPauseMode(Tween.TweenPauseMode.Process);
        _menuMusicTween.TweenProperty(_menuMusic, "volume_db", -12.0f, 1.15f)
            .SetTrans(Tween.TransitionType.Sine)
            .SetEase(Tween.EaseType.Out);
    }

    private void StopMenuMusic()
    {
        _menuMusicGeneration++;
        _menuMusicTween?.Kill();
        _menuMusicTween = null;
        _menuMusic.Stop();
        _menuMusic.VolumeDb = -40.0f;
        RestoreSfxAfterPause();
    }

    private void ShowPlayMenu()
    {
        UpdatePlayMenuState();
        _mainMenu.Hide();
        _playMenu.Show();
        _browserMenu.Hide();
        _advancementsMenu.Hide();
        _customizeMenu.Hide();
    }

    private void UpdatePlayMenuState()
    {
        IReadOnlyList<CampaignSnapshot> snapshots = CampaignSaveService.LoadAll(out IReadOnlyList<string> errors, _campaignRoot);
        foreach (string error in errors)
        {
            GD.PushWarning($"Skipped campaign snapshot: {error}");
        }

        _continueButton.Disabled = snapshots.Count == 0;
        _loadButton.Disabled = snapshots.Count == 0;
        _roomSelectButton.Disabled = false;
    }

    private void StartNewGame()
    {
        if (!CampaignSaveService.DeleteAll(out string? error, _campaignRoot))
        {
            ShowUnavailable("NEW GAME FAILED", $"Campaign saves could not be cleared.\n{error}");
            return;
        }

        StartOpeningSequence();
    }

    private void ContinueCampaign()
    {
        CampaignSnapshot? latest = CampaignSaveService.LoadLatest(_campaignRoot);
        if (latest is null)
        {
            UpdatePlayMenuState();
            return;
        }

        LoadSnapshot(latest);
    }

    private void LoadSnapshot(CampaignSnapshot snapshot)
    {
        if (!CampaignSaveService.DeleteSnapshotsAfter(snapshot, out string? error, _campaignRoot))
        {
            ShowUnavailable("LOAD GAME FAILED", $"Later campaign saves could not be cleared.\n{error}");
            return;
        }

        if (snapshot.Kind == SnapshotKind.RoomStart)
        {
            StartRoomWithCandyHandoff(snapshot.RoomNumber, saveRoomStart: false, snapshot.CampaignElapsedSeconds);
            return;
        }

        _pendingCompletionSnapshot = snapshot;
        SetRoomCompleteDialog(snapshot.RoomNumber, snapshot.RoomName, snapshotSaved: false);
        if (IsAutomatedSmokeRun())
        {
            _roomCompleteDialog.PopupCentered(new Vector2I(560, 230));
        }
        else
        {
            StartRoomCompletionTransition(snapshot.RoomNumber);
        }
    }

    private void ShowLoadBrowser(MenuOrigin origin)
    {
        _browserOrigin = origin;
        _browserHeader.Text = "LOAD GAME";
        ClearBrowserRows();
        IReadOnlyList<CampaignSnapshot> snapshots = CampaignSaveService.LoadAll(out IReadOnlyList<string> errors, _campaignRoot);
        foreach (CampaignSnapshot snapshot in snapshots)
        {
            AddSnapshotRow(snapshot);
        }

        _browserEmptyLabel.Visible = snapshots.Count == 0;
        _browserEmptyLabel.Text = errors.Count == 0
            ? "No campaign snapshots are available."
            : "No valid snapshots are available. Invalid files were skipped.";
        _mainMenu.Hide();
        _playMenu.Hide();
        _pauseMenu.Hide();
        _settingsMenu.Hide();
        _advancementsMenu.Hide();
        _customizeMenu.Hide();
        _browserMenu.Show();
        Godot.Input.MouseMode = Godot.Input.MouseModeEnum.Visible;
    }

    private void ShowRoomSelectBrowser()
    {
        _browserOrigin = MenuOrigin.Main;
        _browserHeader.Text = "SELECT ROOM";
        ClearBrowserRows();
        int rowCount = 0;
        IReadOnlySet<int> completed = CampaignSaveService.GetCompletedRoomNumbers(_campaignRoot);
        foreach (RoomCatalogEntry room in RoomCatalog.All.Where(room => completed.Contains(room.Number)))
        {
            AddRoomSelectRow(room);
            rowCount++;
        }

        _browserEmptyLabel.Visible = rowCount == 0;
        _browserEmptyLabel.Text = "No rooms are available.";
        _playMenu.Hide();
        _browserMenu.Show();
    }

    private void CloseBrowser()
    {
        _browserMenu.Hide();
        if (_browserOrigin == MenuOrigin.Pause)
        {
            _pauseMenu.Show();
        }
        else
        {
            _playMenu.Show();
            UpdatePlayMenuState();
        }
    }

    private void PauseGame()
    {
        if (_gameplayInstance is null)
        {
            return;
        }

        GetTree().Paused = true;
        MuteSfxForPause();
        StartMenuMusic();
        _pauseMenu.Show();
        ApplyCameraSettings(inputEnabled: false, applyDefaultMode: false);
    }

    private void ResumeGame()
    {
        CancelRebind();
        _pauseMenu.Hide();
        _settingsMenu.Hide();
        _browserMenu.Hide();
        _advancementsMenu.Hide();
        _customizeMenu.Hide();
        StopMenuMusic();
        GetTree().Paused = false;
        ApplyCameraSettings(inputEnabled: true, applyDefaultMode: false);
    }

    private void RestartRoom()
    {
        _currentRoom?.RestartRoom();

        ResumeGame();
    }

    private void MuteSfxForPause()
    {
        if (_pauseSfxOverrideActive)
        {
            return;
        }

        int sfxBus = AudioServer.GetBusIndex("SFX");
        if (sfxBus < 0)
        {
            return;
        }

        _sfxMutedBeforePause = AudioServer.IsBusMute(sfxBus);
        AudioServer.SetBusMute(sfxBus, true);
        _pauseSfxOverrideActive = true;
    }

    private void RestoreSfxAfterPause()
    {
        if (!_pauseSfxOverrideActive)
        {
            return;
        }

        int sfxBus = AudioServer.GetBusIndex("SFX");
        if (sfxBus >= 0)
        {
            AudioServer.SetBusMute(sfxBus, _sfxMutedBeforePause);
        }

        _pauseSfxOverrideActive = false;
    }

    private void MuteSfxForRoomTransfer()
    {
        if (_roomTransferSfxOverrideActive)
        {
            return;
        }

        int sfxBus = AudioServer.GetBusIndex("SFX");
        if (sfxBus < 0)
        {
            return;
        }

        _sfxMutedBeforeRoomTransfer = AudioServer.IsBusMute(sfxBus);
        AudioServer.SetBusMute(sfxBus, true);
        _roomTransferSfxOverrideActive = true;
    }

    private void RestoreSfxAfterRoomTransfer()
    {
        if (!_roomTransferSfxOverrideActive)
        {
            return;
        }

        int sfxBus = AudioServer.GetBusIndex("SFX");
        if (sfxBus >= 0)
        {
            AudioServer.SetBusMute(sfxBus, _sfxMutedBeforeRoomTransfer);
        }

        _roomTransferSfxOverrideActive = false;
    }

    private void OpenSettings(MenuOrigin origin)
    {
        CancelRebind();
        _settingsOrigin = origin;
        _settingsHeader.Text = origin == MenuOrigin.Main ? "SETTINGS" : "PAUSED / SETTINGS";
        _mainMenu.Hide();
        _pauseMenu.Hide();
        _customizeMenu.Hide();
        _settingsMenu.Show();
        Godot.Input.MouseMode = Godot.Input.MouseModeEnum.Visible;
    }

    private void CloseSettings()
    {
        CancelRebind();
        _settingsMenu.Hide();
        if (_settingsOrigin == MenuOrigin.Main)
        {
            _mainMenu.Show();
        }
        else
        {
            _pauseMenu.Show();
        }
    }

    private void OpenCustomize(MenuOrigin origin)
    {
        _customizeOrigin = origin;
        _profile = ProfileStore.Load(out string? warning, _profilePath);
        if (!string.IsNullOrWhiteSpace(warning))
        {
            GD.PushWarning(warning);
        }

        PopulateCustomizationOptions();
        SetCustomizeStatus("CURRENT LOOK", locked: false);
        _candyPreview.MotionEnabled = !_settings.ReducedMotion;
        _candyPreview.Visible = true;
        _candyPreview.Apply(_profile);
        _mainMenu.Hide();
        _playMenu.Hide();
        _pauseMenu.Hide();
        _settingsMenu.Hide();
        _browserMenu.Hide();
        _advancementsMenu.Hide();
        _customizeMenu.Show();
        Godot.Input.MouseMode = Godot.Input.MouseModeEnum.Visible;
    }

    private void CloseCustomize()
    {
        _customizeMenu.Hide();
        _candyPreview.Visible = false;
        if (_customizeOrigin == MenuOrigin.Pause)
        {
            _pauseMenu.Show();
        }
        else
        {
            _mainMenu.Show();
        }
    }

    private void PopulateCustomizationOptions()
    {
        _populatingCustomize = true;
        _selectedPrimaryColorId = _profile.PrimaryColorId;
        _selectedSecondaryColorId = _profile.SecondaryColorId;
        _selectedPatternId = _profile.PatternId;
        _selectedTrailId = _profile.TrailId;
        BuildCosmeticGrid(_primaryColorGrid, _primaryColorButtons, CosmeticKind.Color, CosmeticSlot.PrimaryColor);
        BuildCosmeticGrid(_secondaryColorGrid, _secondaryColorButtons, CosmeticKind.Color, CosmeticSlot.SecondaryColor);
        BuildCosmeticGrid(_patternGrid, _patternButtons, CosmeticKind.Pattern, CosmeticSlot.Pattern);
        BuildCosmeticGrid(_trailGrid, _trailButtons, CosmeticKind.Trail, CosmeticSlot.Trail);
        _populatingCustomize = false;
        RefreshCosmeticSwatches();
    }

    private void BuildCosmeticGrid(
        GridContainer grid,
        List<CosmeticSwatchButton> buttons,
        CosmeticKind kind,
        CosmeticSlot slot)
    {
        foreach (Node child in grid.GetChildren())
        {
            grid.RemoveChild(child);
            child.QueueFree();
        }

        buttons.Clear();
        foreach (CosmeticDefinition definition in CosmeticCatalog.OfKind(kind))
        {
            bool locked = !_profile.UnlockedCosmeticIds.Contains(definition.Id);
            string requirement = UnlockRequirementFor(definition.Id);
            CosmeticSwatchButton button = new()
            {
                Name = $"{slot}_{definition.Id}",
            };
            string cosmeticId = definition.Id;
            button.Pressed += () => OnCosmeticSwatchPressed(slot, cosmeticId);
            button.Configure(
                definition,
                locked,
                string.Equals(SelectedCosmeticId(slot), definition.Id, StringComparison.Ordinal),
                requirement,
                ResolveCosmeticColor(_selectedPrimaryColorId),
                ResolveCosmeticColor(_selectedSecondaryColorId));
            grid.AddChild(button);
            buttons.Add(button);
        }
    }

    private void OnCosmeticSwatchPressed(CosmeticSlot slot, string cosmeticId)
    {
        CosmeticSwatchButton? button = ButtonsFor(slot).FirstOrDefault(candidate =>
            string.Equals(candidate.Definition.Id, cosmeticId, StringComparison.Ordinal));
        if (button is null)
        {
            return;
        }

        if (button.IsLocked)
        {
            AdvancementDefinition? advancement = AdvancementCatalog.All.FirstOrDefault(candidate =>
                string.Equals(candidate.RewardCosmeticId, cosmeticId, StringComparison.Ordinal));
            string title = advancement?.DisplayName.ToUpperInvariant() ?? "LOCKED REWARD";
            SetCustomizeStatus($"LOCKED · {title}\n{button.UnlockRequirement}", locked: true);
            return;
        }

        SelectCosmetic(slot, cosmeticId);
    }

    private bool SelectCosmetic(CosmeticSlot slot, string cosmeticId)
    {
        CosmeticSwatchButton? button = ButtonsFor(slot).FirstOrDefault(candidate =>
            string.Equals(candidate.Definition.Id, cosmeticId, StringComparison.Ordinal));
        if (button is null || button.IsLocked)
        {
            return false;
        }

        switch (slot)
        {
            case CosmeticSlot.PrimaryColor:
                _selectedPrimaryColorId = cosmeticId;
                break;
            case CosmeticSlot.SecondaryColor:
                _selectedSecondaryColorId = cosmeticId;
                break;
            case CosmeticSlot.Pattern:
                _selectedPatternId = cosmeticId;
                break;
            case CosmeticSlot.Trail:
                _selectedTrailId = cosmeticId;
                break;
        }

        ApplyCustomizationSelectionsToProfile();
        RefreshCosmeticSwatches();
        _candyPreview.Apply(_profile);
        SetCustomizeStatus("UNSAVED CHANGES", locked: false);
        return true;
    }

    private void RefreshCosmeticSwatches()
    {
        Color primary = ResolveCosmeticColor(_selectedPrimaryColorId);
        Color secondary = ResolveCosmeticColor(_selectedSecondaryColorId);
        foreach ((List<CosmeticSwatchButton> buttons, CosmeticSlot slot) in new[]
        {
            (_primaryColorButtons, CosmeticSlot.PrimaryColor),
            (_secondaryColorButtons, CosmeticSlot.SecondaryColor),
            (_patternButtons, CosmeticSlot.Pattern),
            (_trailButtons, CosmeticSlot.Trail),
        })
        {
            string selectedId = SelectedCosmeticId(slot);
            foreach (CosmeticSwatchButton button in buttons)
            {
                button.SetVisualState(
                    string.Equals(button.Definition.Id, selectedId, StringComparison.Ordinal),
                    primary,
                    secondary);
            }
        }
    }

    private void SaveCustomization()
    {
        ApplyCustomizationSelectionsToProfile();
        if (!ProfileStore.Save(_profile, out string? error, _profilePath))
        {
            SetCustomizeStatus($"SAVE FAILED: {error}", locked: true);
            return;
        }

        _candyPreview.Apply(_profile);
        ApplyProfileToPlayers();
        SetCustomizeStatus("LOOK SAVED", locked: false);
        if (!string.Equals(_profile.PatternId, "none", StringComparison.Ordinal) &&
            !_profile.UnlockedAdvancementIds.Contains("clean-wrapper"))
        {
            UnlockAndNotify(new[] { "clean-wrapper" });
            PopulateCustomizationOptions();
        }
    }

    private void ApplyCustomizationSelectionsToProfile()
    {
        _profile.PrimaryColorId = _selectedPrimaryColorId;
        _profile.SecondaryColorId = _selectedSecondaryColorId;
        _profile.PatternId = _selectedPatternId;
        _profile.TrailId = _selectedTrailId;
        ProfileStore.Normalize(_profile);
        _selectedPrimaryColorId = _profile.PrimaryColorId;
        _selectedSecondaryColorId = _profile.SecondaryColorId;
        _selectedPatternId = _profile.PatternId;
        _selectedTrailId = _profile.TrailId;
    }

    private string SelectedCosmeticId(CosmeticSlot slot)
    {
        return slot switch
        {
            CosmeticSlot.PrimaryColor => _selectedPrimaryColorId,
            CosmeticSlot.SecondaryColor => _selectedSecondaryColorId,
            CosmeticSlot.Pattern => _selectedPatternId,
            CosmeticSlot.Trail => _selectedTrailId,
            _ => string.Empty,
        };
    }

    private List<CosmeticSwatchButton> ButtonsFor(CosmeticSlot slot)
    {
        return slot switch
        {
            CosmeticSlot.PrimaryColor => _primaryColorButtons,
            CosmeticSlot.SecondaryColor => _secondaryColorButtons,
            CosmeticSlot.Pattern => _patternButtons,
            CosmeticSlot.Trail => _trailButtons,
            _ => throw new ArgumentOutOfRangeException(nameof(slot)),
        };
    }

    private static string UnlockRequirementFor(string cosmeticId)
    {
        return AdvancementCatalog.All.FirstOrDefault(advancement =>
            string.Equals(advancement.RewardCosmeticId, cosmeticId, StringComparison.Ordinal))?.Description
            ?? "This reward is not available yet.";
    }

    private static Color ResolveCosmeticColor(string cosmeticId)
    {
        CosmeticDefinition definition = CosmeticCatalog.Find(CosmeticKind.Color, cosmeticId)
            ?? CosmeticCatalog.Find(CosmeticKind.Color, "cherry")!;
        return new Color(definition.PreviewValue);
    }

    private void SetCustomizeStatus(string text, bool locked)
    {
        _customizeStatus.Text = text;
        _customizeStatus.AddThemeColorOverride(
            "font_color",
            locked ? new Color("e5b05e") : new Color("7ac2ad"));
    }

    private void ApplyProfileToPlayers()
    {
        foreach (Node node in GetTree().GetNodesInGroup(PlayerBall.PlayerGroup))
        {
            if (node is PlayerBall player)
            {
                player.ApplyProfile(_profile, _settings.TrailEnabled);
            }
        }
    }

    private void PollAdvancementTelemetry()
    {
        if (_currentPlayer is null || IsAutomatedSmokeRun())
        {
            return;
        }

        List<string> candidates = new(3);
        if (_currentPlayer.MaximumSpeedSinceReset >= 25.0f)
        {
            candidates.Add("speeding-sweet");
        }

        if (_currentPlayer.MaximumSpeedSinceReset >= 40.0f)
        {
            candidates.Add("terminal-sugar");
        }

        if (_currentPlayer.ConsecutiveElasticBounceCount >= 2)
        {
            candidates.Add("double-bounce");
        }

        UnlockAndNotify(candidates);
    }

    private void EvaluateRoomCompletionAdvancements()
    {
        if (_currentRoom is null || _currentPlayer is null || IsAutomatedSmokeRun())
        {
            return;
        }

        List<string> candidates = new(_currentRoom.CompletedAdvancementIds);
        candidates.AddRange(AdvancementService.RoomCompletionMilestones(_currentRoom.RoomNumber));

        if (_currentRoom.RoomNumber is 6 or 11 or 26 && !_currentPlayer.TouchedSideBoundarySinceReset)
        {
            candidates.Add(_currentRoom.RoomNumber switch
            {
                6 => "straight-as-glass",
                11 => "feather-touch",
                _ => "vacuum-packed",
            });
        }

        UnlockAndNotify(candidates);
    }

    private void UnlockAndNotify(IEnumerable<string> advancementIds, bool profileAlreadyChanged = false)
    {
        List<AdvancementDefinition> unlocked = new();
        foreach (string id in advancementIds.Distinct(StringComparer.Ordinal))
        {
            if (AdvancementService.TryUnlock(_profile, id, out AdvancementDefinition? advancement, out _))
            {
                unlocked.Add(advancement!);
            }
        }

        if (unlocked.Count == 0 && !profileAlreadyChanged)
        {
            return;
        }

        if (!ProfileStore.Save(_profile, out string? error, _profilePath))
        {
            GD.PushError($"Advancement profile save failed: {error}");
            _profile = ProfileStore.Load(out _, _profilePath);
            return;
        }

        _advancementNotifications.ReducedMotion = _settings.ReducedMotion;
        foreach (AdvancementDefinition advancement in unlocked)
        {
            if (_currentRoom is not null && _roomCompletionHandled)
            {
                _deferredRoomStartNotifications.Add(advancement);
            }
            else
            {
                _advancementNotifications.Enqueue(advancement);
            }
        }
    }

    private void FlushDeferredRoomStartNotifications(int roomNumber)
    {
        if (_deferredRoomStartNotifications.Count == 0)
        {
            return;
        }

        _advancementNotifications.ReducedMotion = _settings.ReducedMotion;
        foreach (AdvancementDefinition advancement in _deferredRoomStartNotifications)
        {
            _advancementNotifications.Enqueue(advancement);
        }

        _deferredRoomStartNotifications.Clear();
    }

    private bool IsAutomatedSmokeRun() => _runUiSmoke || _runSaveSmoke || _runCampaignFlowSmoke;

    private void OpenAdvancements(MenuOrigin origin)
    {
        _advancementsOrigin = origin;
        _profile = ProfileStore.Load(out string? warning, _profilePath);
        if (!string.IsNullOrWhiteSpace(warning))
        {
            GD.PushWarning(warning);
        }

        PopulateAdvancements();
        _mainMenu.Hide();
        _playMenu.Hide();
        _pauseMenu.Hide();
        _settingsMenu.Hide();
        _browserMenu.Hide();
        _customizeMenu.Hide();
        _advancementsMenu.Show();
        Godot.Input.MouseMode = Godot.Input.MouseModeEnum.Visible;
    }

    private void CloseAdvancements()
    {
        _advancementsMenu.Hide();
        if (_advancementsOrigin == MenuOrigin.Pause)
        {
            _pauseMenu.Show();
        }
        else
        {
            _mainMenu.Show();
        }
    }

    private void PopulateAdvancements()
    {
        foreach (Node child in _advancementsList.GetChildren())
        {
            _advancementsList.RemoveChild(child);
            child.QueueFree();
        }

        int completedCount = AdvancementCatalog.All.Count(definition =>
            _profile.UnlockedAdvancementIds.Contains(definition.Id));
        _advancementsProgress.Text = $"{completedCount} / {AdvancementCatalog.All.Count} COMPLETE";

        foreach (AdvancementDefinition definition in AdvancementCatalog.All)
        {
            AddAdvancementRow(definition, _profile.UnlockedAdvancementIds.Contains(definition.Id));
        }
    }

    private void AddAdvancementRow(AdvancementDefinition definition, bool completed)
    {
        CosmeticDefinition? reward = CosmeticCatalog.FindById(definition.RewardCosmeticId);
        PanelContainer row = new()
        {
            CustomMinimumSize = new Vector2(0.0f, 96.0f),
        };
        StyleBoxFlat rowStyle = new()
        {
            BgColor = completed ? new Color("172a2a") : new Color("111820"),
            BorderColor = completed ? new Color("61b7a5") : new Color("35414a"),
        };
        rowStyle.SetBorderWidthAll(1);
        rowStyle.SetCornerRadiusAll(5);
        rowStyle.ContentMarginLeft = 16.0f;
        rowStyle.ContentMarginTop = 12.0f;
        rowStyle.ContentMarginRight = 16.0f;
        rowStyle.ContentMarginBottom = 12.0f;
        row.AddThemeStyleboxOverride("panel", rowStyle);

        HBoxContainer layout = new();
        layout.AddThemeConstantOverride("separation", 18);
        Label status = new()
        {
            Text = completed ? "COMPLETE" : "LOCKED",
            CustomMinimumSize = new Vector2(120.0f, 0.0f),
            VerticalAlignment = VerticalAlignment.Center,
        };
        status.AddThemeColorOverride("font_color", completed ? new Color("78d4bd") : new Color("7f8b93"));
        status.AddThemeFontSizeOverride("font_size", 15);

        VBoxContainer details = new()
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        Label title = new()
        {
            Text = definition.DisplayName,
        };
        title.AddThemeColorOverride("font_color", new Color("f3dfbd"));
        title.AddThemeFontSizeOverride("font_size", 20);
        Label description = new()
        {
            Text = definition.Description,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        HBoxContainer rewardLine = new()
        {
            LayoutDirection = Control.LayoutDirectionEnum.Ltr,
        };
        rewardLine.AddThemeConstantOverride("separation", 10);
        Label rewardLabel = new()
        {
            Text = "REWARD  /",
            VerticalAlignment = VerticalAlignment.Center,
        };
        rewardLabel.AddThemeColorOverride("font_color", new Color("b88956"));
        rewardLabel.AddThemeFontSizeOverride("font_size", 14);
        rewardLine.AddChild(rewardLabel);
        if (reward is not null)
        {
            CosmeticSwatchButton rewardIcon = new()
            {
                Name = "RewardIcon",
                MouseFilter = Control.MouseFilterEnum.Ignore,
            };
            rewardIcon.Configure(
                reward,
                locked: false,
                selected: false,
                unlockRequirement: string.Empty,
                primaryColor: ResolveCosmeticColor(_profile.PrimaryColorId),
                secondaryColor: ResolveCosmeticColor(_profile.SecondaryColorId));
            rewardIcon.FocusMode = Control.FocusModeEnum.None;
            rewardLine.AddChild(rewardIcon);
        }

        details.AddChild(title);
        details.AddChild(description);
        details.AddChild(rewardLine);
        layout.AddChild(status);
        layout.AddChild(details);
        row.AddChild(layout);
        _advancementsList.AddChild(row);
    }

    private void ShowUnavailable(string title, string message)
    {
        _unavailableDialog.Title = title;
        _unavailableDialog.DialogText = message;
        _unavailableDialog.PopupCentered(new Vector2I(520, 180));
    }

    private void ResetSettingsControls()
    {
        _settings = new GameSettingsData();
        ApplyInputBindings();
        SyncControlsFromSettings();
    }

    private void ReadApplyAndSaveSettings()
    {
        _settings.FpsLimit = _fpsOption.GetSelectedId();
        (_settings.ResolutionWidth, _settings.ResolutionHeight) = _resolutionOption.GetSelectedId() switch
        {
            1 => (1600, 900),
            2 => (1920, 1080),
            _ => (1280, 720),
        };
        _settings.Fullscreen = _windowOption.GetSelectedId() == 1;
        _settings.GraphicsPreset = _presetOption.GetSelectedId();
        _settings.RenderScale = (float)_renderScaleSlider.Value;
        _settings.MsaaLevel = _msaaOption.GetSelectedId();
        _settings.VSyncEnabled = _vsyncCheck.ButtonPressed;
        _settings.ShadowsEnabled = _shadowsCheck.ButtonPressed;
        _settings.MouseSensitivity = (float)_sensitivitySlider.Value;
        _settings.InvertY = _invertYCheck.ButtonPressed;
        _settings.DefaultFirstPerson = _defaultCameraOption.GetSelectedId() == 1;
        _settings.CameraShakeAmount = (float)_cameraShakeSlider.Value;
        _settings.InteractionPrompts = _interactionPromptsCheck.ButtonPressed;
        _settings.MasterVolume = (float)_masterSlider.Value;
        _settings.MusicVolume = (float)_musicSlider.Value;
        _settings.SfxVolume = (float)_sfxSlider.Value;
        _settings.VoiceVolume = (float)_voiceSlider.Value;
        _settings.SubtitlesEnabled = _subtitlesCheck.ButtonPressed;
        _settings.SubtitleScalePercent = _subtitleScaleOption.GetSelectedId();
        _settings.SubtitleBackground = _subtitleBackgroundCheck.ButtonPressed;
        _settings.ReducedMotion = _reducedMotionCheck.ButtonPressed;
        _settings.DisableFlashes = _disableFlashesCheck.ButtonPressed;
        _settings.HighContrastPrompts = _highContrastCheck.ButtonPressed;
        _settings.TrailEnabled = _trailCheck.ButtonPressed;
        CaptureBindingsFromInputMap();
        ApplySettings(save: true, applyDefaultCamera: true);
    }

    private void SyncControlsFromSettings()
    {
        SelectById(_fpsOption, _settings.FpsLimit);
        SelectById(_resolutionOption, ResolutionId(_settings.ResolutionWidth, _settings.ResolutionHeight));
        SelectById(_windowOption, _settings.Fullscreen ? 1 : 0);
        SelectById(_presetOption, _settings.GraphicsPreset);
        _renderScaleSlider.Value = _settings.RenderScale;
        SelectById(_msaaOption, _settings.MsaaLevel);
        _vsyncCheck.ButtonPressed = _settings.VSyncEnabled;
        _shadowsCheck.ButtonPressed = _settings.ShadowsEnabled;
        _sensitivitySlider.Value = _settings.MouseSensitivity;
        _invertYCheck.ButtonPressed = _settings.InvertY;
        SelectById(_defaultCameraOption, _settings.DefaultFirstPerson ? 1 : 0);
        _cameraShakeSlider.Value = _settings.CameraShakeAmount;
        _interactionPromptsCheck.ButtonPressed = _settings.InteractionPrompts;
        _masterSlider.Value = _settings.MasterVolume;
        _musicSlider.Value = _settings.MusicVolume;
        _sfxSlider.Value = _settings.SfxVolume;
        _voiceSlider.Value = _settings.VoiceVolume;
        _subtitlesCheck.ButtonPressed = _settings.SubtitlesEnabled;
        SelectById(_subtitleScaleOption, _settings.SubtitleScalePercent);
        _subtitleBackgroundCheck.ButtonPressed = _settings.SubtitleBackground;
        _reducedMotionCheck.ButtonPressed = _settings.ReducedMotion;
        _disableFlashesCheck.ButtonPressed = _settings.DisableFlashes;
        _highContrastCheck.ButtonPressed = _settings.HighContrastPrompts;
        _trailCheck.ButtonPressed = _settings.TrailEnabled;
        foreach (CheckButton toggle in SettingsCheckButtons())
        {
            RefreshCheckButtonLabel(toggle);
        }
        RefreshBindingLabels();
    }

    private IEnumerable<CheckButton> SettingsCheckButtons()
    {
        yield return _vsyncCheck;
        yield return _shadowsCheck;
        yield return _invertYCheck;
        yield return _interactionPromptsCheck;
        yield return _subtitlesCheck;
        yield return _subtitleBackgroundCheck;
        yield return _reducedMotionCheck;
        yield return _disableFlashesCheck;
        yield return _highContrastCheck;
        yield return _trailCheck;
    }

    private static void RefreshCheckButtonLabel(CheckButton toggle)
    {
        toggle.Text = toggle.ButtonPressed ? "Enabled" : "Disabled";
    }

    private void ApplySettings(bool save, bool applyDefaultCamera)
    {
        Engine.MaxFps = _settings.FpsLimit;
        GetTree().Root.Scaling3DScale = Mathf.Clamp(_settings.RenderScale, 0.5f, 1.0f);
        GetTree().Root.Msaa3D = _settings.MsaaLevel switch
        {
            4 => Viewport.Msaa.Msaa4X,
            2 => Viewport.Msaa.Msaa2X,
            _ => Viewport.Msaa.Disabled,
        };
        ApplyBusVolume("Master", _settings.MasterVolume);
        ApplyBusVolume("Music", _settings.MusicVolume);
        ApplyBusVolume("SFX", _settings.SfxVolume);
        ApplyBusVolume("Voice", _settings.VoiceVolume);
        ApplyInputBindings();
        _menuBackground.MotionEnabled = !_settings.ReducedMotion;
        _candyPreview.MotionEnabled = !_settings.ReducedMotion;
        ApplyProfileToPlayers();

        if (DisplayServer.GetName() != "headless")
        {
            DisplayServer.WindowSetVsyncMode(_settings.VSyncEnabled
                ? DisplayServer.VSyncMode.Enabled
                : DisplayServer.VSyncMode.Disabled);
            DisplayServer.WindowSetMode(_settings.Fullscreen
                ? DisplayServer.WindowMode.Fullscreen
                : DisplayServer.WindowMode.Windowed);
            if (!_settings.Fullscreen)
            {
                DisplayServer.WindowSetSize(new Vector2I(_settings.ResolutionWidth, _settings.ResolutionHeight));
            }
        }

        ApplyShadowSettings();
        ApplyCameraSettings(
            inputEnabled: _gameplayInstance is not null && !GetTree().Paused,
            applyDefaultMode: applyDefaultCamera);
        if (save && !_runUiSmoke)
        {
            Error saveError = SettingsStore.Save(_settings);
            if (saveError != Error.Ok)
            {
                GD.PushError($"Could not save settings: {saveError}");
            }
        }
    }

    private void ApplyCameraSettings(bool inputEnabled, bool applyDefaultMode)
    {
        if (_gameplayInstance is null)
        {
            Godot.Input.MouseMode = Godot.Input.MouseModeEnum.Visible;
            return;
        }

        foreach (Node node in GetTree().GetNodesInGroup(PlayerCameraRig.CameraRigGroup))
        {
            if (node is not PlayerCameraRig cameraRig)
            {
                continue;
            }

            cameraRig.MouseSensitivity = _settings.MouseSensitivity;
            cameraRig.InvertY = _settings.InvertY;
            if (applyDefaultMode)
            {
                cameraRig.SetFirstPerson(_settings.DefaultFirstPerson);
            }

            cameraRig.SetInputEnabled(inputEnabled);
        }
    }

    private void ApplyShadowSettings()
    {
        foreach (Node node in GetTree().GetNodesInGroup(ShadowLightGroup))
        {
            if (node is DirectionalLight3D light)
            {
                light.ShadowEnabled = _settings.ShadowsEnabled;
            }
        }
    }

    private void ApplyInputBindings()
    {
        InputDefaults.ApplyPrimaryBindings(
            (Key)_settings.MoveForwardKey,
            (Key)_settings.MoveBackKey,
            (Key)_settings.MoveLeftKey,
            (Key)_settings.MoveRightKey,
            (Key)_settings.ToggleCameraKey,
            (Key)_settings.InteractKey);
    }

    private void CaptureBindingsFromInputMap()
    {
        _settings.MoveForwardKey = (long)InputDefaults.GetPrimaryKey(InputDefaults.MoveForward);
        _settings.MoveBackKey = (long)InputDefaults.GetPrimaryKey(InputDefaults.MoveBack);
        _settings.MoveLeftKey = (long)InputDefaults.GetPrimaryKey(InputDefaults.MoveLeft);
        _settings.MoveRightKey = (long)InputDefaults.GetPrimaryKey(InputDefaults.MoveRight);
        _settings.ToggleCameraKey = (long)InputDefaults.GetPrimaryKey(InputDefaults.ToggleCamera);
        _settings.InteractKey = (long)InputDefaults.GetPrimaryKey(InputDefaults.Interact);
    }

    private void BeginRebind(StringName action)
    {
        CancelRebind();
        _waitingForKey = true;
        _bindingAction = action;
        _bindingButtons[action].Text = "PRESS A KEY...";
    }

    private void FinishRebind(Key key)
    {
        if (key is Key.Up or Key.Down or Key.Left or Key.Right)
        {
            CancelRebind();
            ShowUnavailable("KEY RESERVED", "Arrow keys remain reserved as secondary movement controls.");
            return;
        }

        foreach ((StringName action, _) in _bindingButtons)
        {
            if (action != _bindingAction && InputDefaults.GetPrimaryKey(action) == key)
            {
                CancelRebind();
                ShowUnavailable("KEY ALREADY IN USE", $"{key.ToString().ToUpperInvariant()} is already assigned to another action.");
                return;
            }
        }

        InputDefaults.RebindPrimary(_bindingAction, key);
        _waitingForKey = false;
        CaptureBindingsFromInputMap();
        RefreshBindingLabels();
    }

    private void CancelRebind()
    {
        if (!_waitingForKey)
        {
            return;
        }

        _waitingForKey = false;
        RefreshBindingLabels();
    }

    private void RefreshBindingLabels()
    {
        foreach ((StringName action, Button button) in _bindingButtons)
        {
            button.Text = InputDefaults.GetPrimaryKey(action).ToString().ToUpperInvariant();
        }
    }

    private void ApplyPresetToControls(int preset)
    {
        switch (preset)
        {
            case 0:
                _renderScaleSlider.Value = 0.65f;
                SelectById(_msaaOption, 0);
                _shadowsCheck.ButtonPressed = false;
                break;
            case 1:
                _renderScaleSlider.Value = 0.8f;
                SelectById(_msaaOption, 0);
                _shadowsCheck.ButtonPressed = false;
                break;
            case 3:
                _renderScaleSlider.Value = 1.0f;
                SelectById(_msaaOption, 4);
                _shadowsCheck.ButtonPressed = true;
                break;
            default:
                _renderScaleSlider.Value = 1.0f;
                SelectById(_msaaOption, 2);
                _shadowsCheck.ButtonPressed = true;
                break;
        }
    }

    private async Task SaveRoomStartAfterFrameAsync(RoomRuntime room)
    {
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        if (!GodotObject.IsInstanceValid(room) || _currentRoom != room || _roomCompletionHandled)
        {
            return;
        }

    }

    private CampaignSnapshot? SaveCurrentSnapshot(SnapshotKind kind)
    {
        if (_currentRoom is null)
        {
            return null;
        }

        CampaignSnapshot snapshot = new()
        {
            RoomId = _currentRoom.RoomId,
            RoomName = _currentRoom.RoomDisplayName,
            RoomNumber = _currentRoom.RoomNumber,
            Kind = kind,
            SavedAtUtc = DateTimeOffset.UtcNow,
            CampaignElapsedSeconds = _campaignElapsedSeconds,
        };
        Image? thumbnail = DisplayServer.GetName() == "headless"
            ? null
            : GetViewport().GetTexture().GetImage();
        if (!CampaignSaveService.Save(snapshot, thumbnail, out string? error, _campaignRoot))
        {
            GD.PushError($"Campaign snapshot failed: {error}");
            return null;
        }

        return snapshot;
    }

    private void OnRoomCompleted()
    {
        if (_currentRoom is null || _roomCompletionHandled)
        {
            return;
        }

        _roomCompletionHandled = true;
        CampaignSaveService.MarkRoomCompleted(_currentRoom.RoomNumber, _campaignRoot);
        if (!IsAutomatedSmokeRun())
        {
            MuteSfxForRoomTransfer();
        }
        EvaluateRoomCompletionAdvancements();
        _pendingCompletionSnapshot = new CampaignSnapshot { RoomId = _currentRoom.RoomId, RoomName = _currentRoom.RoomDisplayName, RoomNumber = _currentRoom.RoomNumber, Kind = SnapshotKind.RoomComplete, CampaignElapsedSeconds = _campaignElapsedSeconds };
        GetTree().Paused = true;
        ApplyCameraSettings(inputEnabled: false, applyDefaultMode: false);
        SetRoomCompleteDialog(_currentRoom.RoomNumber, _currentRoom.RoomDisplayName, snapshotSaved: true);
        if (IsAutomatedSmokeRun())
        {
            _roomCompleteDialog.PopupCentered(new Vector2I(560, 230));
        }
        else
        {
            StartRoomCompletionTransition(_currentRoom.RoomNumber);
        }
    }

    private void SetRoomCompleteDialog(int roomNumber, string roomName, bool snapshotSaved)
    {
        RoomCatalogEntry? room = RoomCatalog.Find(roomNumber);
        string speaker = room?.PostRoomSpeaker ?? "MACHINE";
        string dialogue = room?.PostRoomDialogue ?? "The mechanism turns again.";
        string saveLine = snapshotSaved ? "Snapshot saved. " : string.Empty;
        _roomCompleteDialog.Title = $"ROOM {roomNumber:00} COMPLETE  /  {speaker}";
        string continueLine = roomNumber >= CampaignSaveService.MaximumRoomCount
            ? "Continue to the delivery chute."
            : "Continue to the next chamber.";
        _roomCompleteDialog.DialogText = $"{roomName}\n\n\"{dialogue}\"\n\n{saveLine}{continueLine}";
    }

    private async void StartRoomCompletionTransition(int roomNumber)
    {
        if (_roomTransferRunning)
        {
            return;
        }

        _roomTransferRunning = true;
        int generation = ++_roomTransferGeneration;
        RoomCatalogEntry? room = RoomCatalog.Find(roomNumber);
        string speaker = room?.PostRoomSpeaker ?? "MACHINE";
        string dialogue = room?.PostRoomDialogue ?? "The mechanism turns again.";

        _roomCompleteDialog.Hide();
        _mainMenu.Hide();
        _playMenu.Hide();
        _pauseMenu.Hide();
        _settingsMenu.Hide();
        _browserMenu.Hide();
        _customizeMenu.Hide();
        _advancementsMenu.Hide();
        _loadingOverlay.Hide();
        _roomIntroCard.Hide();
        _menuBackground.SetActive(false);
        GetTree().Paused = true;
        ApplyCameraSettings(inputEnabled: false, applyDefaultMode: false);
        Godot.Input.MouseMode = Godot.Input.MouseModeEnum.Visible;
        MuteSfxForRoomTransfer();

        ShowRoomTransferPresentation(speaker, dialogue);

        _roomDialogueVoice.Stop();
        _roomDialogueVoice.Stream = GD.Load<AudioStream>($"res://assets/audio/voice/room{roomNumber:00}.mp3");
        _roomDialogueVoice.Play();
        _roomTransferSfx.Stop();

        if (!_settings.ReducedMotion)
        {
            _roomTransferTube.Modulate = new Color(1.0f, 1.0f, 1.0f, 0.0f);
            _roomTransferSubtitlePanel.Modulate = new Color(1.0f, 1.0f, 1.0f, 0.0f);
            Tween entrance = CreateTween().SetPauseMode(Tween.TweenPauseMode.Process).SetParallel();
            entrance.TweenProperty(_roomTransferTube, "modulate:a", 1.0f, 0.24f)
                .SetTrans(Tween.TransitionType.Cubic)
                .SetEase(Tween.EaseType.Out);
            entrance.TweenProperty(_roomTransferSubtitlePanel, "modulate:a", 1.0f, 0.24f)
                .SetTrans(Tween.TransitionType.Cubic)
                .SetEase(Tween.EaseType.Out);
            entrance.TweenProperty(_roomTransferTube, "scale", new Vector2(1.0f, 1.0f), 0.38f)
                .From(new Vector2(0.93f, 0.93f))
                .SetTrans(Tween.TransitionType.Quint)
                .SetEase(Tween.EaseType.Out);
            await ToSignal(entrance, Tween.SignalName.Finished);
        }

        double voiceLength = _roomDialogueVoice.Stream?.GetLength() ?? 0.0;
        double holdSeconds = Math.Clamp(voiceLength + 0.55, 2.8, 4.8);
        await ToSignal(
            GetTree().CreateTimer(holdSeconds, processAlways: true),
            SceneTreeTimer.SignalName.Timeout);
        if (generation != _roomTransferGeneration || !_roomTransferRunning)
        {
            return;
        }

        _roomTransferSfx.Stop();
        _roomDialogueVoice.Stop();
        _roomTransferRunning = false;
        ContinueAfterRoomCompletion();
    }

    private static StyleBoxFlat CreateRoomTransferSubtitleStyle(bool withBackground)
    {
        Color border = withBackground ? new Color("638e91") : Colors.Transparent;
        Color background = withBackground
            ? new Color(0.02f, 0.04f, 0.05f, 0.88f)
            : Colors.Transparent;
        return new StyleBoxFlat
        {
            BgColor = background,
            BorderColor = border,
            BorderWidthLeft = withBackground ? 2 : 0,
            BorderWidthTop = withBackground ? 2 : 0,
            BorderWidthRight = withBackground ? 2 : 0,
            BorderWidthBottom = withBackground ? 2 : 0,
            CornerRadiusTopLeft = 10,
            CornerRadiusTopRight = 10,
            CornerRadiusBottomLeft = 10,
            CornerRadiusBottomRight = 10,
        };
    }

    private void CancelRoomCompletionTransition()
    {
        _roomTransferGeneration++;
        _roomTransferRunning = false;
        if (_roomTransferOverlay is not null)
        {
            HideRoomTransferPresentation();
        }
        RestoreSfxAfterRoomTransfer();
    }

    private void ContinueAfterRoomCompletion()
    {
        CampaignSnapshot? completed = _pendingCompletionSnapshot;
        _pendingCompletionSnapshot = null;
        _roomCompleteDialog.Hide();
        GetTree().Paused = false;
        if (completed is null)
        {
            CancelRoomCompletionTransition();
            ShowMainMenu();
            return;
        }

        if (completed.RoomNumber >= CampaignSaveService.MaximumRoomCount)
        {
            CancelRoomCompletionTransition();
            StartEndingSequence();
            return;
        }

        int nextRoomNumber = completed.RoomNumber + 1;
        if (RoomCatalog.Find(nextRoomNumber) is not null)
        {
            StartRoom(nextRoomNumber, saveRoomStart: true, completed.CampaignElapsedSeconds);
            return;
        }

        CancelRoomCompletionTransition();
        ShowMainMenu();
        ShowUnavailable(
            "AVAILABLE ROOMS COMPLETE",
            $"Room {completed.RoomNumber:00} is complete. The next campaign room will be added in its content stage.");
    }

    private void ClearBrowserRows()
    {
        foreach (Node child in _browserList.GetChildren())
        {
            _browserList.RemoveChild(child);
            child.Free();
        }
    }

    private void AddSnapshotRow(CampaignSnapshot snapshot)
    {
        PanelContainer row = new()
        {
            CustomMinimumSize = new Vector2(0.0f, 112.0f),
        };
        HBoxContainer layout = new();
        layout.AddThemeConstantOverride("separation", 16);
        TextureRect thumbnail = new()
        {
            CustomMinimumSize = new Vector2(160.0f, 90.0f),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            Texture = LoadSnapshotTexture(snapshot),
        };
        VBoxContainer details = new()
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        Label title = new()
        {
            Text = $"ROOM {snapshot.RoomNumber:00} - {snapshot.RoomName}",
        };
        title.AddThemeFontSizeOverride("font_size", 20);
        TimeSpan elapsed = TimeSpan.FromSeconds(snapshot.CampaignElapsedSeconds);
        Label metadata = new()
        {
            Text = $"{snapshot.SavedAtUtc.ToLocalTime():yyyy-MM-dd HH:mm}\nPLAY TIME {elapsed:hh\\:mm\\:ss}",
        };
        metadata.AddThemeColorOverride("font_color", new Color("8f9ba3"));
        Button load = new()
        {
            Text = "LOAD",
            CustomMinimumSize = new Vector2(120.0f, 48.0f),
            SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
        };
        CampaignSnapshot capturedSnapshot = snapshot;
        load.Pressed += () => LoadSnapshot(capturedSnapshot);

        details.AddChild(title);
        details.AddChild(metadata);
        layout.AddChild(thumbnail);
        layout.AddChild(details);
        layout.AddChild(load);
        row.AddChild(layout);
        _browserList.AddChild(row);
    }

    private void AddRoomSelectRow(RoomCatalogEntry room)
    {
        Button button = new()
        {
            Text = $"ROOM {room.Number:00}   {room.DisplayName.ToUpperInvariant()}\n{room.MechanicLabel}",
            CustomMinimumSize = new Vector2(0.0f, 74.0f),
            Alignment = HorizontalAlignment.Left,
        };
        int roomNumber = room.Number;
        button.Pressed += () => StartRoomFromRoomSelect(roomNumber);
        _browserList.AddChild(button);
    }

    private void StartRoomFromRoomSelect(int roomNumber)
    {
        StartRoomWithCandyHandoff(roomNumber, saveRoomStart: false, elapsedSeconds: _campaignElapsedSeconds);
    }

    private void StartRoomWithCandyHandoff(int roomNumber, bool saveRoomStart, double elapsedSeconds)
    {
        ShowRoomTransferPresentation();
        _roomTransferSfx.Stop();
        _roomTransferSfx.Play();
        StartRoom(roomNumber, saveRoomStart, elapsedSeconds);
    }

    private static Texture2D LoadSnapshotTexture(CampaignSnapshot snapshot)
    {
        Image image = new();
        if (!string.IsNullOrWhiteSpace(snapshot.ThumbnailPath) &&
            File.Exists(snapshot.ThumbnailPath) &&
            image.Load(snapshot.ThumbnailPath) == Error.Ok)
        {
            return ImageTexture.CreateFromImage(image);
        }

        Image placeholder = Image.CreateEmpty(256, 144, false, Image.Format.Rgb8);
        placeholder.Fill(snapshot.Kind == SnapshotKind.RoomStart
            ? new Color("263747")
            : new Color("4b3826"));
        return ImageTexture.CreateFromImage(placeholder);
    }

    private void RunSaveSmokeTest()
    {
        const string testRoot = "user://campaign-smoke";
        if (!CampaignSaveService.DeleteAll(out string? deleteError, testRoot))
        {
            FailSaveSmoke($"Could not clear test saves: {deleteError}");
            return;
        }

        DateTimeOffset baseTime = DateTimeOffset.UtcNow.AddMinutes(-5);
        for (int roomNumber = 1; roomNumber <= CampaignSaveService.MaximumSnapshotCount; roomNumber++)
        {
            CampaignSnapshot snapshot = new()
            {
                RoomId = $"room-{roomNumber:D2}",
                RoomName = $"Test Room {roomNumber:D2}",
                RoomNumber = roomNumber,
                Kind = SnapshotKind.RoomStart,
                SavedAtUtc = baseTime.AddMinutes(roomNumber),
                CampaignElapsedSeconds = roomNumber * 60.0,
            };
            if (!CampaignSaveService.Save(snapshot, null, out string? saveError, testRoot))
            {
                FailSaveSmoke($"Could not save room {roomNumber}: {saveError}");
                return;
            }
        }

        IReadOnlyList<CampaignSnapshot> all = CampaignSaveService.LoadAll(out IReadOnlyList<string> loadErrors, testRoot);
        if (all.Count != CampaignSaveService.MaximumSnapshotCount || loadErrors.Count != 0)
        {
            FailSaveSmoke($"Expected five valid room-start snapshots, found {all.Count} with {loadErrors.Count} errors.");
            return;
        }

        CampaignSnapshot? selected = all.SingleOrDefault(snapshot => snapshot.RoomNumber == 3);
        if (selected is null)
        {
            FailSaveSmoke("Room 03 save was not available for the load test.");
            return;
        }

        if (!CampaignSaveService.DeleteSnapshotsAfter(selected, out string? discardError, testRoot))
        {
            FailSaveSmoke($"Could not discard saves after Room 03: {discardError}");
            return;
        }

        all = CampaignSaveService.LoadAll(out loadErrors, testRoot);
        if (all.Count != 3 || loadErrors.Count != 0 ||
            all.Any(snapshot => snapshot.RoomNumber > selected.RoomNumber) ||
            CampaignSaveService.LoadLatest(testRoot)?.RoomNumber != selected.RoomNumber)
        {
            FailSaveSmoke("Loading Room 03 did not discard every later campaign save.");
            return;
        }

        CampaignSaveService.MarkRoomCompleted(1, testRoot);

        if (!CampaignSaveService.DeleteAll(out deleteError, testRoot) ||
            CampaignSaveService.LoadAll(out _, testRoot).Count != 0 ||
            CampaignSaveService.GetCompletedRoomNumbers(testRoot).Count != 0)
        {
            FailSaveSmoke($"New Game did not remove every campaign save: {deleteError}");
            return;
        }

        GD.Print("SAVE_SMOKE_PASS: loading a save discards later saves and New Game clears all campaign saves.");
        GetTree().Quit(0);
    }

    private void RunCampaignFlowSmokeStep()
    {
        switch (_campaignFlowSmokeStep)
        {
            case 0:
                if (!CampaignSaveService.DeleteAll(out string? clearError, _campaignRoot))
                {
                    FailCampaignFlowSmoke($"Could not clear integration saves: {clearError}");
                    return;
                }

                StartRoom(roomNumber: 1, saveRoomStart: true, elapsedSeconds: 0.0);
                _campaignFlowWaitFrames = 3;
                _campaignFlowSmokeStep = 1;
                return;
            case 1:
                if (_campaignFlowWaitFrames-- > 0)
                {
                    return;
                }

                IReadOnlyList<CampaignSnapshot> startSnapshots = CampaignSaveService.LoadAll(out _, _campaignRoot);
                if (startSnapshots.Count != 1 || startSnapshots[0].Kind != SnapshotKind.RoomStart)
                {
                    FailCampaignFlowSmoke("Starting a room did not create exactly one Room Start snapshot.");
                    return;
                }

                OnRoomCompleted();
                _campaignFlowSmokeStep = 2;
                return;
            case 2:
                IReadOnlyList<CampaignSnapshot> completedSnapshots = CampaignSaveService.LoadAll(out _, _campaignRoot);
                if (completedSnapshots.Count != 2 ||
                    !completedSnapshots.Any(snapshot => snapshot.Kind == SnapshotKind.RoomStart) ||
                    !completedSnapshots.Any(snapshot => snapshot.Kind == SnapshotKind.RoomComplete) ||
                    !CampaignSaveService.GetCompletedRoomNumbers(_campaignRoot).Contains(1))
                {
                    FailCampaignFlowSmoke("Room completion did not produce the paired snapshot or room-select unlock.");
                    return;
                }

                if (!_roomCompleteDialog.Title.Contains("CHILD", StringComparison.Ordinal) ||
                    !_roomCompleteDialog.DialogText.Contains("Is it still coming?", StringComparison.Ordinal))
                {
                    FailCampaignFlowSmoke("Room 01 completion did not show its assigned story dialogue.");
                    return;
                }

                ContinueAfterRoomCompletion();
                _campaignFlowWaitFrames = 5;
                _campaignFlowSmokeStep = 3;
                return;
            case 3:
                if (_campaignFlowWaitFrames-- > 0)
                {
                    return;
                }

                IReadOnlyList<CampaignSnapshot> roomTwoSnapshots = CampaignSaveService.LoadAll(out _, _campaignRoot);
                if (_currentRoom?.RoomNumber != 2 ||
                    _currentRoom.RoomId != "room-02" ||
                    roomTwoSnapshots.Count != 3 ||
                    !roomTwoSnapshots.Any(snapshot => snapshot.RoomNumber == 2 && snapshot.Kind == SnapshotKind.RoomStart))
                {
                    FailCampaignFlowSmoke("Continuing from Room 01 did not load Room 02 and create its Room Start snapshot.");
                    return;
                }

                OnRoomCompleted();
                _campaignFlowSmokeStep = 4;
                return;
            case 4:
                IReadOnlyList<CampaignSnapshot> roomTwoCompleteSnapshots = CampaignSaveService.LoadAll(out _, _campaignRoot);
                if (roomTwoCompleteSnapshots.Count != 4 ||
                    !roomTwoCompleteSnapshots.Any(snapshot => snapshot.RoomNumber == 2 && snapshot.Kind == SnapshotKind.RoomComplete) ||
                    !CampaignSaveService.GetCompletedRoomNumbers(_campaignRoot).Contains(2))
                {
                    FailCampaignFlowSmoke("Completing Room 02 did not create its Room Complete snapshot or unlock.");
                    return;
                }

                if (!_roomCompleteDialog.Title.Contains("MOTHER", StringComparison.Ordinal) ||
                    !_roomCompleteDialog.DialogText.Contains("The machine is taking its time.", StringComparison.Ordinal))
                {
                    FailCampaignFlowSmoke("Room 02 completion did not show its assigned story dialogue.");
                    return;
                }

                ContinueAfterRoomCompletion();
                _campaignFlowWaitFrames = 5;
                _campaignFlowSmokeStep = 5;
                return;
            case 5:
                if (_campaignFlowWaitFrames-- > 0)
                {
                    return;
                }

                IReadOnlyList<CampaignSnapshot> roomThreeSnapshots = CampaignSaveService.LoadAll(out _, _campaignRoot);
                if (_currentRoom?.RoomNumber != 3 ||
                    _currentRoom.RoomId != "room-03" ||
                    roomThreeSnapshots.Count != 5 ||
                    !roomThreeSnapshots.Any(snapshot => snapshot.RoomNumber == 3 && snapshot.Kind == SnapshotKind.RoomStart))
                {
                    FailCampaignFlowSmoke("Continuing from Room 02 did not load Room 03 and create its Room Start snapshot.");
                    return;
                }

                OnRoomCompleted();
                _campaignFlowSmokeStep = 6;
                return;
            case 6:
                IReadOnlyList<CampaignSnapshot> roomThreeCompleteSnapshots = CampaignSaveService.LoadAll(out _, _campaignRoot);
                if (roomThreeCompleteSnapshots.Count != 6 ||
                    !roomThreeCompleteSnapshots.Any(snapshot => snapshot.RoomNumber == 3 && snapshot.Kind == SnapshotKind.RoomComplete) ||
                    !CampaignSaveService.GetCompletedRoomNumbers(_campaignRoot).Contains(3))
                {
                    FailCampaignFlowSmoke("Completing Room 03 did not create its Room Complete snapshot or unlock.");
                    return;
                }

                if (!_roomCompleteDialog.Title.Contains("CHILD", StringComparison.Ordinal) ||
                    !_roomCompleteDialog.DialogText.Contains("I heard something fall in there.", StringComparison.Ordinal))
                {
                    FailCampaignFlowSmoke("Room 03 completion did not show its assigned story dialogue.");
                    return;
                }

                ContinueAfterRoomCompletion();
                _campaignFlowWaitFrames = 5;
                _campaignFlowSmokeStep = 7;
                return;
            case 7:
                if (_campaignFlowWaitFrames-- > 0)
                {
                    return;
                }

                IReadOnlyList<CampaignSnapshot> roomFourSnapshots = CampaignSaveService.LoadAll(out _, _campaignRoot);
                if (_currentRoom?.RoomNumber != 4 ||
                    _currentRoom.RoomId != "room-04" ||
                    roomFourSnapshots.Count != 7 ||
                    !roomFourSnapshots.Any(snapshot => snapshot.RoomNumber == 4 && snapshot.Kind == SnapshotKind.RoomStart))
                {
                    FailCampaignFlowSmoke("Continuing from Room 03 did not load Room 04 and create its Room Start snapshot.");
                    return;
                }

                OnRoomCompleted();
                _campaignFlowSmokeStep = 8;
                return;
            case 8:
                IReadOnlyList<CampaignSnapshot> roomFourCompleteSnapshots = CampaignSaveService.LoadAll(out _, _campaignRoot);
                if (roomFourCompleteSnapshots.Count != 8 ||
                    !roomFourCompleteSnapshots.Any(snapshot => snapshot.RoomNumber == 4 && snapshot.Kind == SnapshotKind.RoomComplete) ||
                    !CampaignSaveService.GetCompletedRoomNumbers(_campaignRoot).Contains(4))
                {
                    FailCampaignFlowSmoke("Completing Room 04 did not create its Room Complete snapshot or unlock.");
                    return;
                }

                if (!_roomCompleteDialog.Title.Contains("MOTHER", StringComparison.Ordinal) ||
                    !_roomCompleteDialog.DialogText.Contains("Maybe the mechanism needs another push.", StringComparison.Ordinal))
                {
                    FailCampaignFlowSmoke("Room 04 completion did not show its assigned story dialogue.");
                    return;
                }

                ContinueAfterRoomCompletion();
                _campaignFlowWaitFrames = 5;
                _campaignFlowSmokeStep = 9;
                return;
            case 9:
                if (_campaignFlowWaitFrames-- > 0)
                {
                    return;
                }

                IReadOnlyList<CampaignSnapshot> roomFiveSnapshots = CampaignSaveService.LoadAll(out _, _campaignRoot);
                if (_currentRoom?.RoomNumber != 5 ||
                    _currentRoom.RoomId != "room-05" ||
                    roomFiveSnapshots.Count != 9 ||
                    !roomFiveSnapshots.Any(snapshot => snapshot.RoomNumber == 5 && snapshot.Kind == SnapshotKind.RoomStart))
                {
                    FailCampaignFlowSmoke("Continuing from Room 04 did not load Room 05 and create its Room Start snapshot.");
                    return;
                }

                OnRoomCompleted();
                _campaignFlowSmokeStep = 10;
                return;
            case 10:
                IReadOnlyList<CampaignSnapshot> roomFiveCompleteSnapshots = CampaignSaveService.LoadAll(out _, _campaignRoot);
                if (roomFiveCompleteSnapshots.Count != 10 ||
                    !roomFiveCompleteSnapshots.Any(snapshot => snapshot.RoomNumber == 5 && snapshot.Kind == SnapshotKind.RoomComplete) ||
                    !CampaignSaveService.GetCompletedRoomNumbers(_campaignRoot).Contains(5))
                {
                    FailCampaignFlowSmoke("Completing Room 05 did not create its Room Complete snapshot or unlock.");
                    return;
                }

                if (!_roomCompleteDialog.Title.Contains("CHILD", StringComparison.Ordinal) ||
                    !_roomCompleteDialog.DialogText.Contains("I think it moved!", StringComparison.Ordinal))
                {
                    FailCampaignFlowSmoke("Room 05 completion did not show its assigned story dialogue.");
                    return;
                }

                ContinueAfterRoomCompletion();
                _campaignFlowWaitFrames = 5;
                _campaignFlowSmokeStep = 11;
                return;
            case 11:
                if (_campaignFlowWaitFrames-- > 0)
                {
                    return;
                }

                IReadOnlyList<CampaignSnapshot> roomSixSnapshots = CampaignSaveService.LoadAll(out _, _campaignRoot);
                if (_currentRoom?.RoomNumber != 6 ||
                    _currentRoom.RoomId != "room-06" ||
                    roomSixSnapshots.Count != 11 ||
                    !roomSixSnapshots.Any(snapshot => snapshot.RoomNumber == 6 && snapshot.Kind == SnapshotKind.RoomStart))
                {
                    FailCampaignFlowSmoke("Continuing from Room 05 did not load Room 06 and create its Room Start snapshot.");
                    return;
                }

                OnRoomCompleted();
                _campaignFlowSmokeStep = 12;
                return;
            case 12:
                IReadOnlyList<CampaignSnapshot> roomSixCompleteSnapshots = CampaignSaveService.LoadAll(out _, _campaignRoot);
                if (roomSixCompleteSnapshots.Count != 12 ||
                    !roomSixCompleteSnapshots.Any(snapshot => snapshot.RoomNumber == 6 && snapshot.Kind == SnapshotKind.RoomComplete) ||
                    !CampaignSaveService.GetCompletedRoomNumbers(_campaignRoot).Contains(6))
                {
                    FailCampaignFlowSmoke("Completing Room 06 did not create its Room Complete snapshot or unlock.");
                    return;
                }

                if (!_roomCompleteDialog.Title.Contains("MOTHER", StringComparison.Ordinal) ||
                    !_roomCompleteDialog.DialogText.Contains("Now I can hear it rolling.", StringComparison.Ordinal))
                {
                    FailCampaignFlowSmoke("Room 06 completion did not show its assigned story dialogue.");
                    return;
                }

                ContinueAfterRoomCompletion();
                _campaignFlowWaitFrames = 5;
                _campaignFlowSmokeStep = 13;
                return;
            case 13:
                if (_campaignFlowWaitFrames-- > 0)
                {
                    return;
                }

                IReadOnlyList<CampaignSnapshot> roomSevenSnapshots = CampaignSaveService.LoadAll(out _, _campaignRoot);
                if (_currentRoom?.RoomNumber != 7 ||
                    _currentRoom.RoomId != "room-07" ||
                    roomSevenSnapshots.Count != 13 ||
                    !roomSevenSnapshots.Any(snapshot => snapshot.RoomNumber == 7 && snapshot.Kind == SnapshotKind.RoomStart))
                {
                    FailCampaignFlowSmoke("Continuing from Room 06 did not load Room 07 and create its Room Start snapshot.");
                    return;
                }

                OnRoomCompleted();
                _campaignFlowSmokeStep = 14;
                return;
            case 14:
                IReadOnlyList<CampaignSnapshot> roomSevenCompleteSnapshots = CampaignSaveService.LoadAll(out _, _campaignRoot);
                if (roomSevenCompleteSnapshots.Count != 14 ||
                    !roomSevenCompleteSnapshots.Any(snapshot => snapshot.RoomNumber == 7 && snapshot.Kind == SnapshotKind.RoomComplete) ||
                    !CampaignSaveService.GetCompletedRoomNumbers(_campaignRoot).Contains(7))
                {
                    FailCampaignFlowSmoke("Completing Room 07 did not create its Room Complete snapshot or unlock.");
                    return;
                }

                if (!_roomCompleteDialog.Title.Contains("CHILD", StringComparison.Ordinal) ||
                    !_roomCompleteDialog.DialogText.Contains("Why does it sound so sticky?", StringComparison.Ordinal))
                {
                    FailCampaignFlowSmoke("Room 07 completion did not show its assigned story dialogue.");
                    return;
                }

                ContinueAfterRoomCompletion();
                _campaignFlowWaitFrames = 5;
                _campaignFlowSmokeStep = 15;
                return;
            case 15:
                if (_campaignFlowWaitFrames-- > 0)
                {
                    return;
                }

                IReadOnlyList<CampaignSnapshot> roomEightSnapshots = CampaignSaveService.LoadAll(out _, _campaignRoot);
                if (_currentRoom?.RoomNumber != 8 ||
                    _currentRoom.RoomId != "room-08" ||
                    roomEightSnapshots.Count != 15 ||
                    !roomEightSnapshots.Any(snapshot => snapshot.RoomNumber == 8 && snapshot.Kind == SnapshotKind.RoomStart))
                {
                    FailCampaignFlowSmoke("Continuing from Room 07 did not load Room 08 and create its Room Start snapshot.");
                    return;
                }

                OnRoomCompleted();
                _campaignFlowSmokeStep = 16;
                return;
            case 16:
                IReadOnlyList<CampaignSnapshot> roomEightCompleteSnapshots = CampaignSaveService.LoadAll(out _, _campaignRoot);
                if (roomEightCompleteSnapshots.Count != 16 ||
                    !roomEightCompleteSnapshots.Any(snapshot => snapshot.RoomNumber == 8 && snapshot.Kind == SnapshotKind.RoomComplete) ||
                    !CampaignSaveService.GetCompletedRoomNumbers(_campaignRoot).Contains(8))
                {
                    FailCampaignFlowSmoke("Completing Room 08 did not create its Room Complete snapshot or unlock.");
                    return;
                }

                if (!_roomCompleteDialog.Title.Contains("MOTHER", StringComparison.Ordinal) ||
                    !_roomCompleteDialog.DialogText.Contains("That sounded much faster.", StringComparison.Ordinal))
                {
                    FailCampaignFlowSmoke("Room 08 completion did not show its assigned story dialogue.");
                    return;
                }

                ContinueAfterRoomCompletion();
                _campaignFlowWaitFrames = 5;
                _campaignFlowSmokeStep = 17;
                return;
            case 17:
                if (_campaignFlowWaitFrames-- > 0)
                {
                    return;
                }

                IReadOnlyList<CampaignSnapshot> roomNineSnapshots = CampaignSaveService.LoadAll(out _, _campaignRoot);
                if (_currentRoom?.RoomNumber != 9 ||
                    _currentRoom.RoomId != "room-09" ||
                    roomNineSnapshots.Count != 17 ||
                    !roomNineSnapshots.Any(snapshot => snapshot.RoomNumber == 9 && snapshot.Kind == SnapshotKind.RoomStart))
                {
                    FailCampaignFlowSmoke("Continuing from Room 08 did not load Room 09 and create its Room Start snapshot.");
                    return;
                }

                OnRoomCompleted();
                _campaignFlowSmokeStep = 18;
                return;
            case 18:
                IReadOnlyList<CampaignSnapshot> roomNineCompleteSnapshots = CampaignSaveService.LoadAll(out _, _campaignRoot);
                if (roomNineCompleteSnapshots.Count != 18 ||
                    !roomNineCompleteSnapshots.Any(snapshot => snapshot.RoomNumber == 9 && snapshot.Kind == SnapshotKind.RoomComplete) ||
                    !CampaignSaveService.GetCompletedRoomNumbers(_campaignRoot).Contains(9))
                {
                    FailCampaignFlowSmoke("Completing Room 09 did not create its Room Complete snapshot or unlock.");
                    return;
                }

                if (!_roomCompleteDialog.Title.Contains("CHILD", StringComparison.Ordinal) ||
                    !_roomCompleteDialog.DialogText.Contains("Did it just bounce?", StringComparison.Ordinal))
                {
                    FailCampaignFlowSmoke("Room 09 completion did not show its assigned story dialogue.");
                    return;
                }

                ContinueAfterRoomCompletion();
                _campaignFlowWaitFrames = 5;
                _campaignFlowSmokeStep = 19;
                return;
            case 19:
                if (_campaignFlowWaitFrames-- > 0)
                {
                    return;
                }

                IReadOnlyList<CampaignSnapshot> roomTenSnapshots = CampaignSaveService.LoadAll(out _, _campaignRoot);
                if (_currentRoom?.RoomNumber != 10 ||
                    _currentRoom.RoomId != "room-10" ||
                    roomTenSnapshots.Count != 19 ||
                    !roomTenSnapshots.Any(snapshot => snapshot.RoomNumber == 10 && snapshot.Kind == SnapshotKind.RoomStart))
                {
                    FailCampaignFlowSmoke("Continuing from Room 09 did not load Room 10 and create its Room Start snapshot.");
                    return;
                }

                OnRoomCompleted();
                _campaignFlowSmokeStep = 20;
                return;
            case 20:
                IReadOnlyList<CampaignSnapshot> roomTenCompleteSnapshots = CampaignSaveService.LoadAll(out _, _campaignRoot);
                if (roomTenCompleteSnapshots.Count != 20 ||
                    !roomTenCompleteSnapshots.Any(snapshot => snapshot.RoomNumber == 10 && snapshot.Kind == SnapshotKind.RoomComplete) ||
                    !CampaignSaveService.GetCompletedRoomNumbers(_campaignRoot).Contains(10))
                {
                    FailCampaignFlowSmoke("Completing Room 10 did not create its Room Complete snapshot or unlock.");
                    return;
                }

                if (!_roomCompleteDialog.Title.Contains("MOTHER", StringComparison.Ordinal) ||
                    !_roomCompleteDialog.DialogText.Contains("That sounded like the whole machine.", StringComparison.Ordinal))
                {
                    FailCampaignFlowSmoke("Room 10 completion did not show its assigned story dialogue.");
                    return;
                }

                ContinueAfterRoomCompletion();
                _campaignFlowWaitFrames = 5;
                _campaignFlowSmokeStep = 21;
                return;
            case 21:
                if (_campaignFlowWaitFrames-- > 0)
                {
                    return;
                }

                IReadOnlyList<CampaignSnapshot> roomElevenSnapshots = CampaignSaveService.LoadAll(out _, _campaignRoot);
                if (_currentRoom?.RoomNumber != 11 ||
                    _currentRoom.RoomId != "room-11" ||
                    roomElevenSnapshots.Count != 21 ||
                    !roomElevenSnapshots.Any(snapshot => snapshot.RoomNumber == 11 && snapshot.Kind == SnapshotKind.RoomStart))
                {
                    FailCampaignFlowSmoke("Continuing from Room 10 did not load Room 11 and create its Room Start snapshot.");
                    return;
                }

                OnRoomCompleted();
                _campaignFlowSmokeStep = 22;
                return;
            case 22:
                IReadOnlyList<CampaignSnapshot> roomElevenCompleteSnapshots = CampaignSaveService.LoadAll(out _, _campaignRoot);
                if (roomElevenCompleteSnapshots.Count != 22 ||
                    !roomElevenCompleteSnapshots.Any(snapshot => snapshot.RoomNumber == 11 && snapshot.Kind == SnapshotKind.RoomComplete) ||
                    !CampaignSaveService.GetCompletedRoomNumbers(_campaignRoot).Contains(11))
                {
                    FailCampaignFlowSmoke("Completing Room 11 did not create its Room Complete snapshot or unlock.");
                    return;
                }

                if (!_roomCompleteDialog.Title.Contains("CHILD", StringComparison.Ordinal) ||
                    !_roomCompleteDialog.DialogText.Contains("It sounds like it stopped falling.", StringComparison.Ordinal))
                {
                    FailCampaignFlowSmoke("Room 11 completion did not show its assigned story dialogue.");
                    return;
                }

                ContinueAfterRoomCompletion();
                _campaignFlowWaitFrames = 5;
                _campaignFlowSmokeStep = 23;
                return;
            case 23:
                if (_campaignFlowWaitFrames-- > 0)
                {
                    return;
                }

                IReadOnlyList<CampaignSnapshot> roomTwelveSnapshots = CampaignSaveService.LoadAll(out _, _campaignRoot);
                if (_currentRoom?.RoomNumber != 12 ||
                    _currentRoom.RoomId != "room-12" ||
                    roomTwelveSnapshots.Count != 23 ||
                    !roomTwelveSnapshots.Any(snapshot => snapshot.RoomNumber == 12 && snapshot.Kind == SnapshotKind.RoomStart))
                {
                    FailCampaignFlowSmoke("Continuing from Room 11 did not load Room 12 and create its Room Start snapshot.");
                    return;
                }

                OnRoomCompleted();
                _campaignFlowSmokeStep = 24;
                return;
            case 24:
                IReadOnlyList<CampaignSnapshot> roomTwelveCompleteSnapshots = CampaignSaveService.LoadAll(out _, _campaignRoot);
                if (roomTwelveCompleteSnapshots.Count != 24 ||
                    !roomTwelveCompleteSnapshots.Any(snapshot => snapshot.RoomNumber == 12 && snapshot.Kind == SnapshotKind.RoomComplete) ||
                    !CampaignSaveService.GetCompletedRoomNumbers(_campaignRoot).Contains(12))
                {
                    FailCampaignFlowSmoke("Completing Room 12 did not create its Room Complete snapshot or unlock.");
                    return;
                }

                if (!_roomCompleteDialog.Title.Contains("MOTHER", StringComparison.Ordinal) ||
                    !_roomCompleteDialog.DialogText.Contains("That sounded like a hard landing.", StringComparison.Ordinal))
                {
                    FailCampaignFlowSmoke("Room 12 completion did not show its assigned story dialogue.");
                    return;
                }

                ContinueAfterRoomCompletion();
                _campaignFlowWaitFrames = 5;
                _campaignFlowSmokeStep = 25;
                return;
            case 25:
                if (_campaignFlowWaitFrames-- > 0)
                {
                    return;
                }

                IReadOnlyList<CampaignSnapshot> roomThirteenSnapshots = CampaignSaveService.LoadAll(out _, _campaignRoot);
                if (_currentRoom?.RoomNumber != 13 ||
                    _currentRoom.RoomId != "room-13" ||
                    roomThirteenSnapshots.Count != 25 ||
                    !roomThirteenSnapshots.Any(snapshot => snapshot.RoomNumber == 13 && snapshot.Kind == SnapshotKind.RoomStart))
                {
                    FailCampaignFlowSmoke("Continuing from Room 12 did not load Room 13 and create its Room Start snapshot.");
                    return;
                }

                OnRoomCompleted();
                _campaignFlowSmokeStep = 26;
                return;
            case 26:
                IReadOnlyList<CampaignSnapshot> roomThirteenCompleteSnapshots = CampaignSaveService.LoadAll(out _, _campaignRoot);
                if (roomThirteenCompleteSnapshots.Count != 26 ||
                    !roomThirteenCompleteSnapshots.Any(snapshot => snapshot.RoomNumber == 13 && snapshot.Kind == SnapshotKind.RoomComplete) ||
                    !CampaignSaveService.GetCompletedRoomNumbers(_campaignRoot).Contains(13))
                {
                    FailCampaignFlowSmoke("Completing Room 13 did not create its Room Complete snapshot or unlock.");
                    return;
                }

                if (!_roomCompleteDialog.Title.Contains("CHILD", StringComparison.Ordinal) ||
                    !_roomCompleteDialog.DialogText.Contains("I can hear air rushing inside.", StringComparison.Ordinal))
                {
                    FailCampaignFlowSmoke("Room 13 completion did not show its assigned story dialogue.");
                    return;
                }

                ContinueAfterRoomCompletion();
                _campaignFlowWaitFrames = 5;
                _campaignFlowSmokeStep = 27;
                return;
            case 27:
                if (_campaignFlowWaitFrames-- > 0) { return; }
                IReadOnlyList<CampaignSnapshot> roomFourteenSnapshots = CampaignSaveService.LoadAll(out _, _campaignRoot);
                if (_currentRoom?.RoomNumber != 14 || _currentRoom.RoomId != "room-14" || roomFourteenSnapshots.Count != 27 ||
                    !roomFourteenSnapshots.Any(snapshot => snapshot.RoomNumber == 14 && snapshot.Kind == SnapshotKind.RoomStart))
                {
                    FailCampaignFlowSmoke("Continuing from Room 13 did not load Room 14 and create its Room Start snapshot.");
                    return;
                }
                OnRoomCompleted();
                _campaignFlowSmokeStep = 28;
                return;
            case 28:
                IReadOnlyList<CampaignSnapshot> roomFourteenCompleteSnapshots = CampaignSaveService.LoadAll(out _, _campaignRoot);
                if (roomFourteenCompleteSnapshots.Count != 28 || !roomFourteenCompleteSnapshots.Any(snapshot => snapshot.RoomNumber == 14 && snapshot.Kind == SnapshotKind.RoomComplete) || !CampaignSaveService.GetCompletedRoomNumbers(_campaignRoot).Contains(14))
                {
                    FailCampaignFlowSmoke("Completing Room 14 did not create its Room Complete snapshot or unlock."); return;
                }
                if (!_roomCompleteDialog.Title.Contains("MOTHER", StringComparison.Ordinal) || !_roomCompleteDialog.DialogText.Contains("Something just clicked into place.", StringComparison.Ordinal))
                {
                    FailCampaignFlowSmoke("Room 14 completion did not show its assigned story dialogue."); return;
                }
                ContinueAfterRoomCompletion(); _campaignFlowWaitFrames = 5; _campaignFlowSmokeStep = 29; return;
            case 29:
                if (_campaignFlowWaitFrames-- > 0) { return; }
                IReadOnlyList<CampaignSnapshot> roomFifteenSnapshots = CampaignSaveService.LoadAll(out _, _campaignRoot);
                if (_currentRoom?.RoomNumber != 15 || _currentRoom.RoomId != "room-15" || roomFifteenSnapshots.Count != 29 || !roomFifteenSnapshots.Any(snapshot => snapshot.RoomNumber == 15 && snapshot.Kind == SnapshotKind.RoomStart))
                {
                    FailCampaignFlowSmoke("Continuing from Room 14 did not load Room 15 and create its Room Start snapshot."); return;
                }
                OnRoomCompleted();
                _campaignFlowSmokeStep = 30;
                return;
            case 30:
                IReadOnlyList<CampaignSnapshot> roomFifteenCompleteSnapshots = CampaignSaveService.LoadAll(out _, _campaignRoot);
                if (roomFifteenCompleteSnapshots.Count != 30 || !roomFifteenCompleteSnapshots.Any(snapshot => snapshot.RoomNumber == 15 && snapshot.Kind == SnapshotKind.RoomComplete) || !CampaignSaveService.GetCompletedRoomNumbers(_campaignRoot).Contains(15))
                {
                    FailCampaignFlowSmoke("Completing Room 15 did not create its Room Complete snapshot or unlock."); return;
                }
                if (!_roomCompleteDialog.Title.Contains("CHILD", StringComparison.Ordinal) || !_roomCompleteDialog.DialogText.Contains("That one sounded really long.", StringComparison.Ordinal))
                {
                    FailCampaignFlowSmoke("Room 15 completion did not show its assigned story dialogue."); return;
                }
                ContinueAfterRoomCompletion(); _campaignFlowWaitFrames = 5; _campaignFlowSmokeStep = 31; return;
            case 31:
                if (_campaignFlowWaitFrames-- > 0) { return; }
                IReadOnlyList<CampaignSnapshot> roomSixteenSnapshots = CampaignSaveService.LoadAll(out _, _campaignRoot);
                if (_currentRoom?.RoomNumber != 16 || _currentRoom.RoomId != "room-16" || roomSixteenSnapshots.Count != 31 || !roomSixteenSnapshots.Any(snapshot => snapshot.RoomNumber == 16 && snapshot.Kind == SnapshotKind.RoomStart))
                {
                    FailCampaignFlowSmoke("Continuing from Room 15 did not load Room 16 and create its Room Start snapshot."); return;
                }
                OnRoomCompleted();
                _campaignFlowSmokeStep = 32;
                return;
            case 32:
                IReadOnlyList<CampaignSnapshot> roomSixteenCompleteSnapshots = CampaignSaveService.LoadAll(out _, _campaignRoot);
                if (roomSixteenCompleteSnapshots.Count != 32 || !roomSixteenCompleteSnapshots.Any(snapshot => snapshot.RoomNumber == 16 && snapshot.Kind == SnapshotKind.RoomComplete) || !CampaignSaveService.GetCompletedRoomNumbers(_campaignRoot).Contains(16))
                {
                    FailCampaignFlowSmoke("Completing Room 16 did not create its Room Complete snapshot or unlock."); return;
                }
                if (!_roomCompleteDialog.Title.Contains("MOTHER", StringComparison.Ordinal) || !_roomCompleteDialog.DialogText.Contains("That sounded like it launched something.", StringComparison.Ordinal))
                {
                    FailCampaignFlowSmoke("Room 16 completion did not show its assigned story dialogue."); return;
                }
                ContinueAfterRoomCompletion(); _campaignFlowWaitFrames = 5; _campaignFlowSmokeStep = 33; return;
            case 33:
                if (_campaignFlowWaitFrames-- > 0) { return; }
                IReadOnlyList<CampaignSnapshot> roomSeventeenSnapshots = CampaignSaveService.LoadAll(out _, _campaignRoot);
                if (_currentRoom?.RoomNumber != 17 || _currentRoom.RoomId != "room-17" || roomSeventeenSnapshots.Count != 33 || !roomSeventeenSnapshots.Any(snapshot => snapshot.RoomNumber == 17 && snapshot.Kind == SnapshotKind.RoomStart))
                {
                    FailCampaignFlowSmoke("Continuing from Room 16 did not load Room 17 and create its Room Start snapshot."); return;
                }
                OnRoomCompleted();
                _campaignFlowSmokeStep = 34;
                return;
            case 34:
                IReadOnlyList<CampaignSnapshot> roomSeventeenCompleteSnapshots = CampaignSaveService.LoadAll(out _, _campaignRoot);
                if (roomSeventeenCompleteSnapshots.Count != 34 || !roomSeventeenCompleteSnapshots.Any(snapshot => snapshot.RoomNumber == 17 && snapshot.Kind == SnapshotKind.RoomComplete) || !CampaignSaveService.GetCompletedRoomNumbers(_campaignRoot).Contains(17))
                {
                    FailCampaignFlowSmoke("Completing Room 17 did not create its Room Complete snapshot or unlock."); return;
                }
                if (!_roomCompleteDialog.Title.Contains("CHILD", StringComparison.Ordinal) || !_roomCompleteDialog.DialogText.Contains("Something is bouncing around in there!", StringComparison.Ordinal))
                {
                    FailCampaignFlowSmoke("Room 17 completion did not show its assigned story dialogue."); return;
                }
                ContinueAfterRoomCompletion(); _campaignFlowWaitFrames = 5; _campaignFlowSmokeStep = 35; return;
            case 35:
                if (_campaignFlowWaitFrames-- > 0) { return; }
                IReadOnlyList<CampaignSnapshot> roomEighteenSnapshots = CampaignSaveService.LoadAll(out _, _campaignRoot);
                if (_currentRoom?.RoomNumber != 18 || _currentRoom.RoomId != "room-18" || roomEighteenSnapshots.Count != 35 || !roomEighteenSnapshots.Any(snapshot => snapshot.RoomNumber == 18 && snapshot.Kind == SnapshotKind.RoomStart))
                {
                    FailCampaignFlowSmoke("Continuing from Room 17 did not load Room 18 and create its Room Start snapshot."); return;
                }
                OnRoomCompleted();
                _campaignFlowSmokeStep = 36;
                return;
            case 36:
                IReadOnlyList<CampaignSnapshot> roomEighteenCompleteSnapshots = CampaignSaveService.LoadAll(out _, _campaignRoot);
                if (roomEighteenCompleteSnapshots.Count != 36 || !roomEighteenCompleteSnapshots.Any(snapshot => snapshot.RoomNumber == 18 && snapshot.Kind == SnapshotKind.RoomComplete) || !CampaignSaveService.GetCompletedRoomNumbers(_campaignRoot).Contains(18))
                {
                    FailCampaignFlowSmoke("Completing Room 18 did not create its Room Complete snapshot or unlock."); return;
                }
                if (!_roomCompleteDialog.Title.Contains("MOTHER", StringComparison.Ordinal) || !_roomCompleteDialog.DialogText.Contains("I think something just went up.", StringComparison.Ordinal))
                {
                    FailCampaignFlowSmoke("Room 18 completion did not show its assigned story dialogue."); return;
                }
                ContinueAfterRoomCompletion(); _campaignFlowWaitFrames = 5; _campaignFlowSmokeStep = 37; return;
            case 37:
                if (_campaignFlowWaitFrames-- > 0) { return; }
                IReadOnlyList<CampaignSnapshot> roomNineteenSnapshots = CampaignSaveService.LoadAll(out _, _campaignRoot);
                if (_currentRoom?.RoomNumber != 19 || _currentRoom.RoomId != "room-19" || roomNineteenSnapshots.Count != 37 || !roomNineteenSnapshots.Any(snapshot => snapshot.RoomNumber == 19 && snapshot.Kind == SnapshotKind.RoomStart))
                {
                    FailCampaignFlowSmoke("Continuing from Room 18 did not load Room 19 and create its Room Start snapshot."); return;
                }
                OnRoomCompleted();
                _campaignFlowSmokeStep = 38;
                return;
            case 38:
                IReadOnlyList<CampaignSnapshot> roomNineteenCompleteSnapshots = CampaignSaveService.LoadAll(out _, _campaignRoot);
                if (roomNineteenCompleteSnapshots.Count != 38 || !roomNineteenCompleteSnapshots.Any(snapshot => snapshot.RoomNumber == 19 && snapshot.Kind == SnapshotKind.RoomComplete) || !CampaignSaveService.GetCompletedRoomNumbers(_campaignRoot).Contains(19))
                {
                    FailCampaignFlowSmoke("Completing Room 19 did not create its Room Complete snapshot or unlock."); return;
                }
                if (!_roomCompleteDialog.Title.Contains("CHILD", StringComparison.Ordinal) || !_roomCompleteDialog.DialogText.Contains("That was a really loud spring.", StringComparison.Ordinal))
                {
                    FailCampaignFlowSmoke("Room 19 completion did not show its assigned story dialogue."); return;
                }
                ContinueAfterRoomCompletion(); _campaignFlowWaitFrames = 5; _campaignFlowSmokeStep = 39; return;
            case 39:
                if (_campaignFlowWaitFrames-- > 0) { return; }
                IReadOnlyList<CampaignSnapshot> roomTwentySnapshots = CampaignSaveService.LoadAll(out _, _campaignRoot);
                if (_currentRoom?.RoomNumber != 20 || _currentRoom.RoomId != "room-20" || roomTwentySnapshots.Count != 39 || !roomTwentySnapshots.Any(snapshot => snapshot.RoomNumber == 20 && snapshot.Kind == SnapshotKind.RoomStart))
                {
                    FailCampaignFlowSmoke("Continuing from Room 19 did not load Room 20 and create its Room Start snapshot."); return;
                }
                OnRoomCompleted();
                _campaignFlowSmokeStep = 40;
                return;
            case 40:
                IReadOnlyList<CampaignSnapshot> roomTwentyCompleteSnapshots = CampaignSaveService.LoadAll(out _, _campaignRoot);
                if (roomTwentyCompleteSnapshots.Count != 40 || !roomTwentyCompleteSnapshots.Any(snapshot => snapshot.RoomNumber == 20 && snapshot.Kind == SnapshotKind.RoomComplete) || !CampaignSaveService.GetCompletedRoomNumbers(_campaignRoot).Contains(20))
                {
                    FailCampaignFlowSmoke("Completing Room 20 did not create its Room Complete snapshot or unlock."); return;
                }
                if (!_roomCompleteDialog.Title.Contains("MOTHER", StringComparison.Ordinal) || !_roomCompleteDialog.DialogText.Contains("That sounded like the whole launcher assembly.", StringComparison.Ordinal))
                {
                    FailCampaignFlowSmoke("Room 20 completion did not show its assigned story dialogue."); return;
                }
                ContinueAfterRoomCompletion(); _campaignFlowWaitFrames = 5; _campaignFlowSmokeStep = 41; return;
            case 41:
                if (_campaignFlowWaitFrames-- > 0) { return; }
                IReadOnlyList<CampaignSnapshot> roomTwentyOneSnapshots = CampaignSaveService.LoadAll(out _, _campaignRoot);
                if (_currentRoom?.RoomNumber != 21 || _currentRoom.RoomId != "room-21" || roomTwentyOneSnapshots.Count != 41 || !roomTwentyOneSnapshots.Any(snapshot => snapshot.RoomNumber == 21 && snapshot.Kind == SnapshotKind.RoomStart))
                {
                    FailCampaignFlowSmoke("Continuing from Room 20 did not load Room 21 and create its Room Start snapshot."); return;
                }
                OnRoomCompleted();
                _campaignFlowSmokeStep = 42;
                return;
            case 42:
                IReadOnlyList<CampaignSnapshot> roomTwentyOneCompleteSnapshots = CampaignSaveService.LoadAll(out _, _campaignRoot);
                if (roomTwentyOneCompleteSnapshots.Count != 42 || !roomTwentyOneCompleteSnapshots.Any(snapshot => snapshot.RoomNumber == 21 && snapshot.Kind == SnapshotKind.RoomComplete) || !CampaignSaveService.GetCompletedRoomNumbers(_campaignRoot).Contains(21))
                {
                    FailCampaignFlowSmoke("Completing Room 21 did not create its Room Complete snapshot or unlock."); return;
                }
                if (!_roomCompleteDialog.Title.Contains("CHILD", StringComparison.Ordinal) || !_roomCompleteDialog.DialogText.Contains("It suddenly went very quiet.", StringComparison.Ordinal))
                {
                    FailCampaignFlowSmoke("Room 21 completion did not show its assigned story dialogue."); return;
                }
                ContinueAfterRoomCompletion(); _campaignFlowWaitFrames = 5; _campaignFlowSmokeStep = 43; return;
            case 43:
                if (_campaignFlowWaitFrames-- > 0) { return; }
                IReadOnlyList<CampaignSnapshot> roomTwentyTwoSnapshots = CampaignSaveService.LoadAll(out _, _campaignRoot);
                if (_currentRoom?.RoomNumber != 22 || _currentRoom.RoomId != "room-22" || roomTwentyTwoSnapshots.Count != 43 || !roomTwentyTwoSnapshots.Any(snapshot => snapshot.RoomNumber == 22 && snapshot.Kind == SnapshotKind.RoomStart))
                {
                    FailCampaignFlowSmoke("Continuing from Room 21 did not load Room 22 and create its Room Start snapshot."); return;
                }
                OnRoomCompleted(); _campaignFlowSmokeStep = 44; return;
            case 44:
                IReadOnlyList<CampaignSnapshot> roomTwentyTwoCompleteSnapshots = CampaignSaveService.LoadAll(out _, _campaignRoot);
                if (roomTwentyTwoCompleteSnapshots.Count != 44 || !roomTwentyTwoCompleteSnapshots.Any(snapshot => snapshot.RoomNumber == 22 && snapshot.Kind == SnapshotKind.RoomComplete) || !CampaignSaveService.GetCompletedRoomNumbers(_campaignRoot).Contains(22))
                {
                    FailCampaignFlowSmoke("Completing Room 22 did not create its Room Complete snapshot or unlock."); return;
                }
                if (!_roomCompleteDialog.Title.Contains("MOTHER", StringComparison.Ordinal) || !_roomCompleteDialog.DialogText.Contains("It sounds like a tiny ratchet turning.", StringComparison.Ordinal))
                {
                    FailCampaignFlowSmoke("Room 22 completion did not show its assigned story dialogue."); return;
                }
                ContinueAfterRoomCompletion(); _campaignFlowWaitFrames = 5; _campaignFlowSmokeStep = 45; return;
            case 45:
                if (_campaignFlowWaitFrames-- > 0) { return; }
                IReadOnlyList<CampaignSnapshot> roomTwentyThreeSnapshots = CampaignSaveService.LoadAll(out _, _campaignRoot);
                if (_currentRoom?.RoomNumber != 23 || _currentRoom.RoomId != "room-23" || roomTwentyThreeSnapshots.Count != 45 || !roomTwentyThreeSnapshots.Any(snapshot => snapshot.RoomNumber == 23 && snapshot.Kind == SnapshotKind.RoomStart))
                { FailCampaignFlowSmoke("Continuing from Room 22 did not load Room 23 and create its Room Start snapshot."); return; }
                OnRoomCompleted(); _campaignFlowSmokeStep = 46; return;
            case 46:
                IReadOnlyList<CampaignSnapshot> roomTwentyThreeCompleteSnapshots = CampaignSaveService.LoadAll(out _, _campaignRoot);
                if (roomTwentyThreeCompleteSnapshots.Count != 46 || !roomTwentyThreeCompleteSnapshots.Any(snapshot => snapshot.RoomNumber == 23 && snapshot.Kind == SnapshotKind.RoomComplete) || !CampaignSaveService.GetCompletedRoomNumbers(_campaignRoot).Contains(23))
                { FailCampaignFlowSmoke("Completing Room 23 did not create its Room Complete snapshot or unlock."); return; }
                if (!_roomCompleteDialog.Title.Contains("CHILD", StringComparison.Ordinal) || !_roomCompleteDialog.DialogText.Contains("Something in there just wound up!", StringComparison.Ordinal))
                { FailCampaignFlowSmoke("Room 23 completion did not show its assigned story dialogue."); return; }
                ContinueAfterRoomCompletion(); _campaignFlowWaitFrames = 5; _campaignFlowSmokeStep = 47; return;
            case 47:
                if (_campaignFlowWaitFrames-- > 0) { return; }
                IReadOnlyList<CampaignSnapshot> roomTwentyFourSnapshots = CampaignSaveService.LoadAll(out _, _campaignRoot);
                if (_currentRoom?.RoomNumber != 24 || _currentRoom.RoomId != "room-24" || roomTwentyFourSnapshots.Count != 47 || !roomTwentyFourSnapshots.Any(snapshot => snapshot.RoomNumber == 24 && snapshot.Kind == SnapshotKind.RoomStart))
                { FailCampaignFlowSmoke("Continuing from Room 23 did not load Room 24 and create its Room Start snapshot."); return; }
                OnRoomCompleted(); _campaignFlowSmokeStep = 48; return;
            case 48:
                IReadOnlyList<CampaignSnapshot> roomTwentyFourCompleteSnapshots = CampaignSaveService.LoadAll(out _, _campaignRoot);
                if (roomTwentyFourCompleteSnapshots.Count != 48 || !roomTwentyFourCompleteSnapshots.Any(snapshot => snapshot.RoomNumber == 24 && snapshot.Kind == SnapshotKind.RoomComplete) || !CampaignSaveService.GetCompletedRoomNumbers(_campaignRoot).Contains(24))
                { FailCampaignFlowSmoke("Completing Room 24 did not create its Room Complete snapshot or unlock."); return; }
                if (!_roomCompleteDialog.Title.Contains("MOTHER", StringComparison.Ordinal) || !_roomCompleteDialog.DialogText.Contains("I hope that cracking sound was normal.", StringComparison.Ordinal))
                { FailCampaignFlowSmoke("Room 24 completion did not show its assigned story dialogue."); return; }
                ContinueAfterRoomCompletion(); _campaignFlowWaitFrames = 5; _campaignFlowSmokeStep = 49; return;
            case 49:
                if (_campaignFlowWaitFrames-- > 0) { return; }
                IReadOnlyList<CampaignSnapshot> roomTwentyFiveSnapshots = CampaignSaveService.LoadAll(out _, _campaignRoot);
                if (_currentRoom?.RoomNumber != 25 || _currentRoom.RoomId != "room-25" || roomTwentyFiveSnapshots.Count != 49 || !roomTwentyFiveSnapshots.Any(snapshot => snapshot.RoomNumber == 25 && snapshot.Kind == SnapshotKind.RoomStart))
                { FailCampaignFlowSmoke("Continuing from Room 24 did not load Room 25 and create its Room Start snapshot."); return; }
                OnRoomCompleted(); _campaignFlowSmokeStep = 50; return;
            case 50:
                IReadOnlyList<CampaignSnapshot> roomTwentyFiveCompleteSnapshots = CampaignSaveService.LoadAll(out _, _campaignRoot);
                if (roomTwentyFiveCompleteSnapshots.Count != 50 || !roomTwentyFiveCompleteSnapshots.Any(snapshot => snapshot.RoomNumber == 25 && snapshot.Kind == SnapshotKind.RoomComplete) || !CampaignSaveService.GetCompletedRoomNumbers(_campaignRoot).Contains(25))
                { FailCampaignFlowSmoke("Completing Room 25 did not create its Room Complete snapshot or unlock."); return; }
                if (!_roomCompleteDialog.Title.Contains("CHILD", StringComparison.Ordinal) || !_roomCompleteDialog.DialogText.Contains("Why does it keep changing how it rolls?", StringComparison.Ordinal))
                { FailCampaignFlowSmoke("Room 25 completion did not show its assigned story dialogue."); return; }
                ContinueAfterRoomCompletion(); _campaignFlowWaitFrames = 5; _campaignFlowSmokeStep = 51; return;
            case 51:
                if (_campaignFlowWaitFrames-- > 0) { return; }
                IReadOnlyList<CampaignSnapshot> roomTwentySixSnapshots = CampaignSaveService.LoadAll(out _, _campaignRoot);
                if (_currentRoom?.RoomNumber != 26 || _currentRoom.RoomId != "room-26" || roomTwentySixSnapshots.Count != 51 || !roomTwentySixSnapshots.Any(snapshot => snapshot.RoomNumber == 26 && snapshot.Kind == SnapshotKind.RoomStart))
                { FailCampaignFlowSmoke("Continuing from Room 25 did not load Room 26 and create its Room Start snapshot."); return; }
                OnRoomCompleted(); _campaignFlowSmokeStep = 52; return;
            case 52:
                IReadOnlyList<CampaignSnapshot> roomTwentySixCompleteSnapshots = CampaignSaveService.LoadAll(out _, _campaignRoot);
                if (roomTwentySixCompleteSnapshots.Count != 52 || !roomTwentySixCompleteSnapshots.Any(snapshot => snapshot.RoomNumber == 26 && snapshot.Kind == SnapshotKind.RoomComplete) || !CampaignSaveService.GetCompletedRoomNumbers(_campaignRoot).Contains(26))
                { FailCampaignFlowSmoke("Completing Room 26 did not create its Room Complete snapshot or unlock."); return; }
                if (!_roomCompleteDialog.Title.Contains("CHILD", StringComparison.Ordinal) || !_roomCompleteDialog.DialogText.Contains("Those cannons are tracking it through the air!", StringComparison.Ordinal))
                { FailCampaignFlowSmoke("Room 26 completion did not show its assigned story dialogue."); return; }
                ContinueAfterRoomCompletion(); _campaignFlowWaitFrames = 5; _campaignFlowSmokeStep = 53; return;
            case 53:
                if (_campaignFlowWaitFrames-- > 0) { return; }
                IReadOnlyList<CampaignSnapshot> roomTwentySevenSnapshots = CampaignSaveService.LoadAll(out _, _campaignRoot);
                if (_currentRoom?.RoomNumber != 27 || _currentRoom.RoomId != "room-27" || roomTwentySevenSnapshots.Count != 53 || !roomTwentySevenSnapshots.Any(snapshot => snapshot.RoomNumber == 27 && snapshot.Kind == SnapshotKind.RoomStart))
                { FailCampaignFlowSmoke("Continuing from Room 26 did not load Room 27 and create its Room Start snapshot."); return; }
                OnRoomCompleted(); _campaignFlowSmokeStep = 54; return;
            case 54:
                IReadOnlyList<CampaignSnapshot> roomTwentySevenCompleteSnapshots = CampaignSaveService.LoadAll(out _, _campaignRoot);
                if (roomTwentySevenCompleteSnapshots.Count != 54 || !roomTwentySevenCompleteSnapshots.Any(snapshot => snapshot.RoomNumber == 27 && snapshot.Kind == SnapshotKind.RoomComplete) || !CampaignSaveService.GetCompletedRoomNumbers(_campaignRoot).Contains(27))
                { FailCampaignFlowSmoke("Completing Room 27 did not create its Room Complete snapshot or unlock."); return; }
                if (!_roomCompleteDialog.Title.Contains("CHILD", StringComparison.Ordinal) || !_roomCompleteDialog.DialogText.Contains("Did the candy just change direction?", StringComparison.Ordinal))
                { FailCampaignFlowSmoke("Room 27 completion did not show its assigned story dialogue."); return; }
                ContinueAfterRoomCompletion(); _campaignFlowWaitFrames = 5; _campaignFlowSmokeStep = 55; return;
            case 55:
                if (_campaignFlowWaitFrames-- > 0) { return; }
                IReadOnlyList<CampaignSnapshot> roomTwentyEightSnapshots = CampaignSaveService.LoadAll(out _, _campaignRoot);
                if (_currentRoom?.RoomNumber != 28 || _currentRoom.RoomId != "room-28" || roomTwentyEightSnapshots.Count != 55 || !roomTwentyEightSnapshots.Any(snapshot => snapshot.RoomNumber == 28 && snapshot.Kind == SnapshotKind.RoomStart))
                { FailCampaignFlowSmoke("Continuing from Room 27 did not load Room 28 and create its Room Start snapshot."); return; }
                OnRoomCompleted(); _campaignFlowSmokeStep = 56; return;
            case 56:
                IReadOnlyList<CampaignSnapshot> roomTwentyEightCompleteSnapshots = CampaignSaveService.LoadAll(out _, _campaignRoot);
                if (roomTwentyEightCompleteSnapshots.Count != 56 || !roomTwentyEightCompleteSnapshots.Any(snapshot => snapshot.RoomNumber == 28 && snapshot.Kind == SnapshotKind.RoomComplete) || !CampaignSaveService.GetCompletedRoomNumbers(_campaignRoot).Contains(28))
                { FailCampaignFlowSmoke("Completing Room 28 did not create its Room Complete snapshot or unlock."); return; }
                if (!_roomCompleteDialog.Title.Contains("CHILD", StringComparison.Ordinal) || !_roomCompleteDialog.DialogText.Contains("It must be close now!", StringComparison.Ordinal))
                { FailCampaignFlowSmoke("Room 28 completion did not show its assigned story dialogue."); return; }
                _roomCompleteDialog.Hide(); GetTree().Paused = false; CampaignSaveService.DeleteAll(out _, _campaignRoot); _campaignFlowSmokePending = false;
                GD.Print("CAMPAIGN_FLOW_SMOKE_PASS: Rooms 01-28 transitions, dialogue and all 56 paired campaign snapshots are integrated."); GetTree().Quit(0); return;
        }
    }

    private void FailCampaignFlowSmoke(string message)
    {
        _roomCompleteDialog.Hide();
        GetTree().Paused = false;
        CampaignSaveService.DeleteAll(out _, _campaignRoot);
        GD.PushError($"CAMPAIGN_FLOW_SMOKE_FAIL: {message}");
        GetTree().Quit(1);
    }

    private void FailSaveSmoke(string message)
    {
        GD.PushError($"SAVE_SMOKE_FAIL: {message}");
        GetTree().Quit(1);
    }

    private async void RunUiSmokeTest()
    {
        const string smokeSettingsPath = "user://settings-smoke.cfg";
        CampaignSaveService.DeleteAll(out _, _campaignRoot);
        _settings.FpsLimit = 120;
        _settings.RenderScale = 0.8f;
        _settings.MsaaLevel = 0;
        _settings.InvertY = true;
        _settings.SubtitlesEnabled = false;
        _settings.MoveForwardKey = (long)Key.I;
        ApplySettings(save: false, applyDefaultCamera: false);
        SyncControlsFromSettings();
        if (Engine.MaxFps != 120)
        {
            FailUiSmoke("120 FPS setting was not applied.");
            return;
        }

        if (!Mathf.IsEqualApprox(GetTree().Root.Scaling3DScale, 0.8f) ||
            InputDefaults.GetPrimaryKey(InputDefaults.MoveForward) != Key.I)
        {
            FailUiSmoke("Video scale or control rebinding was not applied.");
            return;
        }

        if (SettingsStore.Save(_settings, smokeSettingsPath) != Error.Ok)
        {
            FailUiSmoke("Settings could not be saved.");
            return;
        }

        GameSettingsData loaded = SettingsStore.Load(smokeSettingsPath);
        ConfigFile serializedSettings = new();
        if (loaded.FpsLimit != 120 ||
            !loaded.InvertY ||
            loaded.SubtitlesEnabled ||
            loaded.MoveForwardKey != (long)Key.I ||
            serializedSettings.Load(smokeSettingsPath) != Error.Ok ||
            serializedSettings.HasSectionKey("controls", "restart_room"))
        {
            FailUiSmoke("Settings persistence did not preserve the UI state or still serialized the removed restart shortcut.");
            return;
        }

        DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath(smokeSettingsPath));
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        Label buildStage = GetNode<Label>("Ui/MainMenu/Version");
        HBoxContainer mainLogo = GetNode<HBoxContainer>("Ui/MainMenu/Center/Panel/Layout/Logo");
        Label mainLogoLeft = GetNode<Label>("Ui/MainMenu/Center/Panel/Layout/Logo/Vel");
        TextureRect logoBall = GetNode<TextureRect>("Ui/MainMenu/Center/Panel/Layout/Logo/BallO");
        Control logoRightSpacing = GetNode<Control>("Ui/MainMenu/Center/Panel/Layout/Logo/RightSpacing");
        Label mainLogoRight = GetNode<Label>("Ui/MainMenu/Center/Panel/Layout/Logo/Citex");
        float loadingLeftGap = _loadingLogoBall.Position.X - (_loadingLogoLeft.Position.X + _loadingLogoLeft.Size.X);
        float loadingRightGap = _loadingLogoRight.Position.X - (_loadingLogoBall.Position.X + _loadingLogoBall.Size.X);
        AudioStreamWav? menuStream = _menuMusic.Stream as AudioStreamWav;
        Button pauseRestartButton = GetNode<Button>("Ui/PauseMenu/Center/Panel/Layout/RestartButton");
        Control pausePanel = GetNode<Control>("Ui/PauseMenu/Center/Panel");
        string initialPanoramaRoom = _menuBackground.CurrentRoomKey;
        if (InputMap.HasAction("restart_room") ||
            GetNodeOrNull("Ui/SettingsMenu/Center/Panel/Layout/Tabs/CONTROLS/Layout/Grid/RestartButton") is not null ||
            GetNodeOrNull("Ui/SettingsMenu/Center/Panel/Layout/Tabs/CONTROLS/Layout/Grid/RestartLabel") is not null ||
            pauseRestartButton.Text != "RESTART ROOM" ||
            pausePanel.CustomMinimumSize.Y > 420.0f ||
            GetNodeOrNull("Ui/PauseMenu/Center/Panel/Layout/LoadButton") is not null ||
            GetNodeOrNull("Ui/PauseMenu/Center/Panel/Layout/CustomizeButton") is not null ||
            !_menuBackground.Visible || logoBall.Texture is null || !Mathf.IsEqualApprox(logoBall.CustomMinimumSize.X, 39.0f) ||
            mainLogo.GetThemeConstant("separation") != -3 || !Mathf.IsEqualApprox(logoRightSpacing.CustomMinimumSize.X, 5.0f) ||
            _loadingLogoBall.Texture != logoBall.Texture ||
            !Mathf.IsEqualApprox(_loadingLogoBall.CustomMinimumSize.X, logoBall.CustomMinimumSize.X * 2.0f) ||
            _loadingLogoLeft.GetThemeFontSize("font_size") != mainLogoLeft.GetThemeFontSize("font_size") * 2 ||
            _loadingLogoRight.GetThemeFontSize("font_size") != mainLogoRight.GetThemeFontSize("font_size") * 2 ||
            !Mathf.IsEqualApprox(loadingLeftGap, -6.0f) || !Mathf.IsEqualApprox(loadingRightGap, -2.0f) ||
            logoBall.ExpandMode != TextureRect.ExpandModeEnum.IgnoreSize ||
            (DisplayServer.GetName() != "headless" && !_menuMusic.Playing) ||
            _menuMusic.Bus != "Music" || menuStream?.LoopMode != AudioStreamWav.LoopModeEnum.Forward ||
            _menuBackground.PanoramaCount < 56 ||
            string.IsNullOrWhiteSpace(initialPanoramaRoom) || !_menuBackground.AdvancePanoramaForTesting() ||
            _menuBackground.CurrentRoomKey == initialPanoramaRoom ||
            buildStage.Visible || !string.IsNullOrEmpty(buildStage.Text) ||
            GetNodeOrNull("Ui/MainMenu/Center/Panel/Layout/Eyebrow") is not null ||
            GetNodeOrNull("Ui/MainMenu/Center/Panel/Layout/Subtitle") is not null)
        {
            FailUiSmoke(
                $"Main-menu identity mismatch: logo={logoBall.Texture is not null}, logoSize={logoBall.CustomMinimumSize}, " +
                $"loadingLogoSize={_loadingLogoBall.CustomMinimumSize}, loadingGaps={loadingLeftGap:F1}/{loadingRightGap:F1}, " +
                $"musicPlaying={_menuMusic.Playing}, musicBus={_menuMusic.Bus}, loop={menuStream?.LoopMode}, " +
                $"panoramas={_menuBackground.PanoramaCount}, room={initialPanoramaRoom}, stage={buildStage.Text}.");
            return;
        }

        _settings.ReducedMotion = true;
        ApplySettings(save: false, applyDefaultCamera: false);
        if (_menuBackground.MotionEnabled)
        {
            FailUiSmoke("Reduced Motion did not stop the decorative menu animation.");
            return;
        }

        _settings.ReducedMotion = false;
        ApplySettings(save: false, applyDefaultCamera: false);
        OpenSettings(MenuOrigin.Main);
        TabContainer tabs = GetNode<TabContainer>("Ui/SettingsMenu/Center/Panel/Layout/Tabs");
        Control settingsPanel = GetNode<Control>("Ui/SettingsMenu/Center/Panel");
        Vector2 viewportSize = GetViewport().GetVisibleRect().Size;
        if (!_settingsMenu.Visible || tabs.GetTabCount() != 5 ||
            _subtitlesCheck.ButtonPressed ||
            settingsPanel.Size.X > viewportSize.X || settingsPanel.Size.Y > viewportSize.Y)
        {
            FailUiSmoke("Settings tabs are missing or the settings panel exceeds the viewport.");
            return;
        }

        _invertYCheck.ButtonPressed = true;
        bool enabledLabelMatches = _invertYCheck.Text == "Enabled";
        _invertYCheck.ButtonPressed = false;
        if (!enabledLabelMatches || _invertYCheck.Text != "Disabled" ||
            SettingsCheckButtons().Any(toggle =>
                toggle.LayoutDirection != Control.LayoutDirectionEnum.Ltr ||
                toggle.GetThemeStylebox("focus") is not StyleBoxEmpty ||
                !ReferenceEquals(toggle.GetThemeStylebox("normal"), toggle.GetThemeStylebox("hover"))))
        {
            FailUiSmoke("Settings toggles do not keep their state label or focus styling consistent.");
            return;
        }

        CloseSettings();
        PlayerProfile smokeProfile = ProfileStore.CreateDefault();
        string? profileSaveError = null;
        if (!ProfileStore.Save(smokeProfile, out profileSaveError, _profilePath))
        {
            FailUiSmoke($"Customization profile setup failed: {profileSaveError}");
            return;
        }

        OpenCustomize(MenuOrigin.Main);
        Control customizePanel = GetNode<Control>("Ui/CustomizeMenu/Center/Panel");
        int colorCount = CosmeticCatalog.OfKind(CosmeticKind.Color).Count();
        int patternCount = CosmeticCatalog.OfKind(CosmeticKind.Pattern).Count();
        int trailCount = CosmeticCatalog.OfKind(CosmeticKind.Trail).Count();
        if (!_customizeMenu.Visible || _primaryColorButtons.Count != colorCount ||
            _secondaryColorButtons.Count != colorCount || _patternButtons.Count != patternCount ||
            _trailButtons.Count != trailCount || !_primaryColorButtons.Any(button => button.IsLocked) ||
            !_patternButtons.Any(button => button.IsLocked) || !_trailButtons.Any(button => button.IsLocked) ||
            customizePanel.Size.X > viewportSize.X ||
            customizePanel.Size.Y > viewportSize.Y)
        {
            FailUiSmoke(
                $"Customize swatch catalog/layout mismatch: colors={_primaryColorButtons.Count}/{_secondaryColorButtons.Count}, " +
                $"patterns={_patternButtons.Count}, trails={_trailButtons.Count}, panel={customizePanel.Size}, viewport={viewportSize}.");
            return;
        }

        string primaryBeforeLockedClick = _selectedPrimaryColorId;
        OnCosmeticSwatchPressed(CosmeticSlot.PrimaryColor, "mint");
        if (_selectedPrimaryColorId != primaryBeforeLockedClick ||
            !_customizeStatus.Text.Contains("Complete Room 01", StringComparison.Ordinal))
        {
            FailUiSmoke("A locked cosmetic changed the selection or did not explain its unlock requirement.");
            return;
        }

        if (!SelectCosmetic(CosmeticSlot.PrimaryColor, "blueberry") ||
            !SelectCosmetic(CosmeticSlot.SecondaryColor, "lime") ||
            !SelectCosmetic(CosmeticSlot.Pattern, "spiral") ||
            !SelectCosmetic(CosmeticSlot.Trail, "trail-cyan"))
        {
            FailUiSmoke("An unlocked visual swatch could not be selected.");
            return;
        }

        SaveCustomization();
        PlayerProfile customizedProfile = ProfileStore.Load(out _, _profilePath);
        if (customizedProfile.PrimaryColorId != "blueberry" || customizedProfile.SecondaryColorId != "lime" ||
            customizedProfile.PatternId != "spiral" || customizedProfile.TrailId != "trail-cyan" ||
            _candyPreview.AppliedPatternId != "spiral" || _candyPreview.AppliedTrailId != "trail-cyan" ||
            !customizedProfile.UnlockedAdvancementIds.Contains("clean-wrapper") ||
            !customizedProfile.UnlockedCosmeticIds.Contains("rose"))
        {
            FailUiSmoke("Customize preview, persistence, or the Fresh Coat unlock diverged.");
            return;
        }

        CloseCustomize();
        if (!_mainMenu.Visible)
        {
            FailUiSmoke("Customize did not return to its main-menu origin.");
            return;
        }

        OpenAdvancements(MenuOrigin.Main);
        Control advancementsPanel = GetNode<Control>("Ui/AdvancementsMenu/Center/Panel");
        if (!_advancementsMenu.Visible || _advancementsList.GetChildCount() != AdvancementCatalog.All.Count ||
            !_advancementsProgress.Text.StartsWith($"1 / {AdvancementCatalog.All.Count}", StringComparison.Ordinal) ||
            advancementsPanel.Size.X > viewportSize.X || advancementsPanel.Size.Y > viewportSize.Y)
        {
            FailUiSmoke("Advancements did not show all definitions, profile progress, or a viewport-safe panel.");
            return;
        }

        for (int index = 0; index < AdvancementCatalog.All.Count; index++)
        {
            Node row = _advancementsList.GetChild(index);
            CosmeticSwatchButton? rewardIcon = row.FindChild("RewardIcon", recursive: true, owned: false) as CosmeticSwatchButton;
            CosmeticSwatchButton? customizeIcon = _primaryColorButtons
                .Concat(_secondaryColorButtons)
                .Concat(_patternButtons)
                .Concat(_trailButtons)
                .FirstOrDefault(button => string.Equals(
                    button.Definition.Id,
                    AdvancementCatalog.All[index].RewardCosmeticId,
                    StringComparison.Ordinal));
            if (rewardIcon is null ||
                customizeIcon is null ||
                !string.Equals(rewardIcon.Definition.Id, AdvancementCatalog.All[index].RewardCosmeticId, StringComparison.Ordinal) ||
                rewardIcon.CustomMinimumSize != customizeIcon.CustomMinimumSize ||
                rewardIcon.PreviewPrimaryColor != customizeIcon.PreviewPrimaryColor ||
                rewardIcon.PreviewSecondaryColor != customizeIcon.PreviewSecondaryColor)
            {
                FailUiSmoke($"Advancement {index + 1} does not reuse the matching Customize cosmetic icon.");
                return;
            }
        }

        CloseAdvancements();
        if (!_mainMenu.Visible)
        {
            FailUiSmoke("Advancements did not return to its main-menu origin.");
            return;
        }

        ShowPlayMenu();
        if (!_playMenu.Visible || !_continueButton.Disabled || !_loadButton.Disabled)
        {
            FailUiSmoke("Play submenu did not expose the empty-campaign state correctly.");
            return;
        }

        ShowLoadBrowser(MenuOrigin.Main);
        Control browserPanel = GetNode<Control>("Ui/BrowserMenu/Center/Panel");
        if (!_browserMenu.Visible || browserPanel.Size.X > viewportSize.X || browserPanel.Size.Y > viewportSize.Y)
        {
            FailUiSmoke("Load Game browser is missing or exceeds the viewport.");
            return;
        }

        CloseBrowser();

        StartGame();
        if (!_loadingOverlay.Visible || _loadingRoomLabel.Text != "ROUTING TO THE NEXT CHAMBER" ||
            _loadingRoomLabel.Visible || _loadingProgress.Visible ||
            _loadingLogoLeft.Modulate.A > 0.01f || _loadingLogoRight.Modulate.A > 0.01f ||
            _loadingLogoBall.Modulate.A < 0.99f || _loadingDim.Color.A < 0.99f)
        {
            FailUiSmoke("Loading did not begin on a black screen with only the centered candy visible.");
            return;
        }

        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        if (_gameplayInstance is null || _mainMenu.Visible || _loadingOverlay.Visible || _menuBackground.Visible || _menuMusic.Playing ||
            _roomIntroNumber.Text != "ROOM 01 / 28" || _roomIntroName.Text != "THE DROP")
        {
            FailUiSmoke("Play did not leave the loading screen and enter gameplay cleanly.");
            return;
        }

        PlayerBall? smokePlayer = GetTree().GetFirstNodeInGroup(PlayerBall.PlayerGroup) as PlayerBall;
        if (smokePlayer is null || smokePlayer.AppliedPatternId != "spiral" ||
            !smokePlayer.IsTrailEnabled || smokePlayer.IsTrailEmitting)
        {
            FailUiSmoke("Saved cosmetics were not applied, or the stationary gameplay candy emitted trail particles into the camera.");
            return;
        }

        int smokeSfxBus = AudioServer.GetBusIndex("SFX");
        int smokeVoiceBus = AudioServer.GetBusIndex("Voice");
        bool smokeSfxMuteBeforePause = smokeSfxBus >= 0 && AudioServer.IsBusMute(smokeSfxBus);
        bool smokeVoiceMuteBeforeTransfer = smokeVoiceBus >= 0 && AudioServer.IsBusMute(smokeVoiceBus);
        MuteSfxForRoomTransfer();
        if ((smokeSfxBus >= 0 && !AudioServer.IsBusMute(smokeSfxBus)) ||
            (smokeVoiceBus >= 0 && AudioServer.IsBusMute(smokeVoiceBus) != smokeVoiceMuteBeforeTransfer))
        {
            FailUiSmoke("Room transfer did not silence only the SFX bus while preserving dialogue voice.");
            return;
        }

        RestoreSfxAfterRoomTransfer();
        if (smokeSfxBus >= 0 && AudioServer.IsBusMute(smokeSfxBus) != smokeSfxMuteBeforePause)
        {
            FailUiSmoke("Room transfer did not restore the previous SFX bus state.");
            return;
        }

        AdvancementDefinition? deferredFiveStar = AdvancementCatalog.Find("five-star-batch");
        int notificationsBeforeDeferral = _advancementNotifications.PendingCount;
        if (deferredFiveStar is null)
        {
            FailUiSmoke("Five-Star Batch definition is missing.");
            return;
        }

        _advancementNotifications.SmokeMode = true;
        _deferredRoomStartNotifications.Add(deferredFiveStar);
        FlushDeferredRoomStartNotifications(6);
        if (_deferredRoomStartNotifications.Count != 0 ||
            _advancementNotifications.PendingCount <= notificationsBeforeDeferral)
        {
            FailUiSmoke("Room-completion notification was not released after the next room intro finished.");
            return;
        }

        await ToSignal(GetTree().CreateTimer(0.5, processAlways: true), SceneTreeTimer.SignalName.Timeout);
        if (_advancementNotifications.PendingCount != 0)
        {
            FailUiSmoke("Deferred Five-Star Batch notification did not finish its smoke lifecycle.");
            return;
        }

        PauseGame();
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        if (!GetTree().Paused || !_pauseMenu.Visible ||
            (smokeSfxBus >= 0 && !AudioServer.IsBusMute(smokeSfxBus)) ||
            (DisplayServer.GetName() != "headless" && !_menuMusic.Playing))
        {
            FailUiSmoke("Pause menu did not pause gameplay, silence SFX and start menu music.");
            return;
        }

        OpenAdvancements(MenuOrigin.Pause);
        if (!_advancementsMenu.Visible || _pauseMenu.Visible || !GetTree().Paused)
        {
            FailUiSmoke("Paused Advancements did not preserve the gameplay pause boundary.");
            return;
        }

        CloseAdvancements();
        if (!_pauseMenu.Visible || !GetTree().Paused)
        {
            FailUiSmoke("Paused Advancements did not return to the pause menu.");
            return;
        }

        ResumeGame();
        if (GetTree().Paused || _pauseMenu.Visible || _menuMusic.Playing ||
            (smokeSfxBus >= 0 && AudioServer.IsBusMute(smokeSfxBus) != smokeSfxMuteBeforePause))
        {
            FailUiSmoke("Resume did not stop menu music and restore the previous SFX state.");
            return;
        }

        _settings = new GameSettingsData();
        ApplySettings(save: false, applyDefaultCamera: false);
        CampaignSaveService.DeleteAll(out _, _campaignRoot);
        ProfileStore.DeleteTestFiles(_profilePath);
            GD.Print("UI_SMOKE_PASS: visual menu, loading, main-menu-only load/customization, deferred room-completion notifications, silent room transfer, pause boundary, complete settings, rebinding and 120 FPS work.");
        GetTree().Quit(0);
    }

    private void FailUiSmoke(string message)
    {
        ProfileStore.DeleteTestFiles(_profilePath);
        GD.PushError($"UI_SMOKE_FAIL: {message}");
        GetTree().Quit(1);
    }

    private static void ApplyBusVolume(string busName, float linearVolume)
    {
        int busIndex = AudioServer.GetBusIndex(busName);
        if (busIndex >= 0)
        {
            AudioServer.SetBusVolumeDb(busIndex, linearVolume <= 0.0001f ? -80.0f : Mathf.LinearToDb(linearVolume));
        }
    }

    private static void AddOption(OptionButton option, string label, int id)
    {
        option.AddItem(label, id);
    }

    private static void SelectById(OptionButton option, int id)
    {
        for (int index = 0; index < option.ItemCount; index++)
        {
            if (option.GetItemId(index) == id)
            {
                option.Select(index);
                return;
            }
        }

        option.Select(0);
    }

    private static int ResolutionId(int width, int height)
    {
        return (width, height) switch
        {
            (1600, 900) => 1,
            (1920, 1080) => 2,
            _ => 0,
        };
    }
}
