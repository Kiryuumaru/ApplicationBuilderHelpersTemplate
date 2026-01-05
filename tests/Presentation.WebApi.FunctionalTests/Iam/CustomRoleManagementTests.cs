using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Presentation.WebApi.FunctionalTests.Fixtures;

namespace Presentation.WebApi.FunctionalTests.Iam;

/// <summary>
/// Comprehensive tests for the custom role management system.
/// Tests the full lifecycle of creating roles, assigning permissions to roles,
/// assigning roles to users, and verifying access based on role permissions.
/// </summary>
[Collection(WebApiTestCollection.Name)]
public class CustomRoleManagementTests
{
    private readonly ITestOutputHelper _output;
    private readonly SharedWebApiHost _sharedHost;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private const string TestPassword = "TestP@ssword123!";

    public CustomRoleManagementTests(SharedWebApiHost sharedHost, ITestOutputHelper output)
    {
        _sharedHost = sharedHost;
        _output = output;
    }

    #region Role CRUD Tests

    [Fact]
    public async Task CreateRole_AsAdmin_Succeeds()
    {
        _output.WriteLine("[TEST] CreateRole_AsAdmin_Succeeds");

        var adminAuth = await CreateAdminUserAsync();
        Assert.NotNull(adminAuth);

        var createRoleRequest = new
        {
            Code = $"TEST_ROLE_{Guid.NewGuid():N}".ToUpperInvariant()[..30],
            Name = "Test Custom Role",
            Description = "A test role for functional testing",
            ScopeTemplates = new[]
            {
                new
                {
                    Type = "allow",
                    PermissionPath = "api:iam:users:read",
                    Parameters = new Dictionary<string, string> { { "userId", "{roleUserId}" } }
                }
            }
        };

        _output.WriteLine("[STEP] POST /api/v1/iam/roles...");
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/iam/roles");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminAuth!.AccessToken);
        request.Content = JsonContent.Create(createRoleRequest);
        var response = await _sharedHost.Host.HttpClient.SendAsync(request);

        _output.WriteLine($"[RECEIVED] Status: {(int)response.StatusCode} {response.StatusCode}");
        var body = await response.Content.ReadAsStringAsync();
        _output.WriteLine($"[RECEIVED] Body: {body}");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var role = JsonSerializer.Deserialize<RoleResponse>(body, JsonOptions);
        Assert.NotNull(role);
        Assert.Equal(createRoleRequest.Code, role!.Code);
        Assert.Equal(createRoleRequest.Name, role.Name);
        Assert.False(role.IsSystemRole);

        _output.WriteLine("[PASS] Admin can create custom roles");
    }

    [Fact]
    public async Task CreateRole_AsRegularUser_Returns403()
    {
        _output.WriteLine("[TEST] CreateRole_AsRegularUser_Returns403");

        var userAuth = await RegisterAndGetTokenAsync();
        Assert.NotNull(userAuth);

        var createRoleRequest = new
        {
            Code = $"HACKER_ROLE_{Guid.NewGuid():N}"[..30],
            Name = "Unauthorized Role",
            Description = "Should not be created"
        };

        _output.WriteLine("[STEP] POST /api/v1/iam/roles as regular user...");
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/iam/roles");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", userAuth!.AccessToken);
        request.Content = JsonContent.Create(createRoleRequest);
        var response = await _sharedHost.Host.HttpClient.SendAsync(request);

        _output.WriteLine($"[RECEIVED] Status: {(int)response.StatusCode} {response.StatusCode}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        _output.WriteLine("[PASS] Regular user cannot create roles");
    }

    [Fact]
    public async Task CreateRole_DuplicateCode_Returns409()
    {
        _output.WriteLine("[TEST] CreateRole_DuplicateCode_Returns409");

        var adminAuth = await CreateAdminUserAsync();
        Assert.NotNull(adminAuth);

        var roleCode = $"DUP_ROLE_{Guid.NewGuid():N}"[..30];

        var createRoleRequest = new
        {
            Code = roleCode,
            Name = "First Role",
            Description = "First role with this code"
        };

        // Create first role
        _output.WriteLine("[STEP] Creating first role...");
        using var request1 = new HttpRequestMessage(HttpMethod.Post, "/api/v1/iam/roles");
        request1.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminAuth!.AccessToken);
        request1.Content = JsonContent.Create(createRoleRequest);
        var response1 = await _sharedHost.Host.HttpClient.SendAsync(request1);
        Assert.Equal(HttpStatusCode.Created, response1.StatusCode);

        // Try to create duplicate
        var duplicateRequest = new
        {
            Code = roleCode,
            Name = "Duplicate Role",
            Description = "Should fail"
        };

