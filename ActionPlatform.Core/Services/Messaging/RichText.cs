namespace ActionPlatform.Core.Services.Messaging;

/// <summary>富文本片段：一段文本及加粗标记（渠道无关的通用模型）。</summary>
public sealed record RichTextSegment(string Text, bool Bold = false);

/// <summary>
/// 富文本行：一行的片段列表，按顺序渲染。
/// 与渠道解耦：飞书序列化为 post content 数组行，文本渠道降级时按 Text 顺序拼接。
/// </summary>
public sealed record RichTextLine(IReadOnlyList<RichTextSegment> Segments)
{
    /// <summary>单段普通文本行。</summary>
    public static RichTextLine Plain(string text) => new([new RichTextSegment(text)]);

    /// <summary>单段加粗文本行。</summary>
    public static RichTextLine Bold(string text) => new([new RichTextSegment(text, Bold: true)]);

    /// <summary>多片段混合行（按传入顺序渲染）。</summary>
    public static RichTextLine Mixed(params RichTextSegment[] segments) => new(segments);

    /// <summary>空行（分隔用）。</summary>
    public static RichTextLine Empty => new([]);
}
