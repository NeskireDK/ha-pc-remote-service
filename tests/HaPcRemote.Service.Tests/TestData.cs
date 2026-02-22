namespace HaPcRemote.Service.Tests;

internal static class TestData
{
    public static string Load(string fileName) =>
        File.ReadAllText(Path.Combine(
            Path.GetDirectoryName(typeof(TestData).Assembly.Location)!,
            "TestData", fileName));
}
