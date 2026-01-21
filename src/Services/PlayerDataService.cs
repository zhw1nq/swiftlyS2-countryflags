using swiftlyS2_countryflags.Models;
using System.Collections.Concurrent;
using System.Text.Json;

namespace swiftlyS2_countryflags.Services;

public sealed class PlayerDataService : IDisposable
{
    private readonly string _dataPath;
    private readonly ConcurrentDictionary<ulong, PlayerData> _playerData = new();
    private readonly SemaphoreSlim _fileLock = new(1, 1);
    
    private volatile bool _hasDirtyData;
    private bool _disposed;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public PlayerDataService(string dataPath)
    {
        _dataPath = dataPath;
    }

    public int Count => _playerData.Count;

    public PlayerData GetOrCreate(ulong steamId, bool defaultShowStatus)
    {
        return _playerData.GetOrAdd(steamId, _ => new PlayerData
        {
            ShowFlag = defaultShowStatus,
            CountryCode = string.Empty,
            LastFetch = DateTime.MinValue
        });
    }

    public bool TryGetPlayer(ulong steamId, out PlayerData? data)
    {
        return _playerData.TryGetValue(steamId, out data);
    }

    public void MarkDirty() => _hasDirtyData = true;

    public void SaveIfDirty()
    {
        if (!_hasDirtyData)
            return;

        _hasDirtyData = false;
        _ = SaveAsync();
    }

    public async Task LoadAsync()
    {
        if (!File.Exists(_dataPath))
            return;

        var json = await File.ReadAllTextAsync(_dataPath).ConfigureAwait(false);
        
        if (string.IsNullOrEmpty(json))
            return;

        var data = JsonSerializer.Deserialize<Dictionary<ulong, PlayerData>>(json, JsonOptions);
        if (data == null)
            return;

        foreach (var (steamId, playerData) in data)
        {
            _playerData.TryAdd(steamId, playerData);
        }
    }

    public async Task SaveAsync()
    {
        if (_disposed)
            return;

        if (!await _fileLock.WaitAsync(1000).ConfigureAwait(false))
            return;

        try
        {
            var directory = Path.GetDirectoryName(_dataPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var snapshot = _playerData.ToDictionary(
                kvp => kvp.Key, 
                kvp => new PlayerData
                {
                    ShowFlag = kvp.Value.ShowFlag,
                    CountryCode = kvp.Value.CountryCode,
                    LastFetch = kvp.Value.LastFetch
                }
            );

            var json = JsonSerializer.Serialize(snapshot, JsonOptions);
            await File.WriteAllTextAsync(_dataPath, json).ConfigureAwait(false);
        }
        finally
        {
            _fileLock.Release();
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        
        if (_hasDirtyData)
        {
            _hasDirtyData = false;
            try
            {
                // Use Task.Run to avoid potential deadlock with synchronization context
                Task.Run(async () => await SaveAsync()).Wait(TimeSpan.FromSeconds(5));
            }
            catch (Exception)
            {
                // Ignore save errors during dispose - best effort
            }
        }
        
        _fileLock.Dispose();
    }
}
