-- =============================================================================
-- Tests for weather_pkg, written in plain PL/SQL.
--
-- Run (from the project root):
--   docker exec -i weather-oracle sqlplus -s weather_app/WeatherApp123@//localhost/FREEPDB1 < database/tests/test_weather_pkg.sql
--
-- Every test writes real rows, then the whole script ends with ROLLBACK, so your data
-- is left exactly as it was. Test locations use coordinates near the South Pole so they
-- can't clash with real searches.
--
-- (For bigger projects, look at utPLSQL, a full unit-testing framework for PL/SQL.
--  This script shows the same idea with nothing to install.)
-- =============================================================================
SET SERVEROUTPUT ON SIZE UNLIMITED FORMAT WRAPPED
SET FEEDBACK OFF
WHENEVER SQLERROR EXIT FAILURE ROLLBACK

DECLARE
  g_passed  PLS_INTEGER := 0;
  g_failed  PLS_INTEGER := 0;

  -- Record types matching each cursor's columns, so FETCH ... INTO has somewhere to go.
  TYPE latest_rec IS RECORD (
    temperature NUMBER, feels_like NUMBER, humidity NUMBER, wind_speed NUMBER,
    precipitation NUMBER, weather_code NUMBER, is_day NUMBER,
    observed_at TIMESTAMP, fetched_at TIMESTAMP, is_fresh NUMBER);

  TYPE recent_rec IS RECORD (
    location_id NUMBER, name VARCHAR2(200), country VARCHAR2(100), admin1 VARCHAR2(200),
    latitude NUMBER, longitude NUMBER, last_searched_at TIMESTAMP);

  TYPE stats_rec IS RECORD (
    name VARCHAR2(200), country VARCHAR2(100), search_count NUMBER, popularity_rank NUMBER,
    pct_of_searches NUMBER, avg_temperature NUMBER, last_searched_at TIMESTAMP);

  v_id      NUMBER;
  v_id2     NUMBER;
  v_cur     SYS_REFCURSOR;
  v_latest  latest_rec;
  v_recent  recent_rec;
  v_stats   stats_rec;
  v_count   PLS_INTEGER;

  -- ----- tiny assertion helpers (nested procedures can see the variables above) -----

  PROCEDURE check_that (p_name IN VARCHAR2, p_condition IN BOOLEAN) IS
  BEGIN
    -- A NULL condition counts as a failure: "IF NULL THEN" never runs the THEN branch.
    IF p_condition THEN
      g_passed := g_passed + 1;
      DBMS_OUTPUT.PUT_LINE('  PASS  ' || p_name);
    ELSE
      g_failed := g_failed + 1;
      DBMS_OUTPUT.PUT_LINE('  FAIL  ' || p_name);
    END IF;
  END check_that;

  -- Runs a PL/SQL snippet with dynamic SQL and checks that it raises ORA-20001.
  PROCEDURE expect_invalid_input (p_name IN VARCHAR2, p_plsql IN VARCHAR2) IS
  BEGIN
    EXECUTE IMMEDIATE p_plsql;
    check_that(p_name || ' (no error was raised)', FALSE);
  EXCEPTION
    WHEN OTHERS THEN
      check_that(p_name, SQLCODE = weather_pkg.c_invalid_input);
  END expect_invalid_input;

  FUNCTION new_location (p_name IN VARCHAR2, p_lat IN NUMBER) RETURN NUMBER IS
  BEGIN
    RETURN weather_pkg.upsert_location(p_name, 'Antarctica', NULL, p_lat, 0);
  END new_location;

