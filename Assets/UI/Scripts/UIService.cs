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
        [SerializeField] private Canvas _activeCanvas;

        [SerializeField] private Transform _screenRoot;
        [SerializeField] private Transform _inactiveRoot;

        [SerializeField] private ScreenRegistry _screenRegistry;

        private readonly Dictionary<string, ScreenData> _loadedScreens = new(StringComparer.Ordinal);
        private readonly Dictionary<string, IScreen> _activeSubscreensByHostKey = new(StringComparer.Ordinal);

        private Transform ActiveRoot => _screenRoot != null ? _screenRoot : _activeCanvas.transform;

        protected override void Awake()
        {
            base.Awake();

            if (_activeCanvas == null)
            {
                throw new InvalidOperationException(
                    "ActiveCanvas reference is not assigned. Assign a Canvas in the inspector.");
            }
        }

        public Task<IScreen> LoadScreenAsync(
            string screenKey,
            CancellationToken cancellationToken = default)
        {
            return LoadScreenAsync<IScreen>(screenKey, cancellationToken);
        }

        public async Task<TScreen> LoadScreenAsync<TScreen>(string screenKey,
            CancellationToken cancellationToken = default) where TScreen : class, IScreen
        {
            if (string.IsNullOrWhiteSpace(screenKey))
            {
                throw new ArgumentException("Screen key cannot be null or whitespace.", nameof(screenKey));
            }

            if (_loadedScreens.TryGetValue(screenKey, out ScreenData activeScreenData))
            {
                if (activeScreenData.Instance != null && activeScreenData.Instance.activeSelf)
                {
                    activeScreenData.Instance.SetActive(false);
                }

                if (activeScreenData.Screen is TScreen typedScreen)
                {
                    return typedScreen;
                }

                if (activeScreenData.Instance != null)
                {
                    TScreen foundScreen = activeScreenData.Instance.GetComponentInChildren<TScreen>(true);
                    if (foundScreen != null)
                    {
                        return foundScreen;
                    }
                }

                throw new InvalidOperationException(
                    $"Loaded screen {screenKey} does not contain a component of type {typeof(TScreen).Name}.");
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
                Addressables.InstantiateAsync(address, _inactiveRoot);

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
                _loadedScreens[screenKey] = screenData;

                if (screen is TScreen typed)
                {
                    return typed;
                }

                TScreen found = instance.GetComponentInChildren<TScreen>(true);
                if (found != null)
                {
                    return found;
                }

                throw new InvalidOperationException(
                    $"Loaded screen {screenKey} does not contain a component of type {typeof(TScreen).Name}.");
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

            if (!_loadedScreens.TryGetValue(screenKey, out ScreenData screenData))
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

                _loadedScreens.Remove(screenKey);
            }
        }

        public async Task<TScreen> GetScreenAsync<TScreen>(string screenKey,
            CancellationToken cancellationToken = default)
            where TScreen : class, IScreen
        {
            if (string.IsNullOrWhiteSpace(screenKey))
            {
                throw new ArgumentException("Screen key cannot be null or whitespace.", nameof(screenKey));
            }

            if (_loadedScreens.TryGetValue(screenKey, out ScreenData activeScreenData))
            {
                if (activeScreenData.Screen is TScreen typedScreen)
                {
                    return typedScreen;
                }

                if (activeScreenData.Instance != null)
                {
                    TScreen foundScreen = activeScreenData.Instance.GetComponentInChildren<TScreen>(true);

                    if (foundScreen != null)
                    {
                        return foundScreen;
                    }
                }

                throw new InvalidOperationException(
                    $"Loaded screen {screenKey} does not contain a component of type {typeof(TScreen).Name}.");
            }

            IScreen baseScreen = await LoadScreenAsync(screenKey, cancellationToken);

            if (baseScreen is TScreen typed)
            {
                return typed;
            }

            if (_loadedScreens.TryGetValue(screenKey, out ScreenData screenData) && screenData.Instance != null)
            {
                TScreen found = screenData.Instance.GetComponentInChildren<TScreen>(true);

                if (found != null)
                {
                    return found;
                }
            }

            throw new InvalidOperationException(
                $"Loaded screen {screenKey} does not contain a component of type {typeof(TScreen).Name}.");
        }

        public Task<IScreen> ShowScreenAsync(string screenKey,
            TransitionStyle transitionStyle = TransitionStyle.Animated,
            CancellationToken cancellationToken = default)
        {
            return ShowScreenAsync<IScreen>(screenKey, transitionStyle, cancellationToken);
        }

        public async Task<TScreen> ShowScreenAsync<TScreen>(string screenKey,
            TransitionStyle transitionStyle = TransitionStyle.Animated,
            CancellationToken cancellationToken = default)
            where TScreen : class, IScreen
        {
            if (string.IsNullOrWhiteSpace(screenKey))
            {
                throw new ArgumentException("Screen key cannot be null or whitespace.", nameof(screenKey));
            }

            if (_loadedScreens.TryGetValue(screenKey, out ScreenData activeScreenData))
            {
                if (activeScreenData.Instance != null)
                {
                    activeScreenData.Instance.transform.SetParent(ActiveRoot, false);
                }

                await activeScreenData.Screen.ShowAsync(transitionStyle, cancellationToken);

                if (activeScreenData.Screen is TScreen typedScreen)
                {
                    return typedScreen;
                }

                if (activeScreenData.Instance != null)
                {
                    TScreen foundScreen = activeScreenData.Instance.GetComponentInChildren<TScreen>(true);
                    if (foundScreen != null)
                    {
                        return foundScreen;
                    }
                }

                throw new InvalidOperationException(
                    $"Loaded screen {screenKey} does not contain a component of type {typeof(TScreen).Name}.");
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
                Addressables.InstantiateAsync(address, _inactiveRoot);

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

                IScreen foundScreen = instance.GetComponentInChildren<IScreen>(true);

                if (foundScreen == null)
                {
                    if (instantiateHandle.IsValid())
                    {
                        Addressables.ReleaseInstance(instantiateHandle);
                    }

                    throw new InvalidOperationException(
                        $"Instantiated prefab for {screenKey} does not contain a component implementing IScreen.");
                }

                foundScreen.Initialize(screenKey);

                ScreenData screenData = new ScreenData(screenKey, address, foundScreen, instance, instantiateHandle);
                _loadedScreens[screenKey] = screenData;

                instance.transform.SetParent(ActiveRoot, false);
                await foundScreen.ShowAsync(transitionStyle, cancellationToken);

                if (foundScreen is TScreen typed)
                {
                    return typed;
                }

                TScreen found = instance.GetComponentInChildren<TScreen>(true);
                if (found != null)
                {
                    return found;
                }

                throw new InvalidOperationException(
                    $"Loaded screen {screenKey} does not contain a component of type {typeof(TScreen).Name}.");
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

        public async Task HideScreenAsync(string screenKey,
            ScreenHideBehaviour hideBehaviour = ScreenHideBehaviour.Deactivate,
            TransitionStyle transitionStyle = TransitionStyle.Animated,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(screenKey))
            {
                throw new ArgumentException("Screen key cannot be null or whitespace.", nameof(screenKey));
            }

            if (!_loadedScreens.TryGetValue(screenKey, out ScreenData screenData))
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

            switch (hideBehaviour)
            {
                case ScreenHideBehaviour.Deactivate:
                {
                    if (_inactiveRoot == null)
                    {
                        throw new InvalidOperationException(
                            "Inactive root is not assigned. Assign a Transform to '_inactiveRoot' on UIService.");
                    }

                    if (screenData.Instance != null)
                    {
                        Transform instanceTransform = screenData.Instance.transform;
                        instanceTransform.SetParent(_inactiveRoot, false);

                        if (screenData.Instance.activeSelf)
                        {
                            screenData.Instance.SetActive(false);
                        }
                    }

                    break;
                }

                case ScreenHideBehaviour.Unload:
                {
                    try
                    {
                        if (screenData.Instance != null && screenData.Handle.IsValid())
                        {
                            Addressables.ReleaseInstance(screenData.Handle);
                        }
                    }
                    catch (Exception exception)
                    {
                        Debug.LogException(exception);
                    }

                    _loadedScreens.Remove(screenKey);
                    break;
                }
            }
        }

        public async Task<IScreen> ShowSubscreenAsync(string hostScreenKey, string subscreenScreenKey,
            TransitionStyle transitionStyle = TransitionStyle.Animated, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(hostScreenKey))
            {
                throw new ArgumentException("Host screen key cannot be null or whitespace.", nameof(hostScreenKey));
            }

            if (string.IsNullOrWhiteSpace(subscreenScreenKey))
            {
                throw new ArgumentException("Subscreen screen key cannot be null or whitespace.",
                    nameof(subscreenScreenKey));
            }

            IScreen hostScreen = await GetScreenAsync<IScreen>(hostScreenKey, cancellationToken);

            if (hostScreen is not Screen hostScreenImplementation)
            {
                throw new InvalidOperationException(
                    $"Host screen {hostScreenKey} is expected to inherit from Screen to support subscreens.");
            }

            Transform subscreenRoot = hostScreenImplementation.SubscreenRoot;
            if (subscreenRoot == null)
            {
                throw new InvalidOperationException(
                    $"Host screen {hostScreenKey} does not define a SubscreenRoot. Assign a RectTransform to host subscreens.");
            }

            if (_activeSubscreensByHostKey.TryGetValue(hostScreenKey, out IScreen existingSubscreen) &&
                existingSubscreen != null)
            {
                if (string.Equals(existingSubscreen.ScreenKey, subscreenScreenKey, StringComparison.Ordinal))
                {
                    GameObject existingSubscreenGameObject = existingSubscreen.GetGameObject();
                    if (existingSubscreenGameObject != null)
                    {
                        Transform existingSubscreenTransform = existingSubscreenGameObject.transform;
                        existingSubscreenTransform.SetParent(subscreenRoot, false);
                    }

                    await existingSubscreen.ShowAsync(transitionStyle, cancellationToken);

                    return existingSubscreen;
                }

                try
                {
                    await existingSubscreen.HideAsync(TransitionStyle.Instant, cancellationToken);
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                }
            }

            IScreen subscreen = await GetScreenAsync<IScreen>(subscreenScreenKey, cancellationToken);

            GameObject subscreenGameObject = subscreen.GetGameObject();
            if (subscreenGameObject == null)
            {
                throw new InvalidOperationException(
                    $"Subscreen {subscreenScreenKey} returned a null GameObject.");
            }

            Transform subscreenTransform = subscreenGameObject.transform;
            subscreenTransform.SetParent(subscreenRoot, false);

            await subscreen.ShowAsync(transitionStyle, cancellationToken);

            _activeSubscreensByHostKey[hostScreenKey] = subscreen;

            return subscreen;
        }

        public async Task HideSubscreenAsync(string hostScreenKey,
            ScreenHideBehaviour hideBehaviour = ScreenHideBehaviour.Deactivate,
            TransitionStyle transitionStyle = TransitionStyle.Animated, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(hostScreenKey))
            {
                throw new ArgumentException("Host screen key cannot be null or whitespace.", nameof(hostScreenKey));
            }

            if (!_activeSubscreensByHostKey.TryGetValue(hostScreenKey, out IScreen activeSubscreen))
            {
                return;
            }

            string subscreenKey = activeSubscreen.ScreenKey;

            try
            {
                await HideScreenAsync(subscreenKey, hideBehaviour, transitionStyle, cancellationToken);
            }
            finally
            {
                _activeSubscreensByHostKey.Remove(hostScreenKey);
            }
        }

        private void OnDestroy()
        {
            foreach (KeyValuePair<string, ScreenData> keyValuePair in _loadedScreens)
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

            _loadedScreens.Clear();
        }
    }
}