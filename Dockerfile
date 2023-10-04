FROM mcr.microsoft.com/dotnet/aspnet:7.0 AS base
WORKDIR /app
EXPOSE 80

FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:8.0-preview AS build
ARG TARGETARCH
WORKDIR /src
COPY ["WebBudget.csproj", "."]
RUN dotnet restore "./WebBudget.csproj" -a $TARGETARCH
COPY . .
WORKDIR "/src/."
RUN dotnet build "WebBudget.csproj" -c Release -o /app/build -a $TARGETARCH

FROM build AS publish
RUN dotnet publish "WebBudget.csproj" -c Release -o /app/publish /p:UseAppHost=false -a $TARGETARCH

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "WebBudget.dll"]
