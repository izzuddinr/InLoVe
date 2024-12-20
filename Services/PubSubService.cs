using System;
using System.Collections.Generic;

namespace Qatalyst.Services;

public class PubSubService
{
    private readonly Dictionary<string, List<Action<object>>> _subscribers = new();

    public void Subscribe(string eventName, Action<object> handler)
    {
        if (!_subscribers.ContainsKey(eventName))
        {
            _subscribers[eventName] = [];
        }
        _subscribers[eventName].Add(handler);
    }

    public void Publish(string eventName, object eventData)
    {
        if (_subscribers.ContainsKey(eventName))
        {
            foreach (var handler in _subscribers[eventName])
            {
                handler(eventData);
            }
        }
    }
}