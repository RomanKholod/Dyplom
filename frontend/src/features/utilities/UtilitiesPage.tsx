import { useState } from 'react';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { utilitiesApi, propertiesApi } from '../../api';
import { Plus, Zap, Droplets, Flame, Thermometer, Search } from 'lucide-react';
import { Badge, Modal, Field, inputCls, selectCls, EmptyState, Spinner, Table, Tr, Td } from '../../components/ui';
import type { MeterDto } from '../../types';
import { format } from 'date-fns';
import { uk } from 'date-fns/locale';

const METER_TYPES = [
  { id: 'electricity', label: 'Електроенергія', icon: Zap, unit: 'кВт·год', color: 'text-yellow-500 bg-yellow-50' },
  { id: 'water_cold',  label: 'Вода холодна',   icon: Droplets, unit: 'м³', color: 'text-blue-500 bg-blue-50' },
  { id: 'water_hot',   label: 'Вода гаряча',    icon: Droplets, unit: 'м³', color: 'text-red-400 bg-red-50' },
  { id: 'gas',         label: 'Газ',             icon: Flame, unit: 'м³', color: 'text-orange-500 bg-orange-50' },
  { id: 'heating',     label: 'Теплопостачання', icon: Thermometer, unit: 'Гкал', color: 'text-purple-500 bg-purple-50' },
];

const getMeterType = (type: string) => METER_TYPES.find(t => t.id === type) ?? METER_TYPES[0];

const meterSchema = z.object({
  number: z.string().min(1, "Обов'язкове"),
  type: z.string().min(1, "Обов'язкове"),
  unitId: z.string().min(1, "Обов'язкове"),
  propertyId: z.string().min(1, "Обов'язкове"),
  ratePerUnit: z.coerce.number().positive("Має бути > 0"),
  currentReading: z.coerce.number().min(0),
});
type MeterForm = z.infer<typeof meterSchema>;

const readingSchema = z.object({
  reading: z.coerce.number().min(0, "Обов'язкове"),
  readingDate: z.string().min(1, "Обов'язкове"),
  notes: z.string().optional(),
});
type ReadingForm = z.infer<typeof readingSchema>;

