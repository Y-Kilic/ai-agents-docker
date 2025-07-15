FROM mcr.microsoft.com/dotnet/runtime:8.0
WORKDIR /app
COPY ./src/Agent.Runtime/bin/Release/net8.0/ ./
ENTRYPOINT ["dotnet", "Agent.Runtime.dll"]
