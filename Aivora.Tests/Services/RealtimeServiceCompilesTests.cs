using Aivora.Services.RealtimeService;
using Moq;
using Xunit;

namespace Aivora.Tests.Services;

public class RealtimeServiceCompilesTests
{
    [Fact]
    public void IService_IsMockable()
    {
        var mock = new Mock<IService>();
        Assert.NotNull(mock.Object);
    }
}
