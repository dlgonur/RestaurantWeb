// PostgreSQL backup/restore süreçleri için servis katmanı.
// pg_dump çağrısını güvenli şekilde (şifreyi komuta yazmadan) çalıştırır, dosyayı doğrular,
// listeleme/indirme/silme ve "son N backup" temizliğini yönetir.

using RestaurantWeb.Models;
using RestaurantWeb.Models.ViewModels;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace RestaurantWeb.Services
{
    public class BackupService : IBackupService
    {
        private readonly IConfiguration _cfg;
        private readonly ILogger<BackupService> _logger;
        private readonly string _connStr;
        private readonly string _backupDir;
        private readonly string _pgDumpPath;
        private readonly string _format;
        private readonly int _keepLast;

        public BackupService(IConfiguration cfg, ILogger<BackupService> logger)
        {
            _cfg = cfg;
            _logger = logger;

            _connStr = _cfg.GetConnectionString("PostgreSqlConnection")
                ?? throw new InvalidOperationException("Connection string not found.");

            _backupDir = _cfg["Backup:BackupDir"] ?? "App_Data/Backups";
            _pgDumpPath = _cfg["Backup:PgDumpPath"] ?? "";
            _format = (_cfg["Backup:Format"] ?? "custom").Trim().ToLowerInvariant();
            _keepLast = int.TryParse(_cfg["Backup:KeepLast"], out var k) ? k : 50;
        }

        // pg_dump ile backup oluşturur ve dosya adını döndürür.
        public OperationResult<string> CreateBackup(string? actorUsername)
        {
            if (string.IsNullOrWhiteSpace(_pgDumpPath) || !File.Exists(_pgDumpPath))
                return OperationResult<string>.Fail("pg_dump yolu geçersiz. appsettings.json -> Backup:PgDumpPath kontrol edin.");

            var fullDir = GetFullBackupDir();
            Directory.CreateDirectory(fullDir);

            var stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var safeActor = Sanitize(actorUsername ?? "system");
            var ext = _format == "plain" ? "sql" : "backup";
            var fileName = $"restaurant_{stamp}_{safeActor}.{ext}";
            var fullPath = Path.Combine(fullDir, fileName);

            try
            {
                var cs = ParseConn(_connStr);
                var args = BuildPgDumpArgs(cs, fullPath);

                var psi = new ProcessStartInfo
                {
                    FileName = _pgDumpPath,
                    Arguments = args,
                    UseShellExecute = false,
                    RedirectStandardError = true,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                };

                // Password env: komuta yazmıyoruz
                if (!string.IsNullOrWhiteSpace(cs.Password))
                    psi.Environment["PGPASSWORD"] = cs.Password;

                using var p = Process.Start(psi);
                if (p == null)
                    return OperationResult<string>.Fail("pg_dump başlatılamadı.");

                var stdout = p.StandardOutput.ReadToEnd();
                var stderr = p.StandardError.ReadToEnd();
                p.WaitForExit();

                if (p.ExitCode != 0)
                {
                    _logger.LogError("pg_dump failed. ExitCode={Code} stderr={Err}", p.ExitCode, stderr);
                    TryDelete(fullPath);
                    return OperationResult<string>.Fail("Backup alınamadı. pg_dump hata verdi. (Log’a bakın)");
                }

                if (!File.Exists(fullPath) || new FileInfo(fullPath).Length == 0)
                {
                    _logger.LogError("Backup file missing/empty. stdout={Out} stderr={Err}", stdout, stderr);
                    TryDelete(fullPath);
                    return OperationResult<string>.Fail("Backup dosyası oluşmadı.");
                }

                CleanupOld(fullDir);

                return OperationResult<string>.Ok(fileName, $"Backup oluşturuldu: {fileName}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Backup create failed.");
                TryDelete(fullPath);
                return OperationResult<string>.Fail("Backup alınırken teknik hata oluştu.");
            }
        }

        // Backup klasöründeki dosyaları metadata ile listeler (UI listesi için).
        public OperationResult<List<BackupItemVm>> ListBackups()
        {
            try
            {
                var dir = GetFullBackupDir();
                Directory.CreateDirectory(dir);

                var items = Directory.GetFiles(dir)
                    .Select(p => new FileInfo(p))
                    .OrderByDescending(f => f.CreationTimeUtc)
                    .Select(f => new BackupItemVm
                    {
                        FileName = f.Name,
                        SizeBytes = f.Length,
                        CreatedAt = f.CreationTime
                    })
                    .ToList();

                return OperationResult<List<BackupItemVm>>.Ok(items);
            }
            catch
            {
                return OperationResult<List<BackupItemVm>>.Fail("Backup listesi okunamadı.");
            }
        }

        // Seçilen backup dosyasını indirilebilir byte[] olarak döndürür.
        public OperationResult<(byte[] Bytes, string ContentType, string DownloadName)> GetBackupFile(string fileName)
        {
            if (!IsSafeFileName(fileName))
                return OperationResult<(byte[], string, string)>.Fail("Geçersiz dosya adı.");

            var path = Path.Combine(GetFullBackupDir(), fileName);
            if (!File.Exists(path))
                return OperationResult<(byte[], string, string)>.Fail("Dosya bulunamadı.");

            try
            {
                var bytes = File.ReadAllBytes(path);
                var ct = fileName.EndsWith(".sql", StringComparison.OrdinalIgnoreCase)
                    ? "application/sql"
                    : "application/octet-stream";

                return OperationResult<(byte[], string, string)>.Ok((bytes, ct, fileName));
            }
            catch
            {
                return OperationResult<(byte[], string, string)>.Fail("Dosya okunamadı.");
            }
        }

        // Backup dosyasını siler
        public OperationResult DeleteBackup(string fileName)
        {
            if (!IsSafeFileName(fileName))
                return OperationResult.Fail("Geçersiz dosya adı.");

            var path = Path.Combine(GetFullBackupDir(), fileName);
            if (!File.Exists(path))
                return OperationResult.Fail("Dosya bulunamadı.");

            try
            {
                File.Delete(path);
                return OperationResult.Ok("Backup silindi.");
            }
            catch
            {
                return OperationResult.Fail("Backup silinemedi.");
            }
        }

        // Backup klasörünün mutlak yolunu üretir.
        private string GetFullBackupDir()
        {
            var root = AppContext.BaseDirectory;
            var projectRoot = Directory.GetCurrentDirectory();
            return Path.GetFullPath(Path.Combine(projectRoot, _backupDir));
        }

        // Kullanıcı adını dosya adına uygun hale getirir (sadece [a-z0-9_-]).
        private static string Sanitize(string s)
        {
            s = s.Trim().ToLowerInvariant();
            s = Regex.Replace(s, @"[^a-z0-9_\-]+", "_");
            if (string.IsNullOrWhiteSpace(s)) return "user";
            return s.Length > 32 ? s[..32] : s;
        }

        // Güvenli dosya adı kontrolü (path traversal / invalid chars engeli).
        private static bool IsSafeFileName(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName)) return false;
            if (fileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0) return false;
            if (fileName.Contains("..")) return false;
            return true;
        }

        // Hata durumunda yarım oluşan dosyayı sessizce temizlemeye çalışır.
        private static void TryDelete(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); } catch { }
        }

        // "Son N backup" politikasına göre eski dosyaları temizler.
        private void CleanupOld(string dir)
        {
            if (_keepLast <= 0) return;

            try
            {
                var files = Directory.GetFiles(dir)
                    .Select(p => new FileInfo(p))
                    .OrderByDescending(f => f.CreationTimeUtc)
                    .ToList();

                var toDelete = files.Skip(_keepLast).ToList();
                foreach (var f in toDelete)
                {
                    try { f.Delete(); } catch { }
                }
            }
            catch { }
        }

        // pg_dump argümanlarını üretir (format + host/port/user/db + output file).
        private string BuildPgDumpArgs(ConnParts cs, string outPath)
        {
            var fmtArg = _format == "plain" ? "" : "-Fc";
            var fileArg = _format == "plain"
                ? $"-f \"{outPath}\""
                : $"-f \"{outPath}\"";
            var args =
                $"{fmtArg} -h \"{cs.Host}\" -p {cs.Port} -U \"{cs.Username}\" {fileArg} \"{cs.Database}\"";

            return args.Trim();
        }

        // Connection string parçalama (basit parser).
        private static ConnParts ParseConn(string connStr)
        {
            var dict = connStr.Split(';', StringSplitOptions.RemoveEmptyEntries)
                .Select(x => x.Split('=', 2))
                .Where(a => a.Length == 2)
                .ToDictionary(a => a[0].Trim().ToLowerInvariant(), a => a[1].Trim());

            string Get(string k, string def = "") => dict.TryGetValue(k.ToLowerInvariant(), out var v) ? v : def;

            return new ConnParts
            {
                Host = Get("host", "localhost"),
                Port = int.TryParse(Get("port", "5432"), out var p) ? p : 5432,
                Database = Get("database", ""),
                Username = Get("username", ""),
                Password = Get("password", "")
            };
        }

        // pg_dump için gerekli minimal connection parçaları
        private sealed class ConnParts
        {
            public string Host { get; set; } = "";
            public int Port { get; set; }
            public string Database { get; set; } = "";
            public string Username { get; set; } = "";
            public string Password { get; set; } = "";
        }
    }
}
