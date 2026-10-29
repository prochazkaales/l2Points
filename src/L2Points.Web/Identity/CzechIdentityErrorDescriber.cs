using Microsoft.AspNetCore.Identity;

namespace L2Points.Web.Identity;

/// <summary>Czech texts for the Identity errors that can reach the user.</summary>
public class CzechIdentityErrorDescriber : IdentityErrorDescriber
{
    public override IdentityError DefaultError() => Create(nameof(DefaultError), "Došlo k neznámé chybě.");

    public override IdentityError PasswordMismatch() => Create(nameof(PasswordMismatch), "Současné heslo není správné.");

    public override IdentityError PasswordTooShort(int length) =>
        Create(nameof(PasswordTooShort), $"Heslo musí mít alespoň {length} znaků.");

    public override IdentityError PasswordRequiresDigit() =>
        Create(nameof(PasswordRequiresDigit), "Heslo musí obsahovat alespoň jednu číslici.");

    public override IdentityError PasswordRequiresLower() =>
        Create(nameof(PasswordRequiresLower), "Heslo musí obsahovat alespoň jedno malé písmeno.");

    public override IdentityError PasswordRequiresUpper() =>
        Create(nameof(PasswordRequiresUpper), "Heslo musí obsahovat alespoň jedno velké písmeno.");

    public override IdentityError PasswordRequiresNonAlphanumeric() =>
        Create(nameof(PasswordRequiresNonAlphanumeric), "Heslo musí obsahovat alespoň jeden speciální znak.");

    public override IdentityError PasswordRequiresUniqueChars(int uniqueChars) =>
        Create(nameof(PasswordRequiresUniqueChars), $"Heslo musí obsahovat alespoň {uniqueChars} různých znaků.");

    public override IdentityError DuplicateUserName(string userName) =>
        Create(nameof(DuplicateUserName), $"Login {userName} už existuje.");

    public override IdentityError InvalidUserName(string? userName) =>
        Create(nameof(InvalidUserName), "Login může obsahovat jen písmena bez diakritiky, číslice a znaky - . _ @ +.");

    private static IdentityError Create(string code, string description) =>
        new() { Code = code, Description = description };
}
