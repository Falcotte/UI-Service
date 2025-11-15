using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace AngryKoala.UI
{
    public abstract class UIContainer : MonoBehaviour
    {
        public bool IsVisible { get; private set; }

        public async Task ShowAsync(TransitionStyle transitionStyle = TransitionStyle.Animated,
            CancellationToken cancellationToken = default)
        {
            if (IsVisible)
            {
                return;
            }

            cancellationToken.ThrowIfCancellationRequested();

            OnBeforeShow();

            try
            {
                await OnShowAsync(transitionStyle, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                throw;
            }

            IsVisible = true;

            OnAfterShow();
        }

        public async Task HideAsync(TransitionStyle transitionStyle = TransitionStyle.Animated,
            CancellationToken cancellationToken = default)
        {
            if (IsVisible == false)
            {
                return;
            }

            cancellationToken.ThrowIfCancellationRequested();

            OnBeforeHide();

            try
            {
                await OnHideAsync(transitionStyle, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                throw;
            }

            IsVisible = false;

            OnAfterHide();
        }

        protected virtual Task OnShowAsync(TransitionStyle transitionStyle, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        protected virtual Task OnHideAsync(TransitionStyle transitionStyle, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        protected virtual void OnBeforeShow()
        {
        }

        protected virtual void OnAfterShow()
        {
        }

        protected virtual void OnBeforeHide()
        {
        }

        protected virtual void OnAfterHide()
        {
        }
    }
}