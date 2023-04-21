// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.ComponentModel;
using Microsoft.DocAsCode.Pdf;
using Spectre.Console.Cli;

#nullable enable

namespace Microsoft.DocAsCode;

class Pdf2Command : AsyncCommand<Pdf2Command.Settings>
{
    [Description("Experimental: Creates PDF files for each TOC file in a directory")]
    public class Settings : CommandSettings
    {
        [Description("Path to the directory containing toc.json files")]
        [CommandArgument(0, "[directory]")]
        public string? Directory { get; set; }

        [Description("Installs chrome to print PDF files")]
        [CommandOption("--install")]
        public bool Install { get; set; }
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        if (settings.Install)
        {
            return Playwright.Program.Main(new[] { "install", "chrome" });
        }

        await RunPdf2.CreatePdfForDirectory(settings.Directory ?? Directory.GetCurrentDirectory());
        return 0;
    }
}
