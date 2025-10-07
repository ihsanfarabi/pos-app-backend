using PosApp.Application.Abstractions.Persistence;
using PosApp.Application.Common;
using PosApp.Application.Contracts;
using PosApp.Application.Features.Menu;
using PosApp.Application.Features.Menu.Commands;
using PosApp.Application.Features.Menu.Queries;
using PosApp.Domain.Entities;

namespace PosApp.Application.Tests;

public class MenuHandlersTests
{
    [Fact]
    public async Task CreateMenuItemCommandHandler_PersistsAndReturnsId()
    {
        var repository = Substitute.For<IMenuRepository>();
        MenuItem? captured = null;
        repository
            .AddAsync(Arg.Do<MenuItem>(item => captured = item), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var handler = new CreateMenuItemCommandHandler(repository);
        var command = new CreateMenuItemCommand(new CreateMenuItemDto("  Latte  ", 3.75m));

        var id = await handler.Handle(command, CancellationToken.None);

        Assert.NotEqual(Guid.Empty, id);
        Assert.NotNull(captured);
        Assert.Equal(id, captured!.Id);
        Assert.Equal("Latte", captured.Name);
        Assert.Equal(3.75m, captured.Price);

        await repository.Received(1).AddAsync(Arg.Any<MenuItem>(), Arg.Any<CancellationToken>());
        await repository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateMenuItemCommandHandler_UpdatesExistingItem()
    {
        var repository = Substitute.For<IMenuRepository>();
        var existing = MenuItem.Create("Espresso", 2.5m);
        repository
            .GetByIdAsync(existing.Id, Arg.Any<CancellationToken>())
            .Returns(existing);

        var handler = new UpdateMenuItemCommandHandler(repository);
        var command = new UpdateMenuItemCommand(existing.Id, new UpdateMenuItemDto("  Iced Espresso ", 3m));

        await handler.Handle(command, CancellationToken.None);

        Assert.Equal("Iced Espresso", existing.Name);
        Assert.Equal(3m, existing.Price);
        await repository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateMenuItemCommandHandler_WhenMissing_Throws()
    {
        var repository = Substitute.For<IMenuRepository>();
        repository
            .GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((MenuItem?)null);
        var handler = new UpdateMenuItemCommandHandler(repository);
        var command = new UpdateMenuItemCommand(Guid.NewGuid(), new UpdateMenuItemDto("Americano", 3m));

        await Assert.ThrowsAsync<KeyNotFoundException>(() => handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task DeleteMenuItemCommandHandler_RemovesItem()
    {
        var repository = Substitute.For<IMenuRepository>();
        var existing = MenuItem.Create("Mocha", 4m);
        repository
            .GetByIdAsync(existing.Id, Arg.Any<CancellationToken>())
            .Returns(existing);

        var handler = new DeleteMenuItemCommandHandler(repository);
        var command = new DeleteMenuItemCommand(existing.Id);

        await handler.Handle(command, CancellationToken.None);

        await repository.Received(1).RemoveAsync(existing, Arg.Any<CancellationToken>());
        await repository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteMenuItemCommandHandler_WhenMissing_Throws()
    {
        var repository = Substitute.For<IMenuRepository>();
        repository
            .GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((MenuItem?)null);

        var handler = new DeleteMenuItemCommandHandler(repository);

        await Assert.ThrowsAsync<KeyNotFoundException>(() => handler.Handle(new DeleteMenuItemCommand(Guid.NewGuid()), CancellationToken.None));
    }

    [Fact]
    public async Task GetMenuItemsQueryHandler_NormalizesPagingAndMapsResponse()
    {
        var repository = Substitute.For<IMenuRepository>();
        var menuItems = new List<MenuItem>
        {
            MenuItem.Create("Latte", 4m),
            MenuItem.Create("Espresso", 2.5m)
        };
        var pagedResult = new PagedResult<MenuItem>(menuItems, 1, 5, 10);
        repository
            .GetPagedAsync("cof", 0, 20, Arg.Any<CancellationToken>())
            .Returns(pagedResult);

        var handler = new GetMenuItemsQueryHandler(repository);
        var query = new GetMenuItemsQuery(new MenuQueryDto("cof", -1, -1));

        var result = await handler.Handle(query, CancellationToken.None);

        await repository.Received(1).GetPagedAsync("cof", 0, 20, Arg.Any<CancellationToken>());
        Assert.Equal(2, result.Items.Count);
        Assert.Equal(menuItems[0].Id, result.Items[0].Id);
        Assert.Equal("Latte", result.Items[0].Name);
        Assert.Equal(4m, result.Items[0].Price);
        Assert.Equal(1, result.PageIndex);
        Assert.Equal(5, result.PageSize);
        Assert.Equal(10, result.TotalCount);
    }
}
