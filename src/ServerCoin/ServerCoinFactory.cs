using chia.dotnet;
using chia.dotnet.bls;
using chia.dotnet.clvm;
using chia.dotnet.wallet;
using System.Numerics;

namespace dig.servercoin;

public class ServerCoinFactory
{
    private readonly FullNodeProxy _fullNode;
    private readonly StandardWallet _wallet;
    private readonly Program mirrorPuzzle = Puzzles.LoadPuzzle("p2_parent");
    private readonly Program curriedMirrorPuzzle;
    private readonly Program curriedMirrorPuzzleFromHex;

    public ServerCoinFactory(FullNodeProxy fullNode, StandardWallet wallet)
    {
        _fullNode = fullNode;
        _wallet = wallet;
        curriedMirrorPuzzle = mirrorPuzzle.Curry([Program.FromInt(1)]);
        curriedMirrorPuzzleFromHex = Program.FromHex(HexHelper.SanitizeHex(curriedMirrorPuzzle.HashHex()));
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

    public async Task<bool> DeleteServerCoin(string coinId, string genesisChallenge, ulong fee, CancellationToken token = default)
    {
        var coinRecordResponse = await _fullNode.GetCoinRecordByName(coinId, token);

        var puzzleSolution = await _fullNode.GetPuzzleAndSolution(
            coinRecordResponse.Coin.ParentCoinInfo,
            coinRecordResponse.ConfirmedBlockIndex
        );

        var revealProgram = Program.DeserializeHex(HexHelper.SanitizeHex(puzzleSolution.PuzzleReveal));
        var delegatedPuzzle = chia.dotnet.wallet.Puzzles.PayToConditions.Run(Program.FromList([Program.Nil])).Value;

        var standardTransactionInnerSolution = Program.FromList([
            Program.Nil,
            delegatedPuzzle,
            Program.Nil,
        ]);

        var coinRecords = _wallet.SelectCoinRecords(1 + fee, CoinSelection.Smallest);

        if (coinRecords.Count == 0)
        {
            throw new Exception("Insufficient balance");
        }

        var totalValue = BigInteger.Zero;
        foreach (var record in coinRecords)
        {
            totalValue += record.Coin.Amount;
        }

        var changeAmount = totalValue - fee;

        var coinSpends = coinRecords.Select((coinRecord, index) =>
        {
            var spentPuzzle = _wallet.PuzzleCache.First(puzzle => puzzle.HashHex() == HexHelper.SanitizeHex(coinRecord.Coin.PuzzleHash));
            var solution = new List<Program>();

            if (index == 0)
            {
                // Send the change to the same address
                solution.Add(
                    Program.FromSource(
                        $"({(int)ConditionCodes.CREATE_COIN} {HexHelper.FormatHex(coinRecord.Coin.PuzzleHash)} {changeAmount})"
                    )
                );
            }

            var coinSpend = new CoinSpend
            {
                Coin = coinRecord.Coin,
                PuzzleReveal = spentPuzzle.SerializeHex(),
                Solution = StandardTransaction.GetSolution(solution).SerializeHex(),
            };

            return coinSpend;
        }).ToList();

        var deleteCoinSpend = new CoinSpend
        {
            Coin = coinRecordResponse.Coin,
            PuzzleReveal = curriedMirrorPuzzle.SerializeHex(),
            Solution = Program.FromSource(
                $"({puzzleSolution.Coin.ParentCoinInfo} {revealProgram} {puzzleSolution.Coin.Amount} {standardTransactionInnerSolution})"
            ).SerializeHex(),
        };

        coinSpends.Add(deleteCoinSpend);

        var spendBundle = new SpendBundle
        {
            CoinSpends = coinSpends,
            AggregatedSignature = JacobianPoint.InfinityG2().ToHex(),
        };

        var signedSpendBundle = _wallet.SignSpend(spendBundle, genesisChallenge.FromHex());

        return await _fullNode.PushTx(signedSpendBundle);
    }

    public async Task<IEnumerable<ServerCoin>> GetServerCoins(string launcherId, CancellationToken token = default)
    {
        var launcher = Program.FromHex(launcherId);
        var hint = Program.FromBigInt(launcher.ToBigInt() + 1)
            .ToHex()
            .PadLeft(64, '0')[..64];
        var coinRecords = await _fullNode.GetCoinRecordsByHint(hint, false, cancellationToken: token);
        var servers = new List<ServerCoin>();

        foreach (var coinRecord in coinRecords)
        {
            var puzzleSolution = await _fullNode.GetPuzzleAndSolution(
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
                condition.First.ToInt() == (int)ConditionCodes.CREATE_COIN
            ).ToList();

            var urlString = createCoinConditions.Select(condition => condition.Rest.Rest.Rest.First.Rest);
            var urls = urlString.First().ToList().Select(url => url.ToText());
            var ourPuzzle = _wallet.PuzzleCache.FirstOrDefault(puzzle => puzzle.Equals(revealProgram));

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
