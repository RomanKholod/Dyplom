import { useState } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { propertiesApi, contractsApi, ownersApi } from '../../api';
import {
  ArrowLeft, Building2, Plus, Users, FileText,
  CheckCircle, Clock, Wrench, Home, User
} from 'lucide-react';
import {
  Badge, Modal, Field, inputCls, selectCls,
  Spinner, Table, Tr, Td, EmptyState
} from '../../components/ui';
import type { UnitStatus, OwnerDto } from '../../types';

const UNIT_STATUS: Record<UnitStatus, { label: string; variant: any }> = {
  Available: { label: 'Вільне',      variant: 'green'  },
  Occupied:  { label: 'Зайняте',     variant: 'blue'   },
  UnderRepair:{ label: 'Ремонт',     variant: 'yellow' },
  Reserved:  { label: 'Зарезервовано', variant: 'purple' },
};

const PROPERTY_STATUS_LABELS: Record<string, string> = {
  Available: 'Доступний',
  Rented: 'Зайнятий',
  UnderMaintenance: 'Ремонт',
  Reserved: 'Зарезервовано',
};

const PROPERTY_TYPE_LABELS: Record<string, string> = {
  Residential: 'Житлова',
  Commercial: 'Комерційна',
  Industrial: 'Індустріальна',
  Land: 'Земельна ділянка',
};

const unitSchema = z.object({
  number:        z.string().min(1, "Обов'язкове"),
  floor:         z.coerce.number().min(1),
  area:          z.coerce.number().positive("Має бути > 0"),
  roomsCount:    z.coerce.number().min(0),
  baseRentPrice: z.coerce.number().min(0),
  description:   z.string().optional(),
});
type UnitForm = z.infer<typeof unitSchema>;

