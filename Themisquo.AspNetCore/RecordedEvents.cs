using System.Collections.Generic;

namespace Themisquo.AspNetCore
{
    /// <summary>
    /// Scoped store of the events dispatched while handling the current command, populated by
    /// <see cref="RecordingEventDispatcher"/>. Registered by <c>AddThemisquoCommandLocations()</c>.
    /// </summary>
    public sealed class RecordedEvents
    {
        private readonly List<IEvent> events = [];

        public IReadOnlyList<IEvent> Events => events;

        internal void Record(IEvent @event) => events.Add(@event);
    }
}
