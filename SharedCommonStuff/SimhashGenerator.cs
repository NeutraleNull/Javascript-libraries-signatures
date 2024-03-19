using System;
using System.Buffers;
using System.Collections.Generic;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;

namespace SharedCommonStuff;

public class SimHashGenerator(Dictionary<ExtractedFeatureType, double> categoryWeights, int window, int shingleOverlap, double cutoff)
{
    private const int HashSize = 512;
    private const int HashByteSize = 512 / 8;
    private void UpdateBitCounts(Span<byte> hashBytes, double[] bitCounts, double weight)
    {
        for (int i = 0; i < HashSize; i++)
        {
            if ((hashBytes[i / 8] & (1 << (i % 8))) != 0)
            {
                double newWeight = bitCounts[i] + (int)weight;
                bitCounts[i] = Math.Min(newWeight, cutoff);
            }
            else
                bitCounts[i] -= (int)weight;
        }
    }

    public byte[] Generate(List<(ExtractedFeatureType Type, string Data)> features)
    {
        double[] vector = ArrayPool<double>.Shared.Rent(HashSize);
        int traverse = Math.Max(window - shingleOverlap, 0);
        
        try
        {
            for (int k = 0; k < features.Count; k+=traverse)
            {
                foreach (var shingle in features.Skip(traverse).Take(window))
                {
                    double weight = categoryWeights.GetValueOrDefault(shingle.Type, 1.0);
                    Span<byte> hash = stackalloc byte[HashByteSize];

                    CalculateHash(shingle.Data, hash, SHA512.Create());

                    UpdateBitCounts(hash, vector, weight);
                }
            }

            byte[] simHash = new byte[HashSize / 8];
            for (int i = 0; i < HashSize; i++)
            {
                if (vector[i] > 0)
                    simHash[i / 8] |= (byte)(1 << (i % 8));
            }

            return simHash;
        }
        finally
        {
            ArrayPool<double>.Shared.Return(vector);
        }
    }

    public static int CalculateHammingDistance(byte[] simHash1, byte[] simHash2)
    {
        int distance = 0;
        for (int i = 0; i < simHash1.Length; i++)
        {
            byte diff = (byte)(simHash1[i] ^ simHash2[i]);
            distance += CountBits(diff);
        }
        return distance;
    }
    
    public static double CalculateSimilarity(byte[] simHash1, byte[] simHash2)
    {
        int hammingDistance = CalculateHammingDistance(simHash1, simHash2);
        double similarity = 1.0 - (double)hammingDistance / HashSize;
        return similarity;
    }

    private static int CountBits(byte b)
    {
        int count = 0;
        while (b != 0)
        {
            count += b & 1;
            b >>= 1;
        }
        return count;
    }
    private void CalculateHash(string value, Span<byte> hash, HashAlgorithm hashAlgorithm)
    {
        hashAlgorithm.TryComputeHash(Encoding.UTF8.GetBytes(value), hash, out _);
    }
}