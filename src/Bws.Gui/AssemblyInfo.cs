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

// And to the tests that need a real machine, since 2026-09-23, for one question the window's own
// test project must not ask: whether the desktop's shell answers. That depends on the session the
// tests run in, and the build server runs Bws.Gui.Tests whole - where Bws.Integration.Tests runs
// there only what is marked as running anywhere.
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Bws.Integration.Tests")]
