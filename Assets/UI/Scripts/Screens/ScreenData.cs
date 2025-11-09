using UnityEngine;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace AngryKoala.UI
{
    public readonly struct ScreenData
    {
        public string Key { get; }
        public string Address { get; }
        public IScreen Screen { get; }
        public GameObject Instance { get; }
        public AsyncOperationHandle<GameObject> Handle { get; }

        public ScreenData(string key, string address, IScreen screen, GameObject instance, AsyncOperationHandle<GameObject> handle)
        {
            Key = key;
            Address = address;
            Screen = screen;
            Instance = instance;
            Handle = handle;
        }
    }
}
