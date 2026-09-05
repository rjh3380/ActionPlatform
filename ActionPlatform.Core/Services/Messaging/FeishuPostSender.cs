using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ActionPlatform.Core.Services.Messaging;

/// <summary>
/// 飞书群机器人 post 富文本消息直发。
/// EasyNotice.Feishu 仅支持 text 消息，post 富文本需直发 webhook API。
/// 复用配置段 <c>EasyNotice:Feishu</c>（Enable/WebHook/Secret，与 EasyNotice 初始化同源），无新增配置。
/// 未启用渠道、请求失败或响应错误时记录警告并安全返回，不抛出异常；日志不含 WebHook/Secret。
/// </summary>
public sealed class FeishuPostSender
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<FeishuPostSender> _logger;
    private readonly HttpClient _http = new();

    public FeishuPostSender(IConfiguration configuration, ILogger<FeishuPostSender> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    /// <summary>
    /// 发送富文本消息到飞书（post 类型），返回是否投递成功（code == 0）。
    /// 失败/异常只记日志不抛出，由返回值供调用方决定是否重试。
    /// </summary>
    public async Task<bool> SendPostAsync(string title, IReadOnlyList<RichTextLine> lines, CancellationToken cancellationToken = default)
    {
        var webHook = _configuration["EasyNotice:Feishu:WebHook"];
        var secret = _configuration["EasyNotice:Feishu:Secret"];

        if (string.IsNullOrWhiteSpace(webHook))
        {
            _logger.LogWarning("飞书渠道未启用或未配置 WebHook，富文本消息未发送。Title: {Title}", title);
            return false;
        }

        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        var json = BuildPayloadJson(title, lines, timestamp, secret);
        try
        {
            _logger.LogInformation("飞书 即将发送富文本消息。Title: {Title}", title);
            using var request = new HttpRequestMessage(HttpMethod.Post, webHook)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json"),
            };
            // 配置了 Secret 时加签（飞书签名算法：stringToSign = timestamp + "\n" + secret 的 HMAC-SHA256 → Base64）
            if (!string.IsNullOrWhiteSpace(secret))
            {
                request.Headers.TryAddWithoutValidation("x-lark-signature", Sign(secret, timestamp));
            }

            using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            using var doc = JsonDocument.Parse(body);
            var code = doc.RootElement.TryGetProperty("code", out var codeEl) && codeEl.TryGetInt32(out var c) ? c : -1;
            if (code != 0)
            {
                var msg = doc.RootElement.TryGetProperty("msg", out var msgEl) ? msgEl.GetString() : string.Empty;
                _logger.LogWarning("飞书 富文本消息发送失败。ErrCode: {ErrCode}, ErrMsg: {ErrMsg}", code, msg);
                return false;
            }
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "飞书 富文本消息发送异常，已忽略。Title: {Title}", title);
            return false;
        }
    }

    /// <summary>
    /// 飞书加签：key = "timestamp\nsecret"，msg 为空，HMAC-SHA256 → Base64。
    /// （官方算法：hmac.new(string_to_sign.encode(), digestmod=sha256) → base64）
    /// </summary>
    private static string Sign(string secret, string timestamp)
    {
        var stringToSign = $"{timestamp}\n{secret}";
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(stringToSign));
        return Convert.ToBase64String(hmac.ComputeHash(Array.Empty<byte>()));
    }

    private static string BuildPayloadJson(string title, IReadOnlyList<RichTextLine> lines, string timestamp, string? secret)
    {
        // 新版飞书 webhook 的 post 结构要求 content.post 下带语言键（如 zh_cn），
        // 平铺 title/content 的旧格式返回 19002 "unknown content value"。
        var content = lines
            .Select(line => (object)line.Segments
                .Select(s => (object)new Dictionary<string, object?>
                {
                    ["tag"] = "text",
                    ["text"] = s.Text,
                })
                .ToArray())
            .ToArray();

        var payload = new Dictionary<string, object?>
        {
            ["msg_type"] = "post",
            ["content"] = new Dictionary<string, object?>
            {
                ["post"] = new Dictionary<string, object?>
                {
                    ["zh_cn"] = new Dictionary<string, object?>
                    {
                        ["title"] = title,
                        ["content"] = content,
                    },
                },
            },
        };
        if (!string.IsNullOrWhiteSpace(secret))
        {
            payload["timestamp"] = timestamp;
        }

        return JsonSerializer.Serialize(payload);
    }
}
