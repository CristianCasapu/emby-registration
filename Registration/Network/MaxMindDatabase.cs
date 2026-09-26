using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Registration.Network;

/// <summary>
/// Copiat din plugin-ul Jurnal de acces (emby-access-log). Cititor pentru fisierele MaxMind DB (.mmdb), ca GeoLite2-City si GeoLite2-ASN, dupa
/// specificatia publica https://maxmind.github.io/MaxMind-DB/. Scris aici ca plugin-ul sa
/// ramana un singur .dll (Emby nu incarca dependinte ale plugin-urilor).
///
/// Fisierul se tine in memorie (GeoLite2-City are ~60 MB); cautarile sunt fara blocare.
/// </summary>
public sealed class MaxMindDatabase
{
    private static readonly byte[] MetadataMarker = { 0xAB, 0xCD, 0xEF, (byte)'M', (byte)'a', (byte)'x', (byte)'M', (byte)'i', (byte)'n', (byte)'d', (byte)'.', (byte)'c', (byte)'o', (byte)'m' };

    private readonly byte[] _data;
    private readonly int _recordSize;
    private readonly long _nodeCount;
    private readonly long _dataStart;
    private readonly long _ipv4Start;

    public MaxMindDatabase(byte[] data)
    {
        _data = data;
        var marker = LastIndexOf(data, MetadataMarker);
        if (marker < 0)
        {
            throw new InvalidDataException("Fisierul nu e o baza MaxMind DB (lipseste marcajul de metadate).");
        }

        long offset = marker + MetadataMarker.Length;

        // Pointerii din metadate sunt relativi la inceputul metadatelor.
        if (Decode(ref offset, offset) is not Dictionary<string, object?> metadata)
        {
            throw new InvalidDataException("Metadatele bazei MaxMind DB sunt invalide.");
        }

        _nodeCount = Convert.ToInt64(metadata["node_count"]);
        _recordSize = Convert.ToInt32(metadata["record_size"]);
        IpVersion = Convert.ToInt32(metadata["ip_version"]);
        DatabaseType = metadata.TryGetValue("database_type", out var type) ? type as string ?? string.Empty : string.Empty;
        BuildDate = metadata.TryGetValue("build_epoch", out var epoch) ? DateTimeOffset.FromUnixTimeSeconds(Convert.ToInt64(epoch)) : null;
        if (_recordSize is not (24 or 28 or 32))
        {
            throw new InvalidDataException("Dimensiune de inregistrare nesuportata: " + _recordSize);
        }

        _dataStart = (_nodeCount * _recordSize / 4) + 16;

        // Adresele IPv4 stau intr-un arbore IPv6 dupa 96 de biti zero (::a.b.c.d).
        _ipv4Start = 0;
        if (IpVersion == 6)
        {
            for (var i = 0; i < 96 && _ipv4Start < _nodeCount; i++)
            {
                _ipv4Start = ReadRecord(_ipv4Start, 0);
            }
        }
    }

    public static MaxMindDatabase Open(string path) => new(File.ReadAllBytes(path));

    public string DatabaseType { get; }

    public int IpVersion { get; }

    public DateTimeOffset? BuildDate { get; }

    /// <summary>Inregistrarea pentru adresa (harta cu campurile MaxMind), sau null daca lipseste.</summary>
    public Dictionary<string, object?>? Find(IPAddress address)
    {
        if (address.IsIPv4MappedToIPv6)
        {
            address = address.MapToIPv4();
        }

        var bytes = address.GetAddressBytes();
        if (address.AddressFamily == AddressFamily.InterNetworkV6 && IpVersion == 4)
        {
            return null;
        }

        var node = address.AddressFamily == AddressFamily.InterNetwork && IpVersion == 6 ? _ipv4Start : 0;
        var bits = bytes.Length * 8;
        for (var i = 0; i < bits && node < _nodeCount; i++)
        {
            var bit = (bytes[i >> 3] >> (7 - (i % 8))) & 1;
            node = ReadRecord(node, bit);
        }

        if (node == _nodeCount)
        {
            return null;
        }

        if (node < _nodeCount)
        {
            throw new InvalidDataException("Arborele de cautare MaxMind DB e incomplet.");
        }

        var offset = _dataStart + (node - _nodeCount - 16);
        return Decode(ref offset, _dataStart) as Dictionary<string, object?>;
    }

