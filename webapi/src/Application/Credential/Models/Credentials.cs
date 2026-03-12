using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace Application.Credential.Models;

public sealed class Credentials
{
    public required JsonObject EnvironmentCredentials { get; init; }
}
