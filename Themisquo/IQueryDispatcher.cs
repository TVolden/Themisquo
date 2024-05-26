using System.Threading.Tasks;

namespace Themisquo
{
    public interface IQueryDispatcher
    {
        Task<T> Dispatch<T>(IQuery<T> query);
    }
}
