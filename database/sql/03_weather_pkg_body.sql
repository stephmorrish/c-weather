-- =============================================================================
-- Package BODY: the implementation of everything declared in the spec.
--
-- Transactions: these procedures do NOT commit. The caller decides when work is
-- committed. ODP.NET auto-commits each call when no transaction is open, which
-- suits our API. In SQL*Plus or SQL Developer, run COMMIT yourself.
-- =============================================================================
WHENEVER SQLERROR EXIT FAILURE ROLLBACK

CREATE OR REPLACE PACKAGE BODY weather_pkg AS

  -- Private helper: not in the spec, so only code inside this package can call it.
  PROCEDURE assert_input (p_condition IN BOOLEAN, p_message IN VARCHAR2) IS
  BEGIN
    -- SQL has three-valued logic: a comparison with NULL is neither TRUE nor FALSE but
    -- NULL, and "NOT NULL" is still NULL. Checking "IS NULL" first stops a NULL
    -- argument from slipping past the check.
    IF p_condition IS NULL OR NOT p_condition THEN
      RAISE_APPLICATION_ERROR(c_invalid_input, p_message);
    END IF;
  END assert_input;

  PROCEDURE assert_limit (p_limit IN PLS_INTEGER) IS
  BEGIN
    assert_input(p_limit BETWEEN 1 AND 50, 'Limit must be between 1 and 50.');
  END assert_limit;

  ---------------------------------------------------------------------------
  FUNCTION upsert_location (
    p_name       IN locations.name%TYPE,
    p_country    IN locations.country%TYPE,
    p_admin1     IN locations.admin1%TYPE,
    p_latitude   IN NUMBER,
    p_longitude  IN NUMBER
  ) RETURN locations.location_id%TYPE
  IS
    -- Round to the column's 4 decimals BEFORE comparing. Otherwise 43.70643 would never
    -- equal the stored 43.7064, MERGE would try to insert a duplicate, and the unique
    -- constraint would reject it.
    v_lat  NUMBER := ROUND(p_latitude, 4);
    v_lon  NUMBER := ROUND(p_longitude, 4);
    v_id   locations.location_id%TYPE;
  BEGIN
    assert_input(TRIM(p_name) IS NOT NULL, 'Location name is required.');
    assert_input(v_lat BETWEEN -90 AND 90, 'Latitude must be between -90 and 90.');
    assert_input(v_lon BETWEEN -180 AND 180, 'Longitude must be between -180 and 180.');

    -- A nested block with its own exception handler. If two requests insert the same new
    -- location at the same moment, one MERGE loses the race with DUP_VAL_ON_INDEX. That's
    -- fine, because the row now exists and the SELECT below finds it.
    BEGIN
      MERGE INTO locations l
      USING (SELECT v_lat AS latitude, v_lon AS longitude FROM dual) src
         ON (l.latitude = src.latitude AND l.longitude = src.longitude)
      WHEN MATCHED THEN
        UPDATE SET l.name = TRIM(p_name), l.country = p_country, l.admin1 = p_admin1
      WHEN NOT MATCHED THEN
        INSERT (name, country, admin1, latitude, longitude)
        VALUES (TRIM(p_name), p_country, p_admin1, src.latitude, src.longitude);
    EXCEPTION
      WHEN DUP_VAL_ON_INDEX THEN
        NULL;
    END;

    SELECT location_id
      INTO v_id
      FROM locations
     WHERE latitude = v_lat
       AND longitude = v_lon;

    RETURN v_id;
  END upsert_location;

  ---------------------------------------------------------------------------
  PROCEDURE get_latest_weather (
    p_location_id      IN  locations.location_id%TYPE,
    p_max_age_minutes  IN  PLS_INTEGER,
    p_result           OUT SYS_REFCURSOR
  ) IS
  BEGIN
    assert_input(p_max_age_minutes > 0, 'Max age must be a positive number of minutes.');

    -- OPEN ... FOR hands the caller a cursor (a pointer to a result set) instead of
    -- copying rows. C# reads it like an ordinary query result.
    OPEN p_result FOR
      SELECT temperature, feels_like, humidity, wind_speed, precipitation,
             weather_code, is_day, observed_at, fetched_at,
             CASE
               WHEN fetched_at >= SYS_EXTRACT_UTC(SYSTIMESTAMP)
                                  - NUMTODSINTERVAL(p_max_age_minutes, 'MINUTE')
               THEN 1 ELSE 0
             END AS is_fresh
        FROM weather_readings
       WHERE location_id = p_location_id
       ORDER BY fetched_at DESC
       FETCH FIRST 1 ROW ONLY;          -- uses weather_readings_loc_time_ix
  END get_latest_weather;

  ---------------------------------------------------------------------------
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
  ) IS
  BEGIN
    INSERT INTO weather_readings
      (location_id, temperature, feels_like, humidity, wind_speed,
       precipitation, weather_code, is_day, observed_at)
    VALUES
      (p_location_id, p_temperature, p_feels_like, p_humidity, p_wind_speed,
       p_precipitation, p_weather_code, p_is_day, p_observed_at);
  END save_weather;

  ---------------------------------------------------------------------------
  PROCEDURE log_search (p_location_id IN locations.location_id%TYPE) IS
  BEGIN
    INSERT INTO search_history (location_id) VALUES (p_location_id);
  END log_search;

  ---------------------------------------------------------------------------
  PROCEDURE get_recent_searches (
    p_limit   IN  PLS_INTEGER,
    p_result  OUT SYS_REFCURSOR
  ) IS
  BEGIN
    assert_limit(p_limit);

    -- GROUP BY collapses repeat searches of one place into a single row.
    -- MAX(searched_at) keeps the most recent time it was searched.
    OPEN p_result FOR
      SELECT l.location_id, l.name, l.country, l.admin1, l.latitude, l.longitude,
             MAX(s.searched_at) AS last_searched_at
        FROM search_history s
        JOIN locations l ON l.location_id = s.location_id
       GROUP BY l.location_id, l.name, l.country, l.admin1, l.latitude, l.longitude
       ORDER BY last_searched_at DESC
       FETCH FIRST p_limit ROWS ONLY;
  END get_recent_searches;

  ---------------------------------------------------------------------------
  PROCEDURE get_search_stats (
    p_limit   IN  PLS_INTEGER,
    p_result  OUT SYS_REFCURSOR
  ) IS
  BEGIN
    assert_limit(p_limit);

    -- WITH (a "common table expression") names sub-queries so the main query reads
    -- top to bottom.
    -- Analytic functions (... OVER ()) compute across ALL rows without collapsing them:
    --   RANK()           1, 2, 2, 4 ... ties share a rank
    --   RATIO_TO_REPORT  this row's value / total of all rows
    -- They run before FETCH FIRST, so percentages are of all searches, not just the top N.
    OPEN p_result FOR
      WITH search_counts AS (
        SELECT location_id,
               COUNT(*)         AS search_count,
               MAX(searched_at) AS last_searched_at
          FROM search_history
         GROUP BY location_id
      ),
      avg_temps AS (
        SELECT location_id, ROUND(AVG(temperature), 1) AS avg_temperature
          FROM weather_readings
         GROUP BY location_id
      )
      SELECT l.name, l.country,
             sc.search_count,
             RANK() OVER (ORDER BY sc.search_count DESC)                  AS popularity_rank,
             ROUND(100 * RATIO_TO_REPORT(sc.search_count) OVER (), 1)     AS pct_of_searches,
             t.avg_temperature,                                           -- NULL if never fetched
             sc.last_searched_at
        FROM search_counts sc
        JOIN locations l      ON l.location_id = sc.location_id
        LEFT JOIN avg_temps t ON t.location_id = sc.location_id           -- LEFT keeps rows without temps
       ORDER BY sc.search_count DESC, sc.last_searched_at DESC
       FETCH FIRST p_limit ROWS ONLY;
  END get_search_stats;

END weather_pkg;
/

-- CREATE PACKAGE BODY "succeeds" even with compile errors (it just marks the package
-- INVALID), so check explicitly and fail the script if anything is wrong.
SHOW ERRORS PACKAGE BODY weather_pkg

DECLARE
  v_errors PLS_INTEGER;
BEGIN
  SELECT COUNT(*) INTO v_errors FROM user_errors WHERE name = 'WEATHER_PKG';
  IF v_errors > 0 THEN
    RAISE_APPLICATION_ERROR(-20999, 'weather_pkg has compilation errors - see above.');
  END IF;
END;
/

EXIT
