namespace Domain.Entities;

public enum AnalysisStatus
{
    /// <summary>A multipart upload session has been created, but the client has not confirmed completion.</summary>
    AwaitingUpload = 0,

    /// <summary>The object exists in MinIO and is ready to be analyzed.</summary>
    Uploaded = 1,

    /// <summary>The Zeek service is processing the capture.</summary>
    Analyzing = 2,

    /// <summary>Zeek finished; the LLM is generating the report.</summary>
    Reporting = 3,

    Completed = 4,

    Failed = 5
}
