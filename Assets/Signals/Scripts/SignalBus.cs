using System;
using System.Collections.Generic;

namespace AngryKoala.Signals
{
    public sealed class SignalBus
    {
        private readonly Dictionary<Type, ISubscriberCollection> _subscribers = new();

        private static readonly SignalBus Instance = new();

        private SignalBus()
        {
        }

        public static void Subscribe<TSignal>(Action<TSignal> callback) where TSignal : ISignal
        {
            Instance.SubscribeInternal(callback);
        }
        
        public static void SubscribeOneShot<TSignal>(Action<TSignal> callback) where TSignal : ISignal
        {
            if (callback == null)
            {
                throw new ArgumentNullException(nameof(callback));
            }

            Action<TSignal> wrapper = null;

            wrapper = (signal) =>
            {
                Unsubscribe(wrapper);
                callback(signal);
            };

            Subscribe(wrapper);
        }

        public static void Unsubscribe<TSignal>(Action<TSignal> callback) where TSignal : ISignal
        {
            Instance.UnsubscribeInternal(typeof(TSignal), callback);
        }
        
        /// <summary>
        /// Remove all callbacks whose target equals the given object.
        /// Use in OnDisable/OnDestroy to wipe all of a component's handlers without tracking each subscription.
        /// </summary>
        public static void UnsubscribeAll(object target)
        {
            Instance.UnsubscribeAllInternal(target);
        }
        
        /// <summary>
        /// Remove all subscribers for a specific signal type.
        /// </summary>
        public static void Clear<TSignal>() where TSignal : ISignal
        {
            Instance.ClearInternal(typeof(TSignal));
        }

        /// <summary>
        /// Remove all subscribers of all signal types.
        /// </summary>
        public static void ClearAll()
        {
            Instance.ClearAllInternal();
        }

        public static void Publish<TSignal>(TSignal signal) where TSignal : ISignal
        {
            Instance.PublishInternal(signal);
        }

        #region Internal Methods

        private void SubscribeInternal<TSignal>(Action<TSignal> callback) where TSignal : ISignal
        {
            if (callback == null)
            {
                throw new ArgumentNullException(nameof(callback));
            }

            var type = typeof(TSignal);

            if (!_subscribers.TryGetValue(type, out var subscriberCollection))
            {
                subscriberCollection = new SubscriberCollection<TSignal>();
                _subscribers[type] = subscriberCollection;
            }

            ((SubscriberCollection<TSignal>)subscriberCollection).Add(callback);
        }

        private void UnsubscribeInternal(Type type, Delegate callback)
        {
            if (type == null || callback == null)
            {
                return;
            }

            if (!_subscribers.TryGetValue(type, out var subscriberCollection))
            {
                return;
            }

            subscriberCollection.Remove(callback);

            if (subscriberCollection.Count == 0)
            {
                _subscribers.Remove(type);
            }
        }
        
        private void UnsubscribeAllInternal(object target)
        {
            if (target == null)
            {
                return;
            }

            var emptyTypes = new List<Type>();

            foreach (var keyValuePair in _subscribers)
            {
                keyValuePair.Value.RemoveAll(target);
                if (keyValuePair.Value.Count == 0)
                {
                    emptyTypes.Add(keyValuePair.Key);
                }
            }

            foreach (var emptyType in emptyTypes)
            {
                _subscribers.Remove(emptyType);
            }
        }
        
        private void ClearInternal(Type type)
        {
            _subscribers.Remove(type);
        }

        private void ClearAllInternal()
        {
            _subscribers.Clear();
        }

        private void PublishInternal<TSignal>(TSignal signal) where TSignal : ISignal
        {
            if (!_subscribers.TryGetValue(typeof(TSignal), out var subscriberCollection))
            {
                return;
            }

            subscriberCollection.Publish(signal);
        }

        #endregion
    }
}