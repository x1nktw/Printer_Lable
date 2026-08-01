using FluentAssertions;
using LabelPrint.Application.Services;
using LabelPrint.Application.Tests.Fakes;
using LabelPrint.Domain.Entities;
using LabelPrint.Domain.Enums;
using Microsoft.Extensions.Logging.Abstractions;

namespace LabelPrint.Application.Tests;

public class AuthServiceTests
{
    [Fact]
    public async Task SignIn_Allows_User_Without_Pin()
    {
        var userId = Guid.NewGuid();
        var uow = new InMemoryUnitOfWork();
        uow.AddUser(new User
        {
            Id = userId,
            Name = "Оператор",
            Role = UserRole.Operator,
            IsActive = true,
            PinHash = null
        });

        var session = new UserSession();
        var service = new AuthService(uow, session, NullLogger<AuthService>.Instance);

        var result = await service.SignInAsync(userId, pin: null);

        result.IsSuccess.Should().BeTrue();
        session.CurrentUserId.Should().Be(userId);
        session.CurrentUserName.Should().Be("Оператор");
        session.CurrentUserRole.Should().Be(UserRole.Operator);
        session.IsSignedIn.Should().BeTrue();
    }

    [Fact]
    public async Task SignIn_Rejects_Invalid_Pin_When_Required()
    {
        var userId = Guid.NewGuid();
        var uow = new InMemoryUnitOfWork();
        uow.AddUser(new User
        {
            Id = userId,
            Name = "Secure User",
            Role = UserRole.Administrator,
            IsActive = true,
            PinHash = AuthService.HashPin("1234")
        });

        var session = new UserSession();
        var service = new AuthService(uow, session, NullLogger<AuthService>.Instance);

        var result = await service.SignInAsync(userId, pin: "9999");

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("PIN");
        session.IsSignedIn.Should().BeFalse();
    }

    [Fact]
    public async Task SignOut_Clears_Session()
    {
        var userId = Guid.NewGuid();
        var uow = new InMemoryUnitOfWork();
        uow.AddUser(new User
        {
            Id = userId,
            Name = "Test",
            Role = UserRole.Operator,
            IsActive = true
        });

        var session = new UserSession();
        var service = new AuthService(uow, session, NullLogger<AuthService>.Instance);
        await service.SignInAsync(userId, pin: null);

        service.SignOut();

        session.IsSignedIn.Should().BeFalse();
        session.CurrentUserId.Should().BeNull();
    }
}
