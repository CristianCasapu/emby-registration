using System.Collections.Concurrent;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Users;
using Registration.Storage;

namespace Registration.Flow;

/// <summary>
/// Paznicul de politica: la orice schimbare a politicii unui cont creat prin plugin (si
/// inca „gestionat”), readuce drepturile interzise la „nu” si tine contul dezactivat cat
/// timp cererea asteapta. Bibliotecile, Live TV-ul si limitele raman cum le pune adminul.
/// </summary>
public sealed class PolicyGuard
{
    private readonly IUserManager _userManager;
    private readonly RequestStore _store;
    private readonly ILogger _logger;

    // Actualizarile facute de noi declanseaza iar UserPolicyUpdated.
    private readonly ConcurrentDictionary<long, byte> _updating = new();

    public PolicyGuard(IUserManager userManager, RequestStore store, ILogger logger)
    {
        _userManager = userManager;
        _store = store;
        _logger = logger;
    }

    public void OnPolicyUpdated(User? user)
    {
        if (user != null && !_updating.ContainsKey(user.InternalId))
        {
            Enforce(user);
        }
    }

    public void Enforce(User user)
    {
        if (!(Plugin.Instance?.Configuration.EnforcePolicy ?? true))
        {
            return;
        }

        var id = user.Id.ToString("N");
        var record = _store.Read(d => d.Requests.FirstOrDefault(r => r.UserId == id));
        if (record == null || !record.Managed || record.Status is RequestStatus.Rejected or RequestStatus.Expired or RequestStatus.Deleted)
        {
            return;
        }

        try
        {
            var policy = _userManager.GetUserPolicy(user);
            var changed = AccessPolicy.ApplyForbidden(policy);
            if (RequestStatus.IsWaiting(record.Status) && !policy.IsDisabled)
            {
                policy.IsDisabled = true;
                changed = true;
            }

            if (changed)
            {
                Update(user, policy);
                _logger.Warn("Inregistrare: drepturile interzise ale contului {0} au fost retrase", user.Name);
            }
        }
        catch (Exception ex)
        {
            _logger.ErrorException("Inregistrare: politica contului {0} nu a putut fi verificata", ex, user.Name);
        }
    }

    public void Update(User user, UserPolicy policy)
    {
        _updating.TryAdd(user.InternalId, 0);
        try
        {
            _userManager.UpdateUserPolicy(user.InternalId, policy);
        }
        finally
        {
            _updating.TryRemove(user.InternalId, out _);
        }
    }

    /// <summary>
    /// Alte plugin-uri (ex. Headend) pot modifica politica in primele secunde dupa crearea
    /// contului; pentru un cont aprobat automat, reaplicam setarile dupa ce trec.
    /// </summary>
    public void ReapplyLater(long internalId)
    {
        _ = Task.Run(async () =>
        {
            await Task.Delay(TimeSpan.FromSeconds(20)).ConfigureAwait(false);
            var user = _userManager.GetUserById(internalId);
            if (user == null || Plugin.Instance == null)
            {
                return;
            }

            var policy = _userManager.GetUserPolicy(user);
            AccessPolicy.ApplyConfigured(policy, Plugin.Instance.Configuration);
            AccessPolicy.ApplyForbidden(policy);
            Update(user, policy);
        });
    }
}