BEGIN
  DBMS_OUTPUT.PUT_LINE('weather_pkg tests');

  -- ---------------------------------------------------------------- upsert_location
  DBMS_OUTPUT.PUT_LINE('upsert_location');

  v_id  := weather_pkg.upsert_location('Test A', 'Antarctica', NULL, -89.12341, 0);
  v_id2 := weather_pkg.upsert_location('Test A', 'Antarctica', NULL, -89.12344, 0);
  check_that('coordinates that round to the same 4 decimals reuse one row', v_id = v_id2);

  v_id2 := weather_pkg.upsert_location('Renamed A', 'Antarctica', 'Region', -89.1234, 0);
  SELECT COUNT(*) INTO v_count FROM locations WHERE location_id = v_id AND name = 'Renamed A' AND admin1 = 'Region';
  check_that('an existing location is updated, not duplicated', v_id = v_id2 AND v_count = 1);

  v_id2 := weather_pkg.upsert_location('  Padded  ', 'Antarctica', NULL, -89.5, 0);
  SELECT name INTO v_recent.name FROM locations WHERE location_id = v_id2;
  check_that('the name is trimmed', v_recent.name = 'Padded');

  expect_invalid_input('a NULL name is rejected',
    'DECLARE x NUMBER; BEGIN x := weather_pkg.upsert_location(NULL, ''X'', NULL, 0, 0); END;');
  expect_invalid_input('a blank name is rejected',
    'DECLARE x NUMBER; BEGIN x := weather_pkg.upsert_location(''   '', ''X'', NULL, 0, 0); END;');
  expect_invalid_input('latitude 91 is rejected',
    'DECLARE x NUMBER; BEGIN x := weather_pkg.upsert_location(''X'', ''X'', NULL, 91, 0); END;');
  expect_invalid_input('longitude -181 is rejected',
    'DECLARE x NUMBER; BEGIN x := weather_pkg.upsert_location(''X'', ''X'', NULL, 0, -181); END;');

  -- ---------------------------------------------------------------- get_latest_weather / save_weather
  DBMS_OUTPUT.PUT_LINE('get_latest_weather / save_weather');

  v_id := new_location('Test B', -89.2);
  weather_pkg.get_latest_weather(v_id, 10, v_cur);
  FETCH v_cur INTO v_latest;
  check_that('a location with no readings returns no rows', v_cur%NOTFOUND);
  CLOSE v_cur;

  weather_pkg.save_weather(v_id, -42.5, -50.1, 70, 25.0, 0.3, 71, 1, SYS_EXTRACT_UTC(SYSTIMESTAMP));
  weather_pkg.get_latest_weather(v_id, 10, v_cur);
  FETCH v_cur INTO v_latest;
  CLOSE v_cur;
  check_that('a saved reading comes back', v_latest.temperature = -42.5 AND v_latest.weather_code = 71);
  check_that('a just-saved reading is fresh', v_latest.is_fresh = 1);

  -- An older reading (inserted directly, so we can set fetched_at) must not win.
  v_id := new_location('Test C', -89.3);
  INSERT INTO weather_readings (location_id, temperature, feels_like, humidity, wind_speed,
                                precipitation, weather_code, is_day, observed_at, fetched_at)
  VALUES (v_id, 1, 1, 50, 5, 0, 3, 1, SYS_EXTRACT_UTC(SYSTIMESTAMP),
          SYS_EXTRACT_UTC(SYSTIMESTAMP) - INTERVAL '30' MINUTE);
  weather_pkg.get_latest_weather(v_id, 10, v_cur);
  FETCH v_cur INTO v_latest;
  CLOSE v_cur;
  check_that('a 30-minute-old reading is stale when the max age is 10', v_latest.is_fresh = 0);

  weather_pkg.get_latest_weather(v_id, 60, v_cur);
  FETCH v_cur INTO v_latest;
  CLOSE v_cur;
  check_that('the same reading is fresh when the max age is 60', v_latest.is_fresh = 1);

  weather_pkg.save_weather(v_id, 2, 2, 50, 5, 0, 3, 1, SYS_EXTRACT_UTC(SYSTIMESTAMP));
  weather_pkg.get_latest_weather(v_id, 10, v_cur);
  FETCH v_cur INTO v_latest;
  CLOSE v_cur;
  check_that('the newest of several readings is returned', v_latest.temperature = 2);

  expect_invalid_input('a max age of 0 is rejected',
    'DECLARE c SYS_REFCURSOR; BEGIN weather_pkg.get_latest_weather(1, 0, c); END;');

  -- ---------------------------------------------------------------- search history
  DBMS_OUTPUT.PUT_LINE('log_search / get_recent_searches');

  v_id  := new_location('Test D', -89.4);
  v_id2 := new_location('Test E', -89.6);
  weather_pkg.log_search(v_id);
  weather_pkg.log_search(v_id);
  -- Insert directly with a future time, so E is definitely the most recent search.
  INSERT INTO search_history (location_id, searched_at)
  VALUES (v_id2, SYS_EXTRACT_UTC(SYSTIMESTAMP) + INTERVAL '1' HOUR);

  weather_pkg.get_recent_searches(50, v_cur);
  FETCH v_cur INTO v_recent;
  check_that('the most recent search comes first', v_recent.location_id = v_id2);
  v_count := CASE WHEN v_recent.location_id = v_id THEN 1 ELSE 0 END;
  LOOP
    FETCH v_cur INTO v_recent;
    EXIT WHEN v_cur%NOTFOUND;
    IF v_recent.location_id = v_id THEN v_count := v_count + 1; END IF;
  END LOOP;
  CLOSE v_cur;
  check_that('a place searched twice appears once', v_count = 1);

  weather_pkg.get_recent_searches(1, v_cur);
  v_count := 0;
  LOOP
    FETCH v_cur INTO v_recent;
    EXIT WHEN v_cur%NOTFOUND;
    v_count := v_count + 1;
  END LOOP;
  CLOSE v_cur;
  check_that('the limit is respected', v_count = 1);

  expect_invalid_input('limit 0 is rejected',
    'DECLARE c SYS_REFCURSOR; BEGIN weather_pkg.get_recent_searches(0, c); END;');
  expect_invalid_input('limit 51 is rejected',
    'DECLARE c SYS_REFCURSOR; BEGIN weather_pkg.get_recent_searches(51, c); END;');

  -- ---------------------------------------------------------------- stats
  DBMS_OUTPUT.PUT_LINE('get_search_stats');

  -- Give one test place far more searches than any real place, so it must rank first.
  v_id := new_location('Test Popular', -89.7);
  FOR i IN 1 .. 500 LOOP
    weather_pkg.log_search(v_id);
  END LOOP;
  weather_pkg.save_weather(v_id, 10, 10, 50, 5, 0, 0, 1, SYS_EXTRACT_UTC(SYSTIMESTAMP));
  weather_pkg.save_weather(v_id, 20, 20, 50, 5, 0, 0, 1, SYS_EXTRACT_UTC(SYSTIMESTAMP));

  weather_pkg.get_search_stats(5, v_cur);
  FETCH v_cur INTO v_stats;
  CLOSE v_cur;
  check_that('the most-searched place ranks 1', v_stats.name = 'Test Popular' AND v_stats.popularity_rank = 1);
  check_that('its search count is 500', v_stats.search_count = 500);
  check_that('its average temperature is 15', v_stats.avg_temperature = 15);
  check_that('its share is a percentage between 0 and 100', v_stats.pct_of_searches > 0 AND v_stats.pct_of_searches <= 100);

  -- ---------------------------------------------------------------- summary
  ROLLBACK;   -- undo every row the tests created

  DBMS_OUTPUT.PUT_LINE('----------------------------------------');
  DBMS_OUTPUT.PUT_LINE(g_passed || ' passed, ' || g_failed || ' failed');
  IF g_failed > 0 THEN
    -- Makes sqlplus exit with an error code, so scripts and CI notice the failure.
    RAISE_APPLICATION_ERROR(-20998, g_failed || ' test(s) failed');
  END IF;
END;
/

EXIT
