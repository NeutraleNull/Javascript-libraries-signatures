
using System.Security.Cryptography;
using System.Text;
using MathNet.Numerics.Random;

namespace Infrastructure.SignatureGeneration;

public static class MinHash
{
    private const int NumberHashFunctions = 256;
    private const int Seed = 1337;
    
    public static int[] ComputeMinHash(List<string> features)
    {
        var rng = new Random(Seed);
        
        // Create an array of size numHashFunctions to store minimum hash results for each hash function
        int[] minHashes = new int[NumberHashFunctions];
        for (var i = 0; i < minHashes.Length; i++)
        {
            // Initialize the array elements to a large value
            minHashes[i] = int.MaxValue;
        }

        foreach (var feature in features)
        {
            for (var i = 0; i < NumberHashFunctions; i++)
            {
                var seedNumber = rng.Next();
                var hash = SHA256.HashData(Encoding.UTF8.GetBytes(feature));
                var hashedValue = BitConverter.ToInt32(hash, 0) ^ seedNumber;

                if (hashedValue < minHashes[i])
                {
                    minHashes[i] = hashedValue;
                }
            }
        }

        return minHashes;
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