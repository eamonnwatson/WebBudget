# syntax=docker/dockerfile:1.7

FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG TARGETARCH
WORKDIR /src

COPY ["Directory.Build.props", "Directory.Packages.props", "PredictiveBudget.sln", "./"]
COPY ["src/PredictiveBudget.Application/PredictiveBudget.Application.csproj", "src/PredictiveBudget.Application/"]
COPY ["src/PredictiveBudget.Domain/PredictiveBudget.Domain.csproj", "src/PredictiveBudget.Domain/"]
COPY ["src/PredictiveBudget.Persistence/PredictiveBudget.Persistence.csproj", "src/PredictiveBudget.Persistence/"]
COPY ["src/PredictiveBudget.Web/PredictiveBudget.Web.csproj", "src/PredictiveBudget.Web/"]

RUN dotnet restore "src/PredictiveBudget.Web/PredictiveBudget.Web.csproj" -a $TARGETARCH

COPY . .

RUN dotnet publish "src/PredictiveBudget.Web/PredictiveBudget.Web.csproj" \
    -c Release \
    -o /app/publish \
    -a $TARGETARCH \
    --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    ConnectionStrings__BudgetDb=Data Source=/data/predictivebudget.db

RUN mkdir /data

VOLUME ["/data"]
EXPOSE 8080

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "PredictiveBudget.Web.dll"]
