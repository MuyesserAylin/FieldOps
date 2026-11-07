using System;
using FieldOps.Api.Auth;

namespace FieldOps.Api.Tests.Auth;

public class PasswordServiceTests
{
    private readonly PasswordService _service = new();

    [Fact]
    public void Hash_DoesNotContainThePassword()
    {
        var hash = _service.Hash("fake-test-password-1");

        Assert.DoesNotContain("fake-test-password-1", hash);
    }

    [Fact]
    public void Hash_SamePasswordTwice_GivesDifferentHashes()
    {
        var first = _service.Hash("fake-test-password-1");
        var second = _service.Hash("fake-test-password-1");

        Assert.NotEqual(first, second);
    }

    [Fact]
    public void Verify_CorrectPassword_ReturnsTrue()
    {
        var hash = _service.Hash("fake-test-password-1");

        Assert.True(_service.Verify(hash, "fake-test-password-1"));
    }

    [Fact]
    public void Verify_WrongPassword_ReturnsFalse()
    {
        var hash = _service.Hash("fake-test-password-1");

        Assert.False(_service.Verify(hash, "fake-test-password-2"));
    }
}
