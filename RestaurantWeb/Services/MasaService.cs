using Microsoft.Extensions.Configuration;
using Npgsql;
using RestaurantWeb.Data;
using RestaurantWeb.Models;
using RestaurantWeb.Models.ViewModels;

namespace RestaurantWeb.Services
{
    public class MasaService : IMasaService
    {
        private readonly MasaRepository _masaRepo;
        private readonly SiparisRepository _siparisRepo;
        private readonly RezervasyonRepository _rezRepo;
        private readonly IConfiguration _cfg;

        public MasaService(
            MasaRepository masaRepo,
            SiparisRepository siparisRepo,
            RezervasyonRepository rezRepo,
            IConfiguration cfg)
        {
            _masaRepo = masaRepo;
            _siparisRepo = siparisRepo;
            _rezRepo = rezRepo;
            _cfg = cfg;
        }



        public OperationResult<List<Masa>> GetAll() => _masaRepo.GetAll();
        public OperationResult<Masa> GetById(int id) => _masaRepo.GetById(id);

        public OperationResult Add(int masaNo, int kapasite) => _masaRepo.Add(masaNo, kapasite);
        public OperationResult Update(int id, int masaNo, int kapasite) => _masaRepo.Update(id, masaNo, kapasite);
        public OperationResult<bool> ToggleAktif(int id) => _masaRepo.ToggleAktif(id);
        public OperationResult Delete(int id) => _masaRepo.Delete(id);

        public OperationResult<MasaBoardVm> GetBoard(DateTime now) 
        {
            AutoOpenOrdersFromDueReservations(now);
            var result = _masaRepo.GetAll();
            if (!result.Success)
                return OperationResult<MasaBoardVm>.Fail(result.Message);

            var masalar = result.Data ?? new List<Masa>();
            var blockHours = Math.Max(0, _cfg.GetValue<int>("Reservation:BlockHoursDefault"));

            var ids = masalar.Select(x => x.Id).ToArray();
            var windowMinutes = blockHours * 60; 
            var nextMap = _rezRepo.GetActiveReservationsForTablesInWindow(ids, now, windowMinutes); 


            var items = masalar
                .OrderBy(x => x.MasaNo)
                .Select(m =>
                {
                    nextMap.TryGetValue(m.Id, out var rez);

                    bool blokeli = false;
                    MasaDurumu efektif = m.Durum;

                    if (rez != null)
                    {
                        var blockStart = rez.RezTarih.AddHours(-blockHours);
                        if (now >= blockStart && now < rez.RezTarih)
                        {
                            blokeli = true;
                            if (m.Durum == MasaDurumu.Bos)
                                efektif = MasaDurumu.Rezerve;
                        }
                    }

                    return new MasaBoardItemVm
                    {
                        Id = m.Id,
                        MasaNo = m.MasaNo,
                        Kapasite = m.Kapasite,
                        AktifMi = m.AktifMi,
                        DurumEfektif = efektif,
                        Blokeli = blokeli || !m.AktifMi,
                        RezMusteriAd = rez?.MusteriAd,
                        RezTarih = rez?.RezTarih,
                        RezervasyonId = rez?.RezervasyonId 
                    };

                })
                .ToList();

            // KPI’lar da service tarafında 
            var aktif = items.Count(x => x.AktifMi);
            var dolu = items.Count(x => x.AktifMi && x.DurumEfektif == MasaDurumu.Dolu);
            var blokeliBos = items.Count(x => x.AktifMi && x.DurumEfektif == MasaDurumu.Rezerve);
            var walkinBos = items.Count(x => x.AktifMi && x.DurumEfektif == MasaDurumu.Bos && !x.Blokeli);

            decimal fizikselOran = aktif == 0 ? 0 : (decimal)dolu * 100m / aktif;
            decimal efektifOran = aktif == 0 ? 0 : (decimal)(dolu + blokeliBos) * 100m / aktif;

            var vm = new MasaBoardVm
            {
                Items = items,
                AktifMasa = aktif,
                DoluMasa = dolu,
                BlokeliBosMasa = blokeliBos,
                WalkinBosMasa = walkinBos,
                FizikselDoluluk = Math.Round(fizikselOran, 0),
                EfektifDoluluk = Math.Round(efektifOran, 0)
            };

            return OperationResult<MasaBoardVm>.Ok(vm); 
        }

