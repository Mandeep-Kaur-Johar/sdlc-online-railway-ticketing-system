import type { SeatAvailability, TravelClass } from '../types';
import { availabilityTone, formatCurrency, travelClassLabel } from '../utils/format';

interface Props {
  availability: SeatAvailability[];
  onSelect?: (travelClass: TravelClass) => void;
  selected?: TravelClass | null;
}

/** User Story 3001 - availability displayed by travel class. */
export default function AvailabilityTable({ availability, onSelect, selected }: Props) {
  return (
    <table>
      <thead>
        <tr>
          <th>Travel class</th>
          <th>Fare</th>
          <th>Seats available</th>
          <th>Status</th>
          {onSelect ? <th /> : null}
        </tr>
      </thead>
      <tbody>
        {availability.map((item) => (
          <tr key={item.travelClass}>
            <td>{travelClassLabel(item.travelClass)}</td>
            <td>{formatCurrency(item.fare)}</td>
            <td>
              {item.availableSeats} / {item.totalSeats}
            </td>
            <td>
              <span className={`badge ${availabilityTone(item)}`}>{item.status}</span>
            </td>
            {onSelect ? (
              <td>
                <button
                  type="button"
                  className={selected === item.travelClass ? '' : 'secondary'}
                  disabled={item.availableSeats === 0}
                  onClick={() => onSelect(item.travelClass)}
                >
                  {selected === item.travelClass ? 'Selected' : 'Select'}
                </button>
              </td>
            ) : null}
          </tr>
        ))}
      </tbody>
    </table>
  );
}
