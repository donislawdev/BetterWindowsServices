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

/// <summary>
/// A view model that can say a value somebody typed is wrong, in the words WPF listens for.
///
/// <b>SINCE 2026-09-15, FOR THE BOX OF SECONDS ON THE PLAN SHEET, AND IT IS THE FRAMEWORK'S OWN
/// MECHANISM RATHER THAN A FLAG OF OURS.</b> A binding reads <see cref="INotifyDataErrorInfo"/>
/// on its own - <c>ValidatesOnNotifyDataErrors</c> has been on by default since .NET 4.5 - and
/// sets <c>Validation.HasError</c> on the control, which a style can trigger on WITHOUT naming a
/// property of this assembly. That last part is the reason: `docs/10` trap 10 says a theme file
/// cannot name a type of its own assembly, so a trigger on "our" boolean was never available to
/// the theme, and the component catalogue can put ANY control into its wrong state the same way,
/// generically, by marking its binding invalid.
///
/// <b>The rule stays in the model.</b> What is wrong and why is decided by whoever inherits this,
/// in a property a test can read without a window - this only carries the answer to the binding.
/// Nothing here validates: <see cref="ErrorsOf"/> is the one question, and the default answer is
/// that nothing is ever wrong.
/// </summary>
public abstract class Checked : Observable, INotifyDataErrorInfo
{
    public event EventHandler<DataErrorsChangedEventArgs>? ErrorsChanged;

    /// <summary>Whether anything typed into this model is wrong right now.</summary>
    public virtual bool HasErrors => false;

    /// <summary>What is wrong with one property, as sentences a person reads - nothing when nothing is.</summary>
    protected virtual IEnumerable<string> ErrorsOf(string property) => [];

    /// <summary>
    /// Every sentence about one property, or every sentence about everything when asked with no
    /// name - the shape the interface asks for.
    /// </summary>
    public System.Collections.IEnumerable GetErrors(string? propertyName) =>
        propertyName is null ? [] : ErrorsOf(propertyName);

    /// <summary>
    /// Tells the binding to ask <see cref="ErrorsOf"/> again for one property. Called wherever the
    /// answer can have changed - which includes places that never touched the value, when what
    /// counts as wrong depends on something else, as the box of seconds does on whether it is shown.
    /// </summary>
    protected void RaiseErrors(string property) =>
        ErrorsChanged?.Invoke(this, new DataErrorsChangedEventArgs(property));
}
