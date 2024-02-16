using chia.dotnet.clvm;
using System.Reflection;

namespace dig.servercoin;

public static class Puzzles
{
    public static Program LoadPuzzle(string puzzleName)
    {
        var assemblyLocation = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? throw new InvalidOperationException();
        var fullFilePath = Path.Combine(assemblyLocation, $"puzzles/{puzzleName}.clsp.hex");
        var hex = File.ReadAllText(fullFilePath);
        return Program.FromHex(hex);
    }
}
