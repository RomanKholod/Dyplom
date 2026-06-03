import { api } from './client';
import type { MeterDto, MeterReadingDto, CreateMeterDto, AddReadingDto } from '../types/utilities';

export const utilitiesApi = {
  getAllMeters: () =>
    api.get<MeterDto[]>('/meters').then(r => r.data),

  getMeterById: (id: string) =>
    api.get<MeterDto>(`/meters/${id}`).then(r => r.data),

  createMeter: (payload: CreateMeterDto) =>
    api.post<MeterDto>('/meters', payload).then(r => r.data),

  deleteMeter: (id: string) =>
    api.delete(`/meters/${id}`),

  addReading: (meterId: string, payload: AddReadingDto) =>
    api.post<MeterReadingDto>(`/meters/${meterId}/readings`, payload).then(r => r.data),

  getReadings: (meterId: string) =>
    api.get<MeterReadingDto[]>(`/meters/${meterId}/readings`).then(r => r.data),
};
