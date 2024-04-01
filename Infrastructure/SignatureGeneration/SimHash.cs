using System.Security.Cryptography;
using System.Text;
using FastHashes;

namespace Infrastructure.SignatureGeneration;


public static class SimHash
{
    // Change this to modify the HashSize. Be careful it won't be compatible out of the box
    // with how the database entries and the band logic is implemented
    private const int HashSize = 256;

    private const int Seed = 1337;

    /// <summary>
    /// First we create a vector for the amount of bit length we have.
    /// Then we go over each feature and sum each bit * weight and store the weight in the double[] vector.
    /// Finally, we go over each summed weight in vector and compute a final HashSize length byte Hash.
    /// For each weight greater zero we store a 1, else we store a zero.
    /// We use ulong for performance optimization, because of how the internals can handle 4-byte sequences better than
    /// just array of bytes.
    /// </summary>
    /// <param name="elementExtractedFeatures"></param>
    /// <param name="weights"></param>
    /// <returns></returns>
    public static ulong[] ComputeSimHash(List<(ExtractedFeatureType FeatureType, string Data)> elementExtractedFeatures,
        Dictionary<ExtractedFeatureType, double> weights)
    {
        var vector = new double[HashSize];
        foreach (var featuresWithWeight in elementExtractedFeatures)
        {
            var highwayHash = new HighwayHash256(1337);
            
            byte[] hash = highwayHash.ComputeHash(Encoding.UTF8.GetBytes(featuresWithWeight.Data).AsMemory().Span);
            // Go through each bit. Assume we use an 64-bit hash
            for(int j=0; j < HashSize; j++)
            {
                if ((hash[j / 8] & (1 << (j % 8))) != 0) // check if j-th bit is set
                    vector[j] += weights.GetValueOrDefault(featuresWithWeight.FeatureType, 1);
                else
                    vector[j] -= weights.GetValueOrDefault(featuresWithWeight.FeatureType, 1);
            }
        }

        ulong[] simhash = new ulong[HashSize/sizeof(ulong)];
        // Construct SimHash by setting bits for positive numbers
        for(int i=0; i < HashSize; i++)
        {
            if (vector[i] > 0.0)
                simhash[i / sizeof(ulong)] |= 1UL << (i % sizeof(ulong));
        }

        return simhash;
    }

    /// <summary>
    /// Calculates the Hamming Distance for two equally long hashes by counting the bits that are not the same
    /// by and-ing them together and counting the bits.
    /// </summary>
    /// <param name="hash1"></param>
    /// <param name="hash2"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentException"></exception>
    public static int HammingDistance(ulong[] hash1, ulong[] hash2)
    {
        if (hash1.Length != hash2.Length)
            throw new ArgumentException("Both hashes must be equal in size");
        
        int distance = 0;
        for (int i = 0; i < hash1.Length; i++)
        {
            distance += BitCount(hash1[i] ^ hash2[i]);
        }
        return distance;
    }
    
    /// <summary>
    /// Calculates how similar two hashes are and returns the result as a percentage between 0-100%
    /// </summary>
    /// <param name="hash1"></param>
    /// <param name="hash2"></param>
    /// <returns></returns>
    public static double SimilarityPercentage(ulong[] hash1, ulong[] hash2)
    {
        double hammingDistance = HammingDistance(hash1, hash2);
        double similarity = ((HashSize - hammingDistance) / HashSize) * 100;
        return similarity;
    }

    private static int BitCount(ulong n)
    {
        int count = 0;
        while (n != 0)
        {
            count++;
            n &= (n - 1);
        }
        return count;
    }
}