namespace Aenaon.Domain.Exceptions;

/// <summary>Περιγράφει παραβίαση επιχειρησιακού κανόνα με σταθερό κωδικό σφάλματος.</summary>
public sealed class DomainRuleException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}
