using Application.DTOs.Diagnostics;

namespace Application.Interfaces;

public interface IServiceHealthProbe
{
    Task<ComponentHealthDto> CheckMinioAsync(CancellationToken cancellationToken = default);

    Task<ComponentHealthDto> CheckZeekAsync(CancellationToken cancellationToken = default);

    Task<ComponentHealthDto> CheckOllamaAsync(CancellationToken cancellationToken = default);

    Task<ComponentHealthDto> CheckPostgresAsync(CancellationToken cancellationToken = default);
}
