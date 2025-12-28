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

        private readonly Dictionary<string, ScreenData> _loadedScreensByScreenKey = new(StringComparer.Ordinal);
        private readonly Dictionary<string, IScreen> _activeSubscreensByScreenKey = new(StringComparer.Ordinal);

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

        public Task<IScreen> LoadScreenAsync(string screenKey, CancellationToken cancellationToken = default)
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

            if (_loadedScreensByScreenKey.TryGetValue(screenKey, out ScreenData loadedScreenData))
            {
                if (loadedScreenData.Instance != null && loadedScreenData.Instance.activeSelf)
                {
                    loadedScreenData.Instance.SetActive(false);
                }

                if (loadedScreenData.Screen is TScreen typedScreen)
                {
                    return typedScreen;
                }

                if (loadedScreenData.Instance != null)
                {
                    TScreen foundScreen = loadedScreenData.Instance.GetComponentInChildren<TScreen>(true);
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
                _loadedScreensByScreenKey[screenKey] = screenData;

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

            if (!_loadedScreensByScreenKey.TryGetValue(screenKey, out ScreenData screenData))
            {
                string screenKeyToClear = null;

                foreach (KeyValuePair<string, IScreen> keyValuePair in _activeSubscreensByScreenKey)
                {
                    IScreen trackedSubscreen = keyValuePair.Value;

                    if (trackedSubscreen != null &&
                        string.Equals(trackedSubscreen.ScreenKey, screenKey, StringComparison.Ordinal))
                    {
                        screenKeyToClear = keyValuePair.Key;
                        break;
                    }
                }

                if (!string.IsNullOrEmpty(screenKeyToClear))
                {
                    _activeSubscreensByScreenKey.Remove(screenKeyToClear);
                }

                return;
            }

            if (_activeSubscreensByScreenKey.TryGetValue(screenKey, out IScreen activeSubscreen) &&
                activeSubscreen != null)
            {
                _activeSubscreensByScreenKey.Remove(screenKey);

                string subscreenScreenKey = activeSubscreen.ScreenKey;

                if (!string.IsNullOrWhiteSpace(subscreenScreenKey) &&
                    !string.Equals(subscreenScreenKey, screenKey, StringComparison.Ordinal) &&
                    _loadedScreensByScreenKey.ContainsKey(subscreenScreenKey))
                {
                    try
                    {
                        await UnloadScreenAsync(subscreenScreenKey, cancellationToken);
                    }
                    catch (Exception exception)
                    {
                        Debug.LogException(exception);
                    }
                }
            }
            else
            {
                string screenKeyToClear = null;

                foreach (KeyValuePair<string, IScreen> keyValuePair in _activeSubscreensByScreenKey)
                {
                    IScreen trackedSubscreen = keyValuePair.Value;

                    if (trackedSubscreen != null &&
                        string.Equals(trackedSubscreen.ScreenKey, screenKey, StringComparison.Ordinal))
                    {
                        screenKeyToClear = keyValuePair.Key;
                        break;
                    }
                }

                if (!string.IsNullOrEmpty(screenKeyToClear))
                {
                    _activeSubscreensByScreenKey.Remove(screenKeyToClear);
                }
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

                _loadedScreensByScreenKey.Remove(screenKey);
            }
        }

        public async Task<TScreen> GetScreenAsync<TScreen>(string screenKey,
            CancellationToken cancellationToken = default) where TScreen : class, IScreen
        {
            if (string.IsNullOrWhiteSpace(screenKey))
            {
                throw new ArgumentException("Screen key cannot be null or whitespace.", nameof(screenKey));
            }

            if (_loadedScreensByScreenKey.TryGetValue(screenKey, out ScreenData loadedScreenData))
            {
                if (loadedScreenData.Screen is TScreen typedScreen)
                {
                    return typedScreen;
                }

                if (loadedScreenData.Instance != null)
                {
                    TScreen foundScreen = loadedScreenData.Instance.GetComponentInChildren<TScreen>(true);

                    if (foundScreen != null)
                    {
                        return foundScreen;
                    }
                }

                throw new InvalidOperationException(
                    $"Loaded screen {screenKey} does not contain a component of type {typeof(TScreen).Name}.");
            }

            IScreen screen = await LoadScreenAsync(screenKey, cancellationToken);

            if (screen is TScreen typed)
            {
                return typed;
            }

            if (_loadedScreensByScreenKey.TryGetValue(screenKey, out loadedScreenData) &&
                loadedScreenData.Instance != null)
            {
                TScreen found = loadedScreenData.Instance.GetComponentInChildren<TScreen>(true);

                if (found != null)
                {
                    return found;
                }
            }

            throw new InvalidOperationException(
                $"Loaded screen {screenKey} does not contain a component of type {typeof(TScreen).Name}.");
        }

        public Task<IScreen> ShowScreenAsync(string screenKey,
            TransitionStyle transitionStyle = TransitionStyle.Animated, CancellationToken cancellationToken = default)
        {
            return ShowScreenAsync<IScreen>(screenKey, transitionStyle, cancellationToken);
        }

        public async Task<TScreen> ShowScreenAsync<TScreen>(string screenKey,
            TransitionStyle transitionStyle = TransitionStyle.Animated, CancellationToken cancellationToken = default)
            where TScreen : class, IScreen
        {
            if (string.IsNullOrWhiteSpace(screenKey))
            {
                throw new ArgumentException("Screen key cannot be null or whitespace.", nameof(screenKey));
            }

            if (_loadedScreensByScreenKey.TryGetValue(screenKey, out ScreenData loadedScreenData))
            {
                if (loadedScreenData.Instance != null)
                {
                    loadedScreenData.Instance.transform.SetParent(ActiveRoot, false);
                }

                await loadedScreenData.Screen.ShowAsync(transitionStyle, cancellationToken);

                if (loadedScreenData.Screen is TScreen typedScreen)
                {
                    return typedScreen;
                }

                if (loadedScreenData.Instance != null)
                {
                    TScreen foundScreen = loadedScreenData.Instance.GetComponentInChildren<TScreen>(true);
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

            AsyncOperationHandle<GameObject> instantiateHandle = Addressables.InstantiateAsync(address, _inactiveRoot);

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
                _loadedScreensByScreenKey[screenKey] = screenData;

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

            if (!_loadedScreensByScreenKey.TryGetValue(screenKey, out ScreenData loadedScreenData))
            {
                string screenKeyToClear = null;

                foreach (KeyValuePair<string, IScreen> keyValuePair in _activeSubscreensByScreenKey)
                {
                    IScreen trackedSubscreen = keyValuePair.Value;

                    if (trackedSubscreen != null &&
                        string.Equals(trackedSubscreen.ScreenKey, screenKey, StringComparison.Ordinal))
                    {
                        screenKeyToClear = keyValuePair.Key;
                        break;
                    }
                }

                if (!string.IsNullOrEmpty(screenKeyToClear))
                {
                    _activeSubscreensByScreenKey.Remove(screenKeyToClear);
                }

                return;
            }

            if (_activeSubscreensByScreenKey.TryGetValue(screenKey, out IScreen activeSubscreen) &&
                activeSubscreen != null)
            {
                _activeSubscreensByScreenKey.Remove(screenKey);

                string subscreenScreenKey = activeSubscreen.ScreenKey;

                if (!string.IsNullOrWhiteSpace(subscreenScreenKey) &&
                    !string.Equals(subscreenScreenKey, screenKey, StringComparison.Ordinal))
                {
                    try
                    {
                        await HideScreenAsync(subscreenScreenKey, hideBehaviour, TransitionStyle.Instant,
                            cancellationToken);
                    }
                    catch (Exception exception)
                    {
                        Debug.LogException(exception);
                    }
                }
            }
            else
            {
                string screenKeyToClear = null;

                foreach (KeyValuePair<string, IScreen> keyValuePair in _activeSubscreensByScreenKey)
                {
                    IScreen trackedSubscreen = keyValuePair.Value;

                    if (trackedSubscreen != null &&
                        string.Equals(trackedSubscreen.ScreenKey, screenKey, StringComparison.Ordinal))
                    {
                        screenKeyToClear = keyValuePair.Key;
                        break;
                    }
                }

                if (!string.IsNullOrEmpty(screenKeyToClear))
                {
                    _activeSubscreensByScreenKey.Remove(screenKeyToClear);
                }
            }

            try
            {
                await loadedScreenData.Screen.HideAsync(transitionStyle, cancellationToken);
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

                    if (loadedScreenData.Instance != null)
                    {
                        Transform instanceTransform = loadedScreenData.Instance.transform;
                        instanceTransform.SetParent(_inactiveRoot, false);

                        if (loadedScreenData.Instance.activeSelf)
                        {
                            loadedScreenData.Instance.SetActive(false);
                        }
                    }

                    break;
                }

                case ScreenHideBehaviour.Unload:
                {
                    try
                    {
                        if (loadedScreenData.Instance != null && loadedScreenData.Handle.IsValid())
                        {
                            Addressables.ReleaseInstance(loadedScreenData.Handle);
                        }
                    }
                    catch (Exception exception)
                    {
                        Debug.LogException(exception);
                    }

                    _loadedScreensByScreenKey.Remove(screenKey);
                    break;
                }
            }
        }

        public async Task<IScreen> ShowSubscreenAsync(string screenKey, string subscreenScreenKey,
            TransitionStyle transitionStyle = TransitionStyle.Animated,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(screenKey))
            {
                throw new ArgumentException("Screen key cannot be null or whitespace.", nameof(screenKey));
            }

            if (string.IsNullOrWhiteSpace(subscreenScreenKey))
            {
                throw new ArgumentException("Subscreen key cannot be null or whitespace.", nameof(subscreenScreenKey));
            }

            if (!_loadedScreensByScreenKey.TryGetValue(screenKey, out ScreenData loadedScreenData))
            {
                throw new InvalidOperationException(
                    $"Cannot show subscreen {subscreenScreenKey} because screen {screenKey} is not loaded. " +
                    $"Call ShowScreenAsync or LoadScreenAsync for screen {screenKey} first.");
            }

            IScreen screen = loadedScreenData.Screen;

            if (!screen.IsVisible)
            {
                throw new InvalidOperationException(
                    $"Cannot show subscreen {subscreenScreenKey} because screen {screenKey} is not visible. " +
                    $"Call ShowScreenAsync for screen {screenKey} first.");
            }

            if (screen is not Screen typedScreen)
            {
                throw new InvalidOperationException(
                    $"Screen {screenKey} must inherit from Screen to support subscreens.");
            }

            Transform subscreenRoot = typedScreen.SubscreenRoot;
            if (subscreenRoot == null)
            {
                throw new InvalidOperationException(
                    $"Screen {screenKey} does not define a SubscreenRoot. " +
                    "Assign a RectTransform to support subscreens.");
            }

            GameObject subscreenGameObject;
            Transform subscreenTransform;

            if (_activeSubscreensByScreenKey.TryGetValue(screenKey, out IScreen activeSubscreen) &&
                activeSubscreen != null)
            {
                if (string.Equals(activeSubscreen.ScreenKey, subscreenScreenKey, StringComparison.Ordinal))
                {
                    subscreenGameObject = activeSubscreen.GetGameObject();
                    if (subscreenGameObject != null)
                    {
                        subscreenTransform = subscreenGameObject.transform;
                        subscreenTransform.SetParent(subscreenRoot, false);
                    }

                    await activeSubscreen.ShowAsync(transitionStyle, cancellationToken);
                    return activeSubscreen;
                }

                try
                {
                    await HideScreenAsync(activeSubscreen.ScreenKey, ScreenHideBehaviour.Deactivate,
                        TransitionStyle.Instant, cancellationToken);
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                }
            }

            IScreen subscreen = await LoadScreenAsync(subscreenScreenKey, cancellationToken);

            subscreenGameObject = subscreen.GetGameObject();
            if (subscreenGameObject == null)
            {
                throw new InvalidOperationException(
                    $"Subscreen '{subscreenScreenKey}' returned a null GameObject.");
            }

            subscreenTransform = subscreenGameObject.transform;
            subscreenTransform.SetParent(subscreenRoot, false);

            await subscreen.ShowAsync(transitionStyle, cancellationToken);

            _activeSubscreensByScreenKey[screenKey] = subscreen;

            return subscreen;
        }

        public async Task HideSubscreenAsync(string screenKey,
            ScreenHideBehaviour hideBehaviour = ScreenHideBehaviour.Deactivate,
            TransitionStyle transitionStyle = TransitionStyle.Animated,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(screenKey))
            {
                throw new ArgumentException("Screen key cannot be null or whitespace.", nameof(screenKey));
            }

            if (!_activeSubscreensByScreenKey.TryGetValue(screenKey, out IScreen activeSubscreen) ||
                activeSubscreen == null)
            {
                return;
            }

            _activeSubscreensByScreenKey.Remove(screenKey);

            string subscreenScreenKey = activeSubscreen.ScreenKey;
            
            if (string.IsNullOrWhiteSpace(subscreenScreenKey))
            {
                return;
            }

            try
            {
                await HideScreenAsync( subscreenScreenKey, hideBehaviour, transitionStyle, cancellationToken);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        private void OnDestroy()
        {
            foreach (KeyValuePair<string, ScreenData> keyValuePair in _loadedScreensByScreenKey)
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

            _loadedScreensByScreenKey.Clear();
            _activeSubscreensByScreenKey.Clear();
        }
    }
}