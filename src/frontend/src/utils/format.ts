import type { Booking, SeatAvailability, TravelClass } from '../types';

const CLASS_LABELS: Record<TravelClass, string> = {
  SecondSitting: 'Second Sitting (2S)',
  SleeperClass: 'Sleeper (SL)',
  ThirdAc: 'AC 3 Tier (3A)',
  SecondAc: 'AC 2 Tier (2A)',
  FirstAc: 'AC First Class (1A)'
};

export function travelClassLabel(travelClass: TravelClass): string {
  return CLASS_LABELS[travelClass] ?? travelClass;
}

export function formatCurrency(amount: number): string {
  return new Intl.NumberFormat('en-GB', {
    style: 'currency',
    currency: 'INR',
    minimumFractionDigits: 2
  }).format(amount);
}

export function formatDateTime(isoUtc: string): string {
  if (!isoUtc) {
    return '';
  }
  const date = new Date(isoUtc);
  return Number.isNaN(date.getTime()) ? '' : date.toISOString().replace('T', ' ').slice(0, 16) + ' UTC';
}

export function availabilityTone(availability: SeatAvailability): 'available' | 'filling' | 'waitlist' {
  if (availability.availableSeats === 0) {
    return 'waitlist';
  }
  return availability.availableSeats <= 10 ? 'filling' : 'available';
}

export function canCancel(booking: Pick<Booking, 'status'>): boolean {
  return booking.status === 'Confirmed' || booking.status === 'Pending';
}

export function canPay(booking: Pick<Booking, 'status' | 'paymentStatus'>): boolean {
  return booking.status === 'Pending' && booking.paymentStatus !== 'Succeeded';
}

export function holdCountdownMinutes(holdExpiresUtc: string | null, nowUtc: Date = new Date()): number {
  if (!holdExpiresUtc) {
    return 0;
  }
  const expiry = new Date(holdExpiresUtc).getTime();
  const diffMs = expiry - nowUtc.getTime();
  return diffMs <= 0 ? 0 : Math.ceil(diffMs / 60000);
}

export function newIdempotencyKey(): string {
  return `bk-${Date.now()}-${Math.random().toString(36).slice(2, 10)}`;
}
