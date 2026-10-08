using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Sharp.Shared;

namespace BulletIgnore;

/// <summary>
/// Standalone entry point for dedicated CT-human zombie servers.
/// It does not automatically detect game modes or map names.
/// </summary>
public sealed class BulletPassThroughModule : IModSharpModule
{
    private readonly ISharedSystem _shared;
    private readonly ILogger<BulletPassThroughModule> _logger;
    private readonly string _configPath;
    private PluginConfig _config = new();
    private BulletPassThroughRuntime? _runtime;
    private volatile bool _enabled;

    public BulletPassThroughModule(
        ISharedSystem sharedSystem,
        string dllPath,
        string sharpPath,
        Version version,
        IConfiguration coreConfiguration,
        bool hotReload)
    {
        _shared = sharedSystem;
        _logger = sharedSystem.GetLoggerFactory().CreateLogger<BulletPassThroughModule>();
        _configPath = Path.Combine(sharpPath, "configs", "bullet-ignore.json");
    }

    public string DisplayName => "BulletIgnore (CT-only)";
    public string DisplayAuthor => "BulletIgnore contributors";

    public bool Init()
    {
        try
        {
            // Missing configuration is deliberately disabled, never implicitly enabled.
            _config = File.Exists(_configPath)
                ? PluginConfig.Parse(File.ReadAllText(_configPath))
                : new PluginConfig();
            return true;
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Invalid BulletIgnore configuration; refusing to enable.");
            return false;
        }
    }

    public void PostInit()
    {
        if (!_config.Enabled)
        {
            _logger.LogInformation("BulletIgnore disabled. Configure {Path} to opt in on a CT-human zombie server.", _configPath);
            return;
        }

        _runtime = new BulletPassThroughRuntime(
            _shared, _shared.GetEntityManager(), () => _enabled, _logger);
        _enabled = true;
        if (!_runtime.Activate())
        {
            _enabled = false;
            _runtime.Dispose();
            _runtime = null;
            _logger.LogError("BulletIgnore is inactive; native bullet behavior remains in effect.");
        }
    }

    public void Shutdown()
    {
        _enabled = false;
        _runtime?.Dispose();
        _runtime = null;
    }
}
