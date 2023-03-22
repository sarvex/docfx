﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.DocAsCode.Plugins;

using System.Collections.Immutable;

public interface IPostProcessorHost
{
    /// <summary>
    /// Source file information
    /// </summary>
    IImmutableList<SourceFileInfo> SourceFileInfos { get; }
}