    private long ReadRecord(long node, int side)
    {
        var bytesPerNode = _recordSize / 4;
        var start = checked((int)(node * bytesPerNode));
        switch (_recordSize)
        {
            case 24:
                return side == 0
                    ? (_data[start] << 16) | (_data[start + 1] << 8) | _data[start + 2]
                    : (_data[start + 3] << 16) | (_data[start + 4] << 8) | _data[start + 5];
            case 28:
                // Bitii de sus ai ambelor inregistrari stau in octetul din mijloc.
                var middle = _data[start + 3];
                return side == 0
                    ? ((middle & 0xF0) << 20) | (_data[start] << 16) | (_data[start + 1] << 8) | _data[start + 2]
                    : ((middle & 0x0F) << 24) | (_data[start + 4] << 16) | (_data[start + 5] << 8) | _data[start + 6];
            default:
                return BinaryPrimitives.ReadUInt32BigEndian(_data.AsSpan(start + (side * 4), 4));
        }
    }

    /// <summary>Decodeaza o valoare din sectiunea de date; <paramref name="offset"/> ajunge dupa ea.</summary>
    private object? Decode(ref long offset, long pointerBase)
    {
        var control = _data[offset++];
        var type = control >> 5;
        if (type == 0)
        {
            type = 7 + _data[offset++];
        }

        if (type == 1)
        {
            var target = pointerBase + DecodePointer(control, ref offset);
            return Decode(ref target, pointerBase);
        }

        var size = control & 0x1F;
        if (size == 29)
        {
            size = 29 + _data[offset++];
        }
        else if (size == 30)
        {
            size = 285 + ((_data[offset] << 8) | _data[offset + 1]);
            offset += 2;
        }
        else if (size == 31)
        {
            size = 65821 + ((_data[offset] << 16) | (_data[offset + 1] << 8) | _data[offset + 2]);
            offset += 3;
        }

        object? value;
        switch (type)
        {
            case 2:
                value = Encoding.UTF8.GetString(_data, (int)offset, size);
                offset += size;
                return value;
            case 3:
                value = BinaryPrimitives.ReadDoubleBigEndian(_data.AsSpan((int)offset, 8));
                offset += 8;
                return value;
            case 4:
                value = _data.AsSpan((int)offset, size).ToArray();
                offset += size;
                return value;
            case 5:
            case 6:
            case 8:
            case 9:
            case 10:
                ulong number = 0;
                for (var i = 0; i < size; i++)
                {
                    number = (number << 8) | _data[offset + i];
                }

                offset += size;
                return type == 8 ? unchecked((int)number) : number;
            case 7:
                var map = new Dictionary<string, object?>(size, StringComparer.Ordinal);
                for (var i = 0; i < size; i++)
                {
                    var key = Decode(ref offset, pointerBase) as string ?? string.Empty;
                    map[key] = Decode(ref offset, pointerBase);
                }

                return map;
            case 11:
                var list = new List<object?>(size);
                for (var i = 0; i < size; i++)
                {
                    list.Add(Decode(ref offset, pointerBase));
                }

                return list;
            case 14:
                return size != 0;
            case 15:
                value = BinaryPrimitives.ReadSingleBigEndian(_data.AsSpan((int)offset, 4));
                offset += 4;
                return value;
            default:
                throw new InvalidDataException("Tip de date MaxMind DB necunoscut: " + type);
        }
    }

    private long DecodePointer(int control, ref long offset)
    {
        var size = (control >> 3) & 0x3;
        var high = control & 0x7;
        long pointer;
        switch (size)
        {
            case 0:
                pointer = (high << 8) | _data[offset];
                break;
            case 1:
                pointer = ((high << 16) | (_data[offset] << 8) | _data[offset + 1]) + 2048;
                break;
            case 2:
                pointer = ((high << 24) | (_data[offset] << 16) | (_data[offset + 1] << 8) | _data[offset + 2]) + 526336;
                break;
            default:
                pointer = BinaryPrimitives.ReadUInt32BigEndian(_data.AsSpan((int)offset, 4));
                break;
        }

        offset += size + 1;
        return pointer;
    }

    private static int LastIndexOf(byte[] data, byte[] pattern)
    {
        // Metadatele sunt in ultimii 128 KB.
        var stop = Math.Max(0, data.Length - (128 * 1024));
        for (var i = data.Length - pattern.Length; i >= stop; i--)
        {
            if (data.AsSpan(i, pattern.Length).SequenceEqual(pattern))
            {
                return i;
            }
        }

        return -1;
    }
}
