namespace EcaInformationSystem.Application.Interfaces.Services;

public interface IPsgcSeederService
{
    Task SeedAsync(CancellationToken cancellationToken = default);
}
