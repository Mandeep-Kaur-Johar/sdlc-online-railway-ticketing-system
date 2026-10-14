import type { PassengerProfile } from '../types';

export interface PassengerInput {
  fullName: string;
  age: number;
  gender: string;
  passengerProfileId: number | null;
}

interface Props {
  passengers: PassengerInput[];
  profiles: PassengerProfile[];
  maxPassengers: number;
  onChange: (passengers: PassengerInput[]) => void;
}

/** User Stories 3003 and 3004 - capture passenger details or reuse a saved profile. */
export default function PassengerFields({ passengers, profiles, maxPassengers, onChange }: Props) {
  const update = (index: number, patch: Partial<PassengerInput>) => {
    onChange(passengers.map((p, i) => (i === index ? { ...p, ...patch } : p)));
  };

  const applyProfile = (index: number, profileId: number) => {
    const profile = profiles.find((p) => p.id === profileId);
    if (!profile) {
      update(index, { passengerProfileId: null });
      return;
    }
    update(index, {
      passengerProfileId: profile.id,
      fullName: profile.fullName,
      age: profile.age,
      gender: profile.gender
    });
  };

  return (
    <div>
      {passengers.map((passenger, index) => (
        <div className="row" key={index}>
          <label>
            Saved passenger
            <select
              value={passenger.passengerProfileId ?? ''}
              onChange={(e) => applyProfile(index, Number(e.target.value))}
            >
              <option value="">New passenger</option>
              {profiles.map((profile) => (
                <option key={profile.id} value={profile.id}>
                  {profile.fullName}
                </option>
              ))}
            </select>
          </label>
          <label>
            Full name
            <input
              value={passenger.fullName}
              onChange={(e) => update(index, { fullName: e.target.value })}
              required
            />
          </label>
          <label>
            Age
            <input
              type="number"
              min={0}
              max={120}
              value={passenger.age}
              onChange={(e) => update(index, { age: Number(e.target.value) })}
            />
          </label>
          <label>
            Gender
            <select value={passenger.gender} onChange={(e) => update(index, { gender: e.target.value })}>
              <option>Female</option>
              <option>Male</option>
              <option>Other</option>
            </select>
          </label>
          <button
            type="button"
            className="secondary"
            disabled={passengers.length === 1}
            onClick={() => onChange(passengers.filter((_, i) => i !== index))}
          >
            Remove
          </button>
        </div>
      ))}

      <button
        type="button"
        className="secondary"
        disabled={passengers.length >= maxPassengers}
        onClick={() => onChange([...passengers, { fullName: '', age: 30, gender: 'Female', passengerProfileId: null }])}
      >
        Add passenger ({passengers.length}/{maxPassengers})
      </button>
    </div>
  );
}
