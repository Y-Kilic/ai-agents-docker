using System.Reflection;

namespace Agent.Runtime.Tools;

public static class PluginLoader
{
    public static void LoadPlugins(string? pluginDirectory = null)
    {
        pluginDirectory ??= Path.Combine(AppContext.BaseDirectory, "plugins");
        if (!Directory.Exists(pluginDirectory))
            return;

        var disableCodex = Environment.GetEnvironmentVariable("DISABLE_CODEX");

        foreach (var dll in Directory.GetFiles(pluginDirectory, "*.dll"))
        {
            if (!string.IsNullOrEmpty(disableCodex) &&
                Path.GetFileName(dll).StartsWith("Codex", StringComparison.OrdinalIgnoreCase))
            {
                ToolRegistry.Log("Codex plugin disabled via DISABLE_CODEX");
                continue;
            }
            try
            {
                var asm = Assembly.LoadFrom(dll);
                foreach (var type in asm.GetTypes())
                {
                    if (typeof(ITool).IsAssignableFrom(type) && !type.IsAbstract)
                    {
                        if (Activator.CreateInstance(type) is ITool tool)
                        {
                            ToolRegistry.Register(tool);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                ToolRegistry.Log($"Failed to load plugin {dll}: {ex.Message}");
            }
        }
    }
}
