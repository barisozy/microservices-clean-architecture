namespace Inventory.Application.Common.Exceptions;

public sealed class PersistenceConcurrencyException(Exception innerException) : Exception("The inventory changed concurrently.", innerException);
public sealed class PersistenceWriteException(Exception innerException) : Exception("The inventory could not be persisted.", innerException);
