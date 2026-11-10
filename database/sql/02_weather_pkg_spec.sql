-- =============================================================================
-- Package SPEC: the public "interface" of weather_pkg. Anything listed here can be
-- called from outside (by our C# code). The BODY (next file) holds the implementation.
-- Splitting them means the body can change without breaking callers.
-- =============================================================================
WHENEVER SQLERROR EXIT FAILURE ROLLBACK

CREATE OR REPLACE PACKAGE weather_pkg AS

  -- Raised (as ORA-20001) when a caller passes bad input. The C# API turns it into HTTP 400.
  -- RAISE_APPLICATION_ERROR accepts codes from -20000 to -20999 for our own errors.
  c_invalid_input CONSTANT PLS_INTEGER := -20001;

  -- Insert the location, or update its name if those coordinates already exist.
  -- Returns its location_id. "%TYPE" means "the same type as that column".
  FUNCTION upsert_location (
    p_name       IN locations.name%TYPE,
    p_country    IN locations.country%TYPE,
    p_admin1     IN locations.admin1%TYPE,
    p_latitude   IN NUMBER,
    p_longitude  IN NUMBER
  ) RETURN locations.location_id%TYPE;

  -- Newest reading for a location (0 or 1 rows), with is_fresh = 1 when it was fetched
  -- within p_max_age_minutes. Stale rows are still returned: the API serves them when
  -- Open-Meteo is down.
  PROCEDURE get_latest_weather (
    p_location_id      IN  locations.location_id%TYPE,
    p_max_age_minutes  IN  PLS_INTEGER,
    p_result           OUT SYS_REFCURSOR
  );

  PROCEDURE save_weather (
    p_location_id    IN locations.location_id%TYPE,
    p_temperature    IN weather_readings.temperature%TYPE,
    p_feels_like     IN weather_readings.feels_like%TYPE,
    p_humidity       IN weather_readings.humidity%TYPE,
    p_wind_speed     IN weather_readings.wind_speed%TYPE,
    p_precipitation  IN weather_readings.precipitation%TYPE,
    p_weather_code   IN weather_readings.weather_code%TYPE,
    p_is_day         IN weather_readings.is_day%TYPE,
    p_observed_at    IN weather_readings.observed_at%TYPE
  );

  PROCEDURE log_search (
    p_location_id IN locations.location_id%TYPE
  );

  -- Distinct locations, most recently searched first.
  PROCEDURE get_recent_searches (
    p_limit   IN  PLS_INTEGER,
    p_result  OUT SYS_REFCURSOR
  );

  -- Most-searched locations with rank, share of all searches and average temperature.
  PROCEDURE get_search_stats (
    p_limit   IN  PLS_INTEGER,
    p_result  OUT SYS_REFCURSOR
  );

END weather_pkg;
/

EXIT
