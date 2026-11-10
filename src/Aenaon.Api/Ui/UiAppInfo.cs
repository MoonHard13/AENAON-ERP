namespace Aenaon.Api.Ui;

/// <summary>Εμφανίζει έκδοση από το πραγματικό assembly, χωρίς επανάληψη χειροκίνητων αριθμών στο UI.</summary>
public static class UiAppInfo
{
    public static string Version { get; } =
        typeof(Program).Assembly.GetName().Version?.ToString(3) ?? "unavailable";
}
