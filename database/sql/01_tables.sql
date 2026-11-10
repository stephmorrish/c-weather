-- =============================================================================
-- Tables for the Weather App.
-- All timestamps are stored in UTC (plain TIMESTAMP, no time zone). Storing UTC and
-- converting for display is the simplest way to avoid daylight-saving bugs.
-- "IF NOT EXISTS" is new in Oracle 23ai and makes this script safe to re-run.
-- =============================================================================
WHENEVER SQLERROR EXIT FAILURE ROLLBACK
SET FEEDBACK ON

-- One row per place the user has looked at.
CREATE TABLE IF NOT EXISTS locations (
  location_id  NUMBER GENERATED ALWAYS AS IDENTITY,      -- auto-incrementing key
  name         VARCHAR2(200) NOT NULL,
  country      VARCHAR2(100),
  admin1       VARCHAR2(200),                            -- state / province
  latitude     NUMBER(8,4) NOT NULL,                     -- 4 decimals ≈ 11 m precision
  longitude    NUMBER(8,4) NOT NULL,
  created_at   TIMESTAMP DEFAULT SYS_EXTRACT_UTC(SYSTIMESTAMP) NOT NULL,
  CONSTRAINT locations_pk        PRIMARY KEY (location_id),
  CONSTRAINT locations_coords_uk UNIQUE (latitude, longitude),
  CONSTRAINT locations_lat_ck    CHECK (latitude  BETWEEN -90  AND 90),
  CONSTRAINT locations_lon_ck    CHECK (longitude BETWEEN -180 AND 180)
);

-- Every reading fetched from Open-Meteo. The newest row per location is the cache;
-- older rows are kept so you can query temperature history.
-- Condition/description are NOT stored: they are derived from weather_code in C#
-- (storing derived data invites the two copies to disagree).
CREATE TABLE IF NOT EXISTS weather_readings (
  reading_id     NUMBER GENERATED ALWAYS AS IDENTITY,
  location_id    NUMBER NOT NULL,
  temperature    NUMBER(5,1) NOT NULL,                   -- °C
  feels_like     NUMBER(5,1) NOT NULL,                   -- °C
  humidity       NUMBER(3)   NOT NULL,                   -- %
  wind_speed     NUMBER(5,1) NOT NULL,                   -- km/h
  precipitation  NUMBER(6,2) NOT NULL,                   -- mm
  weather_code   NUMBER(3)   NOT NULL,                   -- WMO code
  is_day         NUMBER(1)   NOT NULL,                   -- 1 = day, 0 = night
  observed_at    TIMESTAMP   NOT NULL,                   -- when Open-Meteo measured it
  fetched_at     TIMESTAMP DEFAULT SYS_EXTRACT_UTC(SYSTIMESTAMP) NOT NULL,  -- when we fetched it
  CONSTRAINT weather_readings_pk      PRIMARY KEY (reading_id),
  CONSTRAINT weather_readings_loc_fk  FOREIGN KEY (location_id) REFERENCES locations (location_id)
                                      ON DELETE CASCADE,
  CONSTRAINT weather_readings_day_ck  CHECK (is_day IN (0, 1)),
  CONSTRAINT weather_readings_hum_ck  CHECK (humidity BETWEEN 0 AND 100)
);

-- "Newest reading for location X" is our hottest query; this index answers it directly.
CREATE INDEX IF NOT EXISTS weather_readings_loc_time_ix
  ON weather_readings (location_id, fetched_at DESC);

-- One row each time the user picks a location in the search box.
CREATE TABLE IF NOT EXISTS search_history (
  search_id    NUMBER GENERATED ALWAYS AS IDENTITY,
  location_id  NUMBER NOT NULL,
  searched_at  TIMESTAMP DEFAULT SYS_EXTRACT_UTC(SYSTIMESTAMP) NOT NULL,
  CONSTRAINT search_history_pk     PRIMARY KEY (search_id),
  CONSTRAINT search_history_loc_fk FOREIGN KEY (location_id) REFERENCES locations (location_id)
                                   ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS search_history_loc_ix  ON search_history (location_id);
CREATE INDEX IF NOT EXISTS search_history_time_ix ON search_history (searched_at DESC);

EXIT
