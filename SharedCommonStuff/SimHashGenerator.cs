using System.Security.Cryptography;
using System.Text;

namespace SharedCommonStuff;

public class SimHashGenerator(int hashByteSize, Dictionary<ExtractedFeatureType, double> featureWeights)
{
    public byte[] GenerateSimHash(List<(ExtractedFeatureType featureType, string data)> extractedFeatures)
    {
        int[] hashBits = new int[hashByteSize * 8];

        foreach (var (featureType, data) in extractedFeatures)
        {
            byte[] hash = HashFeature(data);
            double weight = featureWeights.GetValueOrDefault(featureType, 1.0);

            for (int i = 0; i < hashByteSize * 8; i++)
            {
                if ((hash[i / 8] & (1 << (i % 8))) != 0)
                    hashBits[i] += (int)weight;
                else
                    hashBits[i] -= (int)weight;
            }
        }

        var simHash = new byte[hashByteSize];
        for (int i = 0; i < hashByteSize * 8; i++)
        {
            if (hashBits[i] > 0)
                simHash[i / 8] |= (byte)(1 << (i % 8));
        }

        return simHash;
    }

    private byte[] HashFeature(string 
        data)
    {
        byte[] hash = new byte[hashByteSize];
        SHA512.HashData(Encoding.UTF8.GetBytes(data), hash);
        //using var sha256 = SHA256.Create();
        //sha256.TryComputeHash(Encoding.UTF8.GetBytes(data), hash, out int _);
        //hash = Shake256.HashData(Encoding.UTF8.GetBytes(data), _hashByteSize * 8);
        return hash;
    }
    
    public static float GetSimilarity(byte[] simHash1, byte[] simHash2)
    {
        if (simHash1.Length != simHash2.Length)
            throw new ArgumentException("SimHashes must have the same length.");

        int hammingDistance = 0;
        for (int i = 0; i < simHash1.Length; i++)
        {
            byte xor = (byte)(simHash1[i] ^ simHash2[i]);
            hammingDistance += BitCount(xor);
        }

        int maxDistance = simHash1.Length * 8;
        float similarity = 1.0f - (float)hammingDistance / maxDistance;
        return similarity;
    }

    private static int BitCount(byte b)
    {
        int count = 0;
        while (b != 0)
        {
            count += b & 1;
            b >>= 1;
        }
        return count;
    }
}