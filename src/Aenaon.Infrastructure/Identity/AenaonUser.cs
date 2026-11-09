using Microsoft.AspNetCore.Identity;

namespace Aenaon.Infrastructure.Identity;

/// <summary>Λογαριασμός Identity· η συμμετοχή σε επιχειρήσεις αποθηκεύεται ξεχωριστά.</summary>
public sealed class AenaonUser : IdentityUser<Guid>
{
    public AenaonUser()
    {
        Id = Guid.NewGuid();
    }
}
