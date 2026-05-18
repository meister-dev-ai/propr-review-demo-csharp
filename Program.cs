using System.Globalization;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;

var options = ParseOptions(args);
var projectRoot = Directory.GetCurrentDirectory();
var generator = new SiteGenerator(projectRoot, options.OutputDirectory);

generator.Build();

static GeneratorOptions ParseOptions(string[] args)
{
    var outputDirectory = "dist";

    for (var index = 0; index < args.Length; index += 1)
    {
        if (args[index] == "--output" && index + 1 < args.Length)
        {
            outputDirectory = args[index + 1];
            index += 1;
        }
    }

    return new GeneratorOptions(outputDirectory);
}

internal sealed record GeneratorOptions(string OutputDirectory);

internal sealed class SiteGenerator
{
    private static readonly Regex FrontmatterRegex = new(@"\A---\s*\r?\n(?<frontmatter>[\s\S]*?)\r?\n---\s*\r?\n?(?<body>[\s\S]*)\z", RegexOptions.Compiled);
    private static readonly Regex InlineCodeRegex = new(@"`([^`]+)`", RegexOptions.Compiled);
    private static readonly Regex StrongRegex = new(@"\*\*(.+?)\*\*", RegexOptions.Compiled);
    private static readonly Regex HtmlTagRegex = new(@"<[^>]+>", RegexOptions.Compiled);

    private readonly string _contentDirectory;
    private readonly string _staticDirectory;
    private readonly string _outputDirectory;

    public SiteGenerator(string projectRoot, string outputDirectory)
    {
        _contentDirectory = Path.Combine(projectRoot, "content");
        _staticDirectory = Path.Combine(projectRoot, "static");
        _outputDirectory = Path.GetFullPath(Path.Combine(projectRoot, outputDirectory));
    }

    public void Build()
    {
        var site = LoadSite();

        if (Directory.Exists(_outputDirectory))
        {
            Directory.Delete(_outputDirectory, recursive: true);
        }

        Directory.CreateDirectory(_outputDirectory);
        CopyStaticAssets();
        WritePage(Path.Combine(_outputDirectory, "index.html"), RenderStandardPage(site, site.HomePage));

        foreach (var page in site.Pages.Where(page => page.Slug != "index"))
        {
            WriteRoutePage(page.Path, RenderStandardPage(site, page));
        }

        foreach (var section in site.Sections)
        {
            WriteRoutePage(section.Path, RenderSectionPage(site, section));

            foreach (var article in section.Articles)
            {
                WriteRoutePage(article.Path, RenderArticlePage(site, section, article));
            }
        }
    }

    private SiteModel LoadSite()
    {
        var pages = Directory
            .GetFiles(_contentDirectory, "*.md", SearchOption.TopDirectoryOnly)
            .Select(BuildPage)
            .OrderBy(page => SortOrder(page.Order))
            .ThenBy(page => page.Title, StringComparer.Ordinal)
            .ToList();

        var sections = Directory
            .GetDirectories(_contentDirectory)
            .Select(BuildSection)
            .OrderBy(section => SortOrder(section.Order))
            .ThenBy(section => section.Title, StringComparer.Ordinal)
            .ToList();

        var navigation = pages
            .Select(page => new NavigationItem(page.Title, page.Path, page.Description, page.Order))
            .Concat(sections.Select(section => new NavigationItem(section.Title, section.Path, section.Description, section.Order)))
            .OrderBy(item => SortOrder(item.Order))
            .ThenBy(item => item.Title, StringComparer.Ordinal)
            .ToList();

        var homePage = pages.FirstOrDefault(page => page.Slug == "index")
            ?? throw new InvalidOperationException("Expected content/index.md to exist.");

        return new SiteModel(
            Title: homePage.Title,
            Description: homePage.Description,
            Navigation: navigation,
            HomePage: homePage,
            Pages: pages,
            Sections: sections);
    }

    private PageModel BuildPage(string filePath)
    {
        var slug = Path.GetFileNameWithoutExtension(filePath);
        var markdown = ParseMarkdownFile(filePath);
        var path = slug == "index" ? "/" : $"/{slug}/";

        return new PageModel(
            Slug: slug,
            Path: path,
            Title: markdown.Frontmatter.GetValueOrDefault("title") ?? TitleFromSlug(slug),
            Description: markdown.Frontmatter.GetValueOrDefault("description") ?? string.Empty,
            Order: ParseOptionalInt(markdown.Frontmatter.GetValueOrDefault("order")),
            Html: RenderMarkdown(markdown.Body));
    }

