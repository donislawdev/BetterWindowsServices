// Tests in this assembly run one at a time.
//
// Not a default and not caution: it was measured, on 2026-08-02, by breaking it. This project
// has a guard that holds the managed heap to a ratchet over a thousand ticks of the view model,
// and the day a second test class started loading WPF UI's resource dictionaries - six
// megabytes of assembly and eight hundred keys - that guard went red with a 2.8 MB "leak" it
// had not caused. xUnit runs test classes in parallel by default, so the two were allocating in
// the same process while one of them was measuring.
//
// The failure mode is the one worth naming: a memory guard is only meaningful if nothing else
// allocates while it measures, and nothing in xUnit says so. A future test that merely creates
// objects would have made this guard red at random and taught everybody to rerun until green -
// which is the same as not having it.
//
// Cost: this assembly held 39 tests running in 414 ms when the line was added, so serialising
// them is not a trade anybody has to think about.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
