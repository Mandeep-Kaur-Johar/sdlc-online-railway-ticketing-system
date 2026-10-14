import type {
  Booking,
  NotificationItem,
  PassengerProfile,
  PaymentIntent,
  RefundQuote,
  SupportRequest,
  TrainSearchResult,
  SeatAvailability,
  TravelClass
} from '../types';

export const API_BASE_URL: string =
  (import.meta as unknown as { env?: Record<string, string> }).env?.VITE_API_BASE_URL ??
  'http://localhost:5080';

export class ApiError extends Error {
  constructor(public code: string, message: string, public status: number) {
    super(message);
    this.name = 'ApiError';
  }
}

export async function request<T>(path: string, init: RequestInit = {}): Promise<T> {
  const response = await fetch(`${API_BASE_URL}${path}`, {
    ...init,
    headers: {
      'Content-Type': 'application/json',
      ...(init.headers ?? {})
    }
  });

  if (!response.ok) {
    let code = 'HTTP_ERROR';
    let message = `Request failed with status ${response.status}`;
    try {
      const body = await response.json();
      code = body?.code ?? code;
      message = body?.message ?? message;
    } catch {
      // response body was not JSON - keep the default message
    }
    throw new ApiError(code, message, response.status);
  }

  if (response.status === 204) {
    return undefined as T;
  }

  return (await response.json()) as T;
}

export const api = {
  searchTrains(source: string, destination: string, date: string): Promise<TrainSearchResult[]> {
    const query = new URLSearchParams({ source, destination, date });
    return request<TrainSearchResult[]>(`/api/v1/trains/search?${query.toString()}`);
  },

  availability(scheduleId: number): Promise<SeatAvailability[]> {
    return request<SeatAvailability[]>(`/api/v1/trains/schedules/${scheduleId}/availability`);
  },

  listProfiles(userId: string): Promise<PassengerProfile[]> {
    return request<PassengerProfile[]>(`/api/v1/passengers?userId=${encodeURIComponent(userId)}`);
  },

  createProfile(payload: Omit<PassengerProfile, 'id' | 'idDocumentMasked' | 'isActive'> & { idDocument: string }): Promise<PassengerProfile> {
    return request<PassengerProfile>('/api/v1/passengers', {
      method: 'POST',
      body: JSON.stringify(payload)
    });
  },

  updateProfile(
    id: number,
    payload: Omit<PassengerProfile, 'id' | 'idDocumentMasked' | 'isActive'> & { idDocument: string }
  ): Promise<PassengerProfile> {
    return request<PassengerProfile>(`/api/v1/passengers/${id}`, {
      method: 'PUT',
      body: JSON.stringify(payload)
    });
  },

  createBooking(
    payload: {
      userId: string;
      scheduleId: number;
      travelClass: TravelClass;
      contactEmail: string;
      contactPhone: string;
      passengers: { fullName: string; age: number; gender: string; passengerProfileId: number | null }[];
    },
    idempotencyKey: string
  ): Promise<Booking> {
    return request<Booking>('/api/v1/bookings', {
      method: 'POST',
      headers: { 'Idempotency-Key': idempotencyKey },
      body: JSON.stringify(payload)
    });
  },

  listBookings(userId: string): Promise<Booking[]> {
    return request<Booking[]>(`/api/v1/bookings?userId=${encodeURIComponent(userId)}`);
  },

  getBooking(bookingReference: string): Promise<Booking> {
    return request<Booking>(`/api/v1/bookings/${bookingReference}`);
  },

  refundQuote(bookingReference: string): Promise<RefundQuote> {
    return request<RefundQuote>(`/api/v1/bookings/${bookingReference}/refund-quote`);
  },

  cancelBooking(bookingReference: string, userId: string, reason: string): Promise<Booking> {
    return request<Booking>(`/api/v1/bookings/${bookingReference}/cancel`, {
      method: 'POST',
      body: JSON.stringify({ userId, reason })
    });
  },

  createPaymentIntent(bookingReference: string, method: string): Promise<PaymentIntent> {
    return request<PaymentIntent>('/api/v1/payments/intents', {
      method: 'POST',
      body: JSON.stringify({ bookingReference, method })
    });
  },

  listNotifications(bookingReference?: string): Promise<NotificationItem[]> {
    const suffix = bookingReference ? `?bookingReference=${encodeURIComponent(bookingReference)}` : '';
    return request<NotificationItem[]>(`/api/v1/notifications${suffix}`);
  },

  listSupportRequests(userId: string): Promise<SupportRequest[]> {
    return request<SupportRequest[]>(`/api/v1/support-requests?userId=${encodeURIComponent(userId)}`);
  },

  createSupportRequest(payload: {
    userId: string;
    bookingReference: string | null;
    subject: string;
    description: string;
  }): Promise<SupportRequest> {
    return request<SupportRequest>('/api/v1/support-requests', {
      method: 'POST',
      body: JSON.stringify(payload)
    });
  }
};
