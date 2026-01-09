namespace Domain.Identity.Interfaces;

public interface IPasswordVerifier
{
    bool Verify(string passwordHash, string providedPassword);
}
