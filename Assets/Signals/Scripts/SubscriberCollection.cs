using System;
using System.Collections.Generic;
using UnityEngine;

namespace AngryKoala.Signals
{
    public sealed class SubscriberCollection<TSignal> : ISubscriberCollection where TSignal : ISignal
    {
        private readonly List<Action<TSignal>> _callbacks = new();

        public int Count => _callbacks.Count;

        public void Add(Action<TSignal> callback)
        {
            _callbacks.Add(callback);
        }

        public void Remove(Delegate del)
        {
            _callbacks.Remove((Action<TSignal>)del);
        }

        public void RemoveAll(object target)
        {
            if (target == null)
            {
                return;
            }

            _callbacks.RemoveAll(callback =>
            {
                var callbackTarget = callback.Target;
                return callbackTarget != null && ReferenceEquals(callbackTarget, target);
            });
        }

        public void Publish(object signal)
        {
            var snapshot = _callbacks.ToArray();

            foreach (var callback in snapshot)
            {
                try
                {
                    callback((TSignal)signal);
                }
                catch (Exception ex)
                {
                    var method = callback.Method;
                    var target = callback.Target;

                    string subscriberName = $"{method?.DeclaringType?.FullName}.{method?.Name}";
                    string signalName = typeof(TSignal).FullName;

                    if (target is UnityEngine.Object unityObject)
                    {
                        Debug.LogError(
                            $"Exception in {signalName} subscriber {subscriberName} on target '{unityObject.name}' ({unityObject.GetType().Name}). See stack trace below.",
                            unityObject);

                        Debug.LogException(ex, unityObject);
                    }
                    else
                    {
                        Debug.LogError(
                            $"Exception in {signalName} subscriber {subscriberName} on target {(target?.GetType().FullName ?? "null")}. See stack trace below.");

                        Debug.LogException(ex);
                    }
                }
            }
        }
    }
}