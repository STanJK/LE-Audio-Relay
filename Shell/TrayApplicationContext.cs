using LEAudioRouter.Settings;
using LEAudioRouter.Supervision;
using System.Drawing;

namespace LEAudioRouter.Shell;

internal sealed class TrayApplicationContext :
    ApplicationContext
{
    private readonly RouterConfiguration _configuration =
        new();

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
        _uiDispatcher = new Control();
        _uiDispatcher.CreateControl();

        _supervisor =
            new BackendSupervisor(
                _configuration);

        _supervisor.StateChanged +=
            OnSupervisorStateChanged;

        _menu = new ContextMenuStrip();

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
                RouterMode.GameEffects);

        _gameMediaItem =
            CreateModeItem(
                "GameMedia",
                RouterMode.GameMedia);

        _mediaItem =
            CreateModeItem(
                "Media",
                RouterMode.Media);

        _defaultItem =
            CreateModeItem(
                "Default / unset",
                RouterMode.Default);

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
                Text = "LE Audio Router",
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
        RouterMode mode)
    {
        var item =
            new ToolStripMenuItem(text)
            {
                CheckOnClick = false
            };

        item.Click +=
            (_, _) =>
            {
                _supervisor.SetMode(mode);
                UpdateModeChecks();
            };

        return item;
    }

    private void UpdateModeChecks()
    {
        _gameEffectsItem.Checked =
            _configuration.Mode ==
            RouterMode.GameEffects;

        _gameMediaItem.Checked =
            _configuration.Mode ==
            RouterMode.GameMedia;

        _mediaItem.Checked =
            _configuration.Mode ==
            RouterMode.Media;

        _defaultItem.Checked =
            _configuration.Mode ==
            RouterMode.Default;
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

        _statusItem.Text =
            $"State: {_supervisor.State} | " +
            $"{backend} | " +
            $"Mode: {_configuration.Mode}";

        _notifyIcon.Text =
            _supervisor.State switch
            {
                SupervisorState.Running =>
                    "LE Audio Router - Running",

                SupervisorState.Starting =>
                    "LE Audio Router - Starting backend",

                SupervisorState.RestartRequested or
                SupervisorState.Restarting =>
                    "LE Audio Router - Restarting backend",

                SupervisorState.Faulted =>
                    "LE Audio Router - Recovering backend",

                SupervisorState.Stopped =>
                    "LE Audio Router - Stopped",

                _ =>
                    "LE Audio Router"
            };
    }

    private void ExitApplication()
    {
        _notifyIcon.Visible = false;
        ExitThread();
    }

    protected override void Dispose(
        bool disposing)
    {
        if (disposing)
        {
            _supervisor.StateChanged -=
                OnSupervisorStateChanged;

            _supervisor.Dispose();

            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
            _menu.Dispose();
            _uiDispatcher.Dispose();
        }

        base.Dispose(disposing);
    }
}