export default function UtilitiesPage() {
  const [search, setSearch] = useState('');
  const [addMeterOpen, setAddMeterOpen] = useState(false);
  const [addReadingTarget, setAddReadingTarget] = useState<MeterDto | null>(null);
  const [selectedProperty, setSelectedProperty] = useState('');
  const qc = useQueryClient();

  const { data: meters, isLoading } = useQuery({
    queryKey: ['meters'],
    queryFn: utilitiesApi.getAllMeters,
  });

  const { data: properties } = useQuery({
    queryKey: ['properties-list'],
    queryFn: () => propertiesApi.getAll({ pageSize: 200 }),
  });

  const { data: units } = useQuery({
    queryKey: ['units-for-property', selectedProperty],
    queryFn: () => propertiesApi.getUnits(selectedProperty),
    enabled: !!selectedProperty,
  });

  const meterForm = useForm<MeterForm>({ resolver: zodResolver(meterSchema), defaultValues: { currentReading: 0 } });
  const readingForm = useForm<ReadingForm>({
    resolver: zodResolver(readingSchema),
    defaultValues: { readingDate: new Date().toISOString().split('T')[0] },
  });

  const createMeterMutation = useMutation({
    mutationFn: (d: MeterForm) => utilitiesApi.createMeter({ number: d.number, type: d.type, unitId: d.unitId, ratePerUnit: d.ratePerUnit, currentReading: d.currentReading }),
    onSuccess: () => { qc.invalidateQueries({ queryKey: ['meters'] }); setAddMeterOpen(false); meterForm.reset(); },
  });

  const addReadingMutation = useMutation({
    mutationFn: (d: ReadingForm) => utilitiesApi.addReading(addReadingTarget!.id, { reading: d.reading, readingDate: new Date(d.readingDate).toISOString(), notes: d.notes }),
    onSuccess: () => { qc.invalidateQueries({ queryKey: ['meters'] }); setAddReadingTarget(null); readingForm.reset(); },
  });

  const fmt = (d: string) => format(new Date(d), 'dd.MM.yyyy', { locale: uk });

  const filteredMeters = (meters ?? []).filter(m =>
    !search || m.number.toLowerCase().includes(search.toLowerCase()) || m.unitNumber?.toLowerCase().includes(search.toLowerCase())
  );

  return (
    <div className="space-y-5">
      {/* Header */}
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-xl font-semibold text-gray-900">Комунальні послуги</h1>
          <p className="text-sm text-gray-500 mt-0.5">{meters?.length ?? 0} лічильників</p>
        </div>
        <button onClick={() => setAddMeterOpen(true)}
          className="flex items-center gap-2 bg-blue-600 hover:bg-blue-700 text-white text-sm font-medium px-4 py-2.5 rounded-xl transition-colors">
          <Plus className="w-4 h-4" /> Додати лічильник
        </button>
      </div>

      {/* Meter type summary */}
      <div className="grid grid-cols-5 gap-3">
        {METER_TYPES.map(mt => {
          const count = (meters ?? []).filter(m => m.type === mt.id).length;
          return (
            <div key={mt.id} className="bg-white rounded-2xl border border-gray-100 p-4 flex items-center gap-3">
              <div className={`w-9 h-9 rounded-xl flex items-center justify-center ${mt.color}`}>
                <mt.icon className="w-4 h-4" />
              </div>
              <div>
                <p className="text-xs text-gray-500 leading-tight">{mt.label}</p>
                <p className="font-semibold text-gray-900">{count}</p>
              </div>
            </div>
          );
        })}
      </div>

      {/* Search */}
      <div className="relative">
        <Search className="absolute left-4 top-1/2 -translate-y-1/2 w-4 h-4 text-gray-400" />
        <input value={search} onChange={e => setSearch(e.target.value)}
          placeholder="Пошук за номером лічильника або приміщенням..."
          className="w-full pl-11 pr-4 py-2.5 bg-white border border-gray-200 rounded-xl text-sm focus:outline-none focus:ring-2 focus:ring-blue-500" />
      </div>

      {/* Table */}
      {isLoading ? (
        <div className="flex justify-center py-16"><Spinner size="lg" /></div>
      ) : !filteredMeters.length ? (
        <EmptyState title="Лічильників не знайдено" action={<button onClick={() => setAddMeterOpen(true)} className="text-sm text-blue-600 hover:underline">Додати лічильник</button>} />
      ) : (
        <Table headers={['Лічильник', 'Тип', "Об'єкт / Прим.", 'Поточні показники', 'Дата зняття', 'Тариф', 'Дії']}>
          {filteredMeters.map(m => {
            const mt = getMeterType(m.type);
            return (
              <Tr key={m.id}>
                <Td><span className="font-mono text-sm font-medium text-gray-900">{m.number}</span></Td>
                <Td>
                  <div className="flex items-center gap-2">
                    <div className={`w-7 h-7 rounded-lg flex items-center justify-center ${mt.color}`}>
                      <mt.icon className="w-3.5 h-3.5" />
                    </div>
                    <span className="text-sm text-gray-700">{mt.label}</span>
                  </div>
                </Td>
                <Td>
                  <p className="text-sm text-gray-800">{m.propertyName}</p>
                  <p className="text-xs text-gray-400">Прим. {m.unitNumber}</p>
                </Td>
                <Td>
                  <span className="text-sm font-medium text-gray-900">{m.currentReading.toLocaleString('uk-UA')}</span>
                  <span className="text-xs text-gray-400 ml-1">{mt.unit}</span>
                </Td>
                <Td><span className="text-sm text-gray-600">{m.lastReadingDate ? fmt(m.lastReadingDate) : '—'}</span></Td>
                <Td><span className="text-sm text-gray-700">{m.ratePerUnit.toLocaleString('uk-UA')} грн/{mt.unit}</span></Td>
                <Td>
                  <button
                    onClick={() => { setAddReadingTarget(m); readingForm.setValue('reading', m.currentReading); }}
                    className="flex items-center gap-1.5 text-xs font-medium text-blue-600 hover:text-blue-700 bg-blue-50 hover:bg-blue-100 px-2.5 py-1.5 rounded-lg transition-colors">
                    Внести показники
                  </button>
                </Td>
              </Tr>
            );
          })}
        </Table>
      )}

      {/* Add Meter Modal */}
      <Modal open={addMeterOpen} onClose={() => { setAddMeterOpen(false); meterForm.reset(); }} title="Новий лічильник" size="md">
        <form onSubmit={meterForm.handleSubmit(d => createMeterMutation.mutate(d))} className="space-y-4">
          <div className="grid grid-cols-2 gap-3">
            <Field label="Номер лічильника" error={meterForm.formState.errors.number?.message} required>
              <input {...meterForm.register('number')} className={inputCls} placeholder="Серійний номер" />
            </Field>
            <Field label="Тип" error={meterForm.formState.errors.type?.message} required>
              <select {...meterForm.register('type')} className={selectCls}>
                <option value="">Оберіть тип...</option>
                {METER_TYPES.map(mt => <option key={mt.id} value={mt.id}>{mt.label}</option>)}
              </select>
            </Field>
          </div>
          <Field label="Об'єкт" required>
            <select value={selectedProperty}
              onChange={e => { setSelectedProperty(e.target.value); meterForm.setValue('propertyId', e.target.value); }}
              className={selectCls}>
              <option value="">Оберіть об'єкт...</option>
              {properties?.items.map(p => <option key={p.id} value={p.id}>{p.name}</option>)}
            </select>
          </Field>
          <Field label="Приміщення" error={meterForm.formState.errors.unitId?.message} required>
            <select {...meterForm.register('unitId')} className={selectCls} disabled={!selectedProperty}>
              <option value="">Спочатку оберіть об'єкт...</option>
              {units?.map(u => <option key={u.id} value={u.id}>Прим. {u.number} (поверх {u.floor})</option>)}
            </select>
          </Field>
          <div className="grid grid-cols-2 gap-3">
            <Field label="Початкові показники" error={meterForm.formState.errors.currentReading?.message}>
              <input type="number" step="0.001" {...meterForm.register('currentReading')} className={inputCls} />
            </Field>
            <Field label="Тариф (грн/од.)" error={meterForm.formState.errors.ratePerUnit?.message} required>
              <input type="number" step="0.0001" {...meterForm.register('ratePerUnit')} className={inputCls} />
            </Field>
          </div>
          <div className="flex gap-3 pt-2">
            <button type="button" onClick={() => setAddMeterOpen(false)} className="flex-1 px-4 py-2.5 border border-gray-200 rounded-xl text-sm text-gray-600 hover:bg-gray-50">Скасувати</button>
            <button type="submit" disabled={createMeterMutation.isPending}
              className="flex-1 bg-blue-600 hover:bg-blue-700 disabled:opacity-50 text-white text-sm font-medium px-4 py-2.5 rounded-xl flex items-center justify-center gap-2">
              {createMeterMutation.isPending && <Spinner />} Додати
            </button>
          </div>
        </form>
      </Modal>

      {/* Add Reading Modal */}
      <Modal open={!!addReadingTarget} onClose={() => { setAddReadingTarget(null); readingForm.reset(); }}
        title={`Показники: ${addReadingTarget?.number}`} size="sm">
        <form onSubmit={readingForm.handleSubmit(d => addReadingMutation.mutate(d))} className="space-y-4">
          {addReadingTarget && (
            <div className="bg-gray-50 rounded-xl p-3 text-sm space-y-1">
              <div className="flex justify-between"><span className="text-gray-500">Тип:</span><span>{getMeterType(addReadingTarget.type).label}</span></div>
              <div className="flex justify-between"><span className="text-gray-500">Попередні:</span><span className="font-medium">{addReadingTarget.currentReading.toLocaleString('uk-UA')} {getMeterType(addReadingTarget.type).unit}</span></div>
              <div className="flex justify-between"><span className="text-gray-500">Тариф:</span><span>{addReadingTarget.ratePerUnit} грн/{getMeterType(addReadingTarget.type).unit}</span></div>
            </div>
          )}
          <Field label="Нові показники" error={readingForm.formState.errors.reading?.message} required>
            <input type="number" step="0.001" {...readingForm.register('reading')} className={inputCls} />
          </Field>
          <Field label="Дата зняття" error={readingForm.formState.errors.readingDate?.message} required>
            <input type="date" {...readingForm.register('readingDate')} className={inputCls} />
          </Field>
          <Field label="Нотатки">
            <input {...readingForm.register('notes')} className={inputCls} />
          </Field>
          {readingForm.watch('reading') > (addReadingTarget?.currentReading ?? 0) && (
            <div className="bg-blue-50 text-blue-700 text-sm rounded-xl p-3">
              Споживання: <strong>{(readingForm.watch('reading') - (addReadingTarget?.currentReading ?? 0)).toFixed(3)}</strong> {getMeterType(addReadingTarget?.type ?? '').unit}
              {' '}→ <strong>{((readingForm.watch('reading') - (addReadingTarget?.currentReading ?? 0)) * (addReadingTarget?.ratePerUnit ?? 0)).toFixed(2)} грн</strong>
            </div>
          )}
          <div className="flex gap-3 pt-2">
            <button type="button" onClick={() => setAddReadingTarget(null)} className="flex-1 px-4 py-2.5 border border-gray-200 rounded-xl text-sm text-gray-600 hover:bg-gray-50">Скасувати</button>
            <button type="submit" disabled={addReadingMutation.isPending}
              className="flex-1 bg-blue-600 hover:bg-blue-700 disabled:opacity-50 text-white text-sm font-medium px-4 py-2.5 rounded-xl flex items-center justify-center gap-2">
              {addReadingMutation.isPending && <Spinner />} Зберегти
            </button>
          </div>
        </form>
      </Modal>
    </div>
  );
}
