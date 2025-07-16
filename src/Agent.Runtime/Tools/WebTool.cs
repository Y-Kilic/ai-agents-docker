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

        using var driver = new ChromeDriver(options);
        driver.Navigate().GoToUrl(input);
        await Task.Delay(1000);
        var text = driver.PageSource;
        if (text.Length > 1000)
            text = text.Substring(0, 1000);
        return text;
    }
}
