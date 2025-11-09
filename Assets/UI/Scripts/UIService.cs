using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AngryKoala.Services;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace AngryKoala.UI
{
    public sealed class UIService : BaseService<IUIService>, IUIService
    {
        [SerializeField] private Canvas _canvas;

        [SerializeField] private ScreenRegistry _screenRegistry;

        private readonly Dictionary<string, ScreenData> _activeScreens = new(StringComparer.Ordinal);

        public async Task<IScreen> LoadScreenAsync(string screenKey, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(screenKey))
            {
                throw new ArgumentException("Screen key cannot be null or whitespace.", nameof(screenKey));
            }
            
            if (_activeScreens.TryGetValue(screenKey, out ScreenData existingData))
            {
                if (existingData.Instance != null && existingData.Instance.activeSelf)
                {
                    existingData.Instance.SetActive(false);
                }

                return existingData.Screen;
            }

            if (_screenRegistry == null)
            {
                throw new InvalidOperationException("ScreenRegistry is not assigned on UIService.");
            }

            if (!_screenRegistry.TryGetAddress(screenKey, out string address) || string.IsNullOrWhiteSpace(address))
            {
                throw new KeyNotFoundException($"No Addressables address found for screen key {screenKey}.");
            }

            AsyncOperationHandle<GameObject> instantiateHandle =
                Addressables.InstantiateAsync(address, _canvas != null ? _canvas.transform : null);

            try
            {
                await instantiateHandle.Task;

                if (cancellationToken.IsCancellationRequested)
                {
                    if (instantiateHandle.IsValid())
                    {
                        Addressables.ReleaseInstance(instantiateHandle);
                    }

                    cancellationToken.ThrowIfCancellationRequested();
                }

                GameObject instance = instantiateHandle.Result;

                if (instance == null)
                {
                    if (instantiateHandle.IsValid())
                    {
                        Addressables.ReleaseInstance(instantiateHandle);
                    }

                    throw new InvalidOperationException(
                        $"Failed to instantiate screen {screenKey} from address {address}.");
                }
                
                if (instance.activeSelf)
                {
                    instance.SetActive(false);
                }

                IScreen screen = instance.GetComponentInChildren<IScreen>(true);

                if (screen == null)
                {
                    if (instantiateHandle.IsValid())
                    {
                        Addressables.ReleaseInstance(instantiateHandle);
                    }

                    throw new InvalidOperationException(
                        $"Instantiated prefab for {screenKey} does not contain a component implementing IScreen.");
                }

                screen.Initialize(screenKey);

                ScreenData screenData = new ScreenData(screenKey, address, screen, instance, instantiateHandle);
                _activeScreens[screenKey] = screenData;

                return screen;
            }
            catch (Exception exception)
            {
                if (instantiateHandle.IsValid())
                {
                    Addressables.ReleaseInstance(instantiateHandle);
                }

                Debug.LogException(exception);
                throw;
            }
        }

        public async Task UnloadScreenAsync(string screenKey, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(screenKey))
            {
                throw new ArgumentException("Screen key cannot be null or whitespace.", nameof(screenKey));
            }

            if (!_activeScreens.TryGetValue(screenKey, out ScreenData screenData))
            {
                return;
            }
            
            try
            {
                if (screenData.Instance != null && screenData.Instance.activeSelf)
                {
                    screenData.Instance.SetActive(false);
                }
                
                await Task.Yield();
            }
            finally
            {
                if (screenData.Handle.IsValid())
                {
                    Addressables.ReleaseInstance(screenData.Handle);
                }

                _activeScreens.Remove(screenKey);
            }
        }
        
        public async Task<TScreen> GetScreenAsync<TScreen>(string screenKey, CancellationToken cancellationToken = default)
            where TScreen : class, IScreen
        {
            if (string.IsNullOrWhiteSpace(screenKey))
            {
                throw new ArgumentException("Screen key cannot be null or whitespace.", nameof(screenKey));
            }

            if (_activeScreens.TryGetValue(screenKey, out ScreenData data))
            {
                if (data.Screen is TScreen typedScreen)
                {
                    return typedScreen;
                }

                if (data.Instance != null)
                {
                    TScreen found = data.Instance.GetComponentInChildren<TScreen>(true);
                    
                    if (found != null)
                    {
                        return found;
                    }
                }

                throw new InvalidOperationException($"Loaded screen '{screenKey}' does not contain a component of type {typeof(TScreen).Name}.");
            }

            IScreen baseScreen = await LoadScreenAsync(screenKey, cancellationToken);

            if (baseScreen is TScreen typed)
            {
                return typed;
            }

            if (_activeScreens.TryGetValue(screenKey, out ScreenData loadedData) && loadedData.Instance != null)
            {
                TScreen found = loadedData.Instance.GetComponentInChildren<TScreen>(true);
                
                if (found != null)
                {
                    return found;
                }
            }

            throw new InvalidOperationException($"Loaded screen {screenKey} does not contain a component of type {typeof(TScreen).Name}.");
        }

        public Task<IScreen> ShowScreenAsync(string screenKey, CancellationToken cancellationToken = default)
        {
            return ShowScreenAsync(screenKey, TransitionStyle.Animated, cancellationToken);
        }

        public async Task<IScreen> ShowScreenAsync(string screenKey, TransitionStyle transitionStyle,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(screenKey))
            {
                throw new ArgumentException("Screen key cannot be null or whitespace.", nameof(screenKey));
            }

            if (_activeScreens.TryGetValue(screenKey, out ScreenData activeScreenData))
            {
                await activeScreenData.Screen.ShowAsync(transitionStyle, cancellationToken);
                return activeScreenData.Screen;
            }

            if (_screenRegistry == null)
            {
                throw new InvalidOperationException("ScreenRegistry is not assigned on UIService.");
            }

            if (!_screenRegistry.TryGetAddress(screenKey, out string address) || string.IsNullOrWhiteSpace(address))
            {
                throw new KeyNotFoundException($"No Addressables address found for screen key {screenKey}.");
            }

            AsyncOperationHandle<GameObject> instantiateHandle =
                Addressables.InstantiateAsync(address, _canvas != null ? _canvas.transform : null);

            try
            {
                await instantiateHandle.Task;

                if (cancellationToken.IsCancellationRequested)
                {
                    if (instantiateHandle.IsValid())
                    {
                        Addressables.ReleaseInstance(instantiateHandle);
                    }

                    cancellationToken.ThrowIfCancellationRequested();
                }

                GameObject instance = instantiateHandle.Result;

                if (instance == null)
                {
                    if (instantiateHandle.IsValid())
                    {
                        Addressables.ReleaseInstance(instantiateHandle);
                    }

                    throw new InvalidOperationException(
                        $"Failed to instantiate screen {screenKey} from address {address}.");
                }

                IScreen screen = instance.GetComponentInChildren<IScreen>(true);

                if (screen == null)
                {
                    if (instantiateHandle.IsValid())
                    {
                        Addressables.ReleaseInstance(instantiateHandle);
                    }

                    throw new InvalidOperationException(
                        $"Instantiated prefab for {screenKey} does not contain a component implementing IScreen.");
                }

                screen.Initialize(screenKey);

                ScreenData screenData = new ScreenData(screenKey, address, screen, instance, instantiateHandle);
                _activeScreens[screenKey] = screenData;

                await screen.ShowAsync(transitionStyle, cancellationToken);

                return screen;
            }
            catch (Exception exception)
            {
                if (instantiateHandle.IsValid())
                {
                    Addressables.ReleaseInstance(instantiateHandle);
                }

                Debug.LogException(exception);
                throw;
            }
        }

        public Task HideScreenAsync(string screenKey, CancellationToken cancellationToken = default)
        {
            return HideScreenAsync(screenKey, TransitionStyle.Animated, cancellationToken);
        }

        public async Task HideScreenAsync(string screenKey, TransitionStyle transitionStyle,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(screenKey))
            {
                throw new ArgumentException("Screen key cannot be null or whitespace.", nameof(screenKey));
            }

            if (!_activeScreens.TryGetValue(screenKey, out ScreenData screenData))
            {
                return;
            }

            try
            {
                await screenData.Screen.HideAsync(transitionStyle, cancellationToken);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
            finally
            {
                if (screenData.Handle.IsValid())
                {
                    Addressables.ReleaseInstance(screenData.Handle);
                }

                _activeScreens.Remove(screenKey);
            }
        }

        private void OnDestroy()
        {
            foreach (KeyValuePair<string, ScreenData> keyValuePair in _activeScreens)
            {
                ScreenData screenData = keyValuePair.Value;
                try
                {
                    if (screenData.Handle.IsValid())
                    {
                        Addressables.ReleaseInstance(screenData.Handle);
                    }
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                }
            }

            _activeScreens.Clear();
        }
    }
}