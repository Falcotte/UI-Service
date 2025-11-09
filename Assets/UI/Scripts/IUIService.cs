using System.Threading;
using System.Threading.Tasks;
using AngryKoala.Services;

namespace AngryKoala.UI
{
    public interface IUIService : IService
    {
        Task<IScreen> LoadScreenAsync(string screenKey, CancellationToken cancellationToken = default);

        Task<TScreen> LoadScreenAsync<TScreen>(string screenKey, CancellationToken cancellationToken = default)
            where TScreen : class, IScreen;

        Task UnloadScreenAsync(string screenKey, CancellationToken cancellationToken = default);

        Task<TScreen> GetScreenAsync<TScreen>(string screenKey, CancellationToken cancellationToken = default)
            where TScreen : class, IScreen;

        Task<IScreen> ShowScreenAsync(string screenKey,
            ScreenTransitionStyle screenTransitionStyle = ScreenTransitionStyle.Animated,
            CancellationToken cancellationToken = default);

        Task<TScreen> ShowScreenAsync<TScreen>(string screenKey,
            ScreenTransitionStyle screenTransitionStyle = ScreenTransitionStyle.Animated,
            CancellationToken cancellationToken = default)
            where TScreen : class, IScreen;

        Task HideScreenAsync(string screenKey,
            ScreenHideBehaviour hideBehaviour = ScreenHideBehaviour.Deactivate,
            ScreenTransitionStyle screenTransitionStyle = ScreenTransitionStyle.Animated,
            CancellationToken cancellationToken = default);
    }
}