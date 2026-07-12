FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY ["src/Shared/Shared.csproj", "src/Shared/"]
COPY ["src/PaymentService.Api/PaymentService.Api.csproj", "src/PaymentService.Api/"]
RUN dotnet restore "src/PaymentService.Api/PaymentService.Api.csproj"

COPY src/Shared/ src/Shared/
COPY src/PaymentService.Api/ src/PaymentService.Api/
WORKDIR "/src/src/PaymentService.Api"
RUN dotnet publish "PaymentService.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:5032
EXPOSE 5032
ENTRYPOINT ["dotnet", "PaymentService.Api.dll"]
