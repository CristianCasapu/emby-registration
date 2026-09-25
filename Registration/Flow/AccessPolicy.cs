using MediaBrowser.Model.Users;

namespace Registration.Flow;

/// <summary>
/// Drepturile conturilor create prin plugin. Drepturile „interzise” (administrare,
/// descarcare, partajare, stergere...) nu depind de configuratie: nu exista cale de cod
/// care sa le dea. Bibliotecile, Live TV si limitele vin din configuratie.
/// </summary>
public static class AccessPolicy
{
    /// <summary>Politica completa pentru un cont nou; dezactivat pana la aprobare.</summary>
    public static UserPolicy Build(PluginConfiguration settings, bool disabled)
    {
        var policy = new UserPolicy
        {
            IsDisabled = disabled,
            EnableUserPreferenceAccess = true,
            EnableRemoteAccess = true,
            EnableMediaPlayback = true,
            EnableAudioPlaybackTranscoding = true,
            EnableVideoPlaybackTranscoding = true,
            EnablePlaybackRemuxing = true,
            EnableAllChannels = true,
            EnableAllDevices = true,
        };

        ApplyConfigured(policy, settings);
        ApplyForbidden(policy);
        return policy;
    }

    /// <summary>Partea care vine din setari: biblioteci, Live TV, limite.</summary>
    public static void ApplyConfigured(UserPolicy policy, PluginConfiguration settings)
    {
        policy.EnableAllFolders = settings.AllLibraries;
        policy.EnabledFolders = settings.AllLibraries ? Array.Empty<string>() : settings.Libraries.ToArray();
        policy.EnableLiveTvAccess = settings.EnableLiveTv;
        policy.SimultaneousStreamLimit = Math.Max(0, settings.StreamLimit);
        policy.RemoteClientBitrateLimit = Math.Max(0, settings.RemoteBitrateLimitMbps) * 1_000_000;
    }

    /// <summary>Readuce la „nu” tot ce nu are voie un cont gestionat; true daca a schimbat ceva.</summary>
    public static bool ApplyForbidden(UserPolicy policy)
    {
        var changed = false;

        void Set(bool current, bool wanted, Action<bool> setter)
        {
            if (current != wanted)
            {
                setter(wanted);
                changed = true;
            }
        }

        Set(policy.IsAdministrator, false, v => policy.IsAdministrator = v);
        Set(policy.IsHidden, true, v => policy.IsHidden = v);
        Set(policy.IsHiddenRemotely, true, v => policy.IsHiddenRemotely = v);
        Set(policy.IsHiddenFromUnusedDevices, true, v => policy.IsHiddenFromUnusedDevices = v);
        Set(policy.EnableContentDownloading, false, v => policy.EnableContentDownloading = v);
        Set(policy.EnableSyncTranscoding, false, v => policy.EnableSyncTranscoding = v);
        Set(policy.EnableMediaConversion, false, v => policy.EnableMediaConversion = v);
        Set(policy.EnablePublicSharing, false, v => policy.EnablePublicSharing = v);
        Set(policy.AllowSharingPersonalItems, false, v => policy.AllowSharingPersonalItems = v);
        Set(policy.AllowCameraUpload, false, v => policy.AllowCameraUpload = v);
        Set(policy.EnableContentDeletion, false, v => policy.EnableContentDeletion = v);
        Set(policy.EnableSubtitleManagement, false, v => policy.EnableSubtitleManagement = v);
        Set(policy.EnableSubtitleDownloading, false, v => policy.EnableSubtitleDownloading = v);
        Set(policy.EnableLiveTvManagement, false, v => policy.EnableLiveTvManagement = v);
        Set(policy.EnableRemoteControlOfOtherUsers, false, v => policy.EnableRemoteControlOfOtherUsers = v);
        Set(policy.EnableSharedDeviceControl, false, v => policy.EnableSharedDeviceControl = v);

        if (policy.EnableContentDeletionFromFolders is { Length: > 0 })
        {
            policy.EnableContentDeletionFromFolders = Array.Empty<string>();
            changed = true;
        }

        return changed;
    }
}
