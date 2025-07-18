using System.Reflection;

namespace Agent.Runtime.Tools;

public static class PluginLoader
{
    public static void LoadPlugins(string? pluginDirectory = null)
    {
        pluginDirectory ??= Path.Combine(AppContext.BaseDirectory, "plugins");
        if (!Directory.Exists(pluginDirectory))
            return;

        foreach (var dll in Directory.GetFiles(pluginDirectory, "*.dll"))
        {
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
