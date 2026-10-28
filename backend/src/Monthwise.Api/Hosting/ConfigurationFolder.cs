namespace Monthwise.Api.Hosting;

/// <summary>
/// The configuration folder of an installation (Docker, native or Azure): non-secret settings in
/// <c>settings.json</c> and one file per secret in <c>secrets/</c>, never inside the release.
/// </summary>
internal static class ConfigurationFolder
{
    public const string EnvironmentVariable = "MONTHWISE_CONFIG_DIR";
    public const string CommandLineKey = "config-dir";
    public const string SettingsFileName = "settings.json";
    public const string SecretsFolderName = "secrets";

    /// <summary>
    /// Adds the folder given by <c>--config-dir</c> or <c>MONTHWISE_CONFIG_DIR</c>, when set. A file
    /// <c>secrets/SensitiveData__HashKey</c> becomes the setting <c>SensitiveData:HashKey</c>.
    /// Its values win over appsettings, environment variables and the command line.
    /// </summary>
    public static ConfigurationManager AddConfigurationFolder(this ConfigurationManager configuration)
    {
        var folder = configuration[CommandLineKey] ?? Environment.GetEnvironmentVariable(EnvironmentVariable);
        if (string.IsNullOrWhiteSpace(folder))
        {
            return configuration;
        }

        folder = Path.GetFullPath(folder);
        configuration.AddJsonFile(Path.Combine(folder, SettingsFileName), optional: true, reloadOnChange: false);
        configuration.AddKeyPerFile(Path.Combine(folder, SecretsFolderName), optional: true);
        return configuration;
    }
}
