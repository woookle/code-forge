FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY ["CodeForgeAPI.csproj", "./"]
RUN dotnet restore "CodeForgeAPI.csproj"
COPY . .
RUN dotnet build "CodeForgeAPI.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "CodeForgeAPI.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "CodeForgeAPI.dll"]
