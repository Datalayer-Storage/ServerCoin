using chia.dotnet;
using chia.dotnet.clvm;

namespace dig.servercoin;


public class ServerCoinFactory(EndpointInfo fullNodeHost)
{
    private readonly Program mirrorPuzzle = Puzzles.LoadPuzzle("p2_parent");
    private readonly EndpointInfo _fullNodeHost = fullNodeHost;

    public CoinSpend CreateCoinSpend()
    {
        return new CoinSpend();
    }
}
