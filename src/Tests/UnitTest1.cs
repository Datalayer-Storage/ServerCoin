using chia.dotnet;
using dig.servercoin;

namespace Tests;

public class UnitTest1
{
    [Fact]
    public void Test1()
    {
        var endpointInfo = new EndpointInfo();
        var serverCoinFactory = new ServerCoinFactory(endpointInfo);
        var coinSpend = serverCoinFactory.CreateCoinSpend();
        Assert.NotNull(coinSpend);
    }
}
