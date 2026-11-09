using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace Aenaon.Api.Commands;

public interface IUserSecretsWriter
{
    Task<bool> SaveAsync(string secretsId, string connectionString, CancellationToken cancellationToken);
}

/// <summary>Μεταφέρει το secret στο επίσημο εργαλείο του SDK μέσω UTF-8 stdin.</summary>
public sealed class DotnetUserSecretsWriter : IUserSecretsWriter
{
    public async Task<bool> SaveAsync(string secretsId, string connectionString, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(20));
        var startInfo = new ProcessStartInfo(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardInputEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)
        };
        foreach (string argument in new[] { "user-secrets", "set", "--id", secretsId })
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = new Process { StartInfo = startInfo };
        try
        {
            if (!process.Start())
            {
                return false;
            }

            Task<string> output = process.StandardOutput.ReadToEndAsync(timeout.Token);
            Task<string> error = process.StandardError.ReadToEndAsync(timeout.Token);
            string payload = JsonSerializer.Serialize(new Dictionary<string, string>
            {
                ["ConnectionStrings:Aenaon"] = connectionString
            });
            await process.StandardInput.WriteAsync(payload.AsMemory(), timeout.Token);
            process.StandardInput.Close();
            await process.WaitForExitAsync(timeout.Token);
            await Task.WhenAll(output, error);
            return process.ExitCode == 0;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            StopProcess(process);
            return false;
        }
        catch (OperationCanceledException)
        {
            StopProcess(process);
            throw;
        }
        catch (Exception error) when (error is Win32Exception or IOException or InvalidOperationException)
        {
            StopProcess(process);
            return false;
        }
    }

    private static void StopProcess(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (Exception error) when (error is Win32Exception or InvalidOperationException)
        {
            // Η διεργασία μπορεί να έχει ήδη τερματιστεί ή να μην ξεκίνησε.
        }
    }
}
