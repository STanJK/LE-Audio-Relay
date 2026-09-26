using LEAudioRelay.Lifecycle;
using LEAudioRelay.Settings;
using LEAudioRelay.Supervision;
using LEAudioRelay.Telemetry;
using System.Drawing;

namespace LEAudioRelay.Shell;

internal sealed class TrayApplicationContext :
    ApplicationContext
{
    private readonly RelayConfiguration _configuration =
        new();

    private readonly LifecycleEventLog _lifecycleLog;
    private readonly PowerObserver _powerObserver;
    private readonly BackendSupervisor _supervisor;

    private readonly Control _uiDispatcher;
    private readonly ContextMenuStrip _menu;
    private readonly NotifyIcon _notifyIcon;

    private readonly ToolStripMenuItem _statusItem;

    private readonly ToolStripMenuItem _gameEffectsItem;
    private readonly ToolStripMenuItem _gameMediaItem;
    private readonly ToolStripMenuItem _mediaItem;
    private readonly ToolStripMenuItem _defaultItem;

    public TrayApplicationContext()
    {
        _uiDispatcher =
            new Control();

        _uiDispatcher.CreateControl();

        _lifecycleLog =
            new LifecycleEventLog();

        _powerObserver =
            new PowerObserver();

        _supervisor =
            new BackendSupervisor(
                _configuration,
                _lifecycleLog);

        _powerObserver.Changed +=
            OnPowerChanged;

        _supervisor.StateChanged +=
            OnSupervisorStateChanged;

        _supervisor.UpdatePowerSnapshot(
            _powerObserver.Snapshot);

        _lifecycleLog.Write(
            "APP_START",
            $"version={LifecycleEventLog.ApplicationVersion}; " +
            $"pid={Environment.ProcessId}");

        _menu =
            new ContextMenuStrip();

        _statusItem =
            new ToolStripMenuItem
            {
                Enabled = false
            };

        var modeMenu =
            new ToolStripMenuItem(
                "Mode");

        _gameEffectsItem =
            CreateModeItem(
                "GameEffects",
                RelayMode.GameEffects);

        _gameMediaItem =
            CreateModeItem(
                "GameMedia",
                RelayMode.GameMedia);

        _mediaItem =
            CreateModeItem(
                "Media",
                RelayMode.Media);

        _defaultItem =
            CreateModeItem(
                "Default / unset",
                RelayMode.Default);

        modeMenu.DropDownItems.AddRange(
            [
                _gameEffectsItem,
                _gameMediaItem,
                _mediaItem,
                _defaultItem
            ]);

        var restartItem =
            new ToolStripMenuItem(
                "Restart audio route");

        restartItem.Click +=
            (_, _) =>
                _supervisor.RequestRestart();

        var exitItem =
            new ToolStripMenuItem(
                "Exit");

        exitItem.Click +=
            (_, _) =>
                ExitApplication();

        _menu.Items.Add(
            _statusItem);

        _menu.Items.Add(
            new ToolStripSeparator());

        _menu.Items.Add(
            modeMenu);

        _menu.Items.Add(
            restartItem);

        _menu.Items.Add(
            new ToolStripSeparator());

        _menu.Items.Add(
            exitItem);

        _notifyIcon =
            new NotifyIcon
            {
                Icon = SystemIcons.Application,
                Text = "LE Audio Relay",
                ContextMenuStrip = _menu,
                Visible = true
            };

        _notifyIcon.DoubleClick +=
            (_, _) =>
                _supervisor.RequestRestart();

        UpdateModeChecks();
        UpdateStatus();

        _supervisor.Start();
    }

    private ToolStripMenuItem CreateModeItem(
        string text,
        RelayMode mode)
    {
        var item =
            new ToolStripMenuItem(text)
            {
                CheckOnClick = false
            };

        item.Click +=
            (_, _) =>
            {
                _supervisor.SetMode(
                    mode);

                UpdateModeChecks();
            };

        return item;
    }

    private void UpdateModeChecks()
    {
        _gameEffectsItem.Checked =
            _configuration.Mode ==
            RelayMode.GameEffects;

        _gameMediaItem.Checked =
            _configuration.Mode ==
            RelayMode.GameMedia;

        _mediaItem.Checked =
            _configuration.Mode ==
            RelayMode.Media;

        _defaultItem.Checked =
            _configuration.Mode ==
            RelayMode.Default;
    }

    private void OnPowerChanged(
        object? sender,
        EventArgs e)
    {
        _supervisor.UpdatePowerSnapshot(
            _powerObserver.Snapshot);
    }

    private void OnSupervisorStateChanged(
        object? sender,
        EventArgs e)
    {
        if (_uiDispatcher.IsDisposed)
        {
            return;
        }

        if (_uiDispatcher.InvokeRequired)
        {
            _uiDispatcher.BeginInvoke(
                UpdateStatus);

            return;
        }

        UpdateStatus();
    }

    private void UpdateStatus()
    {
        string backend =
            _supervisor.ActiveBackendGeneration
                is long generation
                ? $"Backend gen: {generation}"
                : "Backend gen: none";

        string stateText =
            _supervisor.State switch
            {
                SupervisorState.Suspended =>
                    "Windows suspended",

                SupervisorState.WaitingForEndpoint =>
                    $"Waiting for {_configuration.DestinationMatch}",

                SupervisorState.TopologyBlocked =>
                    _supervisor.LastError ??
                    "Audio topology blocked",

                SupervisorState.RecoveringFault =>
                    _supervisor.LastError is string error
                        ? $"Recovering: {error}"
                        : "Recovering audio route",

                _ =>
                    _supervisor.State.ToString()
            };

        _statusItem.Text =
            $"{stateText} | " +
            $"{backend} | " +
            $"Mode: {_configuration.Mode}";

        _notifyIcon.Text =
            _supervisor.State switch
            {
                SupervisorState.Running =>
                    "LE Audio Relay - Running",

                SupervisorState.Suspended =>
                    "LE Audio Relay - Suspended",

                SupervisorState.WaitingForEndpoint =>
                    "LE Audio Relay - Waiting for Buds",

                SupervisorState.TopologyBlocked =>
                    "LE Audio Relay - Audio topology blocked",

                SupervisorState.Starting =>
                    "LE Audio Relay - Starting audio route",

                SupervisorState.RestartRequested or
                SupervisorState.Restarting =>
                    "LE Audio Relay - Restarting audio route",

                SupervisorState.RecoveringFault =>
                    "LE Audio Relay - Recovering audio route",

                SupervisorState.Stopped =>
                    "LE Audio Relay - Stopped",

                _ =>
                    "LE Audio Relay"
            };
    }

    private void ExitApplication()
    {
        _notifyIcon.Visible =
            false;

        ExitThread();
    }

    protected override void Dispose(
        bool disposing)
    {
        if (disposing)
        {
            _powerObserver.Changed -=
                OnPowerChanged;

            _supervisor.StateChanged -=
                OnSupervisorStateChanged;

            _supervisor.Dispose();
            _powerObserver.Dispose();

            _lifecycleLog.Write(
                "APP_EXIT",
                $"pid={Environment.ProcessId}");

            _notifyIcon.Visible =
                false;

            _notifyIcon.Dispose();
            _menu.Dispose();
            _uiDispatcher.Dispose();
        }

        base.Dispose(
            disposing);
    }
}
