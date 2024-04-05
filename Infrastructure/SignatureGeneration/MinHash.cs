using System.Text;
using FastHashes;

namespace Infrastructure.SignatureGeneration;

public static class MinHash
{
    private const int NumHashFunctions = 128;
    private const int Seed = 1337;
    private static readonly int[][] Coefficients;

    // compute once and cache
    static MinHash()
    {
        Random random = new Random(Seed);
        Coefficients = new int[NumHashFunctions][];

        for (int i = 0; i < NumHashFunctions; i++)
        {
            Coefficients[i] = new int[] { random.Next(1, int.MaxValue), random.Next(0, int.MaxValue) };
        }
    }

    /// <summary>
    /// Computes the minhash by first generating a hash (using the cached hash functions) for each feature in
    /// the list and then updating the output vector to use the minimum. 
    /// </summary>
    /// <param name="features"></param>
    /// <returns></returns>
    public static int[] ComputeMinHash(List<string> features)
    {
        int[] minHashValues = Enumerable.Repeat(int.MaxValue, NumHashFunctions).ToArray();

        foreach (string feature in features)
        {
            int[] hashValues = GetHashValues(feature);

            for (int i = 0; i < NumHashFunctions; i++)
            {
                minHashValues[i] = Math.Min(minHashValues[i], hashValues[i]);
            }
        }

        return minHashValues;
    }

    /// <summary>
    /// Calculates the hash value using murmurhash. The coefficients are required to "randomise" the hash functions.
    /// </summary>
    /// <param name="feature"></param>
    /// <returns></returns>
    private static int[] GetHashValues(string feature)
    {
        int[] hashValues = new int[NumHashFunctions];
        var featureBytes = Encoding.UTF8.GetBytes(feature).AsMemory();

        for (int i = 0; i < NumHashFunctions; i++)
        {
            int a = Coefficients[i][0];
            int b = Coefficients[i][1];
            var murmurHashGenerator = new MurmurHash32(Seed);
            var hash = BitConverter.ToInt32(murmurHashGenerator.ComputeHash(featureBytes.Span));
            //uint hash = MurmurHash3(featureBytes, Seed);
            hashValues[i] = (int)((a * hash + b) % uint.MaxValue);
        }

        return hashValues;
    }

    public static double GetSimilarity(int[] minHashes1, int[] minHashes2)
    {
        if (minHashes1.Length != minHashes2.Length)
        {
            throw new ArgumentException("Both hash arrays must be of the same length");
        }

        int equalCount = 0;

        for(int i = 0; i < minHashes1.Length; i++)
        {
            if(minHashes1[i] == minHashes2[i])
            {
                equalCount++;
            }
        }

        return (double)equalCount / minHashes1.Length * 100;
    }
}