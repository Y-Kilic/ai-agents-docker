using System;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;

namespace Agent.Runtime.Tools;

public class WebTool : ITool
{
    public string Name => "web";

    public async Task<string> ExecuteAsync(string input)
    {
        var options = new ChromeOptions();
        options.AddArgument("--headless=new");
        options.AddArgument("--no-sandbox");
        options.AddArgument("--disable-gpu");
        options.AddArgument("--disable-dev-shm-usage");
        options.BinaryLocation = "/usr/bin/chromium-browser";

        try
        {
            Console.WriteLine("[WebTool] Starting ChromeDriver");
            ToolRegistry.Log("[WebTool] Starting ChromeDriver");
            using var driver = new ChromeDriver(options);
            driver.Manage().Timeouts().PageLoad = TimeSpan.FromSeconds(30);
            Console.WriteLine("[WebTool] ChromeDriver started");
            ToolRegistry.Log("[WebTool] ChromeDriver started");

            var url = input.Trim().Trim('"');
            if (!url.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                url = "https://" + url;
            Console.WriteLine($"[WebTool] Navigating to {url}");
            ToolRegistry.Log($"[WebTool] Navigating to {url}");

            try
            {
                driver.Navigate().GoToUrl(url);
                await Task.Delay(1000);
                var text = driver.PageSource;
                return text;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[WebTool] Failed to load {url}: {ex.Message}");
                ToolRegistry.Log($"[WebTool] Failed to load {url}: {ex.Message}");
                return "Failed to load website";
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[WebTool] Browser initialization failed: {ex.Message}");
            ToolRegistry.Log($"[WebTool] Browser initialization failed: {ex.Message}");
            return "Failed to start browser";
        }
    }
}
