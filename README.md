# Weather App

A full-stack weather app with a **C# (ASP.NET Core) API**, an **Oracle database with PL/SQL**, and a **React** frontend. Search for any city to see its current weather, with an animated sky that matches the conditions.

> **About this project**
>
> This is an **AI-generated learning project**. It was built with [Claude Code](https://claude.com/claude-code) as a hands-on way to learn and practise **C#** and **PL/SQL**, and to strengthen React, JavaScript and SQL skills. The code is heavily commented to explain the concepts it uses.
>
> It is meant for study and experimenting, not as production software. Read it, change it, break it and fix it.

![Stack: React → C# API → Oracle](https://img.shields.io/badge/stack-React%20%E2%86%92%20C%23%20API%20%E2%86%92%20Oracle%20PL%2FSQL-2f6fde)

---

## Features

- **Location search** with autocomplete, as you type (keyboard friendly: ↑ ↓ Enter Esc).
- **Current conditions**: temperature, feels-like, humidity, wind, precipitation.
- **Animated weather**: sun, moon and stars, clouds, fog, drizzle, rain, snow and thunderstorms, each with day and night versions.
- **Refresh**:
  - a manual Refresh button
  - auto-refresh every 1, 5, 10 or 15 minutes, with a live countdown
- **Caching in Oracle**: readings are reused for 10 minutes, which saves calls to the weather service.
- **Error handling**:
  - If the weather service is down, the app shows the last saved reading, marked "Offline data".
  - If the database is down, weather still loads, just without caching or history.
  - Clear error messages, with a Retry button.
- **Recent searches**, stored in Oracle.
- Remembers your last city and refresh settings, and supports light and dark mode on desktop and mobile.

Weather data comes from [Open-Meteo](https://open-meteo.com/), which is free and needs no API key.

---

## How it works

```
React app (Vite)  ──/api──▶  C# Web API (ASP.NET Core, .NET 10)  ──▶  Open-Meteo
 localhost:5173               localhost:5025                    │
                                                                └──▶  Oracle Database Free (Docker)
                                                                      localhost:1521, PL/SQL package weather_pkg
```

1. The browser only ever talks to the C# API. The Vite dev server forwards `/api` requests to it.
2. The API asks Oracle, through the PL/SQL package `weather_pkg`, whether it has a fresh reading for the location.
3. If it doesn't, the API calls Open-Meteo, saves the result in Oracle, and returns it.

---

## Installation

### Prerequisites

| Tool | Version | Check with |
|---|---|---|
| [.NET SDK](https://dotnet.microsoft.com/download) | 10.0 | `dotnet --version` |
| [Docker](https://docs.docker.com/engine/install/) with Compose v2 | any recent | `docker compose version` |
| [Node.js](https://nodejs.org/) | 22 or newer | `node --version` |
| Git | any | `git --version` |

You also need about 3 GB of free disk space for the Oracle image, and 2 GB or more of free RAM while Oracle is running.

<details>
<summary>Installing the prerequisites on Ubuntu / Linux Mint</summary>

```bash
# .NET 10 SDK (installs to ~/.dotnet, no sudo needed)
curl -sSL https://dot.net/v1/dotnet-install.sh -o dotnet-install.sh
bash dotnet-install.sh --channel 10.0
echo 'export DOTNET_ROOT="$HOME/.dotnet"' >> ~/.bashrc
echo 'export PATH="$HOME/.dotnet:$HOME/.dotnet/tools:$PATH"' >> ~/.bashrc
source ~/.bashrc

# Docker
sudo apt update
sudo apt install -y docker.io docker-compose-v2
sudo usermod -aG docker $USER    # then log out and back in

# Node.js: use nvm (https://github.com/nvm-sh/nvm), then:
nvm install --lts
```
</details>

### Setup

**1. Get the code**
```bash
git clone <your-repo-url> c-weather
cd c-weather
```

**2. Start the Oracle database**
```bash
docker compose up -d
docker compose logs -f oracle
```
The first start downloads the image (about 2 GB) and creates the database, which takes a few minutes. Wait until the log shows `DATABASE IS READY TO USE!`, then press `Ctrl+C` to stop following the log; the database keeps running.

On that first start, the scripts in [`database/sql/`](database/sql/) run automatically. They create the tables and the `weather_pkg` package.

**3. Install the frontend packages**
```bash
cd frontend
npm install
cd ..
```

The C# packages are restored automatically the first time you run the API.

---

## Running the app

Use three terminals, one for each part:

```bash
# Terminal 1: database (skip if it's already running; check with `docker ps`)
docker compose up -d

# Terminal 2: C# API, on http://localhost:5025
cd backend/WeatherApp.Api
dotnet run

# Terminal 3: React app, on http://localhost:5173
cd frontend
npm run dev
```

Open **http://localhost:5173** in your browser.

To stop the app:
- Press `Ctrl+C` in terminals 2 and 3.
- Run `docker compose down` to stop Oracle. Your data is kept.

> After a computer restart, Oracle doesn't start on its own. Run `docker compose up -d` again.

---

## Using the app

1. **Search:** type at least 2 letters of a city name in the search box, then pick a result with the mouse, or with the arrow keys and Enter.
2. **Read the weather:** the card shows current conditions over an animated sky. A **Cached** badge means the reading came from the database (it's under 10 minutes old). **Offline data** means the weather service couldn't be reached, so the last saved reading is shown.
3. **Refresh:**
   - Click **Refresh** to fetch again.
   - Leave **Auto-refresh** on to update automatically. Choose how often in the drop-down.
   - The line on the left shows when the data was last updated and when the next refresh is due.
4. **Recent searches:** click any place in the side panel to view it again.
5. **Errors:** if something goes wrong, a red banner explains what happened. Click **Retry** to try again.

The app remembers your last city and refresh settings in the browser.

---

## Exploring the API and database

### API endpoints

| Method | Route | Description |
|---|---|---|
| GET | `/api/locations/search?q=Toronto` | Find matching places (up to 5) |
| GET | `/api/weather?lat=43.70&lon=-79.40&name=Toronto&country=Canada&logSearch=true` | Current weather. `logSearch=true` adds it to search history |
| GET | `/api/history?limit=10` | Recent searches |
| GET | `/api/stats?limit=10` | Most-searched places, with rank and share of searches |
| GET | `/api/health` | API status and database status |

Errors use the standard [ProblemDetails](https://www.rfc-editor.org/rfc/rfc9457) JSON format:
- `400` for invalid input
- `503` when the weather service or the database is unavailable

Ready-made requests are in [`backend/WeatherApp.Api/WeatherApp.Api.http`](backend/WeatherApp.Api/WeatherApp.Api.http). In VS Code, install the **REST Client** extension, then click **Send Request** above any request.

### Connecting to Oracle

```bash
docker exec -it weather-oracle sqlplus weather_app/WeatherApp123@//localhost/FREEPDB1
```

Some queries to try:
```sql
SELECT * FROM locations;
SELECT * FROM weather_readings ORDER BY fetched_at DESC;
SELECT object_name, object_type, status FROM user_objects;
```

You can also connect from a GUI tool such as SQL Developer or DBeaver:

| Setting | Value |
|---|---|
| Host | `localhost` |
| Port | `1521` |
| Service name | `FREEPDB1` |
| User | `weather_app` |
| Password | `WeatherApp123` |

After editing a SQL file, run the scripts again:
```bash
docker exec weather-oracle bash /container-entrypoint-initdb.d/01_create_schema.sh
```

To delete everything and start the database fresh:
```bash
docker compose down -v
docker compose up -d
```

> The passwords in `docker-compose.yml` and `appsettings.Development.json` are **local development defaults only**. Never reuse them for anything real.

---

## Project structure

```
c-weather/
├── docker-compose.yml          Oracle Database Free container
├── database/
│   ├── init/                   Startup script (runs the SQL files as WEATHER_APP)
│   └── sql/                    Tables, PL/SQL package spec and body
├── backend/WeatherApp.Api/     C# ASP.NET Core Web API
│   ├── Program.cs              Dependency injection and middleware setup
│   ├── Endpoints/              API routes
│   ├── Services/               Open-Meteo client, weather logic, code mapper
│   ├── Data/                   Oracle repository (calls weather_pkg)
│   ├── Errors/                 Turns exceptions into HTTP responses
│   └── Models/                 Request and response records
└── frontend/                   React app (Vite, JavaScript)
    └── src/
        ├── api/                fetch wrapper and error handling
        ├── hooks/              useWeather, useSearchHistory, useDebounce, useNow
        ├── components/         SearchBar, WeatherCard, WeatherAnimation, ...
        └── utils/              Formatting helpers
```

---

## Learning guide

The project was built in phases. Each one is a separate git commit (`git log --oneline`), so you can view it on its own with `git show <commit>`.

| Phase | Focus | Concepts to look for |
|---|---|---|
| 1. C# API | [`Services/`](backend/WeatherApp.Api/Services/), [`Program.cs`](backend/WeatherApp.Api/Program.cs) | records, nullable types, `async`/`await`, dependency injection, typed `HttpClient`, retries and timeouts, switch expressions, ProblemDetails |
| 2. Oracle + PL/SQL | [`database/sql/`](database/sql/), [`Data/OracleWeatherRepository.cs`](backend/WeatherApp.Api/Data/OracleWeatherRepository.cs) | package spec vs body, `MERGE`, `SYS_REFCURSOR`, `RAISE_APPLICATION_ERROR`, `%TYPE`, NULL logic, analytic functions (`RANK`, `RATIO_TO_REPORT`), bind variables, ODP.NET `BindByName` |
| 3. React UI | [`frontend/src/`](frontend/src/) | components and props, custom hooks, effects with cleanup, `AbortController`, derived state, debouncing, accessible combobox |
| 4. Animations + refresh | [`WeatherAnimation.*`](frontend/src/components/), [`useWeather.js`](frontend/src/hooks/useWeather.js) | CSS `@keyframes`, custom properties, container query units, pure rendering, timers, `visibilitychange` |

### Practice exercises

1. **PL/SQL:** add `purge_old_readings(p_keep_days)` to `weather_pkg`. It should delete old rows from `weather_readings` and return how many it deleted (`SQL%ROWCOUNT`).
2. **React:** show `/api/stats` in a "Popular places" panel, using a hook modelled on `useSearchHistory`.
3. **C#:** add a daily forecast. Request `daily=temperature_2m_max,temperature_2m_min` from Open-Meteo and add the matching DTOs and models.
4. **CSS:** add a "windy" effect, with more slanted rain and faster clouds, when `windSpeed` is over 30 km/h.
5. **SQL:** write a query for the warmest and coldest reading for each location, using `MAX`/`MIN` with `KEEP (DENSE_RANK FIRST ...)`.

---

## Troubleshooting

| Problem | Fix |
|---|---|
| Red banner: "Can't reach the weather server" | The C# API isn't running. Start it with `dotnet run` in `backend/WeatherApp.Api`. |
| Recent searches says "History is unavailable" | Oracle isn't running. Run `docker compose up -d`, then wait until `docker ps` shows it as `healthy`. |
| `dotnet: command not found` | Add `~/.dotnet` to your `PATH` (see Prerequisites). |
| `permission denied` from Docker | Run `sudo usermod -aG docker $USER`, then log out and back in. |
| Port 1521, 5025 or 5173 already in use | Stop whatever is using it, or change the port in `docker-compose.yml`, `launchSettings.json` or `vite.config.js`. |
| The SQL scripts didn't run | They only run when the database is first created. Re-run them by hand (see [Connecting to Oracle](#connecting-to-oracle)), or reset with `docker compose down -v`. |

---

## Credits

- Weather and geocoding data: [Open-Meteo](https://open-meteo.com/) (CC BY 4.0)
- Database: [Oracle Database Free](https://www.oracle.com/database/free/), via the [gvenzl/oracle-free](https://hub.docker.com/r/gvenzl/oracle-free) image
- Code generated with [Claude Code](https://claude.com/claude-code) for learning purposes
