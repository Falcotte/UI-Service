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

        Task<IScreen> ShowScreenAsync(string screenKey, TransitionStyle transitionStyle = TransitionStyle.Animated,
            CancellationToken cancellationToken = default);

        Task<TScreen> ShowScreenAsync<TScreen>(string screenKey,
            TransitionStyle transitionStyle = TransitionStyle.Animated, CancellationToken cancellationToken = default)
            where TScreen : class, IScreen;

        Task HideScreenAsync(string screenKey, ScreenHideBehaviour hideBehaviour = ScreenHideBehaviour.Deactivate,
            TransitionStyle transitionStyle = TransitionStyle.Animated, CancellationToken cancellationToken = default);

        Task<IScreen> ShowSubscreenAsync(string screenKey, string subscreenScreenKey,
            TransitionStyle transitionStyle = TransitionStyle.Animated, CancellationToken cancellationToken = default);

        Task HideSubscreenAsync(string screenKey, ScreenHideBehaviour hideBehaviour = ScreenHideBehaviour.Deactivate,
            TransitionStyle transitionStyle = TransitionStyle.Animated, CancellationToken cancellationToken = default);
    }
}