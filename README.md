# MonoBot

[![CI](https://github.com/kusuriya/MonoBot/actions/workflows/ci.yml/badge.svg)](https://github.com/kusuriya/MonoBot/actions/workflows/ci.yml)

A simple IRC bot written in C# on .NET 10, packaged as a Docker container.

## Commands

| Command | Description |
|---|---|
| `.bender` | Returns a random Bender quote from the database |
| `.announcements` | Shows the two most recent channel announcements |
| `.add-announcement <text>` | Adds an announcement for the current channel |
| `.version` | Displays the running MonoBot version and .NET runtime |
| `.help` | Lists available commands |

### Admin commands

Admin commands are restricted to configured admin nicks (see [Configuration](#configuration)).

| Command | Description |
|---|---|
| `!join #channel` | Join a channel |
| `!part [#channel]` | Leave a channel (defaults to current) |
| `!debug on\|off` | Toggle debug logging |
| `!quit` | Shut down the bot |

## Quick Start (Docker)

```bash
cp .env.example .env
# Edit .env — set your admin nick, IRC server, and (optionally) NickServ credentials
docker compose up -d
docker compose logs -f
```

The SQLite database is stored in a named Docker volume (`monobot-data`) and persists across restarts.

## Configuration

All settings can be overridden with environment variables prefixed `MONOBOT_`.
Use double-underscore `__` for nested keys (e.g. `MONOBOT_NickServ__Password`).

| Variable | Default | Description |
|---|---|---|
| `MONOBOT_Server` | `irc.libera.chat` | IRC server hostname |
| `MONOBOT_Port` | `6667` | IRC server port |
| `MONOBOT_Nick` | `monobot` | Bot nickname |
| `MONOBOT_Name` | `MonoBot` | Bot GECOS / real name |
| `MONOBOT_Channels__0` | `#monobot` | First channel to join (add `__1`, `__2`, ... for more) |
| `MONOBOT_Admins__0` | _(none)_ | First admin nick (add `__1`, `__2`, ... for more) |
| `MONOBOT_UseNickServ` | `false` | Enable NickServ authentication |
| `MONOBOT_NickServ__Username` | _(empty)_ | NickServ account name |
| `MONOBOT_NickServ__Password` | _(empty)_ | NickServ password — **set via env var only, never in a file** |
| `MONOBOT_Debug` | `false` | Enable debug-level logging |
| `MONOBOT_Database__Path` | `monobot.db` | Path to SQLite database file |

For Docker, non-secret config is in `docker-compose.yml`; secrets go in `.env` (gitignored).
Copy `.env.example` to `.env` and fill in your values.

## Local Development

```bash
cd src/MonoBot
# edit appsettings.json with your local IRC server settings
dotnet run
```

## Seeding Bender Quotes

The bot ships with an empty database. Add quotes with raw SQL:

```bash
# If running locally
sqlite3 monobot.db "INSERT INTO bender (sayings) VALUES ('Bite my shiny metal ass!');"

# If running in Docker
docker run --rm -v monobot-data:/data alpine \
  sh -c "apk add sqlite && sqlite3 /data/monobot.db \"INSERT INTO bender (sayings) VALUES ('I am Bender, please insert girder.');\""
```

## Building and Testing

```bash
# Build
dotnet build --configuration Release

# Run all tests
dotnet test --configuration Release --verbosity normal

# Build Docker image
docker build -t monobot .
```

## Security Notes

- NickServ passwords must be supplied via environment variable (`MONOBOT_NickServ__Password`), never in committed files.
- Admin authentication is based on IRC nick prefix matching. This relies on NickServ nick registration for effective identity assurance.
- All database queries use parameterized statements — SQL injection is not possible via IRC commands.

## License

MIT — see [LICENSE](LICENSE).

Original author: Jason Barbier
