using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.RegularExpressions;
using Domain.Authorization.Enums;
using Domain.Authorization.Models;

namespace Domain.Authorization.Constants;

public static class Permissions
{
    /// <summary>
    /// Permission identifier for the platform-wide read scope (`_read`).
    /// </summary>
    public static string RootReadIdentifier { get; }

    /// <summary>
    /// Permission identifier for the platform-wide write scope (`_write`).
    /// </summary>
    public static string RootWriteIdentifier { get; }

    /// <summary>
    /// Principal API permission node containing all API-specific scopes and operations.
    /// </summary>
    public static Permission Api { get; }

#if DEBUG
    /// <summary>
    /// Debug-only Security Operations permission root used for internal authorization scenarios.
    /// </summary>
    public static Permission SecOpsDebug { get; }
#endif

    /// <summary>
    /// Root-level permission nodes exposed by the platform.
    /// </summary>
    public static IReadOnlyList<Permission> PermissionTreeRoots { get; }

    public static IReadOnlyCollection<Permission> GetAll() => AllPermissions;

    private static readonly Permission[] AllPermissions;
    private static readonly Permission RootReadPermission;
    private static readonly Permission RootWritePermission;

    static Permissions()
    {
        RootReadPermission = CreateRootScope("_read", "Read access to all platform operations.", PermissionAccessCategory.Read);
        RootWritePermission = CreateRootScope("_write", "Write access to all platform operations.", PermissionAccessCategory.Write);

        RootReadIdentifier = RootReadPermission.Path;
        RootWriteIdentifier = RootWritePermission.Path;

        var roots = BuildPermissionRoots();
        foreach (var root in roots)
        {
            root.SetParent(null);
        }

        PermissionTreeRoots = Array.AsReadOnly(roots);

        Api = PermissionTreeRoots.First(static permission => string.Equals(permission.Identifier, "api", StringComparison.Ordinal));

#if DEBUG
        SecOpsDebug = PermissionTreeRoots.First(static permission => string.Equals(permission.Identifier, "sec_ops_debug", StringComparison.Ordinal));
#endif

        AllPermissions = BuildAllPermissions(roots);
    }

    private static Permission[] BuildPermissionRoots()
    {
        var roots = new List<Permission>
        {
            BuildApiRoot()
        };

#if DEBUG
        roots.Add(BuildSecOpsDebugRoot());
#endif

        return [.. roots];
    }

    private static Permission BuildApiRoot()
    {
        return Node(
            "api",
            "Permissions related to API operations.",
            "API operations",
            Node(
                "user",
                "Manage authenticated user profile and security settings.",
                "user APIs",
                new[] { "userId" },
                Node(
                    "profile",
                    "Manage user profile data.",
                    "profile management",
                    RLeaf("read", "Read the authenticated user's profile."),
                    WLeaf("update", "Update profile attributes such as display name."),
                    WLeaf("avatar", "Upload or replace the user's profile avatar.")
                ),
                Node(
                    "security",
                    "Manage user security credentials.",
                    "user security",
                    RLeaf("activity", "Read recent security-related activity."),
                    WLeaf("change_password", "Change the user's password with the current credential."),
                    WLeaf("reset_password", "Reset a password using an out-of-band token."),
                    WLeaf("mfa_configure", "Configure multi-factor authentication devices.")
                )
            ),
            Node(
                "portfolio",
                "Portfolio management operations scoped to a single user.",
                "portfolio APIs",
                new[] { "userId" },
                Node(
                    "accounts",
                    "Manage trading accounts within a portfolio.",
                    "portfolio accounts",
                    new[] { "accountId" },
                    RLeaf("list", "List all accounts for the portfolio."),
                    RLeaf("read", "Read account details."),
                    WLeaf("create", "Create a new account record."),
                    WLeaf("update", "Update account metadata."),
                    WLeaf("archive", "Archive or deactivate an account.")
                ),
                Node(
                    "positions",
                    "Manage open trading positions for the portfolio.",
                    "portfolio positions",
                    new[] { "positionId" },
                    RLeaf("list", "List open and closed positions."),
                    RLeaf("read", "Read a specific position."),
                    WLeaf("open", "Open a new manual position entry."),
                    WLeaf("close", "Close an existing position.")
                ),
                Node(
                    "performance",
                    "Analyze portfolio performance.",
                    "portfolio performance",
                    RLeaf("summary", "Read aggregate performance metrics."),
                    RLeaf("timeseries", "Read performance data points over time."),
                    RLeaf("distribution", "Read allocation distribution data.")
                )
            ),
            Node(
                "market",
                "Market intelligence and discovery APIs.",
                "market APIs",
                Node(
                    "assets",
                    "Manage the asset catalog.",
                    "asset catalog",
                    RLeaf("list", "List all supported assets."),
                    RLeaf("metadata", "Read metadata for a specific asset.", ["symbol"]),
                    RLeaf("search", "Search for assets matching filter criteria.")
                ),
                Node(
                    "prices",
                    "Price discovery operations.",
                    "price data",
                    new[] { "symbol" },
                    RLeaf("latest", "Read the latest price tick for an asset."),
                    RLeaf("historical", "Read historical candles for an asset.", ["granularity"])
                ),
                Node(
                    "orderbooks",
                    "Order book snapshot and stream data.",
                    "order book data",
                    new[] { "symbol" },
                    RLeaf("read", "Read a current order book snapshot."),
                    RLeaf("stream", "Subscribe to incremental order book updates.")
                )
            )
        );
    }

