using System.Windows;

[assembly:ThemeInfo(
    ResourceDictionaryLocation.None,            //where theme specific resource dictionaries are located
                                                //(used if a resource is not found in the page,
                                                // or application resource dictionaries)
    ResourceDictionaryLocation.SourceAssembly   //where the generic resource dictionary is located
                                                //(used if a resource is not found in the page,
                                                // app, or any theme specific resource dictionaries)
)]

// The view models are the testable half of the window, and they are internal because
// nothing outside this assembly consumes them. Opening them to the test project is the
// alternative to making them public for an audience of one.
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Bws.Gui.Tests")]
