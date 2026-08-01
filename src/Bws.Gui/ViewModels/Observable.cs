using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Bws.Gui.ViewModels;

/// <summary>
/// The smallest thing a view model needs to be one.
///
/// Hand rolled, and that is `ADR-15` rather than pride: a toolkit would generate this and
/// bring a dependency into a tool that runs with administrator rights on production
/// machines. The whole mechanism is twelve lines, and WPF has the interface built in.
/// </summary>
public abstract class Observable : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// Sets a field and says so, and only when the value actually moved.
    ///
    /// The equality check is not an optimisation. A list that raises a change for every
    /// value it was already showing redraws constantly, and `A10` promises a list that
    /// refreshes itself without becoming a torment to use.
    /// </summary>
    protected bool Set<T>(ref T field, T value, [CallerMemberName] string? property = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(property));

        return true;
    }

    /// <summary>
    /// Says a property changed without there being a field behind it.
    ///
    /// For the ones worked out from something else. A switch standing for a member of the
    /// query has no state of its own - it reads the query - so nothing sets it and the
    /// mechanism above never fires for it.
    /// </summary>
    protected void Raise(string property) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(property));
}
