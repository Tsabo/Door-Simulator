# syntax=docker/dockerfile:1
#
# DoorSim — multi-arch image (linux/amd64, linux/arm64).
#
#   docker build -f source/Dockerfile -t doorsim source
#   docker run -p 5000:5000 -v doorsim-data:/data doorsim
#
# The build stage always runs on the build machine's native platform and cross-publishes for the
# target architecture, so arm64 images build without slow QEMU emulation of the SDK.

ARG DOTNET_VERSION=10.0

FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:${DOTNET_VERSION} AS build
ARG TARGETARCH
ARG VERSION=0.0.0-dev
WORKDIR /src

# Blazor WebAssembly client build requirement (mirrors .github/actions/setup-build-env). Publishing
# relinks the wasm runtime with emscripten, which needs python3 — present on CI runners, not here.
RUN apt-get update \
    && apt-get install -y --no-install-recommends python3 \
    && rm -rf /var/lib/apt/lists/* \
    && dotnet workload install wasm-tools

# Restore first, from project files only, so the package cache layer is reused until dependencies
# change. Publish still restores for the target RID (runtime packs), which a RID-less restore can't.
COPY Directory.Build.props Directory.Packages.props nuget.config ./
COPY src/DoorSim/DoorSim.csproj src/DoorSim/
COPY src/DoorSim.Client/DoorSim.Client.csproj src/DoorSim.Client/
COPY src/DoorSim.Shared/DoorSim.Shared.csproj src/DoorSim.Shared/
COPY src/OSDP.Net/OSDP.Net.csproj src/OSDP.Net/
RUN dotnet restore src/DoorSim/DoorSim.csproj

COPY src/ src/
RUN dotnet publish src/DoorSim/DoorSim.csproj \
        -c Release \
        -a $TARGETARCH \
        --no-self-contained \
        -p:Version=$VERSION \
        -o /app

FROM mcr.microsoft.com/dotnet/aspnet:${DOTNET_VERSION} AS runtime

# libgpiod lets System.Device.Gpio drive GPIO when the host passes /dev/gpiochip0 through
# (Raspberry Pi). Without the device the app reports GPIO unavailable and disables Wiegand.
RUN apt-get update \
    && apt-get install -y --no-install-recommends libgpiod2 \
    && rm -rf /var/lib/apt/lists/*

RUN mkdir -p /data && chown $APP_UID /data

ENV DOORSIM_DATA_DIR=/data \
    ASPNETCORE_URLS=http://+:5000 \
    ASPNETCORE_HTTP_PORTS= \
    Seq__ServerUrl=

WORKDIR /app
COPY --from=build /app .

VOLUME /data
EXPOSE 5000
USER $APP_UID

ENTRYPOINT ["dotnet", "DoorSim.dll"]
