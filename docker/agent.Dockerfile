FROM mcr.microsoft.com/dotnet/runtime:8.0

# Install Chromium and ChromeDriver so Selenium can run headless tests
RUN apt-get update \
    && apt-get install -y --no-install-recommends \
        chromium-driver \
        chromium \
    && rm -rf /var/lib/apt/lists/*
ENV WEBDRIVER_CHROME_DRIVER=/usr/lib/chromium-browser/chromedriver
ENV PATH="/usr/lib/chromium-browser:$PATH"

WORKDIR /app
COPY ./src/Agent.Runtime/bin/Release/net8.0/ ./
COPY ./src/Agent.Runtime/bin/Release/net8.0/plugins ./plugins
ENTRYPOINT ["dotnet", "Agent.Runtime.dll"]