        public OperationResult<int> EnsureOpenTable(int masaId, DateTime now) 
        {
            if (masaId <= 0)
                return OperationResult<int>.Fail("Geçersiz masa.");

            var blockHours = Math.Max(0, _cfg.GetValue<int>("Reservation:BlockHoursDefault"));

            var windowMinutes = blockHours * 60; 
            var next = _rezRepo.GetActiveReservationsForTablesInWindow(new[] { masaId }, now, windowMinutes); 

            if (next.TryGetValue(masaId, out var rez))
            {
                var blockStart = rez.RezTarih.AddMinutes(-windowMinutes);
                if (now >= blockStart && now < rez.RezTarih)
                    return OperationResult<int>.Fail($"Bu masa {rez.RezTarih:HH:mm} rezervasyonu için bloke. ({rez.MusteriAd})");
            }

            try
            {
                var connStr = _cfg.GetConnectionString("PostgreSqlConnection")
                              ?? throw new InvalidOperationException("Connection string not found.");

                using var conn = new NpgsqlConnection(connStr);
                conn.Open();

                using var tx = conn.BeginTransaction();

                var res = _siparisRepo.EnsureOpenOrderForTable(conn, tx, masaId);

                if (!res.Success)
                    return OperationResult<int>.Fail(res.Message); // rollback (tx dispose)

                tx.Commit();
                return OperationResult<int>.Ok(res.Data, "Sipariş hazır."); 

            }
            catch (PostgresException ex)
            {
                return OperationResult<int>.Fail($"Veritabanı işlemi sırasında bir hata oluştu. (Kod: {ex.SqlState})");
            }
            catch (Exception)
            {
                return OperationResult<int>.Fail("Beklenmeyen bir hata oluştu. Lütfen tekrar deneyin.");
            }
        }

        private void AutoOpenOrdersFromDueReservations(DateTime now) 
        {
            var grace = _cfg.GetValue<int?>("Reservation:AutoOpenGraceMinutes") ?? 15; 
            if (grace < 1) grace = 1; 
            if (grace > 180) grace = 180; 

            var due = _rezRepo.GetDueActiveReservations(now, grace); 
            if (due.Count == 0) return; 

            var connStr = _cfg.GetConnectionString("PostgreSqlConnection")
                          ?? throw new InvalidOperationException("Connection string not found."); 

            using var conn = new NpgsqlConnection(connStr); 
            conn.Open(); 
            using var tx = conn.BeginTransaction(); 

            foreach (var rez in due)
            {
                // 1) Masa kilitle + durum al
                var masaDurum = _masaRepo.GetDurumAndLock(conn, tx, rez.MasaId); 
                if (masaDurum == null) continue; 
                if (!masaDurum.AktifMi) continue; 

                if (masaDurum.Durum == MasaDurumu.Dolu)
                {
                    // A) masa dolu: sipariş var mı?
                    // Eğer EnsureOpen... "varsa döndürür" ise güvenle çağırabiliriz.
                    var ensure = _siparisRepo.EnsureOpenOrderForTable(conn, tx, rez.MasaId); 
                    if (ensure.Success)
                    {
                        _rezRepo.MarkUsed(conn, tx, rez.RezervasyonId); 
                    }
                    else
                    {
                        // B) anomali: masa dolu ama sipariş yok ya da hata -> dokunma
                        // şimdilik sadece continue (sonra log ekleriz)
                    }

                    continue;
                }

                // C) masa boş: sipariş aç + rezervasyonu kullanıldı yap
                var openRes = _siparisRepo.EnsureOpenOrderForTable(conn, tx, rez.MasaId); 
                if (openRes.Success)
                {
                    _rezRepo.MarkUsed(conn, tx, rez.RezervasyonId); 
                }
            }

            tx.Commit(); 
        }

    }
}
