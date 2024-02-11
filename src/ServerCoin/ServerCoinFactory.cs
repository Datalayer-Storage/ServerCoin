using chia.dotnet;

namespace ServerCoin;

public class ServerCoinFactory(EndpointInfo fullNodeHost)
{
    private readonly EndpointInfo _fullNodeHost = fullNodeHost;

    public CoinSpend CreateCoinSpend()
    {
        return new CoinSpend();
    }
}
