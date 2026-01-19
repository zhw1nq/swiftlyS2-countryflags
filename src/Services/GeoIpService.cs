using MaxMind.GeoIP2;
using MaxMind.GeoIP2.Exceptions;
using Microsoft.Extensions.Logging;
using SwiftlyS2.Shared;

namespace swiftlyS2_countryflags.Services;

public sealed class GeoIpService : IDisposable
{
    private readonly ISwiftlyCore _core;
    private readonly string _databasePath;
    private DatabaseReader? _reader;
    private bool _disposed;
    private bool _initialized;

    public bool DebugEnabled { get; set; }

    public GeoIpService(ISwiftlyCore core, string databasePath)
    {
        _core = core;
        _databasePath = databasePath;
    }

    public bool IsAvailable => _reader != null;

    public bool Initialize()
    {
        if (_initialized)
            return IsAvailable;

        _initialized = true;

        try
        {
            if (!File.Exists(_databasePath))
            {
                _core.Logger.LogWarning("[CountryFlags] GeoIP database not found: {Path}", _databasePath);
                _core.Logger.LogWarning("[CountryFlags] Download GeoLite2-Country.mmdb from MaxMind");
                return false;
            }

            _reader = new DatabaseReader(_databasePath);
            _core.Logger.LogInformation("[CountryFlags] GeoIP database loaded");
            return true;
        }
        catch (Exception ex)
        {
            _core.Logger.LogError("[CountryFlags] Failed to load GeoIP: {Error}", ex.Message);
            return false;
        }
    }

    public string? GetCountryCode(string? ipAddress)
    {
        if (string.IsNullOrEmpty(ipAddress) || _reader == null)
            return null;

        try
        {
            var ip = ExtractIpAddress(ipAddress);
            if (ip == null || IsPrivateIp(ip))
                return null;

            var response = _reader.Country(ip);
            return response.Country.IsoCode;
        }
        catch (AddressNotFoundException)
        {
            return null;
        }
        catch (GeoIP2Exception ex)
        {
            LogDebug("[CountryFlags] GeoIP error: {Error}", ex.Message);
            return null;
        }
        catch (Exception ex)
        {
            LogDebug("[CountryFlags] Error: {Error}", ex.Message);
            return null;
        }
    }

    private void LogDebug(string message, params object[] args)
    {
        if (DebugEnabled)
            _core.Logger.LogDebug(message, args);
    }

    private static string? ExtractIpAddress(string ipAddress)
    {
        var colonIndex = ipAddress.IndexOf(':');
        return colonIndex > 0 ? ipAddress[..colonIndex] : ipAddress;
    }

    private static bool IsPrivateIp(string ip)
    {
        return string.IsNullOrEmpty(ip) ||
               ip == "0.0.0.0" ||
               ip.StartsWith("127.") ||
               ip.StartsWith("192.168.") ||
               ip.StartsWith("10.") ||
               ip.StartsWith("172.16.") ||
               ip.StartsWith("172.17.") ||
               ip.StartsWith("172.18.") ||
               ip.StartsWith("172.19.") ||
               ip.StartsWith("172.20.") ||
               ip.StartsWith("172.21.") ||
               ip.StartsWith("172.22.") ||
               ip.StartsWith("172.23.") ||
               ip.StartsWith("172.24.") ||
               ip.StartsWith("172.25.") ||
               ip.StartsWith("172.26.") ||
               ip.StartsWith("172.27.") ||
               ip.StartsWith("172.28.") ||
               ip.StartsWith("172.29.") ||
               ip.StartsWith("172.30.") ||
               ip.StartsWith("172.31.");
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _reader?.Dispose();
        _reader = null;
    }
}
