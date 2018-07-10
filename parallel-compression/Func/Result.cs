using System;
using JetBrains.Annotations;

namespace Parallel.Compression.Func
{
    public struct Result
    {
        private const string DefaultError = "Unknown error";

        public static Result Successful()
        {
            return new Result
            {
                IsSuccessful = true
            };
        }

        public static Result<T> Successful<T>(T value)
        {
            return Result<T>.Successful(value);
        }

        public static Result Failed([NotNull] string errorMessage)
        {
            return new Result
            {
                IsSuccessful = false,
                ErrorMessage = errorMessage ?? throw new ArgumentNullException(nameof(errorMessage))
            };
        }

        public static Result<T> FailedOf<T>([NotNull] string errorMessage)
        {
            return Result<T>.Failed(errorMessage);
        }

        public static Result<T> FailedWith<T>(T value, [NotNull] string errorMessage)
        {
            return Result<T>.FailedWith(value, errorMessage);
        }

        private string errorMessage;

        public bool IsSuccessful { get; private set; }
        public bool IsFailed => !IsSuccessful;

        public string ErrorMessage
        {
            get => IsFailed && errorMessage == null ? DefaultError : errorMessage;
            private set => errorMessage = value;
        }

        public static implicit operator Result(string failure)
        {
            return Failed(failure);
        }
    }

    public struct Result<T>
    {
        private const string DefaultError = "Unknown error";

        public static Result<T> Successful(T value)
        {
            return new Result<T>
            {
                Value = value,
                HasValue = true,
                IsSuccessful = true
            };
        }

        public static Result<T> Failed([NotNull] string errorMessage)
        {
            return new Result<T>
            {
                IsSuccessful = false,
                ErrorMessage = errorMessage ?? throw new ArgumentNullException(nameof(errorMessage))
            };
        }

        public static Result<T> FailedWith(T failValue, [NotNull] string errorMessage)
        {
            return new Result<T>
            {
                Value = failValue,
                HasValue = true,
                IsSuccessful = false,
                ErrorMessage = errorMessage ?? throw new ArgumentNullException(nameof(errorMessage))
            };
        }

        private string errorMessage;

        public bool HasValue { get; private set; }
        public T Value { get; private set; }
        public bool IsSuccessful { get; private set; }
        public bool IsFailed => !IsSuccessful;

        public string ErrorMessage
        {
            get => IsFailed && errorMessage == null ? DefaultError : errorMessage;
            private set => errorMessage = value;
        }

        public void Deconstruct(out T result, out string error)
        {
            result = Value;
            error = ErrorMessage;
        }

        public void Deconstruct(out bool isSuccessful, out T result, out string error)
        {
            result = Value;
            error = ErrorMessage;
            isSuccessful = IsSuccessful;
        }

        public static implicit operator T(Result<T> result)
        {
            return result.Value;
        }

        public static implicit operator Result<T>(T success)
        {
            return Successful(success);
        }

        public static implicit operator Result<T>(string failure)
        {
            return Failed(failure);
        }
    }

    public struct Result<TSuccess, TFailure>
    {
        public static Result<TSuccess, TFailure> Successful(TSuccess value)
        {
            return new Result<TSuccess, TFailure>
            {
                Value = value,
                IsSuccessful = true
            };
        }

        public static Result<TSuccess, TFailure> Failed(TFailure value)
        {
            return new Result<TSuccess, TFailure>
            {
                IsSuccessful = false,
                FailureValue = value
            };
        }

        public TSuccess Value { get; private set; }
        public TFailure FailureValue { get; private set; }
        public bool IsSuccessful { get; private set; }
        public bool IsFailed => !IsSuccessful;

        public void Deconstruct(out TSuccess successValue, out TFailure failureValue)
        {
            successValue = Value;
            failureValue = FailureValue;
        }

        public static implicit operator TSuccess(Result<TSuccess, TFailure> result)
        {
            return result.Value;
        }

        public static implicit operator Result<TSuccess, TFailure>(TSuccess success)
        {
            return Successful(success);
        }

        public static implicit operator Result<TSuccess, TFailure>(TFailure failure)
        {
            return Failed(failure);
        }
    }
}