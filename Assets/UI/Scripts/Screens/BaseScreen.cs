using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace AngryKoala.UI
{
    public abstract class BaseScreen : MonoBehaviour, IScreen
    {
        public string ScreenKey { get; private set; }

        public bool IsVisible { get; private set; }
        
        public event Action<IScreen> BeforeScreenShow;
        public event Action<IScreen> AfterScreenShow;
        public event Action<IScreen> BeforeScreenHide;
        public event Action<IScreen> AfterScreenHide;

        public void Initialize(string screenKey)
        {
            ScreenKey = screenKey;
        }

        public async Task ShowAsync(TransitionStyle transitionStyle = TransitionStyle.Animated, CancellationToken cancellationToken = default)
        {
            try
            {
                InvokeCallback(BeforeScreenShow);
                
                gameObject.SetActive(true);
                IsVisible = true;

                if (transitionStyle == TransitionStyle.Instant)
                {
                    OnShowInstant();
                }
                else
                {
                    await OnShowAsync(cancellationToken);
                }
                
                InvokeCallback(AfterScreenShow);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                throw;
            }
        }

        public async Task HideAsync(TransitionStyle transitionStyle = TransitionStyle.Animated, CancellationToken cancellationToken = default)
        {
            try
            {
                InvokeCallback(BeforeScreenHide);
                
                if (transitionStyle == TransitionStyle.Instant)
                {
                    OnHideInstant();
                }
                else
                {
                    await OnHideAsync(cancellationToken);
                }
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                throw;
            }
            finally
            {
                IsVisible = false;
                gameObject.SetActive(false);
                
                InvokeCallback(AfterScreenHide);
            }
        }

        public GameObject GetGameObject()
        {
            return gameObject;
        }

        #region Utility

        protected virtual Task OnShowAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        protected virtual Task OnHideAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
        
        protected virtual void OnShowInstant()
        {
        }

        protected virtual void OnHideInstant()
        {
        }
        
        private void InvokeCallback(Action<IScreen> action)
        {
            try
            {
                action?.Invoke(this);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        #endregion
    }
}