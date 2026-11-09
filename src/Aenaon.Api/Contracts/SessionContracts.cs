using Aenaon.Application.Tenancy;

namespace Aenaon.Api.Contracts;

public sealed record LoginRequest(string? UserName, string? Password);
public sealed record CsrfTokenResponse(string Token);
public sealed record CurrentSessionResponse(Guid UserId, string UserName, IReadOnlyList<AccessibleTenant> Tenants);
public sealed record TenantPermissionResponse(Guid TenantId, Guid? StoreId, TenantPermission Permission, bool Allowed);
