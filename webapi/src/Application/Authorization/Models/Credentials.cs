using System.Text.Json.Nodes;

namespace Application.Authorization.Models;

public class Credentials
{
    public required JsonObject EnvironmentCredentials { get; init; }
}
