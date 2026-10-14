export type TravelClass =
  | 'SecondSitting'
  | 'SleeperClass'
  | 'ThirdAc'
  | 'SecondAc'
  | 'FirstAc';

export interface SeatAvailability {
  travelClass: TravelClass;
  totalSeats: number;
  availableSeats: number;
  fare: number;
  status: string;
}

export interface TrainSearchResult {
  scheduleId: number;
  trainNumber: string;
  trainName: string;
  source: string;
  destination: string;
  departureUtc: string;
  arrivalUtc: string;
  status: string;
  availability: SeatAvailability[];
}

export interface PassengerProfile {
  id: number;
  userId: string;
  fullName: string;
  age: number;
  gender: string;
  email: string;
  phone: string;
  idDocumentMasked: string;
  isActive: boolean;
}

export interface BookingPassenger {
  id: number;
  fullName: string;
  age: number;
  gender: string;
  seatNumber: string;
}

export interface Booking {
  bookingReference: string;
  status: string;
  trainNumber: string;
  trainName: string;
  departureUtc: string;
  travelClass: TravelClass;
  seatCount: number;
  totalFare: number;
  holdExpiresUtc: string | null;
  ticketNumber: string | null;
  paymentStatus: string;
  refundAmount: number | null;
  refundPercentage: number | null;
  passengers: BookingPassenger[];
}

export interface PaymentIntent {
  bookingReference: string;
  providerIntentId: string;
  amount: number;
  status: string;
}

export interface RefundQuote {
  bookingReference: string;
  percentage: number;
  amount: number;
  departureUtc: string;
}

export interface NotificationItem {
  id: number;
  bookingReference: string;
  type: string;
  channel: string;
  recipient: string;
  subject: string;
  delivered: boolean;
  sentUtc: string;
}

export interface SupportUpdate {
  status: string;
  note: string;
  author: string;
  createdUtc: string;
}

export interface SupportRequest {
  reference: string;
  userId: string;
  bookingReference: string | null;
  subject: string;
  description: string;
  status: string;
  resolution: string | null;
  createdUtc: string;
  updatedUtc: string;
  updates: SupportUpdate[];
}

export interface ApiErrorBody {
  code: string;
  message: string;
}
