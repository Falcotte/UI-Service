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

        public async Task<IScreen> ShowScreenAsync(string screenKey, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(screenKey))
            {
                throw new ArgumentException("Screen key cannot be null or whitespace.", nameof(screenKey));
            }

            if (_activeScreens.TryGetValue(screenKey, out ScreenData activeScreenData))
            {
                await activeScreenData.Screen.ShowAsync(cancellationToken);

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
                Addressables.InstantiateAsync(address, _canvas.transform);
            try
            {
                await instantiateHandle.Task;

                if (cancellationToken.IsCancellationRequested)
                {
                    Addressables.ReleaseInstance(instantiateHandle);

                    cancellationToken.ThrowIfCancellationRequested();
                }

                GameObject instance = instantiateHandle.Result;

                if (instance == null)
                {
                    Addressables.ReleaseInstance(instantiateHandle);

                    throw new InvalidOperationException(
                        $"Failed to instantiate screen {screenKey} from address {address}.");
                }

                IScreen screen = instance.GetComponentInChildren<IScreen>(true);

                if (screen == null)
                {
                    Addressables.ReleaseInstance(instantiateHandle);

                    throw new InvalidOperationException(
                        $"Instantiated prefab for {screenKey} does not contain a component implementing IScreen.");
                }

                screen.Initialize(screenKey);

                ScreenData screenData = new ScreenData(screenKey, address, screen, instance, instantiateHandle);
                _activeScreens[screenKey] = screenData;

                await screen.ShowAsync(cancellationToken);

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

        public async Task HideScreenAsync(string screenKey, CancellationToken cancellationToken = default)
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
                await screenData.Screen.HideAsync(cancellationToken);
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