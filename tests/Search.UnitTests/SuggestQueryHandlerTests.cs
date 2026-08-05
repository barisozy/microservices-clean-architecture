using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using Search.Application;
using Shouldly;
using Xunit;

namespace Search.UnitTests;

public class SuggestQueryHandlerTests
{
    [Fact]
    public async Task Handle_CallsRepositorySuggestAsync()
    {
        var repositoryMock = new Mock<ISearchReadRepository>();
        var suggestions = new List<string> { "phone", "iphone" };
        repositoryMock.Setup(x => x.SuggestAsync("ph", It.IsAny<CancellationToken>()))
            .ReturnsAsync(suggestions);

        var handler = new SuggestQueryHandler(repositoryMock.Object);
        var result = await handler.Handle(new SuggestQuery("ph"), CancellationToken.None);

        result.ShouldBe(suggestions);
        repositoryMock.Verify(x => x.SuggestAsync("ph", It.IsAny<CancellationToken>()), Times.Once);
    }
}
