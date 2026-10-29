using L2Points.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace L2Points.Infrastructure.Tests;

public class PasswordGeneratorTests
{
    [Fact]
    public async Task Generated_password_passes_identity_policy()
    {
        await using var provider = TestServices.Create();
        using var scope = provider.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var user = new AppUser { UserName = "probe" };

        for (var i = 0; i < 50; i++)
        {
            var password = PasswordGenerator.Generate();

            Assert.Equal(PasswordGenerator.Length, password.Length);
            Assert.Contains(password, char.IsLower);
            Assert.Contains(password, char.IsUpper);
            Assert.Contains(password, char.IsDigit);
            Assert.DoesNotContain(password, c => "0Oo1lI".Contains(c));

            foreach (var validator in users.PasswordValidators)
            {
                Assert.True((await validator.ValidateAsync(users, user, password)).Succeeded, password);
            }
        }
    }

    [Fact]
    public void Generated_passwords_differ()
    {
        var passwords = Enumerable.Range(0, 20).Select(_ => PasswordGenerator.Generate()).ToHashSet();

        Assert.Equal(20, passwords.Count);
    }
}
