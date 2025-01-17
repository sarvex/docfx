﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;

using Microsoft.DocAsCode.Plugins;

namespace Microsoft.DocAsCode.Build.Common;

public abstract class BaseDocumentBuildStep : IDocumentBuildStep
{
    public abstract string Name { get; }

    public abstract int BuildOrder { get; }

    public virtual IEnumerable<FileModel> Prebuild(ImmutableList<FileModel> models, IHostService host)
    {
        return models;
    }

    public virtual void Build(FileModel model, IHostService host)
    {
    }

    public virtual void Postbuild(ImmutableList<FileModel> models, IHostService host)
    {
    }
}
