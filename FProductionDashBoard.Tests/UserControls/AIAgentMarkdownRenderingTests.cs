using FProductionDashBoard;
using System.Globalization;
using System.IO.Packaging;
using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Documents;
using Xunit;

namespace FProductionDashBoard.Tests.UserControls
{
    public class AIAgentMarkdownRenderingTests
    {
        private const string CommonSyntaxSample = """
            # 標題一
            ## 標題二
            ### 標題三
            #### 標題四

            **粗體** 與 *斜體* 以及 `行內程式碼`。

            - 項目一
            - 項目二

            1. 第一
            2. 第二

            | 欄A | 欄B | 欄C |
            |-----|-----|-----|
            | a1  | b1  | c1  |
            | a2  | b2  | c2  |

            ```csharp
            var x = 1;
            ```

            [連結](https://example.com/)

            > 引用文字
            """;

        // WPF 內容元素（含 MdXaml 引擎內部建立的 AvalonEdit TextEditor）必須在 STA 執行緒建立；
        // 刻意不建立 Application（LogServiceTests 的 host 已佔用 process 單例，兩者互不干擾）
        private static void RunSta(Action action)
        {
            Exception? exception = null;
            var thread = new Thread(() =>
            {
                try { action(); }
                catch (Exception ex) { exception = ex; }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.IsBackground = true;
            thread.Start();
            thread.Join();

            if (exception != null)
                ExceptionDispatchInfo.Capture(exception).Throw();
        }

        private static FlowDocument ConvertToDocument(string markdown)
        {
            var converter = new AiMarkdownToDocumentConverter();
            return (FlowDocument)converter.Convert(markdown, typeof(FlowDocument), null!, CultureInfo.InvariantCulture);
        }

        private static IEnumerable<Block> WalkBlocks(BlockCollection blocks)
        {
            foreach (var block in blocks)
            {
                yield return block;
                switch (block)
                {
                    case Section section:
                        foreach (var child in WalkBlocks(section.Blocks))
                            yield return child;
                        break;
                    case List list:
                        foreach (var item in list.ListItems)
                            foreach (var child in WalkBlocks(item.Blocks))
                                yield return child;
                        break;
                    case Table table:
                        foreach (var rowGroup in table.RowGroups)
                            foreach (var row in rowGroup.Rows)
                                foreach (var cell in row.Cells)
                                    foreach (var child in WalkBlocks(cell.Blocks))
                                        yield return child;
                        break;
                }
            }
        }

        private static IEnumerable<Inline> WalkInlines(FlowDocument document)
        {
            foreach (var block in WalkBlocks(document.Blocks))
                if (block is Paragraph paragraph)
                    foreach (var inline in WalkInlines(paragraph.Inlines))
                        yield return inline;
        }

        private static IEnumerable<Inline> WalkInlines(InlineCollection inlines)
        {
            foreach (var inline in inlines)
            {
                yield return inline;
                if (inline is Span span)
                    foreach (var child in WalkInlines(span.Inlines))
                        yield return child;
            }
        }

        [Fact]
        public void Convert_CommonSyntaxSample_ProducesBlocksWithoutException()
            => RunSta(() =>
            {
                var document = ConvertToDocument(CommonSyntaxSample);

                Assert.True(document.Blocks.Count > 0);
                Assert.Contains(WalkBlocks(document.Blocks), b => b is Table);
                Assert.Contains(WalkBlocks(document.Blocks), b => b is List);
            });

        [Fact]
        public void Convert_FencedCodeBlockWithLanguage_RendersAsParagraphNotAvalonEdit()
            => RunSta(() =>
            {
                var document = ConvertToDocument("```csharp\nvar x = 1;\n```");

                Assert.DoesNotContain(WalkBlocks(document.Blocks), b => b is BlockUIContainer);

                var codeBlock = WalkBlocks(document.Blocks)
                    .OfType<Paragraph>()
                    .Single(p => p.Tag as string == "CodeBlock");
                Assert.Contains("var x = 1;", new TextRange(codeBlock.ContentStart, codeBlock.ContentEnd).Text);
            });

        [Fact]
        public void Convert_Hyperlink_HasNoNavigationCommand()
            => RunSta(() =>
            {
                var document = ConvertToDocument("[連結](https://example.com/)");

                var hyperlink = WalkInlines(document).OfType<Hyperlink>().Single();
                Assert.Null(hyperlink.Command);
            });

        [Theory]
        [InlineData("Theme.Dark.xaml")]
        [InlineData("Theme.Light.xaml")]
        public void ThemeDictionary_AiMarkdownStyle_ExistsWithFlowDocumentTargetType(string themeFile)
            => RunSta(() =>
            {
                _ = PackUriHelper.UriSchemePack;   // 無 Application 的環境需先觸發 pack:// scheme 註冊

                var dict = new ResourceDictionary
                {
                    Source = new Uri($"pack://application:,,,/FProductionDashBoard;component/Themes/{themeFile}")
                };

                var style = Assert.IsType<Style>(dict["AiMarkdownStyle"]);
                Assert.Equal(typeof(FlowDocument), style.TargetType);
            });
    }
}
