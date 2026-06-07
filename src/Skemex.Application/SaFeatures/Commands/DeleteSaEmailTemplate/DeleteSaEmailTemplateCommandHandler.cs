using ErrorOr;
using Skemex.Application.Features.Abstractions;
using Skemex.Domain.Entities.EmailTemplates;
using Skemex.Domain.Repositories.Abstractions;
using Skemex.Domain.Services;

namespace Skemex.Application.SaFeatures.Commands.DeleteSaEmailTemplate;

public sealed class DeleteSaEmailTemplateCommandHandler(
    ICurrentUser currentUser,
    IBaseRepository<EmailTemplate> emailTemplateRepository)
    : ICommandHandler<DeleteSaEmailTemplateCommand>
{
    public async Task<ErrorOr<Success>> Handle(
        DeleteSaEmailTemplateCommand request,
        CancellationToken cancellationToken)
    {
        var access = SaSuperAdminContext.RequireSuperAdmin(currentUser);
        if (access.IsError)
        {
            return access.Errors;
        }

        var template = await emailTemplateRepository.GetAsync(
            filter: t => t.Id == request.TemplateId,
            cancellationToken: cancellationToken);

        if (template is null)
        {
            return Error.NotFound("EmailTemplate.NotFound", "Email template was not found.");
        }

        if (template.IsSystem)
        {
            return Error.Validation(
                "EmailTemplate.SystemProtected",
                "System email templates cannot be deleted.");
        }

        await emailTemplateRepository.DeleteAsync(template, cancellationToken);

        return Result.Success;
    }
}
