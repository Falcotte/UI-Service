using System;
using UnityEngine;

namespace AngryKoala.UI
{
    public abstract class Screen : UIContainer, IScreen
    {
        public string ScreenKey { get; private set; }

        public event Action<IScreen> BeforeScreenShow;
        public event Action<IScreen> AfterScreenShow;
        public event Action<IScreen> BeforeScreenHide;
        public event Action<IScreen> AfterScreenHide;

        public void Initialize(string screenKey)
        {
            if (string.IsNullOrWhiteSpace(screenKey))
            {
                throw new ArgumentException("Screen key cannot be null or whitespace.", nameof(screenKey));
            }

            ScreenKey = screenKey;
        }

        public GameObject GetGameObject()
        {
            return gameObject;
        }
        
        protected override void OnBeforeShow()
        {
            InvokeCallback(BeforeScreenShow);
        }

        protected override void OnAfterShow()
        {
            InvokeCallback(AfterScreenShow);
        }

        protected override void OnBeforeHide()
        {
            InvokeCallback(BeforeScreenHide);
        }

        protected override void OnAfterHide()
        {
            InvokeCallback(AfterScreenHide);
        }

        #region Utility

        private void InvokeCallback(Action<IScreen> callback)
        {
            if (callback == null)
            {
                return;
            }
            
            try
            {
                callback?.Invoke(this);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        #endregion
    }
}