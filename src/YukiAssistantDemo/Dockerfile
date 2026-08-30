FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY src/YukiAssistantDemo/YukiAssistantDemo.csproj src/YukiAssistantDemo/
RUN dotnet restore src/YukiAssistantDemo/YukiAssistantDemo.csproj

COPY src/YukiAssistantDemo/ src/YukiAssistantDemo/
RUN dotnet publish src/YukiAssistantDemo/YukiAssistantDemo.csproj \
    --configuration Release \
    --output /app/publish \
    --no-restore \
    --no-self-contained

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
RUN apt-get update \
    && apt-get install --yes --no-install-recommends libgssapi-krb5-2 \
    && rm -rf /var/lib/apt/lists/*
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "YukiAssistantDemo.dll"]
