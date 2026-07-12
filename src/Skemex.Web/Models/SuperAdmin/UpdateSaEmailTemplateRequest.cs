namespace Skemex.Web.Models.SuperAdmin;

public sealed class UpdateSaEmailTemplateRequest
{
    public string? Title { get; set; }

    public string? Subject { get; set; }

    public string? Body { get; set; }
}
