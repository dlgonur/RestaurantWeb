// RaporService: Dashboard raporunun “iş katmanı”.
// - mode (payment/close) ve tarih aralığını normalize eder (defaultlar + swap).
// - Rapor verisini RaporRepository’den alır ve ViewModel’i (DashboardVm) UI’ye hazırlar.

using ClosedXML.Excel;
using RestaurantWeb.Data;
using RestaurantWeb.Models;
using RestaurantWeb.Models.Dtos;
using RestaurantWeb.Models.ViewModels;

namespace RestaurantWeb.Services
{
    public class RaporService : IRaporService
    {
        private readonly RaporRepository _repo;

        public RaporService(RaporRepository repo)
        {
            _repo = repo;
        }

        // Dashboard ekranı için: tarih + mod normalize edilir, repository’den rapor çekilir
        public OperationResult<DashboardVm> GetDashboard(DateTime? baslangic, DateTime? bitis, string? mode) 
        {
            var m = NormalizeMode(mode); 
            NormalizeDates(baslangic, bitis, out var b, out var t); 

            var res = _repo.GetDashboard(b, t, m); 
            if (!res.Success || res.Data == null) return OperationResult<DashboardVm>.Fail(res.Message); 

            res.Data.Mode = m; 
            return OperationResult<DashboardVm>.Ok(res.Data); 
        }

