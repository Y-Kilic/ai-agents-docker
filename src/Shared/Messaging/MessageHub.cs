namespace Shared.Messaging;

using System.Collections.Concurrent;

public static class MessageHub
{
    private static readonly ConcurrentDictionary<string, ConcurrentQueue<string>> _messages = new();

    public static void Send(string to, string message)
    {
        var queue = _messages.GetOrAdd(to, _ => new ConcurrentQueue<string>());
        queue.Enqueue(message);
    }

    public static List<string> Receive(string id)
    {
        var result = new List<string>();
        if (_messages.TryGetValue(id, out var queue))
        {
            while (queue.TryDequeue(out var msg))
                result.Add(msg);
        }
        return result;
    }
}
