// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;
using Xunit;

namespace Microsoft.DocAsCode.Dotnet.Tests;

#nullable enable

public class XmlCommentUnitTest
{
    private static void Verify(string comment, string expected)
    {
        Assert.Equal(expected, XmlComment.Format(comment), ignoreLineEndingDifferences: true);
    }

    private static XmlComment Parse(string comment, Func<string, string?>? resolveCode = null)
    {
        var compilation = CompilationHelper.CreateCompilationFromCSharpCode($"{comment}\npublic class Foo{{}}");
        Assert.Empty(compilation.GetDeclarationDiagnostics());

        var symbol = compilation.GetSymbolsWithName("Foo").Single();
        return XmlComment.Parse(symbol, compilation, new() { ResolveCode = resolveCode });
    }

    private static XmlComment Parse(string code, string symbolName, Func<string, string?>? resolveCode = null)
    {
        var compilation = CompilationHelper.CreateCompilationFromCSharpCode(code);
        Assert.Empty(compilation.GetDeclarationDiagnostics());

        var symbol = compilation.GetSymbolsWithName(symbolName).Single();
        return XmlComment.Parse(symbol, compilation, new() { ResolveCode = resolveCode });
    }

    [Fact]
    public static void Basic()
    {
        Verify("A", "A");
        Verify("<para>a</para>", "<p>a</p>");
    }

    [Fact]
    public static void Note()
    {
        Verify("<note>a</note>", "<div class=\"note\"><h5>note</h5>a</div>");
        Verify("<note type=\"warning\">a</note>", "<div class=\"warning\"><h5>warning</h5>a</div>");
        Verify("<note type=\"tips\">a</note>", "<div class=\"tips\"><h5>tips</h5>a</div>");
    }

    [Fact]
    public static void List()
    {
        Verify(
            """
            ///<list type='bullet'>
            ///    <item>
            ///        <description>
            ///            <code language = 'c#'>
            ///            public class XmlElement
            ///                : XmlLinkedNode
            ///            </code>
            ///            <list type='number'>
            ///                <item>
            ///                    <description>
            ///                        word inside list->listItem->list->listItem->para.>
            ///                        the second line.
            ///                    </description>
            ///                </item>
            ///                <item>
            ///                    <description>item2 in numbered list</description>
            ///                </item>
            ///            </list>
            ///        </description>
            ///    </item>
            ///    <item>
            ///        <description>item2 in bullet list</description>
            ///    </item>
            ///    <item>
            ///        loose text <i>not</i> wrapped in description
            ///    </item>
            ///</list>
            """,
            """
            <ul><li><item>

                    <pre><code class="lang-csharp">
                    public class XmlElement
                        : XmlLinkedNode
                    </code></pre>
                    <ol><li><item>

                                word inside list-&gt;listItem-&gt;list-&gt;listItem-&gt;para.&gt;
                                the second line.

                        </item></li><li><item>
                            item2 in numbered list
                        </item></li></ol>

            </item></li><li><item>
                item2 in bullet list
            </item></li><li><item>
                loose text <i>not</i> wrapped in description
            </item></li></ul>
            """);
    }

    [Fact]
    public static void SeeLangword()
    {
        Verify("<see langword=\"if\" />", "<a href=\"https://learn.microsoft.com/dotnet/csharp/language-reference/statements/selection-statements#the-if-statement\">if</a>");
        Verify("<see langword=\"if\">my if</see>", "<a href=\"https://learn.microsoft.com/dotnet/csharp/language-reference/statements/selection-statements#the-if-statement\">my if</a>");
        Verify("<see langword=\"undefined-langword\" />", "<c>undefined-langword</c>");
        Verify("<see langword=\"undefined-langword\">my</see>", "<c>my</c>");
    }

    [Fact]
    public static void SeeHref()
    {
        Verify("<see href=\"https://example.org\"/>", "<a href=\"https://example.org\">https://example.org</a>");
        Verify("<see href=\"https://example.org\">example</see>", "<a href=\"https://example.org\">example</a>");
    }

    [Fact]
    public void See()
    {
        var input = """
            /// <summary>
            /// <see cref="System.AccessViolationException" />
            /// <see cref="System.AccessViolationException">Exception type</see>
            /// <see cref="System.Int32" />
            /// <see cref="System.Int32">Integer</see>
            /// <see cref="System.Int" />
            /// <see cref="System.Int">int</see>
            /// </summary>
            """;

        Assert.Equal(
            """
            <a class="xref" href="https://learn.microsoft.com/dotnet/api/system.accessviolationexception">AccessViolationException</a>
            <a class="xref" href="https://learn.microsoft.com/dotnet/api/system.accessviolationexception">Exception type</a>
            <a class="xref" href="https://learn.microsoft.com/dotnet/api/system.int32">int</a>
            <a class="xref" href="https://learn.microsoft.com/dotnet/api/system.int32">Integer</a>
            <c class="xref">System.Int</c>
            <c class="xref">int</c>

            """,
            Parse(input).Summary,
            ignoreLineEndingDifferences: true);
    }

