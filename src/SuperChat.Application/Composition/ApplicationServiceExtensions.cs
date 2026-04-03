using Microsoft.Extensions.DependencyInjection;
using SuperChat.Application.Features.Feedback;
using SuperChat.Application.Features.WorkItems;
using SuperChat.Contracts.Features.Feedback;
using SuperChat.Contracts.Features.WorkItems;

namespace SuperChat.Application.Composition;

public static class ApplicationServiceExtensions
{
    public static IServiceCollection AddSuperChatApplication(this IServiceCollection services)
    {
        services.AddSingleton<IFeedbackService, FeedbackAppService>();
        services.AddSingleton<IMeetingWorkItemCommandService, MeetingWorkItemCommandAppService>();
        return services;
    }
}
