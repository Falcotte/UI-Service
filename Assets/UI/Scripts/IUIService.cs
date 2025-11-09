using System.Threading;
using System.Threading.Tasks;
using AngryKoala.Services;

namespace AngryKoala.UI
{
    public interface IUIService : IService
    {
        Task<IScreen> ShowScreenAsync(string screenKey, CancellationToken cancellationToken = default);

        Task HideScreenAsync(string screenKey, CancellationToken cancellationToken = default);
    }
}