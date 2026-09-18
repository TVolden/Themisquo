using System.Threading;
using System.Threading.Tasks;

namespace Themisquo
{
    public interface IQueryDispatcher
    {
        Task<T> Dispatch<T>(IQuery<T> query, CancellationToken cancellationToken);
    }
}
