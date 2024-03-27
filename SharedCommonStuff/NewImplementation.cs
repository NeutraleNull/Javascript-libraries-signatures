namespace SharedCommonStuff;

public class SimHashLSH
{
    private readonly int _hashByteSize;
    private readonly int _numBands;
    private readonly int _numRowsPerBand;
    private readonly Dictionary<byte[], List<byte[]>> _buckets;

    public SimHashLSH(int hashByteSize, int numBands)
    {
        _hashByteSize = hashByteSize;
        _numBands = numBands;
        _numRowsPerBand = _hashByteSize / _numBands;
        _buckets = new Dictionary<byte[], List<byte[]>>(new ByteArrayComparer());
    }

    public void IndexSimHash(byte[] simHash)
    {
        for (int i = 0; i < _numBands; i++)
        {
            byte[] bandHash = ExtractBandHash(simHash, i);
            if (!_buckets.ContainsKey(bandHash))
                _buckets[bandHash] = new List<byte[]>();
            _buckets[bandHash].Add(simHash);
        }
    }

    public List<byte[]> FindCandidates(byte[] querySimHash)
    {
        HashSet<byte[]> candidates = new HashSet<byte[]>(new ByteArrayComparer());

        for (int i = 0; i < _numBands; i++)
        {
            byte[] bandHash = ExtractBandHash(querySimHash, i);
            if (_buckets.TryGetValue(bandHash, out var bucket))
                candidates.UnionWith(bucket);
        }

        return candidates.ToList();
    }

    private byte[] ExtractBandHash(byte[] simHash, int bandIndex)
    {
        int startByte = bandIndex * _numRowsPerBand;
        return simHash.Skip(startByte).Take(_numRowsPerBand).ToArray();
    }

    private class ByteArrayComparer : IEqualityComparer<byte[]>
    {
        public bool Equals(byte[]? x, byte[]? y)
        {
            if (x == null || y == null)
                return x == y;
            return x.SequenceEqual(y);
        }

        public int GetHashCode(byte[]? obj)
        {
            return obj == null ? 0 : obj.Sum(b => b);
        }
    }
}