    [Fact]
    public static void Issue8122()
    {
        var comment = Parse("/// <seealso href=\"#\">Foo's</seealso>");
        Assert.Equal("Foo's", comment.SeeAlsos[0].AltText);
    }

    [Fact]
    public static void Issue4165()
    {
        var comment = Parse(
            """
            public class Foo
            {
                ///<param name="args">arg1</param>
                ///<param name="args">arg2</param>
                public void Bar(string args) {}
            }
            """, "Bar");
        Assert.Equal("arg1", comment.Parameters["args"]);
    }

    [Fact]
    public static void Issue2623()
    {
        var input =
            """
            /// <remarks>
            /// ```csharp
            /// MyClass myClass = new MyClass();
            ///
            /// void Update()
            /// {
            ///     myClass.Execute();
            /// }
            /// ```
            /// </remarks>
            """;

        Assert.Equal(
            """
            ```csharp
            MyClass myClass = new MyClass();

            void Update()
            {
                myClass.Execute();
            }
            ```

            """,
            Parse(input).Remarks,
            ignoreLineEndingDifferences: true);
    }

    [Fact]
    public void TestXmlCommentParser()
    {
        var input = """
            <member name='T:TestClass1.Partial1'>
                <summary>
                    Partial classes <see cref='T:System.AccessViolationException'/><see cref='T:System.AccessViolationException'/>can not cross assemblies, Test <see langword='null'/>

                    ```
                    Classes in assemblies are by definition complete.
                    ```
                </summary>
                <remarks>
                <para>This is <paramref name='ref'/> <paramref />a sample of exception node</para>
                </remarks>
                <returns>Task<see cref='T:System.AccessViolationException'/> returns</returns>

                    <param name='input'>This is <see cref='T:System.AccessViolationException'/>the input</param>

                    <param name = 'output' > This is the output </param >
                    <exception cref='T:System.Xml.XmlException'>This is a sample of exception node. Ref <see href="http://exception.com">Exception</see></exception>
                    <exception cref='System.Xml.XmlException'>This is a sample of exception node with invalid cref</exception>
                    <exception cref=''>This is a sample of invalid exception node</exception>
                    <exception >This is a sample of another invalid exception node</exception>

                <example>
                This sample shows how to call the <see cref="M: Microsoft.DocAsCode.EntityModel.XmlCommentParser.GetExceptions(System.String, Microsoft.DocAsCode.EntityModel.XmlCommentParserContext)"/> method.
                <code>
               class TestClass
                {
                    static int Main()
                    {
                        return GetExceptions(null, null).Count();
                    }
                } 
                </code>
                </example>

                <example>
                This is another example
                </example>
                <example>
                Check empty code.
                <code></code>
                </example>
                <see cref="T:Microsoft.DocAsCode.EntityModel.SpecIdHelper"/>
                <see cref="T:System.Diagnostics.SourceSwitch"/>
                <see cref="Overload:System.String.Compare"/>
                <see href="http://exception.com">Global See section</see>
                <see href="http://exception.com"/>
                <seealso cref="T:System.IO.WaitForChangedResult"/>
                <seealso cref="!:http://google.com">ABCS</seealso>
                <seealso href="http://www.bing.com">Hello Bing</seealso>
                <seealso href="http://www.bing.com"/>
            </member>
            """;

        var commentModel = Parse(input);

        var summary = commentModel.Summary;
        Assert.Equal("""

            Partial classes <xref href="System.AccessViolationException" data-throw-if-not-resolved="false"></xref><xref href="System.AccessViolationException" data-throw-if-not-resolved="false"></xref>can not cross assemblies, Test <a href="https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null">null</a>

            ```
            Classes in assemblies are by definition complete.
            ```

            """, summary, ignoreLineEndingDifferences: true);

        var returns = commentModel.Returns;
        Assert.Equal("Task<xref href=\"System.AccessViolationException\" data-throw-if-not-resolved=\"false\"></xref> returns", returns);

        var paramInput = commentModel.Parameters["input"];
        Assert.Equal("This is <xref href=\"System.AccessViolationException\" data-throw-if-not-resolved=\"false\"></xref>the input", paramInput);

        var remarks = commentModel.Remarks;
        Assert.Equal("""

            <a href="https://example.org">https://example.org</a>
            <a href="https://example.org">example</a>
            <p>This is <code data-dev-comment-type="paramref" class="paramref">ref</code> a sample of exception node</p>
            <ul><li>
            <pre><code class="lang-c#">public class XmlElement
                : XmlLinkedNode</code></pre>
            <ol><li>
                        word inside list->listItem->list->listItem->para.>
                        the second line.
            </li><li>item2 in numbered list</li></ol>
            </li><li>item2 in bullet list</li><li>
            loose text <em>not</em> wrapped in description
            </li></ul>

            """, remarks, ignoreLineEndingDifferences: true);

        var exceptions = commentModel.Exceptions;
        Assert.Single(exceptions);
        Assert.Equal("System.Xml.XmlException", exceptions[0].Type);
        Assert.Equal(@"This is a sample of exception node. Ref <a href=""http://exception.com"">Exception</a>", exceptions[0].Description);

        Assert.Collection(
            commentModel.Examples,
            e => Assert.Equal(
                """

                This sample shows how to call the <see cref="M: Microsoft.DocAsCode.EntityModel.XmlCommentParser.GetExceptions(System.String, Microsoft.DocAsCode.EntityModel.XmlCommentParserContext)"></see> method.
                <pre><code>class TestClass
                {
                    static int Main()
                    {
                        return GetExceptions(null, null).Count();
                    }
                } </code></pre>

                """, e, ignoreLineEndingDifferences: true),
            e => Assert.Equal(
                """

                This is another example

                """, e, ignoreLineEndingDifferences: true),
            e => Assert.Equal(
                """

                Check empty code.
                <pre><code></code></pre>

                """, e, ignoreLineEndingDifferences: true),
            e => Assert.Equal(
                """

                This is an example using source reference.
                <pre><code source="Example.cs" region="Example">    static class Program
                {
                    public int Main(string[] args)
                    {
                        Console.HelloWorld();
                    }
                }</code></pre>

                """, e, ignoreLineEndingDifferences: true)
            );

        commentModel = Parse(input);

        var seeAlsos = commentModel.SeeAlsos;
        Assert.Equal(3, seeAlsos.Count);
        Assert.Equal("System.IO.WaitForChangedResult", seeAlsos[0].LinkId);
        Assert.Null(seeAlsos[0].AltText);
        Assert.Equal("http://www.bing.com", seeAlsos[1].LinkId);
        Assert.Equal("Hello Bing", seeAlsos[1].AltText);
        Assert.Equal("http://www.bing.com", seeAlsos[2].AltText);
        Assert.Equal("http://www.bing.com", seeAlsos[2].LinkId);
    }

