namespace Aenaon.Infrastructure.Persistence;

/// <summary>Μόνιμη απόδειξη μίας επιτυχημένης εντολής για ασφαλή επανάληψη του ίδιου αιτήματος.</summary>
public sealed class MenuDraftOperationReceipt
{
    private MenuDraftOperationReceipt()
    {
        RequestHash = string.Empty;
    }

    public MenuDraftOperationReceipt(Guid tenantId, Guid requestId, Guid menuId,
        Guid? recordId, long revision, string requestHash, Guid actorUserId,
        DateTime occurredUtc)
    {
        TenantId = tenantId;
        RequestId = requestId;
        MenuId = menuId;
        RecordId = recordId;
        Revision = revision;
        RequestHash = requestHash;
        ActorUserId = actorUserId;
        OccurredUtc = occurredUtc;
    }

    public Guid TenantId { get; private set; }
    public Guid RequestId { get; private set; }
    public Guid MenuId { get; private set; }
    public Guid? RecordId { get; private set; }
    public long Revision { get; private set; }
    public string RequestHash { get; private set; }
    public Guid ActorUserId { get; private set; }
    public DateTime OccurredUtc { get; private set; }
}
