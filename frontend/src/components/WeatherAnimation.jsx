import './WeatherAnimation.css'

// How many falling particles each condition gets.
const DROPS = { drizzle: 16, rain: 45, thunderstorm: 55 }
const FLAKES = { snow: 32 }
const CLOUDS = { 'partly-cloudy': 2, cloudy: 4, drizzle: 3, rain: 3, snow: 3, thunderstorm: 3 }
const STAR_COUNT = 22

// Deterministic "random" number in [0, 1). React components must be pure (same props,
// same output). With Math.random(), every re-render would move the raindrops, so each
// particle's position comes from its index instead.
function seeded(index, salt) {
  const x = Math.sin(index * 12.9898 + salt * 78.233) * 43758.5453
  return x - Math.floor(x)
}

// Array.from({ length: n }, fn) builds [fn(0), fn(1), ... fn(n-1)].
const range = (n) => Array.from({ length: n }, (_, i) => i)

// Each particle passes its numbers to CSS through custom properties (--x, --delay, ...).
// React supports them directly in the style prop. CSS does the animating, so React
// renders the scene once and never re-renders it per frame.
//
// A NEGATIVE animation-delay starts the animation part-way through, so rain is already
// falling when the card appears instead of every drop starting at the top together.
export default function WeatherAnimation({ condition, isDay }) {
  const isClearish = condition === 'clear' || condition === 'partly-cloudy'

  return (
    <div className={`scene scene--${condition} scene--${isDay ? 'day' : 'night'}`} aria-hidden="true">
      {!isDay && isClearish &&
        range(STAR_COUNT).map((i) => (
          <span
            key={`star-${i}`}
            className="scene__star"
            style={{
              '--x': `${seeded(i, 1) * 100}%`,
              '--y': `${seeded(i, 2) * 70}%`,
              '--delay': `${-seeded(i, 3) * 4}s`,
            }}
          />
        ))}

      {isClearish && (isDay ? <div className="scene__sun" /> : <div className="scene__moon" />)}

      {condition === 'fog' &&
        range(4).map((i) => (
          <span
            key={`fog-${i}`}
            className="scene__fog"
            style={{ '--y': `${18 + i * 20}%`, '--duration': `${14 + i * 5}s`, '--delay': `${-i * 3}s` }}
          />
        ))}

      {range(CLOUDS[condition] ?? 0).map((i) => (
        <span
          key={`cloud-${i}`}
          className="scene__cloud"
          style={{
            '--y': `${6 + seeded(i, 4) * 34}%`,
            '--scale': 0.7 + seeded(i, 5) * 0.7,
            '--duration': `${38 + seeded(i, 6) * 30}s`,
            '--delay': `${-seeded(i, 7) * 60}s`,
          }}
        />
      ))}

      {range(DROPS[condition] ?? 0).map((i) => (
        <span
          key={`drop-${i}`}
          className="scene__drop"
          style={{
            '--x': `${seeded(i, 8) * 105}%`,
            '--duration': `${0.55 + seeded(i, 9) * 0.45}s`,
            '--delay': `${-seeded(i, 10) * 2}s`,
          }}
        />
      ))}

      {range(FLAKES[condition] ?? 0).map((i) => (
        <span
          key={`flake-${i}`}
          className="scene__flake"
          style={{
            '--x': `${seeded(i, 11) * 100}%`,
            '--size': `${3 + seeded(i, 12) * 5}px`,
            '--sway': `${10 + seeded(i, 13) * 25}px`,
            '--duration': `${5 + seeded(i, 14) * 5}s`,
            '--delay': `${-seeded(i, 15) * 10}s`,
          }}
        />
      ))}

      {condition === 'thunderstorm' && <div className="scene__lightning" />}
    </div>
  )
}
