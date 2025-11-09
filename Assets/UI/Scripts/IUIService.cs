using System.Threading;
using System.Threading.Tasks;
using AngryKoala.Services;

namespace AngryKoala.UI
{
    public interface IUIService : IService
    {
        Task<IScreen> LoadScreenAsync(string screenKey, CancellationToken cancellationToken = default);
        Task UnloadScreenAsync(string screenKey, CancellationToken cancellationToken = default);
        
        Task<TScreen> GetScreenAsync<TScreen>(string screenKey, CancellationToken cancellationToken = default)
            where TScreen : class, IScreen;
        
        Task<IScreen> ShowScreenAsync(string screenKey, CancellationToken cancellationToken = default);
        Task<IScreen> ShowScreenAsync(string screenKey, TransitionStyle transitionStyle, CancellationToken cancellationToken = default);

        Task HideScreenAsync(string screenKey, CancellationToken cancellationToken = default);
        Task HideScreenAsync(string screenKey, TransitionStyle transitionStyle, CancellationToken cancellationToken = default);
    }
}