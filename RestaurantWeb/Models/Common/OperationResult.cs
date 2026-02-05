// Katmanlar arası standart başarı/hata dönüş modeli.
// Controller–Service–Repository hattında tek tip sonuç taşımak için kullanılır.

namespace RestaurantWeb.Models
{
    public class OperationResult
    {
        public bool Success { get; }
        public string Message { get; }

        protected OperationResult(bool success, string message)
        {
            Success = success;
            Message = message;
        }

        // Veri taşımayan başarılı sonuç
        public static OperationResult Ok(string message = "İşlem başarılı.")
            => new(true, message);

        // Genel hata sonucu
        public static OperationResult Fail(string message)
            => new(false, message);
    }

    // Veri taşıyan generic sonuç modeli
    public class OperationResult<T> : OperationResult
    {
        public T? Data { get; } 

        private OperationResult(bool success, string message, T? data)
            : base(success, message)
        {
            Data = data;
        }

        // Veri içeren başarılı sonuç
        public static OperationResult<T> Ok(T data, string message = "İşlem başarılı.")
            => new(true, message, data);

        // Veri taşımayan hata sonucu (Data default/null)
        public static new OperationResult<T> Fail(string message)
            => new(false, message, default);
    }
}
