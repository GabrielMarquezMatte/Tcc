using System.Runtime.CompilerServices;
using DownloadData.Data;
using DownloadData.Entities;
using Microsoft.EntityFrameworkCore;

namespace DownloadData.Repositories
{
    public sealed class AdjustedPriceRepository(StockContext stockContext)
    {
        private const string QUERY = @"
        SELECT ""TickerId"", ""LastDate"", EXP(SUM(LN(""SplitFactor"")) OVER(PARTITION BY ""TickerId"" ORDER BY ""ApprovalDate"")) AS ""SplitFactor"", ""ApprovalDate"", ""Type""
        FROM ""Splits""
        ";
        public async IAsyncEnumerable<Split> GetSplitFactorAsync([EnumeratorCancellation] CancellationToken cancellationToken)
        {
            await foreach (var split in stockContext.Splits.FromSqlRaw(QUERY).AsAsyncEnumerable().WithCancellation(cancellationToken).ConfigureAwait(false))
            {
                yield return split;
            }
        }
    }
}