    private SectionModel BuildSection(string directoryPath)
    {
        var slug = Path.GetFileName(directoryPath);
        var indexPath = Path.Combine(directoryPath, "_index.md");

        if (!File.Exists(indexPath))
        {
            throw new InvalidOperationException($"Expected section index file at '{indexPath}'.");
        }

        var markdown = ParseMarkdownFile(indexPath);
        var articles = Directory
            .GetFiles(directoryPath, "*.md", SearchOption.TopDirectoryOnly)
            .Where(path => !string.Equals(Path.GetFileName(path), "_index.md", StringComparison.Ordinal))
            .Select(path => BuildArticle(slug, path))
            .OrderByDescending(article => article.DateSortKey, StringComparer.Ordinal)
            .ThenBy(article => SortOrder(article.Order))
            .ThenBy(article => article.Title, StringComparer.Ordinal)
            .ToList();

        return new SectionModel(
            Slug: slug,
            Path: $"/{slug}/",
            Title: markdown.Frontmatter.GetValueOrDefault("title") ?? TitleFromSlug(slug),
            Description: markdown.Frontmatter.GetValueOrDefault("description") ?? string.Empty,
            Order: ParseOptionalInt(markdown.Frontmatter.GetValueOrDefault("order")),
            Html: RenderMarkdown(markdown.Body),
            Articles: articles);
    }

    private ArticleModel BuildArticle(string sectionSlug, string filePath)
    {
        var slug = Path.GetFileNameWithoutExtension(filePath);
        var markdown = ParseMarkdownFile(filePath);
        var description = markdown.Frontmatter.GetValueOrDefault("description") ?? string.Empty;
        var html = RenderMarkdown(markdown.Body);

        return new ArticleModel(
            Slug: slug,
            Path: $"/{sectionSlug}/{slug}/",
            Title: markdown.Frontmatter.GetValueOrDefault("title") ?? TitleFromSlug(slug),
            Description: description,
            Summary: markdown.Frontmatter.GetValueOrDefault("summary") ?? description,
            DateDisplay: NormalizeDateDisplay(markdown.Frontmatter.GetValueOrDefault("date")),
            DateSortKey: NormalizeDateSortKey(markdown.Frontmatter.GetValueOrDefault("date")),
            ReadingTime: EstimateReadingTime(html),
            Order: ParseOptionalInt(markdown.Frontmatter.GetValueOrDefault("order")),
            Html: html);
    }

    private ParsedMarkdown ParseMarkdownFile(string filePath)
    {
        var source = File.ReadAllText(filePath);
        var match = FrontmatterRegex.Match(source);

        if (!match.Success)
        {
            return new ParsedMarkdown(new Dictionary<string, string>(StringComparer.Ordinal), source.Trim());
        }

        var frontmatter = new Dictionary<string, string>(StringComparer.Ordinal);
        var frontmatterLines = match.Groups["frontmatter"].Value.Split(["\r\n", "\n"], StringSplitOptions.None);

        foreach (var line in frontmatterLines)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var separatorIndex = line.IndexOf(':');
            if (separatorIndex <= 0)
            {
                continue;
            }

            var key = line[..separatorIndex].Trim();
            var value = line[(separatorIndex + 1)..].Trim();
            frontmatter[key] = value;
        }

