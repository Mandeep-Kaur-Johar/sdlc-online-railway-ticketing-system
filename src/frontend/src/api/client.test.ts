import { afterEach, describe, expect, it, vi } from 'vitest';
import { ApiError, api, request } from './client';

function mockFetch(response: Partial<Response> & { json?: () => Promise<unknown> }) {
  const fn = vi.fn().mockResolvedValue(response as Response);
  vi.stubGlobal('fetch', fn);
  return fn;
}

afterEach(() => {
  vi.unstubAllGlobals();
  vi.restoreAllMocks();
});

describe('request', () => {
  it('returns the parsed JSON body on success', async () => {
    mockFetch({ ok: true, status: 200, json: async () => ({ value: 42 }) });

    await expect(request<{ value: number }>('/api/v1/test')).resolves.toEqual({ value: 42 });
  });

  it('maps an API error body to an ApiError', async () => {
    mockFetch({ ok: false, status: 409, json: async () => ({ code: 'INSUFFICIENT_SEATS', message: 'Only 1 seat remains.' }) });

    await expect(request('/api/v1/bookings')).rejects.toMatchObject({
      name: 'ApiError',
      code: 'INSUFFICIENT_SEATS',
      status: 409
    });
  });

  it('falls back to a generic message when the error body is not JSON', async () => {
    mockFetch({
      ok: false,
      status: 500,
      json: async () => {
        throw new Error('not json');
      }
    });

    await expect(request('/api/v1/bookings')).rejects.toBeInstanceOf(ApiError);
  });
});

describe('api endpoints', () => {
  it('builds the train search query string (User Story 3000)', async () => {
    const fetchMock = mockFetch({ ok: true, status: 200, json: async () => [] });

    await api.searchTrains('NDLS', 'BCT', '2026-05-01');

    const url = fetchMock.mock.calls[0][0] as string;
    expect(url).toContain('/api/v1/trains/search?');
    expect(url).toContain('source=NDLS');
    expect(url).toContain('destination=BCT');
    expect(url).toContain('date=2026-05-01');
  });

  it('sends the idempotency key when creating a booking (User Story 3003)', async () => {
    const fetchMock = mockFetch({ ok: true, status: 200, json: async () => ({}) });

    await api.createBooking(
      {
        userId: 'user-001',
        scheduleId: 7,
        travelClass: 'ThirdAc',
        contactEmail: 'a@b.com',
        contactPhone: '+44',
        passengers: [{ fullName: 'Asha', age: 30, gender: 'Female', passengerProfileId: null }]
      },
      'idem-123'
    );

    const init = fetchMock.mock.calls[0][1] as RequestInit;
    expect(init.method).toBe('POST');
    expect((init.headers as Record<string, string>)['Idempotency-Key']).toBe('idem-123');
  });

  it('requests a refund quote before cancellation (User Story 3007)', async () => {
    const fetchMock = mockFetch({ ok: true, status: 200, json: async () => ({}) });

    await api.refundQuote('RTS-ABCD1234');

    expect(fetchMock.mock.calls[0][0]).toContain('/api/v1/bookings/RTS-ABCD1234/refund-quote');
  });
});
