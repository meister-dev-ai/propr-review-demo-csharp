# Bug Scenarios

These feature branches are intentionally defective review targets built from a clean `main` branch.

- `feature/bug_1`: add reading-time metadata but compute it from rendered HTML instead of markdown text
- `feature/bug_2`: add a latest-posts panel but sort posts in ascending date order
- `feature/bug_3`: render formatted article summaries as raw HTML and widen the HTML injection surface
- `feature/bug_4`: add related-post navigation but exclude the current article incorrectly when dates are missing or titles collide
- `feature/bug_5`: make article cards fully clickable using nested interactive elements
- `feature/bug_6`: generate a sitemap but omit article pages from the output
- `feature/bug_7`: add a draft preview panel but read the preview file from an unchecked environment path under the project root
- `feature/bug_8`: render an about-page intro snippet but inject the page description as raw HTML
- `feature/bug_9`: add article title filtering but treat the filter as an unchecked regular expression
- `feature/bug_10`: run an optional HTML formatter but execute the environment-provided formatter command through `/bin/sh`
- `feature/bug_11`: add build summary logging but append unsanitized build details to an environment-selected file
- `feature/bug_12`: add navigation cache keys but change equality and hashing to disagree for duplicate paths
- `feature/bug_13`: use a shared page title comparer but compare each title to itself so page ordering breaks
- `feature/bug_14`: capture a startup note but read `_outputDirectory` before the constructor assigns it
- `feature/bug_15`: refactor markdown source loading but leave the file reader undisposed
- `feature/bug_16`: add date display fallback but swallow invalid dates by returning `null` instead of the original value
- `feature/bug_17`: handle empty article descriptions but duplicate the same description check and skip valid metadata output
- `feature/bug_18`: show section article counts but index the first article even when the section is empty
- `feature/bug_19`: add a page body class but call `string.Format` with too few arguments and crash rendering
- `feature/bug_20`: cache page metadata by path but build a duplicate-key map that is never used
- `feature/bug_21`: track markdown content state but reset the flag incorrectly and never use it
- `feature/bug_22`: show article canonical paths but build them from the date sort key and null-forgive missing dates
- `feature/bug_23`: use a buffered page writer but never dispose the stream or flush the writer
- `feature/bug_24`: highlight matching terms but inject the raw search term into `<mark>` tags after encoding
- `feature/bug_25`: load an optional imported page but validate one path and then rebuild a different rooted path
- `feature/bug_26`: retry order parsing but repeat the same parse twice and add no real fallback
- `feature/semantic_bug_2`: document that user-facing sections must reuse the shared section/article pipeline but add showcase through a bespoke parallel model