        return new ParsedMarkdown(frontmatter, match.Groups["body"].Value.Trim());
    }

    private static int SortOrder(int? value) => value ?? int.MaxValue;

    private static int? ParseOptionalInt(string? value)
    {
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;
    }

    private static string? NormalizeDateSortKey(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return DateOnly.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)
            ? parsed.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
            : value;
    }

    private static string? NormalizeDateDisplay(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return DateOnly.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)
            ? parsed.ToString("MMMM d, yyyy", CultureInfo.InvariantCulture)
            : value;
    }

    private static string TitleFromSlug(string slug)
    {
        if (slug == "index")
        {
            return "Home";
        }

        return string.Join(
            " ",
            slug.Split('-', StringSplitOptions.RemoveEmptyEntries)
                .Select(part => char.ToUpperInvariant(part[0]) + part[1..]));
    }

    private void CopyStaticAssets()
    {
        var sourcePath = Path.Combine(_staticDirectory, "styles.css");
        var destinationPath = Path.Combine(_outputDirectory, "styles.css");
        File.Copy(sourcePath, destinationPath, overwrite: true);
    }

    private void WriteRoutePage(string routePath, string html)
    {
        var relativeDirectory = routePath == "/"
            ? string.Empty
            : routePath.Trim('/').Replace('/', Path.DirectorySeparatorChar);
        var outputDirectory = Path.Combine(_outputDirectory, relativeDirectory);
        Directory.CreateDirectory(outputDirectory);
        WritePage(Path.Combine(outputDirectory, "index.html"), html);
    }

    private static void WritePage(string filePath, string html)
    {
        File.WriteAllText(filePath, html, Encoding.UTF8);
    }

    private string RenderStandardPage(SiteModel site, PageModel page)
    {
        return RenderDocument(
            site,
            page.Title,
            page.Description,
            page.Path,
            $$"""
            <article class="panel stack-gap">
              {{RenderPanelDescription(page.Description)}}
              <div class="markdown">{{page.Html}}</div>
            </article>
            """);
    }

    private string RenderSectionPage(SiteModel site, SectionModel section)
    {
        var articleCards = string.Join(Environment.NewLine, section.Articles.Select(article => $$"""
            <article class="article-card">
              <div class="article-card-meta">
                {{RenderArticleMeta(article)}}
              </div>
              <h2><a href="{{article.Path}}">{{HtmlEncode(article.Title)}}</a></h2>
              <p>{{HtmlEncode(article.Summary)}}</p>
            </article>
            """));

        var articleSection = section.Articles.Count == 0
            ? ""
            : $$"""
            <section class="stack-gap" aria-labelledby="articles-heading">
              <h2 id="articles-heading">Articles</h2>
              <div class="article-list">
                {{articleCards}}
              </div>
            </section>
            """;

        return RenderDocument(
            site,
            section.Title,
            section.Description,
            section.Path,
            $$"""
            <article class="panel stack-gap">
              {{RenderPanelDescription(section.Description)}}
              <div class="markdown">{{section.Html}}</div>
              {{articleSection}}
            </article>
            """);
    }

    private string RenderArticlePage(SiteModel site, SectionModel section, ArticleModel article)
    {
        return RenderDocument(
            site,
            article.Title,
            article.Description,
            article.Path,
            $$"""
            <article class="panel stack-gap">
              <a class="back-link" href="{{section.Path}}">Back to {{HtmlEncode(section.Title)}}</a>
              {{RenderArticleHeader(article)}}
              <div class="markdown">{{article.Html}}</div>
            </article>
            """);
    }

    private string RenderDocument(SiteModel site, string pageTitle, string description, string currentPath, string mainContent)
    {
        var fullTitle = currentPath == "/" ? site.Title : $"{pageTitle} | {site.Title}";
        var navigation = string.Join(Environment.NewLine, site.Navigation.Select(item =>
        {
            var className = item.Path == currentPath ? "nav-link nav-link-active" : "nav-link";
            return $"<a class=\"{className}\" href=\"{item.Path}\">{HtmlEncode(item.Title)}</a>";
        }));

        return $$"""
        <!DOCTYPE html>
        <html lang="en">
        <head>
          <meta charset="utf-8">
          <meta name="viewport" content="width=device-width, initial-scale=1">
          <title>{{HtmlEncode(fullTitle)}}</title>
          <meta name="description" content="{{HtmlEncode(description)}}">
          <link rel="stylesheet" href="/styles.css">
        </head>
        <body>
          <div class="app-shell">
            <header class="site-header">
              <div>
                <a class="site-title" href="/">{{HtmlEncode(site.Title)}}</a>
                <p class="site-tagline">{{HtmlEncode(site.Description)}}</p>
              </div>
              <nav class="site-nav" aria-label="Primary navigation">
                {{navigation}}
              </nav>
            </header>
            <main>
              {{mainContent}}
            </main>
          </div>
        </body>
        </html>
        """;
    }

    private static string RenderPanelDescription(string description)
    {
        return string.IsNullOrWhiteSpace(description)
            ? string.Empty
            : $"<header class=\"panel-header\"><p>{HtmlEncode(description)}</p></header>";
    }

    private static string RenderArticleHeader(ArticleModel article)
    {
        var meta = RenderArticleMeta(article);
        return string.IsNullOrWhiteSpace(meta)
            ? string.Empty
            : $"<header class=\"panel-header\"><p>{meta}</p></header>";
    }

    private static string RenderArticleMeta(ArticleModel article)
    {
        var parts = new List<string>();

        if (article.DateDisplay is not null)
        {
            parts.Add($"<span>{HtmlEncode(article.DateDisplay)}</span>");
        }

        if (!string.IsNullOrWhiteSpace(article.ReadingTime))
        {
            parts.Add($"<span>{HtmlEncode(article.ReadingTime)}</span>");
        }

        if (!string.IsNullOrWhiteSpace(article.Description))
        {
            parts.Add($"<span>{HtmlEncode(article.Description)}</span>");
        }

        return string.Join(string.Empty, parts);
    }

    private static string? EstimateReadingTime(string html)
    {
        var renderedText = HtmlTagRegex.Replace(html, " ");
        var wordCount = renderedText
            .Split([' ', '\r', '\n', '\t'], StringSplitOptions.RemoveEmptyEntries)
            .Length;

        if (wordCount == 0)
        {
            return null;
        }

        var minutes = Math.Max(1, (int)Math.Ceiling(wordCount / 200d));
        return $"{minutes} min read";
    }

    private static string RenderMarkdown(string markdown)
    {
        var lines = markdown.Replace("\r\n", "\n").Split('\n');
        var html = new StringBuilder();
        var paragraphLines = new List<string>();
        var listItems = new List<string>();

        void FlushParagraph()
        {
            if (paragraphLines.Count == 0)
            {
                return;
            }

            var content = string.Join(" ", paragraphLines);
            html.Append("<p>").Append(RenderInline(content)).AppendLine("</p>");
            paragraphLines.Clear();
        }

        void FlushList()
        {
            if (listItems.Count == 0)
            {
                return;
            }

            html.AppendLine("<ul>");
            foreach (var item in listItems)
            {
                html.Append("  <li>").Append(RenderInline(item)).AppendLine("</li>");
            }

            html.AppendLine("</ul>");
            listItems.Clear();
        }

        foreach (var rawLine in lines)
        {
            var line = rawLine.Trim();

            if (line.Length == 0)
            {
                FlushParagraph();
                FlushList();
                continue;
            }

            if (line.StartsWith("# ", StringComparison.Ordinal))
            {
                FlushParagraph();
                FlushList();
                html.Append("<h1>").Append(RenderInline(line[2..].Trim())).AppendLine("</h1>");
                continue;
            }

            if (line.StartsWith("- ", StringComparison.Ordinal))
            {
                FlushParagraph();
                listItems.Add(line[2..].Trim());
                continue;
            }

            FlushList();
            paragraphLines.Add(line);
        }

        FlushParagraph();
        FlushList();

        return html.ToString().Trim();
    }

    private static string RenderInline(string text)
    {
        var encoded = HtmlEncode(text);
        encoded = StrongRegex.Replace(encoded, "<strong>$1</strong>");
        encoded = InlineCodeRegex.Replace(encoded, "<code>$1</code>");
        return encoded;
    }

    private static string HtmlEncode(string value) => WebUtility.HtmlEncode(value);
}

internal sealed record ParsedMarkdown(IReadOnlyDictionary<string, string> Frontmatter, string Body);

internal sealed record NavigationItem(string Title, string Path, string Description, int? Order);

internal sealed record PageModel(string Slug, string Path, string Title, string Description, int? Order, string Html);

internal sealed record ArticleModel(
    string Slug,
    string Path,
    string Title,
    string Description,
    string Summary,
    string? DateDisplay,
    string? DateSortKey,
    string? ReadingTime,
    int? Order,
    string Html);

internal sealed record SectionModel(
    string Slug,
    string Path,
    string Title,
    string Description,
    int? Order,
    string Html,
    IReadOnlyList<ArticleModel> Articles);

internal sealed record SiteModel(
    string Title,
    string Description,
    IReadOnlyList<NavigationItem> Navigation,
    PageModel HomePage,
    IReadOnlyList<PageModel> Pages,
    IReadOnlyList<SectionModel> Sections);
