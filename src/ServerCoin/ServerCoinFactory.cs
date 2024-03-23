using chia.dotnet;
using chia.dotnet.bls;
using chia.dotnet.clvm;
using chia.dotnet.wallet;
using System.Numerics;

namespace dig.servercoin;

public class ServerCoinFactory
{
    private readonly Program mirrorPuzzle = Puzzles.LoadPuzzle("p2_parent");
    private readonly Program curriedMirrorPuzzle;
    private readonly Program curriedMirrorPuzzleFromHex;

    public ServerCoinFactory()
    {
        curriedMirrorPuzzle = mirrorPuzzle.Curry([Program.FromInt(1)]);
        var curriedMirrorPuzzleHex = HexHelper.SanitizeHex(curriedMirrorPuzzle.HashHex());
        curriedMirrorPuzzleFromHex = Program.FromHex(curriedMirrorPuzzleHex);
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

        return null;
    }

    public async Task<IEnumerable<ServerCoin>> GetServerCoins(FullNodeProxy fullNode, StandardWallet wallet, string launcherId, CancellationToken token = default)
    {
        var launcher = Program.FromHex(launcherId);
        var hint = Program.FromBigInt(launcher.ToBigInt() + 1)
            .ToHex()
            .PadLeft(64, '0')[..64];
        var coinRecords = await fullNode.GetCoinRecordsByHint(hint, true, cancellationToken: token);
        var servers = new List<ServerCoin>();

        foreach (var coinRecord in coinRecords)
        {
            var puzzleSolution = await fullNode.GetPuzzleAndSolution(
                coinRecord.Coin.ParentCoinInfo,
                coinRecord.ConfirmedBlockIndex,
                token
            );

            var revealProgram = Program.DeserializeHex(HexHelper.SanitizeHex(puzzleSolution.PuzzleReveal));
            var solutionProgram = Program.DeserializeHex(HexHelper.SanitizeHex(puzzleSolution.Solution));
            var conditions = revealProgram.Run(solutionProgram).Value;
            var createCoinConditions = conditions.ToList().Where(condition =>
                condition.ToList().Count == 4 &&
                condition.Rest.First.Equals(curriedMirrorPuzzleFromHex) &&
                condition.First.ToInt() == 51
            ).ToList();

            var urlString = createCoinConditions.Select(condition => condition.Rest.Rest.Rest.First.Rest);
            var urls = urlString.First().ToList().Select(url => url.ToText());
            var ourPuzzle = wallet.PuzzleCache.FirstOrDefault(puzzle => puzzle.Equals(revealProgram));

            servers.Add(new ServerCoin
            {
                Amount = coinRecord.Coin.Amount,
                CoinId = HexHelper.SanitizeHex(HexHelper.FormatHex(coinRecord.Coin.CoinId.ToHex())),
                LauncherId = HexHelper.SanitizeHex(HexHelper.FormatHex(launcher.ToHex())),
                Ours = ourPuzzle != null,
                Urls = urls,
            });
        }
        return servers;
    }
}
