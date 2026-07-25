namespace Application.Exceptions;

/// <summary>
/// Raised when an analysis does not exist, or exists but belongs to another user. The two
/// cases are deliberately indistinguishable so the API does not leak record existence.
/// </summary>
public class AnalysisNotFoundException(Guid analysisId)
    : Exception($"Analysis '{analysisId}' was not found.");

/// <summary>Raised when an operation is not valid for the analysis's current status.</summary>
public class InvalidAnalysisStateException(string message) : Exception(message);

/// <summary>Raised when a request is rejected before any work is started.</summary>
public class AnalysisValidationException(string message) : Exception(message);

/// <summary>Raised when a downstream service (Zeek, Ollama, storage) fails.</summary>
public class AnalysisPipelineException(string message, Exception? innerException = null)
    : Exception(message, innerException);
