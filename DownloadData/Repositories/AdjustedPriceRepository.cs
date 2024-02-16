using System.Runtime.CompilerServices;
using DownloadData.Data;

namespace DownloadData.Repositories
{
    public sealed class AdjustedPriceRepository(StockContext stockContext)
    {
        public async IAsyncEnumerable<(DateOnly, double)> GetSplitFactorAsync([EnumeratorCancellation] CancellationToken cancellationToken)
        {
            double lastSplitFactor = 1;
            await foreach (var splitFactor in stockContext.Splits.AsAsyncEnumerable().ConfigureAwait(false).WithCancellation(cancellationToken))
            {
                lastSplitFactor *= splitFactor.SplitFactor;
                yield return (splitFactor.ApprovalDate, lastSplitFactor);
            }
        }
    }
}