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

        public void Initialize(string screenKey)
        {
            ScreenKey = screenKey;
        }

        public async Task ShowAsync(CancellationToken cancellationToken)
        {
            try
            {
                gameObject.SetActive(true);
                IsVisible = true;

                await OnShowAsync(cancellationToken);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                throw;
            }
        }

        public async Task HideAsync(CancellationToken cancellationToken)
        {
            try
            {
                await OnHideAsync(cancellationToken);

                IsVisible = false;
                gameObject.SetActive(false);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                throw;
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

        #endregion
    }
}