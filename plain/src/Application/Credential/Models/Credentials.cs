using System.Text.Json.Nodes;

namespace Application.Credential.Models;

public class Credentials
{
    public required JsonObject EnvironmentCredentials { get; init; }

    public required JsonObject SharedCredentials { get; init; }
}
