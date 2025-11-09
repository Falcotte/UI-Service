using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace AngryKoala.UI
{
    public interface IScreen
    {
        string ScreenKey { get; }
        
        bool IsVisible { get; }

        event Action<IScreen> BeforeScreenShow;
        event Action<IScreen> AfterScreenShow;
        event Action<IScreen> BeforeScreenHide;
        event Action<IScreen> AfterScreenHide;
        
        void Initialize(string screenKey);
        
        Task ShowAsync(TransitionStyle transitionStyle = TransitionStyle.Animated, CancellationToken cancellationToken = default);
        Task HideAsync(TransitionStyle transitionStyle = TransitionStyle.Animated, CancellationToken cancellationToken = default);
        
        GameObject GetGameObject();
    }
}