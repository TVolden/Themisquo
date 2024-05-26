using System;

namespace Themisquo
{
    public interface ICommand
    {
        Guid ProcessId { get; }
        Guid Instance { get; }
    }
}
