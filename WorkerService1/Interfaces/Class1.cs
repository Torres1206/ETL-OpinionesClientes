
namespace WorkerService1.Interfaces
{
    public interface IExtractor
    {
        Task ExtractAsync(CancellationToken cancellationToken);
    }
}