        // Dashboard verisini Excel’e export eder (ClosedXML)
        public OperationResult<ReportExcelDto> ExportDashboardExcel(DateTime? baslangic, DateTime? bitis, string? mode) 
        {
            var dashRes = GetDashboard(baslangic, bitis, mode); 
            if (!dashRes.Success || dashRes.Data == null) return OperationResult<ReportExcelDto>.Fail(dashRes.Message); 

            var vm = dashRes.Data;

            try
            {
                using var wb = new XLWorkbook();

                // 1) Özet
                var wsOzet = wb.Worksheets.Add("Ozet");
                wsOzet.Cell(1, 1).Value = "Baslangic";
                wsOzet.Cell(1, 2).Value = vm.Baslangic;
                wsOzet.Cell(1, 2).Style.DateFormat.Format = "yyyy-MM-dd";

                wsOzet.Cell(2, 1).Value = "Bitis";
                wsOzet.Cell(2, 2).Value = vm.Bitis;
                wsOzet.Cell(2, 2).Style.DateFormat.Format = "yyyy-MM-dd";

                wsOzet.Cell(3, 1).Value = "Rapor Tipi";
                wsOzet.Cell(3, 2).Value = (vm.Mode == "close" ? "Satis (Kapanis)" : "Kasa (Odeme)");

                wsOzet.Cell(5, 1).Value = "Siparis Sayisi";
                wsOzet.Cell(5, 2).Value = vm.SiparisSayisi;
                wsOzet.Cell(5, 2).Style.NumberFormat.Format = "0";

                wsOzet.Cell(6, 1).Value = "Toplam Ciro";
                wsOzet.Cell(6, 2).Value = vm.ToplamCiro;
                wsOzet.Cell(6, 2).Style.NumberFormat.Format = "#,##0.00";

                wsOzet.Cell(7, 1).Value = "Ortalama Sepet";
                wsOzet.Cell(7, 2).Value = vm.OrtalamaSepet;
                wsOzet.Cell(7, 2).Style.NumberFormat.Format = "#,##0.00";

                wsOzet.Range("A1:A7").Style.Font.Bold = true;
                wsOzet.Columns().AdjustToContents();

                // 2) Günlük Trend
                var wsTrend = wb.Worksheets.Add("GunlukCiro");
                wsTrend.Cell(1, 1).Value = "Gun";
                wsTrend.Cell(1, 2).Value = "Ciro";
                wsTrend.Range("A1:B1").Style.Font.Bold = true;

                var row = 2;
                foreach (var x in vm.GunlukCiro)
                {
                    wsTrend.Cell(row, 1).Value = x.Gun;
                    wsTrend.Cell(row, 2).Value = x.Ciro;
                    row++;
                }

                wsTrend.Column(1).Style.DateFormat.Format = "yyyy-MM-dd";
                wsTrend.Column(2).Style.NumberFormat.Format = "#,##0.00";
                wsTrend.Columns().AdjustToContents();

                // 3) Ödeme dağılımı
                var wsPay = wb.Worksheets.Add("OdemeDagilimi");
                wsPay.Cell(1, 1).Value = "Yontem";
                wsPay.Cell(1, 2).Value = "Tutar";
                wsPay.Range("A1:B1").Style.Font.Bold = true;

                row = 2;
                foreach (var kv in vm.OdemeDagilimi.OrderBy(x => x.Key))
                {
                    wsPay.Cell(row, 1).Value = kv.Key;
                    wsPay.Cell(row, 2).Value = kv.Value;
                    row++;
                }

                wsPay.Column(2).Style.NumberFormat.Format = "#,##0.00";
                wsPay.Columns().AdjustToContents();

                // 4) Top 10 ürün
                var wsTop = wb.Worksheets.Add("TopUrunler");
                wsTop.Cell(1, 1).Value = "Urun";
                wsTop.Cell(1, 2).Value = "Adet";
                wsTop.Cell(1, 3).Value = "Ciro";
                wsTop.Range("A1:C1").Style.Font.Bold = true;

                row = 2;
                foreach (var tItem in vm.TopUrunler)
                {
                    wsTop.Cell(row, 1).Value = tItem.UrunAd;
                    wsTop.Cell(row, 2).Value = tItem.Adet;
                    wsTop.Cell(row, 3).Value = tItem.Ciro;
                    row++;
                }

                wsTop.Column(3).Style.NumberFormat.Format = "#,##0.00";
                wsTop.Columns().AdjustToContents();

                // 5) Kategori ciro
                var wsCat = wb.Worksheets.Add("KategoriCiro");
                wsCat.Cell(1, 1).Value = "Kategori";
                wsCat.Cell(1, 2).Value = "Toplam Adet";
                wsCat.Cell(1, 3).Value = "Brut Ciro";
                wsCat.Cell(1, 4).Value = "Net Ciro";
                wsCat.Range("A1:D1").Style.Font.Bold = true;

                row = 2;
                foreach (var k in vm.KategoriCiro)
                {
                    wsCat.Cell(row, 1).Value = k.KategoriAd;
                    wsCat.Cell(row, 2).Value = k.ToplamAdet;
                    wsCat.Cell(row, 3).Value = k.BrutCiro;
                    wsCat.Cell(row, 4).Value = k.NetCiro;
                    row++;
                }

                wsCat.Column(3).Style.NumberFormat.Format = "#,##0.00";
                wsCat.Column(4).Style.NumberFormat.Format = "#,##0.00";
                wsCat.Columns().AdjustToContents();

                // 6) Personel performans
                var wsPerf = wb.Worksheets.Add("PersonelPerformans");
                wsPerf.Cell(1, 1).Value = "PersonelId";
                wsPerf.Cell(1, 2).Value = "AdSoyad";
                wsPerf.Cell(1, 3).Value = "Siparis Sayisi";
                wsPerf.Cell(1, 4).Value = "Ciro";
                wsPerf.Range("A1:D1").Style.Font.Bold = true;

                row = 2;
                foreach (var pItem in vm.PersonelPerformans)
                {
                    wsPerf.Cell(row, 1).Value = pItem.PersonelId;
                    wsPerf.Cell(row, 2).Value = pItem.AdSoyad;
                    wsPerf.Cell(row, 3).Value = pItem.SiparisSayisi;
                    wsPerf.Cell(row, 4).Value = pItem.Ciro;
                    row++;
                }

                wsPerf.Column(4).Style.NumberFormat.Format = "#,##0.00";
                wsPerf.Columns().AdjustToContents();

                using var ms = new MemoryStream();
                wb.SaveAs(ms);

                var dto = new ReportExcelDto
                {
                    Bytes = ms.ToArray(),
                    FileName = $"Dashboard_{vm.Mode}_{vm.Baslangic:yyyyMMdd}_{vm.Bitis:yyyyMMdd}.xlsx"
                };

                return OperationResult<ReportExcelDto>.Ok(dto);
            }
            catch
            {
                return OperationResult<ReportExcelDto>.Fail("Excel oluşturulurken hata oluştu.");
            }
        }

        // Mode normalize: UI dışından gelse bile sadece payment/close kabul
        private static string NormalizeMode(string? mode) 
        {
            var m = (mode ?? "payment").ToLowerInvariant();
            if (m != "payment" && m != "close") m = "payment";
            return m;
        }

        // Tarih ters girilirse swap
        private static void NormalizeDates(DateTime? baslangic, DateTime? bitis, out DateTime b, out DateTime t) 
        {
            b = (baslangic?.Date ?? DateTime.Today);
            t = (bitis?.Date ?? DateTime.Today);
            if (t < b) (b, t) = (t, b);
        }
    }
}