        _output.WriteLine("[STEP] Creating duplicate role...");
        using var request2 = new HttpRequestMessage(HttpMethod.Post, "/api/v1/iam/roles");
        request2.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminAuth.AccessToken);
        request2.Content = JsonContent.Create(duplicateRequest);
        var response2 = await _sharedHost.Host.HttpClient.SendAsync(request2);

        _output.WriteLine($"[RECEIVED] Status: {(int)response2.StatusCode} {response2.StatusCode}");

        Assert.Equal(HttpStatusCode.Conflict, response2.StatusCode);
        _output.WriteLine("[PASS] Duplicate role code returns 409 Conflict");
    }

    [Fact]
    public async Task CreateRole_ReservedCode_Returns409()
    {
        _output.WriteLine("[TEST] CreateRole_ReservedCode_Returns409");

        var adminAuth = await CreateAdminUserAsync();
        Assert.NotNull(adminAuth);

        var createRoleRequest = new
        {
            Code = "ADMIN", // Reserved system role code
            Name = "Fake Admin",
            Description = "Should not be created"
        };

        _output.WriteLine("[STEP] POST /api/v1/iam/roles with reserved code 'ADMIN'...");
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/iam/roles");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminAuth!.AccessToken);
        request.Content = JsonContent.Create(createRoleRequest);
        var response = await _sharedHost.Host.HttpClient.SendAsync(request);

        _output.WriteLine($"[RECEIVED] Status: {(int)response.StatusCode} {response.StatusCode}");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        _output.WriteLine("[PASS] Reserved role code returns 409 Conflict");
    }

    [Fact]
    public async Task GetRole_ExistingRole_ReturnsRole()
    {
        _output.WriteLine("[TEST] GetRole_ExistingRole_ReturnsRole");

        var adminAuth = await CreateAdminUserAsync();
        Assert.NotNull(adminAuth);

        // Create a role first
        var roleCode = $"GET_ROLE_{Guid.NewGuid():N}"[..30];
        var createRoleRequest = new
        {
            Code = roleCode,
            Name = "Role to Get",
            Description = "For testing GET endpoint"
        };

        using var createRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/iam/roles");
        createRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminAuth!.AccessToken);
        createRequest.Content = JsonContent.Create(createRoleRequest);
        var createResponse = await _sharedHost.Host.HttpClient.SendAsync(createRequest);
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        var createdRole = JsonSerializer.Deserialize<RoleResponse>(
            await createResponse.Content.ReadAsStringAsync(), JsonOptions);
        Assert.NotNull(createdRole);

        // Now get the role
        _output.WriteLine($"[STEP] GET /api/v1/iam/roles/{createdRole!.Id}...");
        using var getRequest = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/iam/roles/{createdRole.Id}");
        getRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminAuth.AccessToken);
        var getResponse = await _sharedHost.Host.HttpClient.SendAsync(getRequest);

        _output.WriteLine($"[RECEIVED] Status: {(int)getResponse.StatusCode} {getResponse.StatusCode}");

        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);

        var retrievedRole = JsonSerializer.Deserialize<RoleResponse>(
            await getResponse.Content.ReadAsStringAsync(), JsonOptions);
        Assert.NotNull(retrievedRole);
        Assert.Equal(createdRole.Id, retrievedRole!.Id);
        // Role codes are normalized to uppercase
        Assert.Equal(roleCode.ToUpperInvariant(), retrievedRole.Code);

        _output.WriteLine("[PASS] GetRole returns the correct role");
    }

    [Fact]
    public async Task GetRole_NonExistentRole_Returns404()
    {
        _output.WriteLine("[TEST] GetRole_NonExistentRole_Returns404");

        var adminAuth = await CreateAdminUserAsync();
        Assert.NotNull(adminAuth);

        var fakeRoleId = Guid.NewGuid();

        _output.WriteLine($"[STEP] GET /api/v1/iam/roles/{fakeRoleId}...");
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/iam/roles/{fakeRoleId}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminAuth!.AccessToken);
        var response = await _sharedHost.Host.HttpClient.SendAsync(request);

        _output.WriteLine($"[RECEIVED] Status: {(int)response.StatusCode} {response.StatusCode}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        _output.WriteLine("[PASS] Non-existent role returns 404");
    }

    [Fact]
    public async Task UpdateRole_AsAdmin_Succeeds()
    {
        _output.WriteLine("[TEST] UpdateRole_AsAdmin_Succeeds");

        var adminAuth = await CreateAdminUserAsync();
        Assert.NotNull(adminAuth);

        // Create a role
        var roleCode = $"UPD_ROLE_{Guid.NewGuid():N}"[..30];
        var createRoleRequest = new
        {
            Code = roleCode,
            Name = "Original Name",
            Description = "Original Description"
        };

        using var createRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/iam/roles");
        createRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminAuth!.AccessToken);
        createRequest.Content = JsonContent.Create(createRoleRequest);
        var createResponse = await _sharedHost.Host.HttpClient.SendAsync(createRequest);
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        var createdRole = JsonSerializer.Deserialize<RoleResponse>(
            await createResponse.Content.ReadAsStringAsync(), JsonOptions);
        Assert.NotNull(createdRole);

        // Update the role
        var updateRequest = new
        {
            Name = "Updated Name",
            Description = "Updated Description"
        };

        _output.WriteLine($"[STEP] PUT /api/v1/iam/roles/{createdRole!.Id}...");
        using var putRequest = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/iam/roles/{createdRole.Id}");
        putRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminAuth.AccessToken);
        putRequest.Content = JsonContent.Create(updateRequest);
        var putResponse = await _sharedHost.Host.HttpClient.SendAsync(putRequest);

        _output.WriteLine($"[RECEIVED] Status: {(int)putResponse.StatusCode} {putResponse.StatusCode}");

        Assert.Equal(HttpStatusCode.OK, putResponse.StatusCode);

        var updatedRole = JsonSerializer.Deserialize<RoleResponse>(
            await putResponse.Content.ReadAsStringAsync(), JsonOptions);
        Assert.NotNull(updatedRole);
        Assert.Equal("Updated Name", updatedRole!.Name);
        Assert.Equal("Updated Description", updatedRole.Description);

        _output.WriteLine("[PASS] Admin can update custom roles");
    }

    [Fact]
    public async Task UpdateRole_SystemRole_Returns400()
    {
        _output.WriteLine("[TEST] UpdateRole_SystemRole_Returns400");

        var adminAuth = await CreateAdminUserAsync();
        Assert.NotNull(adminAuth);

        // Try to update the ADMIN system role (ID is well-known)
        var adminRoleId = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var updateRequest = new
        {
            Name = "Hacked Admin",
            Description = "Should fail"
        };

        _output.WriteLine($"[STEP] PUT /api/v1/iam/roles/{adminRoleId} (system role)...");
        using var request = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/iam/roles/{adminRoleId}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminAuth!.AccessToken);
        request.Content = JsonContent.Create(updateRequest);
        var response = await _sharedHost.Host.HttpClient.SendAsync(request);

        _output.WriteLine($"[RECEIVED] Status: {(int)response.StatusCode} {response.StatusCode}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        _output.WriteLine("[PASS] Cannot update system roles");
    }

    [Fact]
    public async Task DeleteRole_AsAdmin_Succeeds()
    {
        _output.WriteLine("[TEST] DeleteRole_AsAdmin_Succeeds");

        var adminAuth = await CreateAdminUserAsync();
        Assert.NotNull(adminAuth);

        // Create a role to delete
        var roleCode = $"DEL_ROLE_{Guid.NewGuid():N}"[..30];
        var createRoleRequest = new
        {
            Code = roleCode,
            Name = "Role to Delete",
            Description = "Will be deleted"
        };

        using var createRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/iam/roles");
        createRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminAuth!.AccessToken);
        createRequest.Content = JsonContent.Create(createRoleRequest);
        var createResponse = await _sharedHost.Host.HttpClient.SendAsync(createRequest);
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        var createdRole = JsonSerializer.Deserialize<RoleResponse>(
            await createResponse.Content.ReadAsStringAsync(), JsonOptions);
        Assert.NotNull(createdRole);

        // Delete the role
        _output.WriteLine($"[STEP] DELETE /api/v1/iam/roles/{createdRole!.Id}...");
        using var deleteRequest = new HttpRequestMessage(HttpMethod.Delete, $"/api/v1/iam/roles/{createdRole.Id}");
        deleteRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminAuth.AccessToken);
        var deleteResponse = await _sharedHost.Host.HttpClient.SendAsync(deleteRequest);

        _output.WriteLine($"[RECEIVED] Status: {(int)deleteResponse.StatusCode} {deleteResponse.StatusCode}");

        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        // Verify it's deleted
        using var getRequest = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/iam/roles/{createdRole.Id}");
        getRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminAuth.AccessToken);
        var getResponse = await _sharedHost.Host.HttpClient.SendAsync(getRequest);
        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);

        _output.WriteLine("[PASS] Admin can delete custom roles");
    }

    [Fact]
    public async Task DeleteRole_SystemRole_Returns400()
    {
        _output.WriteLine("[TEST] DeleteRole_SystemRole_Returns400");

        var adminAuth = await CreateAdminUserAsync();
        Assert.NotNull(adminAuth);

        // Try to delete the ADMIN system role
        var adminRoleId = Guid.Parse("00000000-0000-0000-0000-000000000001");

        _output.WriteLine($"[STEP] DELETE /api/v1/iam/roles/{adminRoleId} (system role)...");
        using var request = new HttpRequestMessage(HttpMethod.Delete, $"/api/v1/iam/roles/{adminRoleId}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminAuth!.AccessToken);
        var response = await _sharedHost.Host.HttpClient.SendAsync(request);

        _output.WriteLine($"[RECEIVED] Status: {(int)response.StatusCode} {response.StatusCode}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        _output.WriteLine("[PASS] Cannot delete system roles");
    }

    [Fact]
    public async Task ListRoles_AsAdmin_ReturnsAllRoles()
    {
        _output.WriteLine("[TEST] ListRoles_AsAdmin_ReturnsAllRoles");

        var adminAuth = await CreateAdminUserAsync();
        Assert.NotNull(adminAuth);

        _output.WriteLine("[STEP] GET /api/v1/iam/roles...");
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/iam/roles");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminAuth!.AccessToken);
        var response = await _sharedHost.Host.HttpClient.SendAsync(request);

        _output.WriteLine($"[RECEIVED] Status: {(int)response.StatusCode} {response.StatusCode}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<RoleListResponse>(body, JsonOptions);
        Assert.NotNull(result);
        Assert.True(result!.Roles.Count >= 2, "Should have at least ADMIN and USER system roles");

        // Verify system roles are present
        Assert.Contains(result.Roles, r => r.Code == "ADMIN" && r.IsSystemRole);
        Assert.Contains(result.Roles, r => r.Code == "USER" && r.IsSystemRole);

        _output.WriteLine("[PASS] ListRoles returns all roles including system roles");
    }

    #endregion

    #region Role Assignment and Permission Verification Tests

    [Fact]
    public async Task CustomRole_GrantsCorrectPermissions()
    {
        _output.WriteLine("[TEST] CustomRole_GrantsCorrectPermissions");

        var adminAuth = await CreateAdminUserAsync();
        Assert.NotNull(adminAuth);

        // Create a custom role that grants permission to read IAM users
        var roleCode = $"READER_{Guid.NewGuid():N}"[..30];
        var createRoleRequest = new
        {
            Code = roleCode,
            Name = "User Reader",
            Description = "Can read any user's details",
            ScopeTemplates = new[]
            {
                new
                {
                    Type = "allow",
                    PermissionPath = "api:iam:users:read"
                    // No parameters = global access
                }
            }
        };

        _output.WriteLine("[STEP] Creating custom role with api:iam:users:read permission...");
        using var createRoleReq = new HttpRequestMessage(HttpMethod.Post, "/api/v1/iam/roles");
        createRoleReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminAuth!.AccessToken);
        createRoleReq.Content = JsonContent.Create(createRoleRequest);
        var createRoleResp = await _sharedHost.Host.HttpClient.SendAsync(createRoleReq);
        Assert.Equal(HttpStatusCode.Created, createRoleResp.StatusCode);

        // Create a regular user
        var regularUser = await RegisterAndGetTokenAsync();
        Assert.NotNull(regularUser);
        var regularUserId = regularUser!.User!.Id;

        // Create another user to test access against
        var targetUser = await RegisterAndGetTokenAsync();
        Assert.NotNull(targetUser);
        var targetUserId = targetUser!.User!.Id;

        // Verify regular user cannot access target user BEFORE role assignment
        _output.WriteLine("[STEP] Verifying regular user CANNOT access other user before role assignment...");
        using var beforeReq = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/iam/users/{targetUserId}");
        beforeReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", regularUser.AccessToken);
        var beforeResp = await _sharedHost.Host.HttpClient.SendAsync(beforeReq);
        _output.WriteLine($"[RECEIVED] Before assignment: {(int)beforeResp.StatusCode} {beforeResp.StatusCode}");
        Assert.Equal(HttpStatusCode.Forbidden, beforeResp.StatusCode);

        // Assign the custom role to regular user (as admin)
        _output.WriteLine("[STEP] Assigning custom role to regular user...");
        var assignRequest = new { UserId = regularUserId, RoleCode = roleCode };
        using var assignReq = new HttpRequestMessage(HttpMethod.Post, "/api/v1/iam/roles/assign");
        assignReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminAuth.AccessToken);
        assignReq.Content = JsonContent.Create(assignRequest);
        var assignResp = await _sharedHost.Host.HttpClient.SendAsync(assignReq);
        _output.WriteLine($"[RECEIVED] Assignment: {(int)assignResp.StatusCode} {assignResp.StatusCode}");
        Assert.Equal(HttpStatusCode.NoContent, assignResp.StatusCode);

        // User needs to get a new token to reflect the new role
        _output.WriteLine("[STEP] Getting new token for user with new role...");
        var newUserAuth = await LoginAsync(regularUser.User!.Username!, TestPassword);
        Assert.NotNull(newUserAuth);

        // Verify regular user CAN access target user AFTER role assignment
        _output.WriteLine("[STEP] Verifying regular user CAN access other user after role assignment...");
        using var afterReq = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/iam/users/{targetUserId}");
        afterReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", newUserAuth!.AccessToken);
        var afterResp = await _sharedHost.Host.HttpClient.SendAsync(afterReq);
        _output.WriteLine($"[RECEIVED] After assignment: {(int)afterResp.StatusCode} {afterResp.StatusCode}");
        Assert.Equal(HttpStatusCode.OK, afterResp.StatusCode);

        _output.WriteLine("[PASS] Custom role correctly grants permissions");
    }

    [Fact]
    public async Task CustomRole_WithParameters_GrantsScopedPermissions()
    {
        _output.WriteLine("[TEST] CustomRole_WithParameters_GrantsScopedPermissions");

        var adminAuth = await CreateAdminUserAsync();
        Assert.NotNull(adminAuth);

        // Create a custom role with parameterized permission
        var roleCode = $"SCOPED_{Guid.NewGuid():N}"[..30];
        var createRoleRequest = new
        {
            Code = roleCode,
            Name = "Scoped Reader",
            Description = "Can only read specific user",
            ScopeTemplates = new[]
            {
                new
                {
                    Type = "allow",
                    PermissionPath = "api:iam:users:read",
                    Parameters = new Dictionary<string, string> { { "userId", "{roleUserId}" } }
                }
            }
        };

        _output.WriteLine("[STEP] Creating custom role with scoped permission...");
        using var createRoleReq = new HttpRequestMessage(HttpMethod.Post, "/api/v1/iam/roles");
        createRoleReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminAuth!.AccessToken);
        createRoleReq.Content = JsonContent.Create(createRoleRequest);
        var createRoleResp = await _sharedHost.Host.HttpClient.SendAsync(createRoleReq);
        Assert.Equal(HttpStatusCode.Created, createRoleResp.StatusCode);

        // Create regular users
        var regularUser = await RegisterAndGetTokenAsync();
        Assert.NotNull(regularUser);
        var regularUserId = regularUser!.User!.Id;

        var targetUser = await RegisterAndGetTokenAsync();
        Assert.NotNull(targetUser);
        var targetUserId = targetUser!.User!.Id;

        // Assign the scoped role with targetUserId as the parameter
        _output.WriteLine("[STEP] Assigning scoped role with specific userId parameter...");
        var assignRequest = new
        {
            UserId = regularUserId,
            RoleCode = roleCode,
            ParameterValues = new Dictionary<string, string?> { { "roleUserId", targetUserId.ToString() } }
        };
        using var assignReq = new HttpRequestMessage(HttpMethod.Post, "/api/v1/iam/roles/assign");
        assignReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminAuth.AccessToken);
        assignReq.Content = JsonContent.Create(assignRequest);
        var assignResp = await _sharedHost.Host.HttpClient.SendAsync(assignReq);
        Assert.Equal(HttpStatusCode.NoContent, assignResp.StatusCode);

        // Get new token
        var newUserAuth = await LoginAsync(regularUser.User!.Username!, TestPassword);
        Assert.NotNull(newUserAuth);

        // Verify user CAN access the target user (the one specified in parameters)
        _output.WriteLine($"[STEP] Verifying user can access target user {targetUserId}...");
        using var accessTargetReq = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/iam/users/{targetUserId}");
        accessTargetReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", newUserAuth!.AccessToken);
        var accessTargetResp = await _sharedHost.Host.HttpClient.SendAsync(accessTargetReq);
        _output.WriteLine($"[RECEIVED] Access target: {(int)accessTargetResp.StatusCode}");
        Assert.Equal(HttpStatusCode.OK, accessTargetResp.StatusCode);

        // Create a third user that the regular user should NOT be able to access
        var thirdUser = await RegisterAndGetTokenAsync();
        Assert.NotNull(thirdUser);
        var thirdUserId = thirdUser!.User!.Id;

        // Verify user CANNOT access a different user
        _output.WriteLine($"[STEP] Verifying user CANNOT access different user {thirdUserId}...");
        using var accessThirdReq = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/iam/users/{thirdUserId}");
        accessThirdReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", newUserAuth.AccessToken);
        var accessThirdResp = await _sharedHost.Host.HttpClient.SendAsync(accessThirdReq);
        _output.WriteLine($"[RECEIVED] Access third: {(int)accessThirdResp.StatusCode}");
        Assert.Equal(HttpStatusCode.Forbidden, accessThirdResp.StatusCode);

        _output.WriteLine("[PASS] Scoped role correctly limits permissions to specified parameter");
    }

    [Fact]
    public async Task RemoveRole_RevokesPermissions()
    {
        _output.WriteLine("[TEST] RemoveRole_RevokesPermissions");

        var adminAuth = await CreateAdminUserAsync();
        Assert.NotNull(adminAuth);

        // Create a custom role
        var roleCode = $"REVOKE_{Guid.NewGuid():N}"[..30];
        var createRoleRequest = new
        {
            Code = roleCode,
            Name = "Role to Revoke",
            Description = "Will be removed",
            ScopeTemplates = new[]
            {
                new { Type = "allow", PermissionPath = "api:iam:users:read" }
            }
        };

        using var createRoleReq = new HttpRequestMessage(HttpMethod.Post, "/api/v1/iam/roles");
        createRoleReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminAuth!.AccessToken);
        createRoleReq.Content = JsonContent.Create(createRoleRequest);
        var createRoleResp = await _sharedHost.Host.HttpClient.SendAsync(createRoleReq);
        Assert.Equal(HttpStatusCode.Created, createRoleResp.StatusCode);

        var createdRole = JsonSerializer.Deserialize<RoleResponse>(
            await createRoleResp.Content.ReadAsStringAsync(), JsonOptions);
        Assert.NotNull(createdRole);

        // Create users
        var regularUser = await RegisterAndGetTokenAsync();
        Assert.NotNull(regularUser);
        var regularUserId = regularUser!.User!.Id;

        var targetUser = await RegisterAndGetTokenAsync();
        Assert.NotNull(targetUser);
        var targetUserId = targetUser!.User!.Id;

        // Assign role
        var assignRequest = new { UserId = regularUserId, RoleCode = roleCode };
        using var assignReq = new HttpRequestMessage(HttpMethod.Post, "/api/v1/iam/roles/assign");
        assignReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminAuth.AccessToken);
        assignReq.Content = JsonContent.Create(assignRequest);
        await _sharedHost.Host.HttpClient.SendAsync(assignReq);

        // Verify access works
        var withRoleAuth = await LoginAsync(regularUser.User!.Username!, TestPassword);
        using var accessReq1 = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/iam/users/{targetUserId}");
        accessReq1.Headers.Authorization = new AuthenticationHeaderValue("Bearer", withRoleAuth!.AccessToken);
        var accessResp1 = await _sharedHost.Host.HttpClient.SendAsync(accessReq1);
        Assert.Equal(HttpStatusCode.OK, accessResp1.StatusCode);

        // Remove the role
        _output.WriteLine("[STEP] Removing role from user...");
        var removeRequest = new { UserId = regularUserId, RoleId = createdRole!.Id };
        using var removeReq = new HttpRequestMessage(HttpMethod.Post, "/api/v1/iam/roles/remove");
        removeReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminAuth.AccessToken);
        removeReq.Content = JsonContent.Create(removeRequest);
        var removeResp = await _sharedHost.Host.HttpClient.SendAsync(removeReq);
        Assert.Equal(HttpStatusCode.NoContent, removeResp.StatusCode);

        // Get new token after role removal
        var withoutRoleAuth = await LoginAsync(regularUser.User!.Username!, TestPassword);
        Assert.NotNull(withoutRoleAuth);

        // Verify access is now denied
        _output.WriteLine("[STEP] Verifying access is denied after role removal...");
        using var accessReq2 = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/iam/users/{targetUserId}");
        accessReq2.Headers.Authorization = new AuthenticationHeaderValue("Bearer", withoutRoleAuth!.AccessToken);
        var accessResp2 = await _sharedHost.Host.HttpClient.SendAsync(accessReq2);
        _output.WriteLine($"[RECEIVED] After removal: {(int)accessResp2.StatusCode}");
        Assert.Equal(HttpStatusCode.Forbidden, accessResp2.StatusCode);

        _output.WriteLine("[PASS] Removing role correctly revokes permissions");
    }

    /// <summary>
    /// Tests that modifying a role's permissions takes effect immediately without requiring
    /// users to re-login or regenerate tokens. This is a key feature of role-based access control.
    /// </summary>
    [Fact]
    public async Task ModifyRole_TakesEffectWithoutReLogin()
    {
        _output.WriteLine("[TEST] ModifyRole_TakesEffectWithoutReLogin");
        _output.WriteLine("This test verifies the core RBAC principle: token contains role code, permissions resolved at runtime");

        var adminAuth = await CreateAdminUserAsync();
        Assert.NotNull(adminAuth);

        // Step 1: Create a custom role initially WITHOUT api:iam:users:read permission
        var roleCode = $"DYNAMIC_{Guid.NewGuid():N}"[..30];
        var createRoleRequest = new
        {
            Code = roleCode,
            Name = "Dynamic Role",
            Description = "Role that will be modified",
            ScopeTemplates = Array.Empty<object>() // No permissions initially
        };

        _output.WriteLine("[STEP] Creating custom role without any permissions...");
        using var createRoleReq = new HttpRequestMessage(HttpMethod.Post, "/api/v1/iam/roles");
        createRoleReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminAuth!.AccessToken);
        createRoleReq.Content = JsonContent.Create(createRoleRequest);
        var createRoleResp = await _sharedHost.Host.HttpClient.SendAsync(createRoleReq);
        Assert.Equal(HttpStatusCode.Created, createRoleResp.StatusCode);

        var createdRole = JsonSerializer.Deserialize<RoleResponse>(
            await createRoleResp.Content.ReadAsStringAsync(), JsonOptions);
        Assert.NotNull(createdRole);

        // Step 2: Create users and assign the empty role
        var regularUser = await RegisterAndGetTokenAsync();
        Assert.NotNull(regularUser);
        var regularUserId = regularUser!.User!.Id;

        var targetUser = await RegisterAndGetTokenAsync();
        Assert.NotNull(targetUser);
        var targetUserId = targetUser!.User!.Id;

        _output.WriteLine("[STEP] Assigning custom role to user...");
        var assignRequest = new { UserId = regularUserId, RoleCode = roleCode };
        using var assignReq = new HttpRequestMessage(HttpMethod.Post, "/api/v1/iam/roles/assign");
        assignReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminAuth.AccessToken);
        assignReq.Content = JsonContent.Create(assignRequest);
        var assignResp = await _sharedHost.Host.HttpClient.SendAsync(assignReq);
        Assert.Equal(HttpStatusCode.NoContent, assignResp.StatusCode);

        // Step 3: Login and get token (this token contains the role code)
        _output.WriteLine("[STEP] Logging in to get token with role code...");
        var userAuth = await LoginAsync(regularUser.User!.Username!, TestPassword);
        Assert.NotNull(userAuth);
        var originalToken = userAuth!.AccessToken;

        // Step 4: Verify access is DENIED (role has no permissions yet)
        _output.WriteLine("[STEP] Verifying access is DENIED with empty role...");
        using var accessReq1 = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/iam/users/{targetUserId}");
        accessReq1.Headers.Authorization = new AuthenticationHeaderValue("Bearer", originalToken);
        var accessResp1 = await _sharedHost.Host.HttpClient.SendAsync(accessReq1);
        _output.WriteLine($"[RECEIVED] Before role modification: {(int)accessResp1.StatusCode}");
        Assert.Equal(HttpStatusCode.Forbidden, accessResp1.StatusCode);

        // Step 5: Modify the role to ADD api:iam:users:read permission
        _output.WriteLine("[STEP] Modifying role to add api:iam:users:read permission...");
        var updateRoleRequest = new
        {
            Name = "Dynamic Role - Updated",
            Description = "Role with read permission",
            ScopeTemplates = new[]
            {
                new { Type = "allow", PermissionPath = "api:iam:users:read" }
            }
        };
        using var updateReq = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/iam/roles/{createdRole!.Id}");
        updateReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminAuth.AccessToken);
        updateReq.Content = JsonContent.Create(updateRoleRequest);
        var updateResp = await _sharedHost.Host.HttpClient.SendAsync(updateReq);
        _output.WriteLine($"[RECEIVED] Update role: {(int)updateResp.StatusCode}");
        Assert.Equal(HttpStatusCode.OK, updateResp.StatusCode);

        // Step 6: Using the SAME TOKEN (no re-login), verify access is now GRANTED
        _output.WriteLine("[STEP] Verifying access is GRANTED using the SAME token (no re-login)...");
        using var accessReq2 = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/iam/users/{targetUserId}");
        accessReq2.Headers.Authorization = new AuthenticationHeaderValue("Bearer", originalToken);
        var accessResp2 = await _sharedHost.Host.HttpClient.SendAsync(accessReq2);
        _output.WriteLine($"[RECEIVED] After role modification (same token): {(int)accessResp2.StatusCode}");
        Assert.Equal(HttpStatusCode.OK, accessResp2.StatusCode);

        // Step 7: Modify the role again to REMOVE the permission
        _output.WriteLine("[STEP] Modifying role to remove permission...");
        var removePermissionRequest = new
        {
            Name = "Dynamic Role - Permission Removed",
            Description = "Role without permission again",
            ScopeTemplates = Array.Empty<object>()
        };
        using var removePermReq = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/iam/roles/{createdRole.Id}");
        removePermReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminAuth.AccessToken);
        removePermReq.Content = JsonContent.Create(removePermissionRequest);
        var removePermResp = await _sharedHost.Host.HttpClient.SendAsync(removePermReq);
        Assert.Equal(HttpStatusCode.OK, removePermResp.StatusCode);

        // Step 8: Using the SAME TOKEN, verify access is now DENIED again
        _output.WriteLine("[STEP] Verifying access is DENIED using the SAME token after permission removal...");
        using var accessReq3 = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/iam/users/{targetUserId}");
        accessReq3.Headers.Authorization = new AuthenticationHeaderValue("Bearer", originalToken);
        var accessResp3 = await _sharedHost.Host.HttpClient.SendAsync(accessReq3);
        _output.WriteLine($"[RECEIVED] After permission removal (same token): {(int)accessResp3.StatusCode}");
        Assert.Equal(HttpStatusCode.Forbidden, accessResp3.StatusCode);

        _output.WriteLine("[PASS] Role modifications take effect immediately without requiring token regeneration!");
        _output.WriteLine("This proves: tokens contain role codes, permissions are resolved at runtime from the database.");
    }

    #endregion

    #region Negative Tests

    [Fact]
    public async Task AssignRole_NonExistentRole_Returns404()
    {
        _output.WriteLine("[TEST] AssignRole_NonExistentRole_Returns404");

        var adminAuth = await CreateAdminUserAsync();
        Assert.NotNull(adminAuth);

        var regularUser = await RegisterAndGetTokenAsync();
        Assert.NotNull(regularUser);

        var assignRequest = new
        {
            UserId = regularUser!.User!.Id,
            RoleCode = "NONEXISTENT_ROLE_XYZ"
        };

        _output.WriteLine("[STEP] POST /api/v1/iam/roles/assign with non-existent role...");
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/iam/roles/assign");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminAuth!.AccessToken);
        request.Content = JsonContent.Create(assignRequest);
        var response = await _sharedHost.Host.HttpClient.SendAsync(request);

        _output.WriteLine($"[RECEIVED] Status: {(int)response.StatusCode} {response.StatusCode}");

        // The service correctly returns NotFound when the role doesn't exist
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        _output.WriteLine("[PASS] Assigning non-existent role returns 404");
    }

    [Fact]
    public async Task AssignRole_NonExistentUser_Returns404()
    {
        _output.WriteLine("[TEST] AssignRole_NonExistentUser_Returns404");

        var adminAuth = await CreateAdminUserAsync();
        Assert.NotNull(adminAuth);

        var assignRequest = new
        {
            UserId = Guid.NewGuid(), // Non-existent user
            RoleCode = "USER"
        };

        _output.WriteLine("[STEP] POST /api/v1/iam/roles/assign with non-existent user...");
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/iam/roles/assign");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminAuth!.AccessToken);
        request.Content = JsonContent.Create(assignRequest);
        var response = await _sharedHost.Host.HttpClient.SendAsync(request);

        _output.WriteLine($"[RECEIVED] Status: {(int)response.StatusCode} {response.StatusCode}");

        // The service correctly returns NotFound when the user doesn't exist
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        _output.WriteLine("[PASS] Assigning role to non-existent user returns 404");
    }

    [Fact]
    public async Task CreateRole_InvalidScopeTemplateType_Returns400()
    {
        _output.WriteLine("[TEST] CreateRole_InvalidScopeTemplateType_Returns400");

        var adminAuth = await CreateAdminUserAsync();
        Assert.NotNull(adminAuth);

        var createRoleRequest = new
        {
            Code = $"INVALID_{Guid.NewGuid():N}"[..30],
            Name = "Invalid Role",
            ScopeTemplates = new[]
            {
                new
                {
                    Type = "invalid_type", // Not "allow" or "deny"
                    PermissionPath = "api:iam:users:read"
                }
            }
        };

        _output.WriteLine("[STEP] POST /api/v1/iam/roles with invalid scope template type...");
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/iam/roles");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminAuth!.AccessToken);
        request.Content = JsonContent.Create(createRoleRequest);
        var response = await _sharedHost.Host.HttpClient.SendAsync(request);

        _output.WriteLine($"[RECEIVED] Status: {(int)response.StatusCode} {response.StatusCode}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        _output.WriteLine("[PASS] Invalid scope template type returns 400");
    }

    [Fact]
    public async Task RoleEndpoints_Unauthenticated_Returns401()
    {
        _output.WriteLine("[TEST] RoleEndpoints_Unauthenticated_Returns401");

        var endpoints = new[]
        {
            ("GET", "/api/v1/iam/roles"),
            ("GET", $"/api/v1/iam/roles/{Guid.NewGuid()}"),
            ("POST", "/api/v1/iam/roles"),
            ("PUT", $"/api/v1/iam/roles/{Guid.NewGuid()}"),
            ("DELETE", $"/api/v1/iam/roles/{Guid.NewGuid()}"),
            ("POST", "/api/v1/iam/roles/assign"),
            ("POST", "/api/v1/iam/roles/remove")
        };

        foreach (var (method, endpoint) in endpoints)
        {
            _output.WriteLine($"[STEP] {method} {endpoint} without authentication...");
            using var request = new HttpRequestMessage(new HttpMethod(method), endpoint);

            if (method is "POST" or "PUT")
            {
                request.Content = JsonContent.Create(new { });
            }

            var response = await _sharedHost.Host.HttpClient.SendAsync(request);
            _output.WriteLine($"[RECEIVED] {(int)response.StatusCode} {response.StatusCode}");
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        _output.WriteLine("[PASS] All role endpoints require authentication");
    }

    #endregion

    #region Helper Methods

    private async Task<AuthResponse?> CreateAdminUserAsync()
    {
        var username = $"admin_{Guid.NewGuid():N}";

        var createAdminRequest = new
        {
            Username = username,
            Email = $"{username}@test.com",
            Password = TestPassword
        };

        _output.WriteLine($"[HELPER] Creating admin user: {username}");
        var response = await _sharedHost.Host.HttpClient.PostAsJsonAsync("/api/v1/debug/create-admin", createAdminRequest);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            _output.WriteLine($"[ERROR] Create admin failed: {error}");
            return null;
        }

        // Now login to get tokens
        return await LoginAsync(username, TestPassword);
    }

    private async Task<AuthResponse?> RegisterAndGetTokenAsync()
    {
        var username = $"user_{Guid.NewGuid():N}";

        var registerRequest = new
        {
            Username = username,
            Password = TestPassword,
            ConfirmPassword = TestPassword,
            Email = $"{username}@test.com"
        };

        var response = await _sharedHost.Host.HttpClient.PostAsJsonAsync("/api/v1/auth/register", registerRequest);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            _output.WriteLine($"[ERROR] Registration failed: {error}");
            return null;
        }

        var content = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<AuthResponse>(content, JsonOptions);
    }

    private async Task<AuthResponse?> LoginAsync(string username, string password)
    {
        var loginRequest = new { Username = username, Password = password };
        var response = await _sharedHost.Host.HttpClient.PostAsJsonAsync("/api/v1/auth/login", loginRequest);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            _output.WriteLine($"[ERROR] Login failed: {error}");
            return null;
        }

        var content = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<AuthResponse>(content, JsonOptions);
    }

    #endregion

    #region Response Types

    private sealed class AuthResponse
    {
        public string AccessToken { get; set; } = string.Empty;
        public string RefreshToken { get; set; } = string.Empty;
        public string TokenType { get; set; } = string.Empty;
        public int ExpiresIn { get; set; }
        public UserInfo? User { get; set; }
    }

    private sealed class UserInfo
    {
        public Guid Id { get; set; }
        public string? Username { get; set; }
        public string? Email { get; set; }
        public IReadOnlyCollection<string>? Roles { get; set; }
        public IReadOnlyCollection<string>? Permissions { get; set; }
    }

    private sealed class RoleResponse
    {
        public Guid Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public bool IsSystemRole { get; set; }
        public IReadOnlyCollection<string> Parameters { get; set; } = [];
        public IReadOnlyCollection<ScopeTemplateInfo>? ScopeTemplates { get; set; }
    }

    private sealed class ScopeTemplateInfo
    {
        public string Type { get; set; } = string.Empty;
        public string PermissionPath { get; set; } = string.Empty;
        public IReadOnlyDictionary<string, string>? Parameters { get; set; }
    }

    private sealed class RoleListResponse
    {
        public IReadOnlyCollection<RoleResponse> Roles { get; set; } = [];
    }

    #endregion
}
