export interface MeterDto {
  id: string;
  number: string;
  type: string;
  unitId: string;
  unitNumber: string;
  propertyName: string;
  currentReading: number;
  lastReadingDate: string;
  ratePerUnit: number;
}

export interface MeterReadingDto {
  id: string;
  meterId: string;
  reading: number;
  readingDate: string;
  consumption: number;
  amount: number;
  notes?: string;
}

export interface CreateMeterDto {
  number: string;
  type: string;
  unitId: string;
  ratePerUnit: number;
  currentReading: number;
}

export interface AddReadingDto {
  reading: number;
  readingDate: string;
  notes?: string;
}
