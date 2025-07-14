namespace Shared.Messaging;

using System.Collections.Concurrent;

public static class MemoryHub
{
    private static readonly ConcurrentDictionary<string, ConcurrentQueue<string>> _memories = new();

    public static void Send(string to, string entry)
    {
        var queue = _memories.GetOrAdd(to, _ => new ConcurrentQueue<string>());
        queue.Enqueue(entry);
    }

    public static List<string> Receive(string id)
    {
        var result = new List<string>();
        if (_memories.TryGetValue(id, out var queue))
        {
            while (queue.TryDequeue(out var entry))
                result.Add(entry);
        }
        return result;
    }
}
