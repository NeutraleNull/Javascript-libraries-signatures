using System.Reflection;
using MinHashSharp;

namespace SharedCommonStuff;

public class MinHashGenerator
{
    public static MinHash GenerateMinHash(List<(ExtractedFeatureType type, string data)> input, int numberPermutations)
    {
        var hash = new MinHash(256, 1337);
        hash.Update(input.Select(x => x.data).ToArray());
        return hash;
    }
}

public static class MinHashExtension
{
    public static byte[] GetByteHash(this MinHash minHash)
    {
        var values = minHash.HashValues(0, 1024);
        var outputHash = new byte[values.Length * sizeof(uint)];
        Buffer.BlockCopy(values, 0, outputHash, 0, outputHash.Length);
        return outputHash;
    }

    public static MinHash GetMinHashFromBytes(this MinHash minHash, int randomNumberPermutations, byte[] input)
    {
        var minhash = new MinHash(randomNumberPermutations, 1337);

        // Get the field information for the _hashValues field
        var hashValuesField = minhash.GetType().GetField("_hashValues", BindingFlags.NonPublic | BindingFlags.Instance);
        
        var uintArray = new uint[input.Length / 4];
        Buffer.BlockCopy(input, 0, uintArray, 0, input.Length);
        
        // Set the value of the _hashValues field using reflection
        hashValuesField?.SetValue(minHash, uintArray);
        return minhash;
    }
}