using System.Text.Json;
using GeneratorGame.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GeneratorGame.Pages.Api;

[IgnoreAntiforgeryToken]
public class EnotWebhookModel : PageModel
{
    private readonly EnotPaymentService _payments;

    public EnotWebhookModel(EnotPaymentService payments)
    {
        _payments = payments;
    }

    public async Task<IActionResult> OnPostAsync()
    {
        Request.EnableBuffering();
        using var reader = new StreamReader(Request.Body, leaveOpen: true);
        var rawBody = await reader.ReadToEndAsync();
        Request.Body.Position = 0;

        var data = await ReadDataAsync(rawBody);
        if (Request.Headers.TryGetValue("x-api-sha256-signature", out var sha256Signature))
        {
            data["x-api-sha256-signature"] = sha256Signature.ToString();
        }

        var result = await _payments.ProcessWebhookAsync(data, rawBody);
        if (!result.Ok)
        {
            Console.WriteLine($"ENOT WEBHOOK REJECTED: {result.Message}");
            return BadRequest(result.Message);
        }

        Console.WriteLine($"ENOT WEBHOOK OK: {result.Message}");
        return Content("OK");
    }

    private async Task<Dictionary<string, string>> ReadDataAsync(string rawBody)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (Request.HasFormContentType)
        {
            var form = await Request.ReadFormAsync();
            foreach (var pair in form)
            {
                result[pair.Key] = pair.Value.ToString();
            }

            return result;
        }

        if (!string.IsNullOrWhiteSpace(rawBody))
        {
            try
            {
                using var doc = JsonDocument.Parse(rawBody);
                if (doc.RootElement.ValueKind == JsonValueKind.Object)
                {
                    foreach (var prop in doc.RootElement.EnumerateObject())
                    {
                        result[prop.Name] = prop.Value.ValueKind == JsonValueKind.String
                            ? prop.Value.GetString() ?? ""
                            : prop.Value.ToString();
                    }
                }
            }
            catch
            {
                foreach (var part in rawBody.Split('&', StringSplitOptions.RemoveEmptyEntries))
                {
                    var pieces = part.Split('=', 2);
                    if (pieces.Length == 2)
                    {
                        result[Uri.UnescapeDataString(pieces[0])] = Uri.UnescapeDataString(pieces[1].Replace("+", " "));
                    }
                }
            }
        }

        return result;
    }
}
