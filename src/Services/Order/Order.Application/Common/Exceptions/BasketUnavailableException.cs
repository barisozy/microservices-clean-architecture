namespace Order.Application.Common.Exceptions;

public sealed class BasketUnavailableException(string message, Exception? innerException = null)
    : Exception(message, innerException);
