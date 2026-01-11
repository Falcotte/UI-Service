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
            try
            {
                cancellationToken.ThrowIfCancellationRequested();

                OnBeforeShow();

                gameObject.SetActive(true);
                IsVisible = true;

                switch (transitionStyle)
                {
                    case TransitionStyle.Instant:
                        OnShowInstant();
                        break;

                    case TransitionStyle.Animated:
                    default:
                        await OnShowAsync(cancellationToken);
                        break;
                }

                OnAfterShow();
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
        }

        public async Task HideAsync(TransitionStyle transitionStyle = TransitionStyle.Animated,
            CancellationToken cancellationToken = default)
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();

                OnBeforeHide();

                switch (transitionStyle)
                {
                    case TransitionStyle.Instant:
                        OnHideInstant();
                        break;

                    case TransitionStyle.Animated:
                    default:
                        await OnHideAsync(cancellationToken);
                        break;
                }
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
            finally
            {
                IsVisible = false;
                gameObject.SetActive(false);

                OnAfterHide();
            }
        }

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