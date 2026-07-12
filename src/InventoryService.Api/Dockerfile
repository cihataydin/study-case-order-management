FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY ["src/Shared/Shared.csproj", "src/Shared/"]
COPY ["src/InventoryService.Api/InventoryService.Api.csproj", "src/InventoryService.Api/"]
RUN dotnet restore "src/InventoryService.Api/InventoryService.Api.csproj"

COPY src/Shared/ src/Shared/
COPY src/InventoryService.Api/ src/InventoryService.Api/
WORKDIR "/src/src/InventoryService.Api"
RUN dotnet publish "InventoryService.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:5050
EXPOSE 5050
ENTRYPOINT ["dotnet", "InventoryService.Api.dll"]
