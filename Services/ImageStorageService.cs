using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Processing;

namespace TrendMarketServer.Services;

// Yüklenen tüm ürün/varyant/yorum görselleri diske yazılmadan önce buradan geçer:
// (1) telefon kameralarının EXIF döndürme bilgisi piksellere işlenir — aksi halde bazı
//     fotoğraflar yan/ters kaydedilip istemcide yamuk/kırpık görünür;
// (2) uzun kenarı MaxDimension'ı aşan görseller orana sadık kalınarak küçültülür ve JPEG'e
//     çevrilir — hem dosya boyutu hem sayfa yükleme hızı ciddi şekilde iyileşir.
public class ImageStorageService
{
    private const int MaxDimension = 1600;
    private const int JpegQuality = 82;

    private readonly IWebHostEnvironment _env;
    private readonly ILogger<ImageStorageService> _logger;

    public ImageStorageService(IWebHostEnvironment env, ILogger<ImageStorageService> logger)
    {
        _env = env;
        _logger = logger;
    }

    // Başarılı olursa "/uploads/..." yolunu döner; görsel ImageSharp tarafından desteklenmeyen bir
    // formattaysa (ör. HEIC) ya da bozuksa null döner — çağıran taraf bunu 500 yerine anlaşılır bir
    // "desteklenmeyen görsel" hatası olarak müşteriye/satıcıya gösterebilir.
    public async Task<string?> SaveAsync(IFormFile file)
    {
        var uploadsRoot = Path.Combine(_env.ContentRootPath, "wwwroot", "uploads");
        Directory.CreateDirectory(uploadsRoot);

        var fileName = $"{Guid.NewGuid()}.jpg";
        var filePath = Path.Combine(uploadsRoot, fileName);

        try
        {
            using var stream = file.OpenReadStream();
            using var image = await Image.LoadAsync(stream);

            image.Mutate(x =>
            {
                x.AutoOrient();
                if (image.Width > MaxDimension || image.Height > MaxDimension)
                {
                    x.Resize(new ResizeOptions
                    {
                        Mode = ResizeMode.Max,
                        Size = new Size(MaxDimension, MaxDimension),
                    });
                }
            });

            await image.SaveAsync(filePath, new JpegEncoder { Quality = JpegQuality });
            return $"/uploads/{fileName}";
        }
        catch (Exception ex) when (ex is UnknownImageFormatException or InvalidImageContentException)
        {
            _logger.LogWarning(ex, "Desteklenmeyen/bozuk görsel yüklendi: {FileName}", file.FileName);
            return null;
        }
    }
}
