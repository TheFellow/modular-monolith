using System.Security.Cryptography;
using System.Text;
using Xunit;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace Mixology.Testing;

public sealed class RandomTestCaseOrderer : ITestCaseOrderer
{
    public IEnumerable<TTestCase> OrderTestCases<TTestCase>(IEnumerable<TTestCase> testCases)
        where TTestCase : ITestCase =>
        RandomTestOrder.Order(testCases, static testCase => testCase.UniqueID);
}

public sealed class RandomTestCollectionOrderer : ITestCollectionOrderer
{
    public IEnumerable<ITestCollection> OrderTestCollections(
        IEnumerable<ITestCollection> testCollections) =>
        RandomTestOrder.Order(testCollections, static collection => collection.UniqueID.ToString());
}

internal static class RandomTestOrder
{
    private const string SeedEnvironmentVariable = "MIXOLOGY_TEST_ORDER_SEED";

    private static readonly string Seed = CreateSeed();

    public static IEnumerable<T> Order<T>(IEnumerable<T> values, Func<T, string> identity) =>
        values.OrderBy(value => SortKey(identity(value)), StringComparer.Ordinal);

    private static string SortKey(string identity)
    {
        byte[] content = Encoding.UTF8.GetBytes($"{Seed}\n{identity}");
        return Convert.ToHexString(SHA256.HashData(content));
    }

    private static string CreateSeed()
    {
        string? configured = Environment.GetEnvironmentVariable(SeedEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return configured;
        }

        string generated = Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
        Console.Error.WriteLine($"{SeedEnvironmentVariable}={generated}");
        return generated;
    }
}
