# Stage 1 — Build
FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build
WORKDIR /src

# Copy project files first for layer caching
COPY MonoBot.slnx .
COPY src/MonoBot.Abstractions/MonoBot.Abstractions.csproj        src/MonoBot.Abstractions/
COPY src/MonoBot/MonoBot.csproj                                   src/MonoBot/
COPY src/Plugins/MonoBot.Plugin.Bender/MonoBot.Plugin.Bender.csproj               src/Plugins/MonoBot.Plugin.Bender/
COPY src/Plugins/MonoBot.Plugin.Announcements/MonoBot.Plugin.Announcements.csproj src/Plugins/MonoBot.Plugin.Announcements/
COPY src/Plugins/MonoBot.Plugin.Admin/MonoBot.Plugin.Admin.csproj                 src/Plugins/MonoBot.Plugin.Admin/
COPY src/Plugins/MonoBot.Plugin.Core/MonoBot.Plugin.Core.csproj                   src/Plugins/MonoBot.Plugin.Core/
COPY tests/MonoBot.Tests/MonoBot.Tests.csproj                     tests/MonoBot.Tests/
RUN dotnet restore

# Copy all source
COPY src/ src/
COPY tests/ tests/

# Publish main app
RUN dotnet publish src/MonoBot/MonoBot.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish

# Publish each built-in plugin into the plugins/ subdirectory
RUN for proj in \
      src/Plugins/MonoBot.Plugin.Bender/MonoBot.Plugin.Bender.csproj \
      src/Plugins/MonoBot.Plugin.Announcements/MonoBot.Plugin.Announcements.csproj \
      src/Plugins/MonoBot.Plugin.Admin/MonoBot.Plugin.Admin.csproj \
      src/Plugins/MonoBot.Plugin.Core/MonoBot.Plugin.Core.csproj; do \
    dotnet publish "$proj" \
        --configuration Release \
        --no-restore \
        --output /app/publish/plugins; \
    done

# Stage 2 — Runtime
FROM mcr.microsoft.com/dotnet/runtime:10.0-alpine AS runtime
WORKDIR /app

# Run as non-root for least-privilege
RUN addgroup -S monobot && adduser -S monobot -G monobot
USER monobot

# Persistent SQLite database volume
VOLUME ["/data"]

COPY --from=build /app/publish .

# plugins/ is resolved relative to AppContext.BaseDirectory (/app) by default.
# Override with MONOBOT_PluginsPath if you mount external plugins at a different path.

ENTRYPOINT ["dotnet", "MonoBot.dll"]
