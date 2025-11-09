using System;
using System.Collections.Generic;
using UnityEngine;

namespace AngryKoala.UI
{
    [CreateAssetMenu(fileName = "ScreenRegistry", menuName = "AngryKoala/UI/Screen Registry")]
    public sealed class ScreenRegistry : ScriptableObject
    {
        [SerializeField] private List<ScreenRegistration> _registrations = new();

        private Dictionary<string, string> _registry;

        public bool TryGetAddress(string screenKey, out string address)
        {
            if (_registry == null)
            {
                BuildRegistry();
            }

            if (screenKey == null)
            {
                address = null;
                return false;
            }

            return _registry.TryGetValue(screenKey, out address);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            BuildRegistry();
        }
#endif

        private void OnEnable()
        {
            BuildRegistry();
        }

        private void BuildRegistry()
        {
            _registry = new Dictionary<string, string>(StringComparer.Ordinal);
            if (_registrations == null)
            {
                return;
            }

            for (int i = 0; i < _registrations.Count; i++)
            {
                string key = _registrations[i].Key ?? string.Empty;
                string address = _registrations[i].Address ?? string.Empty;

                if (string.IsNullOrWhiteSpace(key))
                {
                    continue;
                }

                _registry.TryAdd(key, address);
            }
        }

        public IReadOnlyList<ScreenRegistration> GetRegistrations()
        {
            return _registrations;
        }
    }
}