using chia.dotnet;
using chia.dotnet.wallet;
using dig.servercoin;

namespace Tests;

public class ApiTests
{
    private const ulong RESERVE_AMOUNT = 300000;

    [SkippableFact]
    public async Task ListCoins()
    {
        var x = Environment.GetEnvironmentVariable("CHIA_ROOT");
        Skip.If(Environment.GetEnvironmentVariable("CHIA_ROOT") is null);

        using CancellationTokenSource cts = new(100000);

        var config = Config.Open();
        var fullNodeEndpoint = config.GetEndpoint("full_node");
        var fullNode = new FullNodeProxy(new HttpRpcClient(fullNodeEndpoint), "wallet.tests");

        var walletEndpoint = config.GetEndpoint("wallet");
        var walletProxy = new WalletProxy(new HttpRpcClient(walletEndpoint), "wallet.tests");
        var keyStore = await KeyStore.CreateFrom(walletProxy);

        var wallet = new StandardWallet(fullNode, keyStore);
        await wallet.Sync(null, cts.Token);

        var serverCoinFactory = new ServerCoinFactory();
        var coins = await serverCoinFactory.GetServerCoins(fullNode, wallet, "26632ad912845e6f77714ca0996b21eb647803162dd444275ab5c09bea9426ea");
        Assert.NotEmpty(coins);
    }

    private static async Task<ulong> GetFee(FullNodeProxy node)
    {
        using CancellationTokenSource cts = new(10000);
        var fee = await node.GetFeeEstimate(RESERVE_AMOUNT, [5 * 60], cts.Token);

        return fee.estimates.First();
    }
}
