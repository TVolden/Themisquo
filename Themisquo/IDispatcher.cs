using System.Threading;
using System.Threading.Tasks;

namespace Themisquo
{
    public interface IDispatcher : IQueryDispatcher
    {
        Task Dispatch(ICommand command, CancellationToken cancellationToken);
    }
}
