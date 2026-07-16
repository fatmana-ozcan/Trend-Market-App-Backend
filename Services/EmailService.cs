using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace TrendMarketServer.Services;

// Resend (https://resend.com) API'si üzerinden doğrulama kodu e-postası gönderir. API anahtarı
// appsettings'e değil, .NET User Secrets'a konur (bkz. README/kurulum notu) — böylece anahtar
// repoya asla commit edilmez. Anahtar tanımlı değilse gönderim sessizce atlanır; uygulama demo
// modunda (kod ekranda/response'ta gösterilerek) çalışmaya devam eder.
public class EmailService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _config;
    private readonly ILogger<EmailService> _logger;

    public EmailService(IHttpClientFactory httpClientFactory, IConfiguration config, ILogger<EmailService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _config = config;
        _logger = logger;
    }

    public async Task<bool> SendVerificationCodeAsync(string? toEmail, string toName, string code, string purpose)
    {
        if (string.IsNullOrWhiteSpace(toEmail)) return false;

        var apiKey = _config["Email:ResendApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            _logger.LogWarning("E-posta gönderimi atlandı: Email:ResendApiKey yapılandırılmamış.");
            return false;
        }

        // Kendi doğrulanmış alan adın yoksa Resend'in test/onboarding adresinden gönderim yapılabilir.
        var fromAddress = _config["Email:FromAddress"] ?? "Trend Market <onboarding@resend.dev>";

        try
        {
            var client = _httpClientFactory.CreateClient(nameof(EmailService));
            client.BaseAddress = new Uri("https://api.resend.com/");
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

            var payload = new
            {
                from = fromAddress,
                to = new[] { toEmail },
                subject = "Trend Market Doğrulama Kodu",
                text =
                    $"Merhaba {toName},\n\n" +
                    $"{purpose} için doğrulama kodunuz: {code}\n\n" +
                    "Bu kod 5 dakika içinde geçerliliğini yitirecektir.\n" +
                    "Bu işlemi siz başlatmadıysanız bu e-postayı yok sayabilirsiniz.",
            };

            using var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            var response = await client.PostAsync("emails", content);

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync();
                _logger.LogError("Resend e-posta gönderimi başarısız ({Status}): {Body}", response.StatusCode, body);
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Doğrulama e-postası gönderilemedi: {Email}", toEmail);
            return false;
        }
    }
}
