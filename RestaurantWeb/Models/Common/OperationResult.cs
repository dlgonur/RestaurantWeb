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

        public static OperationResult Ok(string message = "İşlem başarılı.")
            => new(true, message);

        public static OperationResult Fail(string message)
            => new(false, message);
    }

    public class OperationResult<T> : OperationResult
    {
        public T? Data { get; } 

        private OperationResult(bool success, string message, T? data)
            : base(success, message)
        {
            Data = data;
        }
                public static OperationResult<T> Ok(T data, string message = "İşlem başarılı.")
            => new(true, message, data);

        public static new OperationResult<T> Fail(string message)
            => new(false, message, default);
    }
}
