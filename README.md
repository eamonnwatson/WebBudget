# PredictiveBudget

PredictiveBudget can now run as a Docker container and be published as an ARM64 image for a Raspberry Pi.

## Local development on your PC

Your normal Visual Studio and `dotnet run` workflow stays native on your AMD64 Windows machine. Nothing in the Docker setup replaces the existing project-based debug experience.

Run the web app locally without Docker:

```powershell
dotnet run --project src/PredictiveBudget.Web/PredictiveBudget.Web.csproj
```

The default local database remains `src/PredictiveBudget.Web/App_Data/predictivebudget.db`.

## Run with Docker Compose

Build and start the app locally:

```powershell
docker compose up --build -d
```

The app will be available at `http://localhost:8080`.

SQLite data is persisted in the named Docker volume `predictivebudget-data`, mounted at `/data` inside the container.

To stop the app:

```powershell
docker compose down
```

## Build a Raspberry Pi ARM64 image

Build an ARM64 image locally:

```powershell
docker buildx build --platform linux/arm64 -t predictivebudget:arm64 .
```

If you want Docker to load the ARM64 image into your local image store after the build, add `--load`.

## Publish an ARM64 image to a registry

Push an ARM64 image for your Raspberry Pi to a registry:

```powershell
docker buildx build `
  --platform linux/arm64 `
  --tag ghcr.io/<your-account>/predictivebudget:latest `
  --push .
```

If you want one tag that works on both desktop and Raspberry Pi, publish a multi-arch image:

```powershell
docker buildx build `
  --platform linux/amd64,linux/arm64 `
  --tag ghcr.io/<your-account>/predictivebudget:latest `
  --push .
```

## Deploy on the Raspberry Pi

Use a 64-bit Raspberry Pi OS install so the device can run `linux/arm64` images.

Pull and run the published image on the Pi:

```bash
docker run -d \
  --name predictivebudget \
  --restart unless-stopped \
  -p 8080:8080 \
  -v predictivebudget-data:/data \
  ghcr.io/<your-account>/predictivebudget:latest
```

The container listens on port `8080`, and the database lives at `/data/predictivebudget.db` inside the container.
