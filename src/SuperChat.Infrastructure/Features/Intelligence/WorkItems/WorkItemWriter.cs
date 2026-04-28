using Microsoft.Extensions.Logging;
using SuperChat.Contracts.Features.Intelligence.Meetings;
using SuperChat.Domain.Features.Intelligence;
using SuperChat.Infrastructure.Diagnostics;
using SuperChat.Infrastructure.Features.Intelligence.Extraction;

namespace SuperChat.Infrastructure.Features.Intelligence.WorkItems;

internal sealed class WorkItemWriter(
    IMeetingService meetingService,
    ILogger<WorkItemWriter> logger)
{
    public async Task AcceptRangeAsync(IEnumerable<ExtractedItem> items, CancellationToken cancellationToken)
    {
        var incomingItems = items.ToList();
        var filteredItems = incomingItems
            .Where(ExtractedItemFilters.ShouldKeep)
            .ToList();

        logger.LogInformation(
            "Prepared extracted items for writing. IncomingItemCount={IncomingItemCount}, RetainedItemCount={RetainedItemCount}, ItemKinds={ItemKinds}.",
            incomingItems.Count,
            filteredItems.Count,
            MessagePipelineTrace.SummarizeKinds(filteredItems));

        if (filteredItems.Count == 0)
        {
            return;
        }

        await meetingService.UpsertRangeAsync(filteredItems, cancellationToken);
        logger.LogInformation(
            "Completed writing extracted items. MeetingCount={MeetingCount}.",
            filteredItems.Count(item => item.Kind == ExtractedItemKind.Meeting));
    }
}