    private static Permission[] BuildAllPermissions(IReadOnlyList<Permission> roots)
    {
        var flattened = new List<Permission>();

        foreach (var root in roots)
        {
            flattened.AddRange(root.Traverse());
        }

        var permissions = new Permission[flattened.Count + 2];
        permissions[0] = RootReadPermission;
        permissions[1] = RootWritePermission;
        flattened.CopyTo(permissions, 2);
        return permissions;
    }

#if DEBUG
    private static Permission BuildSecOpsDebugRoot() =>
        Node(
            "sec_ops_debug",
            "Debug-only security-operations permissions for advanced authorization testing.",
            "debug security operation APIs",
            new[] { "tenantId" },
            Node(
                "v1",
                "Debug security operations version 1.",
                "debug security operations v1 APIs",
                Node(
                    "forensics",
                    "Manage debug-only forensic datasets and transfers.",
                    "debug forensic dataset operations",
                    new[] { "datasetId", "region" },
                    RLeaf("list", "List forensic datasets for a tenant."),
                    RLeaf("metadata", "Read forensic dataset metadata."),
                    RLeaf("download", "Download forensic dataset artifacts."),
                    RLeaf("exists", "Check whether a forensic dataset exists."),
                    WLeaf("upload", "Upload forensic dataset artifacts for analysis.", ["hash"]),
                    WLeaf("delete", "Delete forensic datasets from storage."),
                    WLeaf("quarantine", "Quarantine a dataset for additional investigation.", ["reason"])
                ),
                Node(
                    "playbooks",
                    "Coordinate debug-only rapid response playbooks.",
                    "debug rapid response playbook operations",
                    RLeaf("list", "List available rapid response playbooks."),
                    RLeaf("read", "Read a rapid response playbook definition.", ["playbookId"]),
                    WLeaf("deploy", "Deploy a rapid response playbook.", ["playbookId", "deviceId", "appId"]),
                    WLeaf("cancel", "Cancel an in-flight rapid response playbook.", ["playbookId", "executionId"])
                ),
                Node(
                    "incident_workspace",
                    "Manage debug-only incident workspaces.",
                    "debug incident workspace operations",
                    new[] { "incidentId", "region" },
                    RLeaf("timeline", "Read incident timeline records."),
                    RLeaf("audit_log", "Review incident audit logs."),
                    WLeaf("containment", "Trigger incident containment actions.", ["deviceId", "approvalCode"]),
                    WLeaf("eradication", "Trigger incident eradication workflows.", ["deviceId", "toolset"]),
                    WLeaf("cleanup", "Complete incident cleanup tasks.")
                )
            )
        );
#endif

    private static Permission Leaf(
        string identifier,
        string description,
        PermissionAccessCategory accessCategory = PermissionAccessCategory.Unspecified,
        string[]? parameters = null) => new()
    {
        Identifier = identifier,
        Description = description,
        Permissions = [],
        Parameters = parameters is { Length: > 0 } values ? [.. values] : [],
        AccessCategory = accessCategory
    };

    private static Permission RLeaf(string identifier, string description, string[]? parameters = null) =>
        Leaf(identifier, description, PermissionAccessCategory.Read, parameters);

    private static Permission WLeaf(string identifier, string description, string[]? parameters = null) =>
        Leaf(identifier, description, PermissionAccessCategory.Write, parameters);

    private static Permission Scope(string identifier, string description, PermissionAccessCategory accessCategory) => new()
    {
        Identifier = identifier,
        Description = description,
        Permissions = [],
        Parameters = [],
        AccessCategory = accessCategory
    };

    private static Permission ReadScope(string scopeLabel) =>
        Scope("_read", $"Read access to {scopeLabel}.", PermissionAccessCategory.Read);

    private static Permission WriteScope(string scopeLabel) =>
        Scope("_write", $"Write access to {scopeLabel}.", PermissionAccessCategory.Write);

    private static Permission Node(
        string identifier,
        string description,
        string scopeLabel,
        params object[] childrenAndParameters)
    {
        var parameters = Array.Empty<string>();
        var children = new List<Permission>();

        foreach (var item in childrenAndParameters)
        {
            switch (item)
            {
                case Permission permission:
                    children.Add(permission);
                    break;
                case string[] parameterArray:
                    parameters = parameterArray;
                    break;
                default:
                    throw new InvalidOperationException($"Unsupported node argument type '{item?.GetType().FullName}'.");
            }
        }

        var childPermissions = new Permission[children.Count + 2];
        childPermissions[0] = ReadScope(scopeLabel);
        childPermissions[1] = WriteScope(scopeLabel);

        if (children.Count > 0)
        {
            children.CopyTo(childPermissions, 2);
        }

        return new Permission
        {
            Identifier = identifier,
            Description = description,
            Permissions = childPermissions,
            Parameters = parameters.Length == 0 ? [] : parameters
        };
    }

    private static Permission CreateRootScope(string identifier, string description, PermissionAccessCategory category) => new()
    {
        Identifier = identifier,
        Description = description,
        Permissions = [],
        Parameters = [],
        AccessCategory = category
    };
}
