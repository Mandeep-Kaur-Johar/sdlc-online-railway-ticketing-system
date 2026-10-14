import { describe, expect, it } from 'vitest';
import {
  availabilityTone,
  canCancel,
  canPay,
  formatDateTime,
  holdCountdownMinutes,
  newIdempotencyKey,
  travelClassLabel
} from './format';

describe('travelClassLabel (User Story 3001)', () => {
  it('renders a passenger friendly label for every travel class', () => {
    expect(travelClassLabel('ThirdAc')).toBe('AC 3 Tier (3A)');
    expect(travelClassLabel('SleeperClass')).toBe('Sleeper (SL)');
    expect(travelClassLabel('FirstAc')).toBe('AC First Class (1A)');
  });
});

describe('availabilityTone (User Story 3001)', () => {
  const base = { travelClass: 'ThirdAc' as const, totalSeats: 64, fare: 1850, status: 'Available' };

  it('flags sold out inventory as waitlist', () => {
    expect(availabilityTone({ ...base, availableSeats: 0 })).toBe('waitlist');
  });

  it('flags low inventory as filling fast', () => {
    expect(availabilityTone({ ...base, availableSeats: 6 })).toBe('filling');
  });

  it('flags healthy inventory as available', () => {
    expect(availabilityTone({ ...base, availableSeats: 40 })).toBe('available');
  });
});

describe('booking state helpers (User Stories 3006 and 3007)', () => {
  it('allows cancellation only for pending or confirmed bookings', () => {
    expect(canCancel({ status: 'Confirmed' })).toBe(true);
    expect(canCancel({ status: 'Pending' })).toBe(true);
    expect(canCancel({ status: 'Cancelled' })).toBe(false);
    expect(canCancel({ status: 'Failed' })).toBe(false);
  });

  it('allows payment only while the booking is pending and unpaid', () => {
    expect(canPay({ status: 'Pending', paymentStatus: 'None' })).toBe(true);
    expect(canPay({ status: 'Pending', paymentStatus: 'Succeeded' })).toBe(false);
    expect(canPay({ status: 'Confirmed', paymentStatus: 'Succeeded' })).toBe(false);
  });
});

describe('holdCountdownMinutes (15 minute seat hold)', () => {
  it('returns the remaining whole minutes', () => {
    const now = new Date('2026-01-01T10:00:00Z');
    expect(holdCountdownMinutes('2026-01-01T10:15:00Z', now)).toBe(15);
  });

  it('returns zero once the hold has expired', () => {
    const now = new Date('2026-01-01T10:20:00Z');
    expect(holdCountdownMinutes('2026-01-01T10:15:00Z', now)).toBe(0);
    expect(holdCountdownMinutes(null, now)).toBe(0);
  });
});

describe('formatDateTime', () => {
  it('formats an ISO timestamp in UTC', () => {
    expect(formatDateTime('2026-05-01T08:30:00Z')).toBe('2026-05-01 08:30 UTC');
  });

  it('returns an empty string for invalid input', () => {
    expect(formatDateTime('')).toBe('');
    expect(formatDateTime('not-a-date')).toBe('');
  });
});

describe('newIdempotencyKey (User Story 3003)', () => {
  it('produces unique keys so retries never double book', () => {
    const keys = new Set(Array.from({ length: 50 }, () => newIdempotencyKey()));
    expect(keys.size).toBe(50);
  });
});