export default function PropertyDetailPage() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const qc = useQueryClient();
  const [addUnitOpen, setAddUnitOpen] = useState(false);
  const [activeTab, setActiveTab] = useState<'units' | 'contracts'>('units');

  const { data: property, isLoading } = useQuery({
    queryKey: ['property', id],
    queryFn: () => propertiesApi.getById(id!),
    enabled: !!id,
  });

  const { data: units, isLoading: unitsLoading } = useQuery({
    queryKey: ['units', id],
    queryFn: () => propertiesApi.getUnits(id!),
    enabled: !!id,
  });

  const { data: contracts } = useQuery({
    queryKey: ['contracts', 'property', id],
    queryFn: () => contractsApi.getAll({ propertyId: id, pageSize: 100 }),
    enabled: !!id,
  });

  // Додатково завантажуємо список власників для точного локального пошуку, якщо ownerName порожній
  const { data: ownersData } = useQuery({
    queryKey: ['owners-list'],
    queryFn: () => ownersApi.getAll(),
    enabled: !!property?.ownerId,
  });
  const owners = ownersData?.items ?? (Array.isArray(ownersData) ? ownersData : []);

  const form = useForm<UnitForm>({
    resolver: zodResolver(unitSchema),
    defaultValues: { floor: 1, roomsCount: 1, baseRentPrice: 0 },
  });

  const createUnitMutation = useMutation({
    mutationFn: (d: UnitForm) => propertiesApi.createUnit({ ...d, propertyId: id! } as any),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['units', id] });
      qc.invalidateQueries({ queryKey: ['property', id] });
      setAddUnitOpen(false);
      form.reset();
    },
  });

  if (isLoading) return <div className="flex justify-center py-16"><Spinner size="lg" /></div>;
  if (!property) return <div className="text-center py-16 text-gray-400">Об'єкт не знайдено</div>;

  // ─── ЛОКАЛЬНИЙ ПЕРЕРАХУНОК НА ВИПАДОК 0 ВІД БЕКЕНДУ ─────────────────
  const totalUnitsCount = property.unitsCount > 0 ? property.unitsCount : (units?.length ?? 0);
  
  const occupiedUnitsCount = property.occupiedUnitsCount > 0 
    ? property.occupiedUnitsCount 
    : (units?.filter(u => u.status === 'Occupied').length ?? 0);

  const occupancyRate = totalUnitsCount > 0
    ? Math.round((occupiedUnitsCount / totalUnitsCount) * 100)
    : 0;

  // Визначаємо надійне ім'я власника
  const resolvedOwnerName = property.ownerName || owners.find((o: OwnerDto) => o.id === property.ownerId)?.fullName || '—';

  return (
    <div className="space-y-5">
      {/* Header */}
      <div className="flex items-center gap-3">
        <button onClick={() => navigate('/properties')}
          className="p-2 text-gray-400 hover:text-gray-600 hover:bg-gray-100 rounded-xl transition-colors">
          <ArrowLeft className="w-5 h-5" />
        </button>
        <div className="flex-1">
          <h1 className="text-xl font-semibold text-gray-900">{property.name}</h1>
          <p className="text-sm text-gray-500">{property.address}, {property.city}</p>
        </div>
        <Badge variant={property.status === 'Available' ? 'green' : property.status === 'Rented' ? 'blue' : 'yellow'}>
          {PROPERTY_STATUS_LABELS[property.status] || property.status}
        </Badge>
      </div>

      {/* Stats cards */}
      <div className="grid grid-cols-2 lg:grid-cols-4 gap-4">
        {[
          { icon: Building2, label: 'Площа',       value: `${property.totalArea} м²`,          color: 'bg-blue-50 text-blue-600' },
          { icon: Home,      label: 'Приміщень',   value: totalUnitsCount,                    color: 'bg-purple-50 text-purple-600' },
          { icon: Users,     label: 'Заповнено',   value: `${occupancyRate}%`,                  color: 'bg-green-50 text-green-600' },
          { icon: FileText,  label: 'Договорів',   value: contracts?.totalCount ?? 0,           color: 'bg-orange-50 text-orange-600' },
        ].map(({ icon: Icon, label, value, color }) => (
          <div key={label} className="bg-white rounded-2xl border border-gray-100 p-4 flex items-center gap-3">
            <div className={`w-10 h-10 rounded-xl flex items-center justify-center ${color}`}>
              <Icon className="w-5 h-5" />
            </div>
            <div>
              <p className="text-xs text-gray-500">{label}</p>
              <p className="text-lg font-semibold text-gray-900">{value}</p>
            </div>
          </div>
        ))}
      </div>

      {/* Occupancy bar */}
      <div className="bg-white rounded-2xl border border-gray-100 p-4">
        <div className="flex justify-between text-sm mb-2">
          <span className="text-gray-600">Заповненість</span>
          <span className="font-medium text-gray-900">
            {occupiedUnitsCount} / {totalUnitsCount} приміщень
          </span>
        </div>
        <div className="h-2 bg-gray-100 rounded-full overflow-hidden">
          <div className="h-full bg-blue-500 rounded-full transition-all"
            style={{ width: `${occupancyRate}%` }} />
        </div>
        <div className="flex justify-between text-xs text-gray-400 mt-2 pt-1 border-t border-gray-50 items-center">
          <span className="flex items-center gap-1 text-gray-600 font-medium">
            <User className="w-3.5 h-3.5 text-gray-400" />
            Власник: <span className="text-gray-900">{resolvedOwnerName}</span>
          </span>
          <span>Поверхів: {property.floorsCount} | {PROPERTY_TYPE_LABELS[property.type] || property.type}</span>
        </div>
      </div>

      {/* Tabs */}
      <div className="flex gap-1 p-1 bg-gray-100 rounded-xl w-fit">
        {[
          { id: 'units',     label: `Приміщення (${totalUnitsCount})` },
          { id: 'contracts', label: `Договори (${contracts?.totalCount ?? 0})` },
        ].map(t => (
          <button key={t.id} onClick={() => setActiveTab(t.id as any)}
            className={`px-5 py-2 text-sm font-medium rounded-lg transition-colors ${
              activeTab === t.id ? 'bg-white text-gray-900 shadow-sm' : 'text-gray-500 hover:text-gray-700'
            }`}>
            {t.label}
          </button>
        ))}
      </div>

      {/* Units tab */}
      {activeTab === 'units' && (
        <div className="space-y-4">
          <div className="flex justify-end">
            <button onClick={() => setAddUnitOpen(true)}
              className="flex items-center gap-2 bg-blue-600 hover:bg-blue-700 text-white text-sm font-medium px-4 py-2.5 rounded-xl transition-colors">
              <Plus className="w-4 h-4" /> Додати приміщення
            </button>
          </div>

          {unitsLoading ? (
            <div className="flex justify-center py-10"><Spinner size="lg" /></div>
          ) : !units?.length ? (
            <EmptyState title="Приміщень ще немає"
              action={<button onClick={() => setAddUnitOpen(true)} className="text-sm text-blue-600 hover:underline">Додати перше приміщення</button>} />
          ) : (
            <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-3">
              {units.map(u => {
                const st = UNIT_STATUS[u.status as UnitStatus] ?? { label: u.status, variant: 'gray' };
                return (
                  <div key={u.id} className="bg-white rounded-2xl border border-gray-100 p-4">
                    <div className="flex items-start justify-between mb-3">
                      <div>
                        <p className="font-semibold text-gray-900 text-base">№ {u.number}</p>
                        <p className="text-xs text-gray-400">Поверх {u.floor}</p>
                      </div>
                      <Badge variant={st.variant}>{st.label}</Badge>
                    </div>
                    <div className="grid grid-cols-3 gap-2 text-center">
                      <div className="bg-gray-50 rounded-xl p-2">
                        <p className="text-sm font-semibold text-gray-900">{u.area}</p>
                        <p className="text-xs text-gray-400">м²</p>
                      </div>
                      <div className="bg-gray-50 rounded-xl p-2">
                        <p className="text-sm font-semibold text-gray-900">{u.roomsCount}</p>
                        <p className="text-xs text-gray-400">кімнат</p>
                      </div>
                      <div className="bg-gray-50 rounded-xl p-2">
                        <p className="text-sm font-semibold text-gray-900">{(u.baseRentPrice / 1000).toFixed(1)}к</p>
                        <p className="text-xs text-gray-400">грн/міс</p>
                      </div>
                    </div>
                  </div>
                );
              })}
            </div>
          )}
        </div>
      )}

      {/* Contracts tab */}
      {activeTab === 'contracts' && (
        <div>
          {!contracts?.items.length ? (
            <EmptyState title="Договорів для цього об'єкту немає" />
          ) : (
            <Table headers={['Номер', 'Орендар', 'Приміщення', 'Термін', 'Плата', 'Статус']}>
              {contracts.items.map(c => (
                <Tr key={c.id} onClick={() => navigate('/contracts')}>
                  <Td><span className="font-mono text-sm">{c.number}</span></Td>
                  <Td><span className="text-sm text-gray-800">{c.tenantName}</span></Td>
                  <Td><span className="text-sm text-gray-600">№ {c.unitNumber}</span></Td>
                  <Td>
                    <p className="text-xs text-gray-600">
                      {new Date(c.startDate).toLocaleDateString('uk-UA')} –{' '}
                      {new Date(c.endDate).toLocaleDateString('uk-UA')}
                    </p>
                  </Td>
                  <Td><span className="text-sm font-medium">{c.monthlyRent.toLocaleString('uk-UA')} грн</span></Td>
                  <Td>
                    <Badge variant={
                      c.status === 'Active' ? 'green' : c.status === 'Draft' ? 'gray' : 'red'
                    }>{c.status === 'Active' ? 'Активний' : c.status === 'Draft' ? 'Чернетка' : c.status}</Badge>
                  </Td>
                </Tr>
              ))}
            </Table>
          )}
        </div>
      )}

      {/* Add Unit Modal */}
      <Modal open={addUnitOpen} onClose={() => { setAddUnitOpen(false); form.reset(); }}
        title="Нове приміщення" size="md">
        <form onSubmit={form.handleSubmit(d => createUnitMutation.mutate(d))} className="space-y-4">
          <div className="grid grid-cols-2 gap-3">
            <Field label="Номер приміщення" error={form.formState.errors.number?.message} required>
              <input {...form.register('number')} className={inputCls} placeholder="А101" />
            </Field>
            <Field label="Поверх" error={form.formState.errors.floor?.message} required>
              <input type="number" min="1" {...form.register('floor')} className={inputCls} />
            </Field>
          </div>
          <div className="grid grid-cols-3 gap-3">
            <Field label="Площа (м²)" error={form.formState.errors.area?.message} required>
              <input type="number" step="0.1" {...form.register('area')} className={inputCls} />
            </Field>
            <Field label="Кімнат" error={form.formState.errors.roomsCount?.message}>
              <input type="number" min="0" {...form.register('roomsCount')} className={inputCls} />
            </Field>
            <Field label="Орендна плата (грн)" error={form.formState.errors.baseRentPrice?.message}>
              <input type="number" {...form.register('baseRentPrice')} className={inputCls} />
            </Field>
          </div>
          <Field label="Опис">
            <textarea {...form.register('description')} rows={2} className={inputCls} />
          </Field>
          <div className="flex gap-3 pt-2">
            <button type="button" onClick={() => setAddUnitOpen(false)}
              className="flex-1 px-4 py-2.5 border border-gray-200 rounded-xl text-sm text-gray-600 hover:bg-gray-50">
              Скасувати
            </button>
            <button type="submit" disabled={createUnitMutation.isPending}
              className="flex-1 bg-blue-600 hover:bg-blue-700 disabled:opacity-50 text-white text-sm font-medium px-4 py-2.5 rounded-xl flex items-center justify-center gap-2">
              {createUnitMutation.isPending && <Spinner />} Додати
            </button>
          </div>
        </form>
      </Modal>
    </div>
  );
}