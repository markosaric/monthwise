using Microsoft.Extensions.Configuration;
using Monthwise.Api.Hosting;

namespace Monthwise.Api.Tests;

/// <summary>Settings and secrets read from an installation's configuration folder (FR-5, FR-6).</summary>
public sealed class ConfigurationFolderTests : IDisposable
{
    private readonly string _folder = Directory.CreateTempSubdirectory("monthwise-config-").FullName;

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    [Fact]
    public void Reads_settings_json_and_one_file_per_secret()
    {
        File.WriteAllText(Path.Combine(_folder, "settings.json"), """{ "Database": { "MigrateOnStartup": true } }""");
        var secrets = Directory.CreateDirectory(Path.Combine(_folder, "secrets")).FullName;
        File.WriteAllText(Path.Combine(secrets, "SensitiveData__HashKey"), "made-up-hash-key");
        File.WriteAllText(Path.Combine(secrets, "ConnectionStrings__Monthwise"), "Host=db;Password=made-up\n");

        var configuration = new ConfigurationManager();
        configuration["config-dir"] = _folder;
        configuration["SensitiveData:HashKey"] = "overridden";
        configuration.AddConfigurationFolder();

        Assert.Equal("True", configuration["Database:MigrateOnStartup"]);
        Assert.Equal("made-up-hash-key", configuration["SensitiveData:HashKey"]);
        Assert.Equal("Host=db;Password=made-up", configuration.GetConnectionString("Monthwise"));
    }

    [Fact]
    public void A_folder_without_files_adds_nothing()
    {
        var configuration = new ConfigurationManager();
        configuration["config-dir"] = _folder;

        configuration.AddConfigurationFolder();

        Assert.Null(configuration["SensitiveData:HashKey"]);
    }
}
