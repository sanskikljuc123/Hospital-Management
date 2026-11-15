namespace Services.Common
{
    public enum ResultStatus
    {
        Ok,
        NotFound,
        Invalid,
        Conflict,
        Forbidden
    }

    /// <summary>Outcome of a business operation; the API maps the status to an HTTP response.</summary>
    public class ServiceResult<T>
    {
        public ResultStatus Status { get; private init; }
        public T? Value { get; private init; }
        public string? Error { get; private init; }
        public bool Success => Status == ResultStatus.Ok;

        public static ServiceResult<T> Ok(T value) => new() { Status = ResultStatus.Ok, Value = value };
        public static ServiceResult<T> NotFound(string error) => new() { Status = ResultStatus.NotFound, Error = error };
        public static ServiceResult<T> Invalid(string error) => new() { Status = ResultStatus.Invalid, Error = error };
        public static ServiceResult<T> Conflict(string error) => new() { Status = ResultStatus.Conflict, Error = error };
        /// <summary>The caller's role may not perform this business action (the endpoint itself was allowed).</summary>
        public static ServiceResult<T> Forbidden(string error) => new() { Status = ResultStatus.Forbidden, Error = error };
    }
}