    [Fact]
    public void ParamRefTypeParamRef()
    {
        var input =
            """
            public class Foo
            {
                /// <summary>
                /// <typeparamref name="T"/>
                /// <paramref name="arg"/>
                /// </summary>
                /// <typeparam name="T"></typeparam>
                /// <param name="arg"></param>
                public void Bar<T>(T arg) { }
            }
            """;

        Assert.Equal(
            """
            <c>T</c>
            <c>arg</c>

            """,
            Parse(input, "Bar").Summary,
            ignoreLineEndingDifferences: true);
    }

    [Fact]
    public void InheritDoc()
    {
        var code =
            """
            /// <summary>Summary of IFoo</summary>
            /// <remarks>Remarks of IFoo</remarks>
            public interface IFoo
            {
                /// <summary>Summary of M</summary>
                void M();
            }

            /// <inheritdoc cref="IFoo" />
            public class Foo : IFoo
            {
                public void M() {}

                public override string ToString() => "";
            }
            """;

        var compilation = CompilationHelper.CreateCompilationFromCSharpCode(code);
        Assert.Empty(compilation.GetDeclarationDiagnostics());

        var foo = compilation.GetSymbolsWithName("Foo").OfType<INamedTypeSymbol>().Single();
        Assert.Equal("Summary of IFoo", XmlComment.Parse(foo, compilation).Summary);
        Assert.Equal("Remarks of IFoo", XmlComment.Parse(foo, compilation).Remarks);
        Assert.Equal("Summary of M", XmlComment.Parse(foo.GetMembers("M").Single(), compilation).Summary);

        // Pending https://github.com/dotnet/roslyn/pull/66668
        Assert.Null(XmlComment.Parse(foo.GetMembers("ToString").Single(), compilation).Summary);
    }

    [Fact]
    public void XamlCodeSource()
    {
        var example =
            """
            <UserControl
                x:Class="Examples"
                xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                xmlns:mc="http://schemas.openxmlformats.org/markup-compatibility/2006"
                xmlns:d="http://schemas.microsoft.com/expression/blend/2008"
                mc: Ignorable = "d"
                d:DesignHeight="300" d:DesignWidth="300" >
                <UserControl.Resources>

                <!-- <Example> -->
                <Grid>
                  <TextBlock Text="Hello World" />
                </Grid>
                <!-- </Example> -->
            </UserControl>
            """;

        var comment = Parse("/// <example><code source='Example.xaml' region='Example'/></example>", _ => example);

        Assert.Equal(
            """
            <pre><code class="lang-xaml">&lt;Grid&gt;
              &lt;TextBlock Text=&quot;Hello World&quot; /&gt;
            &lt;/Grid&gt;
            </code></pre>
            """, comment.Examples.Single(), ignoreLineEndingDifferences: true);
    }

    [Fact]
    public void CSharpCodeSource()
    {
        var example = """
            using System;

            namespace Example
            {
            #region Example
                static class Program
                {
                    public int Main(string[] args)
                    {
                        Console.HelloWorld();
                    }
                }
            #endregion
            }
            """;

        var comment = Parse("/// <example><code source='Example.cs' region='Example'/></example>", _ => example);

        Assert.Equal(
            """
            <pre><code class="lang-cs">static class Program
            {
                public int Main(string[] args)
                {
                    Console.HelloWorld();
                }
            }
            </code></pre>
            """, comment.Examples.Single(), ignoreLineEndingDifferences: true);
    }
}
