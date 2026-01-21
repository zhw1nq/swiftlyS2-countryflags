using Microsoft.Extensions.Logging;
using SwiftlyS2.Shared;
using System.Net;
using System.Text;

namespace swiftlyS2_countryflags.Services;

/// <summary>
/// Minimal MaxMind MMDB reader for country lookups only.
/// This avoids the MaxMind.GeoIP2 NuGet package which crashes on Linux during assembly load.
/// </summary>
public sealed class GeoIpService : IDisposable
{
    private readonly ISwiftlyCore _core;
    private readonly string _databasePath;
    private MmdbReader? _reader;
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

            var fileInfo = new FileInfo(_databasePath);
            if (fileInfo.Length < 1024)
            {
                _core.Logger.LogError("[CountryFlags] GeoIP database appears corrupted (too small): {Size} bytes", fileInfo.Length);
                return false;
            }

            LogDebug("[CountryFlags] Loading GeoIP database: {Path} ({Size:F2} MB)", 
                _databasePath, fileInfo.Length / 1024.0 / 1024.0);

            _reader = new MmdbReader(_databasePath);
            
            _core.Logger.LogInformation("[CountryFlags] GeoIP database loaded successfully (nodes: {Nodes}, record size: {Size})", 
                _reader.NodeCount, _reader.RecordSize);
            return true;
        }
        catch (Exception ex)
        {
            _core.Logger.LogError("[CountryFlags] Failed to load GeoIP: {Error}", ex.Message);
            LogDebug("[CountryFlags] Stack trace: {Stack}", ex.StackTrace ?? "N/A");
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

            return _reader.GetCountryCode(ip);
        }
        catch (Exception ex)
        {
            LogDebug("[CountryFlags] Error getting country: {Error}", ex.Message);
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

/// <summary>
/// Minimal MMDB file reader that only extracts country ISO codes.
/// Based on MaxMind DB format specification: https://maxmind.github.io/MaxMind-DB/
/// </summary>
internal sealed class MmdbReader : IDisposable
{
    private readonly byte[] _data;
    private readonly int _metadataStart;
    public int NodeCount { get; }
    public int RecordSize { get; }
    private readonly int _nodeByteSize;
    private readonly int _searchTreeSize;
    private readonly int _dataSectionStart;
    private readonly int _ipVersion;

    // Metadata marker: 0xABCDEF followed by "MaxMind.com"
    private static readonly byte[] MetadataMarker = { 
        0xAB, 0xCD, 0xEF, 
        0x4D, 0x61, 0x78, 0x4D, 0x69, 0x6E, 0x64, 0x2E, 0x63, 0x6F, 0x6D 
    };
    
    public MmdbReader(string path)
    {
        _data = File.ReadAllBytes(path);
        
        // Find metadata marker by searching from end of file
        _metadataStart = FindMetadataMarker();
        if (_metadataStart < 0)
            throw new InvalidDataException("Invalid MMDB file - metadata marker not found");
        
        // Parse metadata - it starts right after the marker
        var metaOffset = _metadataStart + MetadataMarker.Length;
        var metadata = DecodeValue(ref metaOffset);
        
        if (metadata is not Dictionary<string, object> meta)
            throw new InvalidDataException($"Invalid MMDB metadata type: {metadata?.GetType().Name ?? "null"}");
        
        // Extract required fields
        NodeCount = GetMetaInt(meta, "node_count");
        RecordSize = GetMetaInt(meta, "record_size");
        _ipVersion = GetMetaInt(meta, "ip_version");
        
        _nodeByteSize = RecordSize * 2 / 8;
        _searchTreeSize = NodeCount * _nodeByteSize;
        _dataSectionStart = _searchTreeSize + 16; // 16-byte null separator
    }

    private int GetMetaInt(Dictionary<string, object> meta, string key)
    {
        if (!meta.TryGetValue(key, out var value))
            throw new InvalidDataException($"Missing metadata key: {key}. Available keys: {string.Join(", ", meta.Keys)}");
        return Convert.ToInt32(value);
    }

    private int FindMetadataMarker()
    {
        // Search from the end (metadata is at the end of the file)
        for (var i = _data.Length - MetadataMarker.Length; i >= Math.Max(0, _data.Length - 131072); i--)
        {
            var match = true;
            for (var j = 0; j < MetadataMarker.Length && match; j++)
            {
                if (_data[i + j] != MetadataMarker[j])
                    match = false;
            }
            if (match) return i;
        }
        return -1;
    }

    public string? GetCountryCode(string ipString)
    {
        if (!IPAddress.TryParse(ipString, out var ip))
            return null;

        var bytes = ip.GetAddressBytes();
        
        // Handle IPv4 addresses in IPv6 database
        if (_ipVersion == 6 && bytes.Length == 4)
        {
            var ipv6Bytes = new byte[16];
            ipv6Bytes[10] = 0xFF;
            ipv6Bytes[11] = 0xFF;
            Array.Copy(bytes, 0, ipv6Bytes, 12, 4);
            bytes = ipv6Bytes;
        }

        // Walk the search tree
        var node = 0;
        var bitCount = bytes.Length * 8;

        for (var i = 0; i < bitCount && node < NodeCount; i++)
        {
            var bit = (bytes[i >> 3] >> (7 - (i & 7))) & 1;
            node = ReadNode(node, bit);
        }

        if (node == NodeCount)
            return null; // Not found

        if (node > NodeCount)
        {
            // Calculate data section offset
            var dataOffset = (node - NodeCount) - 16 + _dataSectionStart;
            if (dataOffset >= 0 && dataOffset < _metadataStart)
            {
                var result = DecodeValue(ref dataOffset);
                return ExtractCountryCode(result);
            }
        }

        return null;
    }

    private int ReadNode(int nodeNumber, int bit)
    {
        var offset = nodeNumber * _nodeByteSize;
        
        if (offset + _nodeByteSize > _searchTreeSize)
            return NodeCount; // Invalid node
        
        return RecordSize switch
        {
            24 => bit == 0
                ? (_data[offset] << 16) | (_data[offset + 1] << 8) | _data[offset + 2]
                : (_data[offset + 3] << 16) | (_data[offset + 4] << 8) | _data[offset + 5],
            28 => bit == 0
                ? ((_data[offset + 3] >> 4) << 24) | (_data[offset] << 16) | (_data[offset + 1] << 8) | _data[offset + 2]
                : ((_data[offset + 3] & 0x0F) << 24) | (_data[offset + 4] << 16) | (_data[offset + 5] << 8) | _data[offset + 6],
            32 => bit == 0
                ? (_data[offset] << 24) | (_data[offset + 1] << 16) | (_data[offset + 2] << 8) | _data[offset + 3]
                : (_data[offset + 4] << 24) | (_data[offset + 5] << 16) | (_data[offset + 6] << 8) | _data[offset + 7],
            _ => throw new InvalidDataException($"Unsupported record size: {RecordSize}")
        };
    }

    private object? DecodeValue(ref int offset)
    {
        if (offset >= _data.Length)
            return null;
            
        var ctrlByte = _data[offset++];
        var type = ctrlByte >> 5;
        
        // Extended type
        if (type == 0)
        {
            if (offset >= _data.Length) return null;
            type = _data[offset++] + 7;
        }

        // Decode size
        var size = ctrlByte & 0x1F;
        if (size >= 29 && type != 1) // Not for pointers
        {
            size = DecodeSize(ref offset, size);
        }

        return type switch
        {
            1 => DecodePointer(ref offset, ctrlByte), // pointer
            2 => DecodeString(ref offset, size), // UTF-8 string  
            3 => DecodeDouble(ref offset), // double
            4 => DecodeBytes(ref offset, size), // bytes
            5 => DecodeUInt16(ref offset, size), // uint16
            6 => DecodeUInt32(ref offset, size), // uint32
            7 => DecodeMap(ref offset, size), // map
            8 => DecodeInt32(ref offset, size), // int32
            9 => DecodeUInt64(ref offset, size), // uint64
            10 => DecodeUInt128(ref offset, size), // uint128
            11 => DecodeArray(ref offset, size), // array
            14 => false, // boolean false
            15 => true, // boolean true
            _ => null // unknown
        };
    }

    private int DecodeSize(ref int offset, int sizeIndicator)
    {
        return sizeIndicator switch
        {
            29 => 29 + _data[offset++],
            30 => 285 + ((_data[offset++] << 8) | _data[offset++]),
            31 => 65821 + ((_data[offset++] << 16) | (_data[offset++] << 8) | _data[offset++]),
            _ => sizeIndicator
        };
    }

    private object? DecodePointer(ref int offset, byte ctrlByte)
    {
        var pointerSize = ((ctrlByte >> 3) & 0x3);
        var pointerValue = ctrlByte & 0x7;

        var pointer = pointerSize switch
        {
            0 => (pointerValue << 8) | _data[offset++],
            1 => ((pointerValue << 16) | (_data[offset++] << 8) | _data[offset++]) + 2048,
            2 => ((pointerValue << 24) | (_data[offset++] << 16) | (_data[offset++] << 8) | _data[offset++]) + 526336,
            3 => (_data[offset++] << 24) | (_data[offset++] << 16) | (_data[offset++] << 8) | _data[offset++],
            _ => 0
        };

        var ptrOffset = _dataSectionStart + pointer;
        return DecodeValue(ref ptrOffset);
    }

    private string DecodeString(ref int offset, int size)
    {
        var str = Encoding.UTF8.GetString(_data, offset, size);
        offset += size;
        return str;
    }

    private double DecodeDouble(ref int offset)
    {
        var bytes = new byte[8];
        Array.Copy(_data, offset, bytes, 0, 8);
        if (BitConverter.IsLittleEndian)
            Array.Reverse(bytes);
        offset += 8;
        return BitConverter.ToDouble(bytes, 0);
    }

    private byte[] DecodeBytes(ref int offset, int size)
    {
        var bytes = new byte[size];
        Array.Copy(_data, offset, bytes, 0, size);
        offset += size;
        return bytes;
    }

    private int DecodeUInt16(ref int offset, int size)
    {
        var value = 0;
        for (var i = 0; i < size; i++)
            value = (value << 8) | _data[offset++];
        return value;
    }

    private long DecodeUInt32(ref int offset, int size)
    {
        long value = 0;
        for (var i = 0; i < size; i++)
            value = (value << 8) | _data[offset++];
        return value;
    }

    private long DecodeInt32(ref int offset, int size)
    {
        return DecodeUInt32(ref offset, size);
    }

    private ulong DecodeUInt64(ref int offset, int size)
    {
        ulong value = 0;
        for (var i = 0; i < size; i++)
            value = (value << 8) | _data[offset++];
        return value;
    }

    private object DecodeUInt128(ref int offset, int size)
    {
        // Just return as byte array for simplicity
        return DecodeBytes(ref offset, size);
    }

    private Dictionary<string, object> DecodeMap(ref int offset, int count)
    {
        var map = new Dictionary<string, object>();
        
        for (var i = 0; i < count; i++)
        {
            var key = DecodeValue(ref offset);
            var value = DecodeValue(ref offset);
            
            if (key is string keyStr && value != null)
                map[keyStr] = value;
        }

        return map;
    }

    private List<object?> DecodeArray(ref int offset, int count)
    {
        var list = new List<object?>();
        
        for (var i = 0; i < count; i++)
            list.Add(DecodeValue(ref offset));

        return list;
    }

    private static string? ExtractCountryCode(object? data)
    {
        if (data is not Dictionary<string, object> map)
            return null;

        // Try "country" first
        if (map.TryGetValue("country", out var countryObj) && countryObj is Dictionary<string, object> country)
        {
            if (country.TryGetValue("iso_code", out var isoCode) && isoCode is string code)
                return code;
        }

        // Fall back to "registered_country"
        if (map.TryGetValue("registered_country", out var regCountryObj) && regCountryObj is Dictionary<string, object> regCountry)
        {
            if (regCountry.TryGetValue("iso_code", out var isoCode) && isoCode is string code)
                return code;
        }

        return null;
    }

    public void Dispose()
    {
        // No unmanaged resources
    }
}
