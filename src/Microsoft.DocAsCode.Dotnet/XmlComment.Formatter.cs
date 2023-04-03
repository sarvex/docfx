// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text;
using System.Text.RegularExpressions;
using System.Web;
using System.Xml.Linq;

#nullable enable

namespace Microsoft.DocAsCode.Dotnet;

partial class XmlComment
{
    private static readonly Regex RegionRegex = new(@"^\s*#region\s*(.*)$");
    private static readonly Regex XmlRegionRegex = new(@"^\s*<!--\s*<([^/\s].*)>\s*-->$");
    private static readonly Regex EndRegionRegex = new(@"^\s*#endregion\s*.*$");
    private static readonly Regex XmlEndRegionRegex = new(@"^\s*<!--\s*</(.*)>\s*-->$");

    public delegate (string name, string? href) ResolveCrefDelegate(string cref);

    public static string Format(string rawXml, ResolveCrefDelegate? resolveCref = null, Func<string, string?>? resolveCode = null)
    {
        // Recommended XML tags for C# documentation comments:
        // https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/xmldoc/recommended-tags#code
        //
        // Sandcastle XML Comments Guide:
        // https://ewsoftware.github.io/XMLCommentsGuide/html/4268757F-CE8D-4E6D-8502-4F7F2E22DDA3.htm
        var result = new StringBuilder();
        var root = XElement.Parse($"<tag>{rawXml}</tag>", LoadOptions.PreserveWhitespace);
        foreach (var node in root.Nodes())
        {
            FormatNode(node);
        }

        return result.ToString();

        StringBuilder FormatNode(XNode node)
        {
            return node switch
            {
                XText text => result.Append(HttpUtility.HtmlEncode(text.Value)),
                XElement element => FormatElement(element),
                _ => result,
            };

            StringBuilder FormatElement(XElement e)
            {
                return e.Name.LocalName switch
                {
                    "para" => FormatChildNodes("<p>", "</p>"),
                    "code" => FormatCode(),
                    "term" => FormatChildNodes("<span class=\"term\">", "</span>"),
                    "description" => FormatChildNodes(),
                    "list" => e.Attribute("type")?.Value switch
                    {
                        "table" => FormatTable(),
                        "number" => FormatList("<ol>", "</ol>"),
                        _ => FormatList("<ul>", "</ul>"),
                    },
                    "typeparamref" or "paramref" => FormatChildNodes("<c>", "</c>", e.Attribute("name")?.Value),
                    "see" or "seealso" => FormatSeeOrSeeAlso(),
                    "note" => FormatNote(),
                    _ => FormatChildNodes($"<{e.Name.LocalName}>", $"</{e.Name.LocalName}>"),
                };

                StringBuilder FormatChildNodes(string? open = null, string? close = null, string? content = null)
                {
                    if (open != null)
                        result.Append(open);

                    if (content is null || e.Nodes().Any())
                    {
                        foreach (var child in e.Nodes())
                            FormatNode(child);
                    }
                    else
                    {
                        result.Append(HttpUtility.HtmlEncode(content));
                    }

                    if (close != null)
                        result.Append(close);

                    return result;
                }

                StringBuilder FormatTable()
                {
                    result.Append("<table>");
                    if (e.Elements().FirstOrDefault(e => e.Name.LocalName is "listheader") is { } listheader)
                    {
                        result.Append("<thead><tr>");
                        foreach (var child in listheader.Nodes())
                        {
                            result.Append("<td>");
                            FormatNode(child);
                            result.Append("</td>");
                        }
                        result.Append("</tr></thead>");
                    }

                    result.Append("<tbody>");
                    foreach (var child in e.Elements())
                    {
                        if (child.Name.LocalName is "item")
                        {
                            result.Append("<td>");
                            FormatNode(child);
                            result.Append("</td>");
                        }
                    }
                    return result.Append("</tbody></table>");
                }

                StringBuilder FormatList(string open, string close)
                {
                    result.Append(open);
                    foreach (var child in e.Elements())
                    {
                        if (child.Name.LocalName is "item")
                        {
                            result.Append("<li>");
                            FormatNode(child);
                            result.Append("</li>");
                        }
                    }
                    return result.Append(close);
                }

                StringBuilder FormatSeeOrSeeAlso()
                {
                    var href = e.Attribute("href")?.Value;
                    if (!string.IsNullOrEmpty(href))
                    {
                        return FormatChildNodes($"<a href=\"{HttpUtility.HtmlAttributeEncode(href)}\">", "</a>", href);
                    }

                    if (e.Name.LocalName is "see" && e.Attribute("langword")?.Value is { } langword)
                    {
                        href = SymbolUrlResolver.GetLangwordUrl(langword);
                        return string.IsNullOrEmpty(href)
                            ? FormatChildNodes($"<c>", "</c>", langword)
                            : FormatChildNodes($"<a href=\"{HttpUtility.HtmlAttributeEncode(href)}\">", "</a>", langword);
                    }

                    if (resolveCref != null && e.Attribute("cref")?.Value is { } cref)
                    {
                        if (cref.StartsWith("!:"))
                            return FormatChildNodes($"<c class=\"xref\">", "</c>", cref.Substring(2));

                        (var name, href) = resolveCref(cref);

                        return string.IsNullOrEmpty(href)
                            ? FormatChildNodes($"<c class=\"xref\">", "</c>", name)
                            : FormatChildNodes($"<a class=\"xref\" href=\"{HttpUtility.HtmlAttributeEncode(href)}\">", "</a>", name);
                    }

                    return result;
                }

                StringBuilder FormatNote()
                {
                    var type = e.Attribute("type")?.Value ?? "note";
                    return FormatChildNodes($"<div class=\"{type}\"><h5>{HttpUtility.HtmlEncode(type)}</h5>", "</div>");
                }

                StringBuilder FormatCode()
                {
                    if (e.Attribute("source")?.Value is { } source)
                    {
                        var lang = Path.GetExtension(source).TrimStart('.');
                        var code = ResolveCodeSource(source, e.Attribute("region")?.Value, resolveCode);
                        return result.Append($"<pre><code class=\"lang-{HttpUtility.HtmlAttributeEncode(lang)}\">{HttpUtility.HtmlEncode(code)}</code></pre>");
                    }
                    return FormatChildNodes("<pre><code class=\"lang-csharp\">", "</code></pre>");
                }
            }
        }
    }

