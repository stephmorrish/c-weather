import { describe, expect, it } from 'vitest'
import { formatPlace, formatRegion, timeAgo } from './format.js'

describe('formatPlace', () => {
  it('joins name, region and country', () => {
    expect(formatPlace({ name: 'Toronto', admin1: 'Ontario', country: 'Canada' })).toBe('Toronto, Ontario, Canada')
  })

  it('drops duplicates and empty parts', () => {
    expect(formatPlace({ name: 'São Paulo', admin1: 'São Paulo', country: 'Brazil' })).toBe('São Paulo, Brazil')
    expect(formatPlace({ name: 'Tokyo', admin1: null, country: 'Japan' })).toBe('Tokyo, Japan')
  })
})

describe('formatRegion', () => {
  it('leaves out the place name', () => {
    expect(formatRegion({ name: 'São Paulo', admin1: 'São Paulo', country: 'Brazil' })).toBe('Brazil')
  })
})

describe('timeAgo', () => {
  // Passing "now" in as a parameter (instead of calling Date.now() inside) is what makes
  // this function easy to test: the result is fully decided by the inputs.
  const now = Date.parse('2026-11-10T12:00:00Z')
  const minutesAgo = (m) => new Date(now - m * 60_000).toISOString()

  it.each([
    [0.5, /now/],
    [5, /5 min/],
    [90, /2 hours|2 hr/],
    [60 * 24, /yesterday|1 day/],
  ])('%s minutes ago → %s', (minutes, expected) => {
    expect(timeAgo(minutesAgo(minutes), now)).toMatch(expected)
  })
})
