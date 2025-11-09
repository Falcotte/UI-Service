using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace AngryKoala.UI
{
    public interface IScreen
    {
        string ScreenKey { get; }
        
        bool IsVisible { get; }

        void Initialize(string screenKey);
        
        Task ShowAsync(CancellationToken cancellationToken);
        Task HideAsync(CancellationToken cancellationToken);
        
        GameObject GetGameObject();
    }
}