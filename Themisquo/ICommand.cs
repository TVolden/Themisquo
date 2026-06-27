using System;

namespace Themisquo
{
    public interface ICommand
    {
        Guid Instance { get; }
    }
}
