using chia.dotnet;
using chia.dotnet.clvm;
using System.Numerics;

namespace dig.servercoin;

public class ServerCoinFactory
{
    private readonly Program mirrorPuzzle = Puzzles.LoadPuzzle("p2_parent");
    private readonly Program curriedMirrorPuzzle;

    public ServerCoinFactory()
    {
        curriedMirrorPuzzle = mirrorPuzzle.Curry([Program.FromInt(1)]);
    }

    public Coin CreateServerCoin(Program launcherId, IEnumerable<CoinRecord> coinRecords, ulong amount, ulong fee)
    {
        var hint = Program.FromBigInt(launcherId.ToBigInt() + 1)
            .ToHex()
            .PadLeft(64, '0')[..64];

        var totalValue = BigInteger.Zero;
        foreach (var coinRecord in coinRecords)
        {
            totalValue += coinRecord.Coin.Amount;
        }
        var changeAmount = totalValue - fee - amount;

    }
    public CoinSpend CreateCoinSpend()
    {
        return new CoinSpend();
    }
}
