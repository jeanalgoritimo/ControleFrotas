namespace ControleFrotas;

public sealed class BusinessException(string message) : Exception(message);
public sealed class RecordNotFoundException : Exception;
