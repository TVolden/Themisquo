using System.Threading.Tasks;

namespace Themisquo
{
    public interface IEventObserver<TEvent> where TEvent : IEvent
    {
        Task Invoke(TEvent @event);
    }
}
