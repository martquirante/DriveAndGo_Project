using DriveAndGo_App.Contracts;
using DriveAndGo_App.Models;
using System.Text.Json;

namespace DriveAndGo_App.Services.Uploads;

public sealed class FileUploadService : IFileUploadService
{
    private readonly HttpClient _httpClient;

    public FileUploadService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<string?> PickAndUploadAsync(UploadCategory category, CancellationToken cancellationToken = default)
    {
        var result = await FilePicker.Default.PickAsync(new PickOptions
        {
            PickerTitle = "Select a file"
        });

        if (result == null)
        {
            return null;
        }

        await using var stream = await result.OpenReadAsync();
        using var content = new MultipartFormDataContent();
        using var fileContent = new StreamContent(stream);
        content.Add(fileContent, "file", result.FileName);

        var endpoint = category switch
        {
            UploadCategory.PaymentProof => "upload/payment-proof",
            UploadCategory.IssueImage => "upload/issue-image",
            UploadCategory.MessageAttachment => "upload/message-attachment",
            _ => "upload/payment-proof"
        };

        var response = await _httpClient.PostAsync(endpoint, content, cancellationToken);
        response.EnsureSuccessStatusCode();

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        return document.RootElement.TryGetProperty("url", out var url)
            ? url.GetString()
            : document.RootElement.TryGetProperty("Url", out var capitalUrl)
                ? capitalUrl.GetString()
                : null;
    }
}
