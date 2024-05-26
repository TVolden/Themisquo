using System.Threading.Tasks;

namespace Themisquo
{
    public interface IEventDispatcher
    {
        Task Dispatch(IEvent @event);
    }
}
