using System.Globalization;
using System.Security.Cryptography;
using Aenaon.Application.Tenancy;
using Microsoft.AspNetCore.DataProtection;

namespace Aenaon.Api.Ui;

/// <summary>Αποθηκεύει την επιλογή εργασίας σε κρυπτογραφημένο cookie, δεμένο με τον χρήστη.</summary>
public sealed class WorkspacePreferenceService(
    IDataProtectionProvider dataProtection,
    ILogger<WorkspacePreferenceService> logger)
{
    private const string CookieName = "__Host-AenaonERP.Workspace";
    private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(30);
    private readonly IDataProtector _protector =
        dataProtection.CreateProtector("AenaonERP.Ui.WorkspacePreference.v1");

    public bool TryRead(HttpContext context, Guid authenticatedUserId,
        out Guid tenantId, out Guid? storeId)
    {
        tenantId = Guid.Empty;
        storeId = null;

        if (authenticatedUserId == Guid.Empty ||
            !context.Request.Cookies.TryGetValue(CookieName, out string? protectedValue) ||
            string.IsNullOrWhiteSpace(protectedValue))
        {
            return false;
        }

        try
        {
            string[] parts = _protector.Unprotect(protectedValue).Split('|');
            if (parts.Length != 4 ||
                !Guid.TryParse(parts[0], out Guid cookieUserId) ||
                !Guid.TryParse(parts[1], out Guid selectedTenantId) ||
                !Guid.TryParse(parts[2], out Guid selectedStoreId) ||
                !long.TryParse(parts[3], NumberStyles.None,
                    CultureInfo.InvariantCulture, out long expiresUnix) ||
                cookieUserId != authenticatedUserId ||
                selectedTenantId == Guid.Empty ||
                DateTimeOffset.UtcNow.ToUnixTimeSeconds() >= expiresUnix)
            {
                return false;
            }

            tenantId = selectedTenantId;
            storeId = selectedStoreId == Guid.Empty ? null : selectedStoreId;
            return true;
        }
        catch (CryptographicException)
        {
            logger.LogWarning("Invalid protected workspace cookie was rejected");
            return false;
        }
        catch (FormatException)
        {
            logger.LogWarning("Malformed workspace cookie was rejected");
            return false;
        }
    }

    public void Save(HttpContext context, Guid authenticatedUserId, TenantWorkspaceSnapshot selection)
    {
        if (authenticatedUserId == Guid.Empty ||
            selection.Tenant is null ||
            selection.Status is not (TenantWorkspaceStatus.Ready or TenantWorkspaceStatus.NoStores))
        {
            throw new InvalidOperationException("A validated workspace is required.");
        }

        Guid tenantId = selection.Tenant.TenantId;
        Guid storeId = selection.Store?.StoreId ?? Guid.Empty;
        long expiration = DateTimeOffset.UtcNow.Add(Lifetime).ToUnixTimeSeconds();
        string plain = string.Join("|",
            authenticatedUserId.ToString("D"), tenantId.ToString("D"),
            storeId.ToString("D"), expiration.ToString(CultureInfo.InvariantCulture));

        context.Response.Cookies.Append(CookieName, _protector.Protect(plain),
            new CookieOptions
            {
                Path = "/",
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.Strict,
                IsEssential = true,
                MaxAge = Lifetime
            });

        logger.LogInformation("Workspace preference updated for {UserId}, {TenantId}, {StoreId}",
            authenticatedUserId, tenantId, selection.Store?.StoreId);
    }

    public static void Clear(HttpContext context)
    {
        context.Response.Cookies.Delete(CookieName, new CookieOptions
        {
            Path = "/",
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict
        });
    }
}
