using System;

namespace Themisquo
{
    public interface IEvent
    {
        int Version { get; }
        DateTime EventTime { get; }
        Guid ProcessId { get; }
    }
}
