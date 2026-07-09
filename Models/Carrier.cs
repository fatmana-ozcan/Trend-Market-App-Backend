namespace TrendMarketServer.Models;

public class CarrierInfo
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string TrackingUrlTemplate { get; set; } = string.Empty;
}

// Gerçek bir kargo firması entegrasyonu yok; müşteri/satıcı tarafında seçilebilecek
// sabit bir demo kargo firması listesi ve buradan türetilen iletişim/takip bilgileri.
public static class Carriers
{
    public static readonly List<CarrierInfo> All = new()
    {
        // Not: Kargo firmalarının çoğu, takip numarasını URL üzerinden otomatik doldurmuyor
        // (form + görsel doğrulama/captcha ile korunuyor). Bu yüzden şablonlar, elde mevcut
        // ve doğrulanmış olan firmanın genel takip sayfasına yönlendiriyor; Sürat Kargo
        // hariç diğerlerinde {0} yer tutucusu kullanılmıyor.
        new CarrierInfo
        {
            Code = "yurtici",
            Name = "Yurtiçi Kargo",
            Phone = "0850 250 90 00",
            Email = "musterihizmetleri@yurticikargo.com",
            TrackingUrlTemplate = "https://www.yurticikargo.com/tr/online-servisler/gonderi-sorgula",
        },
        new CarrierInfo
        {
            Code = "aras",
            Name = "Aras Kargo",
            Phone = "0850 250 00 00",
            Email = "musterihizmetleri@araskargo.com.tr",
            TrackingUrlTemplate = "https://www.araskargo.com.tr/",
        },
        new CarrierInfo
        {
            Code = "mng",
            Name = "MNG Kargo",
            Phone = "0850 211 06 06",
            Email = "musterihizmetleri@mngkargo.com.tr",
            TrackingUrlTemplate = "https://www.dhlecommerce.com.tr/CargoTracking/Track",
        },
        new CarrierInfo
        {
            Code = "ptt",
            Name = "PTT Kargo",
            Phone = "444 1 788",
            Email = "musterihizmetleri@ptt.gov.tr",
            TrackingUrlTemplate = "https://turkiye.gov.tr/ptt-gonderi-takip",
        },
        new CarrierInfo
        {
            Code = "surat",
            Name = "Sürat Kargo",
            Phone = "0850 224 0 224",
            Email = "musterihizmetleri@suratkargo.com.tr",
            TrackingUrlTemplate = "https://www.suratkargo.com.tr/KargoTakip/?kargotakipno={0}",
        },
    };

    public static CarrierInfo? Find(string? code) => All.FirstOrDefault(c => c.Code == code);

    public static string? BuildTrackingUrl(string? code, string? trackingNumber)
    {
        if (string.IsNullOrEmpty(code) || string.IsNullOrEmpty(trackingNumber)) return null;
        var carrier = Find(code);
        if (carrier == null || string.IsNullOrEmpty(carrier.TrackingUrlTemplate)) return null;
        return carrier.TrackingUrlTemplate.Replace("{0}", Uri.EscapeDataString(trackingNumber));
    }
}
