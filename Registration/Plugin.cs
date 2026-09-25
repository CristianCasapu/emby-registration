using System.Security.Cryptography;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;

namespace Registration;

/// <summary>
/// Inregistrare: vizitatorii isi cer singuri un cont pe o pagina publica, protejata
/// anti-roboti; contul porneste dezactivat, ascuns si fara drepturi de descarcare,
/// partajare sau administrare, si se activeaza dupa aprobarea adminului.
/// </summary>
public class Plugin : BasePlugin<PluginConfiguration>, IHasWebPages
{
    public static readonly Guid PluginId = new("f94669d3-60d5-4470-b709-8eda287b7b0c");

    public Plugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer)
        : base(applicationPaths, xmlSerializer)
    {
        Instance = this;
    }

    /// <summary>
    /// Cheia HMAC a tokenurilor de formular, generata la prima pornire. Nu in constructor:
    /// acolo Emby nu a setat inca calea fisierului de configurare.
    /// </summary>
    public void EnsureFormSecret()
    {
        if (string.IsNullOrEmpty(Configuration.FormSecret))
        {
            Configuration.FormSecret = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
            SaveConfiguration();
        }
    }

    public static Plugin? Instance { get; private set; }

    public static event EventHandler? ConfigurationChanged;

    public override string Name => "Înregistrare";

    public override string Description =>
        "Pagină publică pentru cereri de cont noi, cu protecție anti-roboți, aprobare de către admin și drepturi minime implicite.";

    public override Guid Id => PluginId;

    public string StoreDirectory => Path.Combine(DataFolderPath, "data");

    public string UpdatesDirectory => Path.Combine(DataFolderPath, "updates");

    /// <summary>Calea DLL-ului incarcat, pentru actualizare.</summary>
    public string AssemblyPath => AssemblyFilePath;

    public override void UpdateConfiguration(BasePluginConfiguration configuration)
    {
        // Pagina de configurare nu cunoaste cheia HMAC; o pastram pe cea existenta.
        if (configuration is PluginConfiguration incoming && string.IsNullOrEmpty(incoming.FormSecret))
        {
            incoming.FormSecret = Configuration.FormSecret;
        }

        base.UpdateConfiguration(configuration);
        ConfigurationChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Emby sterge DLL-ul; datele (cereri cu nume, e-mail, telefon) raman, daca adminul nu a
    /// cerut altfel. Conturile Emby create prin plugin raman conturi obisnuite.
    /// </summary>
    public override void OnUninstalling()
    {
        if (Configuration.DeleteDataOnUninstall)
        {
            try
            {
                if (Directory.Exists(DataFolderPath))
                {
                    Directory.Delete(DataFolderPath, recursive: true);
                }

                if (File.Exists(ConfigurationFilePath))
                {
                    File.Delete(ConfigurationFilePath);
                }
            }
            catch (IOException)
            {
                // Dezinstalarea continua; fisierele ramase se pot sterge manual (vezi README).
            }
        }

        base.OnUninstalling();
    }

    public IEnumerable<PluginPageInfo> GetPages() => new[]
    {
        new PluginPageInfo
        {
            Name = "registration",
            DisplayName = "Înregistrare",
            EmbeddedResourcePath = "Registration.Configuration.registration.html",
            IsMainConfigPage = true,
            EnableInMainMenu = true,
            MenuIcon = "person_add",
        },
        new PluginPageInfo { Name = "registrationjs", EmbeddedResourcePath = "Registration.Configuration.registration.js", IsMainConfigPage = false },
    };
}
