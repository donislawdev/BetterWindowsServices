using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace Bws.Gui;

/// <summary>
/// Walking up from whatever a press landed on.
///
/// <b>Whichever tree the node is actually in, and that is not defensive.</b> The thing under a pointer
/// can be a content element - a Run inside a TextBlock - rather than a visual, and
/// <c>VisualTreeHelper.GetParent</c> throws on those. A visual goes up the visual tree, which reaches
/// the template parts a press really lands on, and anything else goes up the logical one.
///
/// <b>A popup's content stops at its own root.</b> The root of a popup has no visual parent, so a
/// walk that starts inside the list under the search box never reaches the window - which is the
/// answer the window needs when it asks whether a press was beside the box.
/// </summary>
internal static class Trees
{
    /// <summary>The node above this one, or nothing at the top of the tree it is in.</summary>
    internal static DependencyObject? Parent(DependencyObject node) =>
        node is Visual or Visual3D
            ? VisualTreeHelper.GetParent(node)
            : LogicalTreeHelper.GetParent(node);

    /// <summary>Whether <paramref name="node"/> is <paramref name="container"/> or somewhere inside it.</summary>
    internal static bool Within(DependencyObject? node, DependencyObject? container)
    {
        if (container is null)
        {
            return false;
        }

        for (var at = node; at is not null; at = Parent(at))
        {
            if (ReferenceEquals(at, container))
            {
                return true;
            }
        }

        return false;
    }
}
