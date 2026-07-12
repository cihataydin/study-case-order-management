FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY ["src/Shared/Shared.csproj", "src/Shared/"]
COPY ["src/OrderService.Api/OrderService.Api.csproj", "src/OrderService.Api/"]
RUN dotnet restore "src/OrderService.Api/OrderService.Api.csproj"

COPY src/Shared/ src/Shared/
COPY src/OrderService.Api/ src/OrderService.Api/
WORKDIR "/src/src/OrderService.Api"
RUN dotnet publish "OrderService.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:5277
EXPOSE 5277
ENTRYPOINT ["dotnet", "OrderService.Api.dll"]
