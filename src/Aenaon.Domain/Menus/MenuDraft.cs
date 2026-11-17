using Aenaon.Domain.Exceptions;

namespace Aenaon.Domain.Menus;

/// <summary>Μοναδικός αναθεωρήσιμος χώρος εργασίας ανά μενού, χωρίς Published ή POS κατάσταση.</summary>
public sealed class MenuDraft
{
    private MenuDraft()
    {
    }

    private MenuDraft(Guid tenantId, Guid menuId, DateTime editedUtc)
    {
        MenuText.Identity(tenantId, menuId, "μενού");
        VerifyUtc(editedUtc);
        TenantId = tenantId;
        MenuId = menuId;
        Revision = 1;
        EditedUtc = editedUtc;
    }

    public static MenuDraft Create(Guid tenantId, Guid menuId, DateTime editedUtc) =>
        new(tenantId, menuId, editedUtc);

    /// <summary>Αυξάνει τη revision για optimistic concurrency σε κάθε επιτυχημένη μεταβολή.</summary>
    public void Touch(DateTime editedUtc)
    {
        VerifyUtc(editedUtc);
        if (Revision == long.MaxValue)
        {
            throw new DomainRuleException("menu.revision.exhausted", "Το προσχέδιο δεν δέχεται άλλες αναθεωρήσεις.");
        }

        Revision++;
        EditedUtc = editedUtc;
    }

    private static void VerifyUtc(DateTime value)
    {
        if (value.Kind != DateTimeKind.Utc)
        {
            throw new DomainRuleException("menu.time.utc_required",
                "Η ημερομηνία πρέπει να χρησιμοποιεί UTC.");
        }
    }

    public Guid MenuId { get; private set; }
    public Guid TenantId { get; private set; }
    public long Revision { get; private set; }
    public DateTime EditedUtc { get; private set; }
}
