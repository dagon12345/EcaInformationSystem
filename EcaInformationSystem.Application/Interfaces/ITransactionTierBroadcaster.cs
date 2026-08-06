namespace EcaInformationSystem.Application.Interfaces
{
    // Abstraction so Infrastructure (LogRepository) can announce "a qualifying
    // transaction was just recorded" without depending on SignalR/ASP.NET Core
    // hosting types — the Api layer supplies the real (SignalR) implementation
    // via DI; Infrastructure registers a no-op default so it still compiles/runs
    // standalone (e.g. in tests) without the Api layer wired up.
    public interface ITransactionTierBroadcaster
    {
        Task NotifyTransactionRecordedAsync(string userName);
    }
}
