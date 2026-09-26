using LEAudioRouter.Settings;
using LEAudioRouter.Supervision;
using System.Drawing;

namespace LEAudioRouter.Shell;

internal sealed class TrayApplicationContext : ApplicationContext
{
    private readonly DesiredRouterState _desired = new();
    private readonly BackendSupervisor _supervisor;
    private readonly Control _uiDispatcher;
    private readonly ContextMenuStrip _menu;
    private readonly NotifyIcon _notifyIcon;

    private readonly ToolStripMenuItem _statusItem;
    private readonly ToolStripMenuItem _enabledItem;
    private readonly ToolStripMenuItem _autoReconnectItem;

    private readonly ToolStripMenuItem _gameEffectsItem;
    private readonly ToolStripMenuItem _gameMediaItem;
    private readonly ToolStripMenuItem _mediaItem;
    private readonly ToolStripMenuItem _defaultItem;

    public TrayApplicationContext()
    {
        _uiDispatcher = new Control();
        _uiDispatcher.CreateControl();

        _supervisor = new BackendSupervisor(_desired);
        _supervisor.StateChanged += OnSupervisorStateChanged;

        _menu = new ContextMenuStrip();

        _statusItem = new ToolStripMenuItem
        {
            Enabled = false
        };

        _enabledItem = new ToolStripMenuItem("Router enabled")
        {
            CheckOnClick = true,
            Checked = _desired.Enabled
        };
        _enabledItem.CheckedChanged += (_, _) =>
            _supervisor.SetEnabled(_enabledItem.Checked);

        var modeMenu = new ToolStripMenuItem("Mode");

        _gameEffectsItem = CreateModeItem(
            "GameEffects",
            RouterMode.GameEffects);

        _gameMediaItem = CreateModeItem(
            "GameMedia",
            RouterMode.GameMedia);

        _mediaItem = CreateModeItem(
            "Media",
            RouterMode.Media);

        _defaultItem = CreateModeItem(
            "Default / unset",
            RouterMode.Default);

        modeMenu.DropDownItems.AddRange(
            [
                _gameEffectsItem,
                _gameMediaItem,
                _mediaItem,
                _defaultItem
            ]);

        _autoReconnectItem = new ToolStripMenuItem("Auto reconnect")
        {
            CheckOnClick = true,
            Checked = _desired.AutoReconnect
        };
        _autoReconnectItem.CheckedChanged += (_, _) =>
            _supervisor.SetAutoReconnect(_autoReconnectItem.Checked);

        var restartItem = new ToolStripMenuItem("Restart audio route");
        restartItem.Click += (_, _) => _supervisor.RequestRestart();

        var exitItem = new ToolStripMenuItem("Exit");
        exitItem.Click += (_, _) => ExitApplication();

        _menu.Items.Add(_statusItem);
        _menu.Items.Add(new ToolStripSeparator());
        _menu.Items.Add(_enabledItem);
        _menu.Items.Add(modeMenu);
        _menu.Items.Add(_autoReconnectItem);
        _menu.Items.Add(restartItem);
        _menu.Items.Add(new ToolStripSeparator());
        _menu.Items.Add(exitItem);

        _notifyIcon = new NotifyIcon
        {
            Icon = SystemIcons.Application,
            Text = "LE Audio Router",
            ContextMenuStrip = _menu,
            Visible = true
        };

        _notifyIcon.DoubleClick += (_, _) =>
            _supervisor.RequestRestart();

        UpdateModeChecks();
        UpdateStatus();

        _supervisor.Start();
    }

    private ToolStripMenuItem CreateModeItem(
        string text,
        RouterMode mode)
    {
        var item = new ToolStripMenuItem(text)
        {
            CheckOnClick = false
        };

        item.Click += (_, _) =>
        {
            _supervisor.SetMode(mode);
            UpdateModeChecks();
        };

        return item;
    }

    private void UpdateModeChecks()
    {
        _gameEffectsItem.Checked =
            _desired.Mode == RouterMode.GameEffects;

        _gameMediaItem.Checked =
            _desired.Mode == RouterMode.GameMedia;

        _mediaItem.Checked =
            _desired.Mode == RouterMode.Media;

        _defaultItem.Checked =
            _desired.Mode == RouterMode.Default;
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
            _uiDispatcher.BeginInvoke(UpdateStatus);
            return;
        }

        UpdateStatus();
    }

    private void UpdateStatus()
    {
        _statusItem.Text =
            $"State: {_supervisor.State} | " +
            $"Mode: {_desired.Mode} | " +
            $"Restart gen: {_supervisor.RestartGeneration}";

        _notifyIcon.Text =
            _supervisor.State switch
            {
                SupervisorState.Running =>
                    "LE Audio Router - Running",

                SupervisorState.RestartRequested =>
                    "LE Audio Router - Restart requested",

                SupervisorState.Stopped =>
                    "LE Audio Router - Stopped",

                _ =>
                    "LE Audio Router - Shell active"
            };
    }

    private void ExitApplication()
    {
        _notifyIcon.Visible = false;
        ExitThread();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _supervisor.StateChanged -= OnSupervisorStateChanged;
            _supervisor.Dispose();

            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
            _menu.Dispose();
            _uiDispatcher.Dispose();
        }

        base.Dispose(disposing);
    }
}
