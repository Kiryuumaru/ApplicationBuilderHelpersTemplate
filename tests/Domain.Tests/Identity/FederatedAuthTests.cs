using Domain.Identity.Models;
using Domain.Identity.Services;
using Domain.Identity.ValueObjects;

namespace Domain.Tests.Identity;

public class FederatedAuthTests
{
    [Fact]
    public void RegisterExternal_CreatesUserWithIdentityLink()
    {
        var user = User.RegisterExternal(
            "federatedUser",
            "google",
            "google-sub-1",
            providerEmail: "user@example.com",
            displayName: "User One",
            email: "user@example.com");

        Assert.Equal("federatedUser", user.Username);
        Assert.Single(user.IdentityLinks);
        var link = user.IdentityLinks.First();
        Assert.Equal("google", link.Provider);
        Assert.Equal("google-sub-1", link.Subject);
        Assert.Equal("user@example.com", link.Email);
    }

    [Fact]
    public void RegisterExternal_AllowsNullEmail()
    {
        var user = User.RegisterExternal("anon", "github", "gh-anon");

        Assert.Null(user.Email);
        Assert.Single(user.IdentityLinks);
        Assert.Null(user.IdentityLinks.First().Email);
    }

    [Fact]
    public void LinkIdentity_Then_Authenticate_LocalFails_WhenNoPassword()
    {
        var user = User.Register("noPasswordUser", "np@example.com");
        user.LinkIdentity("github", "gh-42", "np@example.com", "NP");
        user.Activate();

        var service = new UserAuthenticationService(new StubSecretValidator("secret"));

        Assert.Throws<Domain.Identity.Exceptions.AuthenticationException>(() => service.Authenticate(user, "secret".AsSpan(), DateTimeOffset.UtcNow));
    }

    [Fact]
    public void AddPasswordAfterLink_AllowsLocalAuth()
    {
        var user = User.Register("maybeLocal", "local@example.com");
        user.LinkIdentity("google", "g-1", "local@example.com", "Local");
        user.Activate();

        var credential = PasswordCredential.Create("argon2", "hash", "salt", 3);
        user.SetPasswordCredential(credential);

        var service = new UserAuthenticationService(new StubSecretValidator("secret"));
        var session = service.Authenticate(user, "secret".AsSpan(), DateTimeOffset.UtcNow);

        Assert.Equal(user.Id, session.UserId);
    }

    private sealed class StubSecretValidator(string expected) : Domain.Identity.Interfaces.IUserSecretValidator
    {
        public bool Verify(Domain.Identity.ValueObjects.PasswordCredential credential, ReadOnlySpan<char> secret)
        {
            return secret.SequenceEqual(expected.AsSpan());
        }
    }
}
