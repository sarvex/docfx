// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Xml;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Shared.Extensions;
using Microsoft.CodeAnalysis.Shared.Utilities;
using Microsoft.DocAsCode.DataContracts.ManagedReference;

#nullable enable

namespace Microsoft.DocAsCode.Dotnet;

internal partial class XmlComment
{
    public string? Summary { get; init; }

    public string? Remarks { get; init; }

    public string? Returns { get; init; }

    public List<ExceptionInfo> Exceptions { get; init; } = default!;

    public List<LinkInfo> SeeAlsos { get; init; } = default!;

    public List<string> Examples { get; init; } = default!;

    public Dictionary<string, string?> Parameters { get; init; } = default!;

    public Dictionary<string, string?> TypeParameters { get; init; } = default!;

    public string? GetParameter(string name)
    {
        return Parameters.TryGetValue(name, out var value) ? value : null;
    }

    public string? GetTypeParameter(string name)
    {
        return TypeParameters.TryGetValue(name, out var value) ? value : null;
    }

    public static XmlComment Parse(ISymbol symbol, Compilation compilation, XmlCommentParserContext? context = null)
    {
        var seeAlsos = new List<LinkInfo>();
        var documentationComment = symbol.GetDocumentationComment(compilation, expandIncludes: true, expandInheritdoc: true);

        XmlFragmentParser.ParseFragment(documentationComment.FullXmlFragment, PopulateSeeAlsos, 0);

        return new()
        {
            Parameters = documentationComment.ParameterNames.ToDictionary(n => n, n => FormatComment(documentationComment.GetParameterText(n))),
            TypeParameters = documentationComment.TypeParameterNames.ToDictionary(n => n, n => FormatComment(documentationComment.GetTypeParameterText(n))),
            Summary = FormatComment(documentationComment.SummaryText),
            Remarks = FormatComment(documentationComment.RemarksText),
            Examples = FormatComment(documentationComment.ExampleText) is { } example ? new() { example } : new(),
            Exceptions = documentationComment.ExceptionTypes.Select(e => new ExceptionInfo
            {
                Type = e,
                Description = FormatComment(string.Join("\n", documentationComment.GetExceptionTexts(e))),
            }).ToList(),
            SeeAlsos = seeAlsos, // TODO: top level seealsos
        };

        string? FormatComment(string? rawXml)
        {
            if (string.IsNullOrEmpty(rawXml))
                return null;

            return Format(rawXml, ResolveCref, context?.ResolveCode);
        }

        (string name, string? href) ResolveCref(string cref)
        {
            var symbol = DocumentationCommentId.GetFirstSymbolForDeclarationId(cref, compilation);
            if (symbol is null)
                return (cref, null);

            return (SymbolFormatter.GetNameWithType(symbol, SyntaxLanguage.CSharp), SymbolUrlResolver.GetSymbolUrl(symbol, compilation));
        }

        void PopulateSeeAlsos(XmlReader reader, int _)
        {
            if (reader.NodeType is XmlNodeType.Element && reader.Name is "seealso")
                FormatComment(reader.ReadInnerXml());
            else
                reader.Read();
        }
    }
}
