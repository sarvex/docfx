// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.DocAsCode.Common;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Outline;
using UglyToad.PdfPig.Outline.Destinations;
using UglyToad.PdfPig.Writer;

#nullable enable

namespace Microsoft.DocAsCode.Pdf;

/// <summary>
/// A PDF outline shares the same data shape as toc.json output
/// </summary>
class PdfOutline
{
    public bool EnablePdf { get; set; }
    public string? Name { get; set; }
    public PdfOutline[]? Items { get; set; }
    public string? Href { get; set; }
}

static class RunPdf2
{
    private readonly static string? s_toolVersion = Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyFileVersionAttribute>()?.Version;

    public static async Task CreatePdfForDirectory(string directory)
    {
        directory = Path.GetFullPath(directory);

        var jsonOptions = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

        var tocs = (
            from path in Directory.EnumerateFiles(directory, "toc.json", new EnumerationOptions { RecurseSubdirectories = true })
            let toc = JsonSerializer.Deserialize<PdfOutline>(File.ReadAllBytes(path), jsonOptions)
            let url = new Uri(Path.GetRelativePath(directory, path), UriKind.Relative)
            where toc != null && toc.EnablePdf
            select (path, url, toc)).ToList();

        if (tocs.Count <= 0)
        {
            Logger.LogWarning($"No toc.json is not available with {{ \"enablePdf\": true }} in {directory}");
            return;
        }

        using var server = Serve(directory);
        await server.StartAsync();
        var serverUrl = server.Urls.First();

        using var http = new HttpClient();
        using var playwright = await Playwright.Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync();
        var browserPagePool = new ConcurrentBag<IPage>();

        foreach (var (path, url, toc) in tocs)
        {
            NormalizePdfHref(new(new Uri(serverUrl), url), toc);
        }

        await Parallel.ForEachAsync(tocs, async (item, _) =>
        {
            var outputPath = Path.ChangeExtension(item.path, ".pdf");
            var bytes = await MergePdf(item.toc, PrintPdf);
            File.WriteAllBytes(outputPath, bytes);
        });

        void NormalizePdfHref(Uri baseUrl, PdfOutline node)
        {
            if (!string.IsNullOrEmpty(node.Href))
                node.Href = new Uri(baseUrl, node.Href).ToString();

            if (node.Items is not null)
                foreach (var item in node.Items)
                    NormalizePdfHref(baseUrl, item);
        }

        Task<byte[]?> PrintPdf(PdfOutline node)
        {
            return node.Href is { } href && href.StartsWith(serverUrl) ? PrintPdfFile(href) : Task.FromResult<byte[]?>(null);
        }

        async Task<byte[]?> PrintPdfFile(string url)
        {
            var page = browserPagePool.TryTake(out var existingPage) ? existingPage : await browser.NewPageAsync();

            try
            {
                var pageResponse = await page.GotoAsync(new Uri(url).GetLeftPart(UriPartial.Query));
                if (pageResponse is null || !pageResponse.Ok)
                {
                    Logger.LogWarning($"Cannot print PDF: [{pageResponse?.Status}] {url}");
                    return null;
                }

                return await page.PdfAsync();
            }
            finally
            {
                browserPagePool.Add(page);
            }
        }
    }

    private static WebApplication Serve(string directory)
    {
        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();

        var app = builder.Build();
        app.UseFileServer(new FileServerOptions
        {
            FileProvider = new PhysicalFileProvider(directory),
        });
        return app;
    }

    private static async Task<byte[]> MergePdf(PdfOutline outline, Func<PdfOutline, Task<byte[]?>> readPdf)
    {
        var currentPageNumber = 1;
        var pageNumbers = new Dictionary<PdfOutline, int>();
        var builder = new PdfDocumentBuilder();
        builder.DocumentInformation.Creator = $"docfx {s_toolVersion}";

        await WriteNode(outline);

        builder.Bookmarks = new(ToBookmarks(outline.Items));
        return builder.Build();

        async Task WriteNode(PdfOutline node)
        {
            if (await readPdf(node) is { } bytes)
                WritePdf(node, bytes);

            if (node.Items is not null)
                foreach (var item in node.Items)
                    await WriteNode(item);
        }

        void WritePdf(PdfOutline node, byte[] bytes)
        {
            var document = PdfDocument.Open(bytes);
            for (var i = 0; i < document.NumberOfPages; i++)
                builder.AddPage(document, i + 1);

            pageNumbers[node] = currentPageNumber;
            currentPageNumber += document.NumberOfPages;
        }

        BookmarkNode[] ToBookmarks(PdfOutline[]? nodes)
        {
            return Array.ConvertAll(nodes ?? Array.Empty<PdfOutline>(), ToBookmark);

            BookmarkNode ToBookmark(PdfOutline node)
            {
                if (pageNumbers.TryGetValue(node, out var pageNumber))
                {
                    return new DocumentBookmarkNode(
                        node.Name ?? "",
                        0,
                        new(pageNumber, ExplicitDestinationType.FitHorizontally, ExplicitDestinationCoordinates.Empty),
                        ToBookmarks(node.Items));
                }

                return new UriBookmarkNode(node.Name ?? "", 0, node.Href ?? "", ToBookmarks(node.Items));
            }
        }
    }
}