    private static string? ResolveCodeSource(string source, string? region, Func<string, string?>? resolveCode)
    {
        var code = resolveCode?.Invoke(source);
        if (code is null)
            return null;

        if (string.IsNullOrEmpty(region))
            return code;

        var (regionRegex, endRegionRegex) = Path.GetExtension(source).ToLowerInvariant() switch
        {
            ".xml" or ".xaml" or ".html" or ".cshtml" or ".vbhtml" => (XmlRegionRegex, XmlEndRegionRegex),
            _ => (RegionRegex, EndRegionRegex),
        };

        var lines = new List<string>();
        var regionCount = 0;
        var reader = new StringReader(code);
        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            var match = regionRegex.Match(line);
            if (match.Success)
            {
                var name = match.Groups[1].Value.Trim();
                if (name == region)
                {
                    ++regionCount;
                    continue;
                }
                else if (regionCount > 0)
                {
                    ++regionCount;
                }
            }
            else if (regionCount > 0 && endRegionRegex.IsMatch(line))
            {
                --regionCount;
                if (regionCount == 0)
                {
                    break;
                }
            }

            if (regionCount > 0)
            {
                lines.Add(line);
            }
        }

        return TrimEachLine(lines);
    }

    private static string TrimEachLine(List<string> lines)
    {
        var minLeadingWhitespace = int.MaxValue;
        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;

            var leadingWhitespace = 0;
            while (leadingWhitespace < line.Length && char.IsWhiteSpace(line[leadingWhitespace]))
                leadingWhitespace++;

            minLeadingWhitespace = Math.Min(minLeadingWhitespace, leadingWhitespace);
        }

        var builder = new StringBuilder();
        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line))
                builder.AppendLine();
            else
                builder.AppendLine(line.Substring(minLeadingWhitespace));
        }
        return builder.ToString();
    }
}
