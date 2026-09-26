using MediaBrowser.Controller;
using MediaBrowser.Controller.Devices;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Plugins;
using MediaBrowser.Model.Activity;
using MediaBrowser.Model.Events;
using MediaBrowser.Model.Logging;
using Registration.Network;
using Registration.Notifications;
using Registration.Security;
using Registration.Storage;
using Registration.Updates;

namespace Registration.Flow;

/// <summary>Porneste plugin-ul: stocarea, paznicul de politica si intretinerea periodica.</summary>
public sealed class RegistrationHost : IServerEntryPoint
{
    private static readonly TimeSpan Tick = TimeSpan.FromMinutes(5);

    private readonly IUserManager _userManager;
    private readonly IServerApplicationHost _appHost;
    private readonly ILogger _logger;
    private readonly IActivityManager _activity;
    private readonly IDeviceManager _deviceManager;
    private Timer? _timer;
    private int _running;

    public RegistrationHost(IUserManager userManager, IServerApplicationHost appHost, IActivityManager activity, IDeviceManager deviceManager, ILogManager logManager)
    {
        _deviceManager = deviceManager;
        _userManager = userManager;
        _appHost = appHost;
        _activity = activity;
        _logger = logManager.GetLogger("Registration");
    }

    public static RegistrationManager? Manager { get; private set; }

    public static GitHubUpdater? Updater { get; private set; }

    public void Run()
    {
        var plugin = Plugin.Instance ?? throw new InvalidOperationException("Plugin-ul nu este incarcat.");
        plugin.EnsureFormSecret();
        var store = new RequestStore(plugin.StoreDirectory);
        var notifier = new AdminNotifier(_activity, _logger);
        var manager = new RegistrationManager(_userManager, _logger, store, notifier, new WebChecks(_logger))
        {
            DefaultServerName = _appHost.FriendlyName ?? "Emby",
            ServerId = _appHost.SystemId,
            Networks = new NetworkClassifier(_logger),
            DeviceManager = _deviceManager,
            PluginsDataRoot = Path.GetDirectoryName(plugin.DataFolderPath) ?? "/var/lib/emby/plugins",
        };
        manager.Guard = new PolicyGuard(_userManager, store, _logger);
        Manager = manager;
        Updater = new GitHubUpdater(plugin, _appHost, notifier, _logger);

        _userManager.UserPolicyUpdated += OnUserPolicyUpdated;
        _userManager.UserDeleted += OnUserDeleted;
        _timer = new Timer(_ => OnTick(), null, TimeSpan.FromMinutes(1), Tick);
    }

    private void OnUserPolicyUpdated(object? sender, GenericEventArgs<User> e) => Manager?.Guard?.OnPolicyUpdated(e.Argument);

    private void OnUserDeleted(object? sender, GenericEventArgs<User> e)
    {
        if (e.Argument != null)
        {
            Manager?.OnUserDeleted(e.Argument.Id, DateTimeOffset.UtcNow);
        }
    }

    private void OnTick()
    {
        if (Interlocked.Exchange(ref _running, 1) == 1)
        {
            return;
        }

        _ = Task.Run(async () =>
        {
            try
            {
                var now = DateTimeOffset.UtcNow;
                if (Manager != null)
                {
                    await Manager.MaintainAsync(now).ConfigureAwait(false);
                    Manager.Notifier.FlushIfDue(now);
                }

                if (Updater != null)
                {
                    await Updater.AutoCheckAsync(now).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                _logger.ErrorException("Inregistrare: intretinerea periodica a esuat", ex);
            }
            finally
            {
                Interlocked.Exchange(ref _running, 0);
            }
        });
    }

    public void Dispose()
    {
        _userManager.UserPolicyUpdated -= OnUserPolicyUpdated;
        _userManager.UserDeleted -= OnUserDeleted;
        _timer?.Dispose();
    }
}
