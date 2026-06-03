import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { propertiesApi, ownersApi } from '../../api';
import { Plus, Search, Building2, Pencil, Trash2, MapPin, User } from 'lucide-react';
import { Modal, Field, inputCls, EmptyState, Spinner } from '../../components/ui';
import type { PropertyDto, OwnerDto } from '../../types';

// ─── СХЕМА ОБ'ЄКТА НЕРУХОМОСТІ (ПЕРЕВЕДЕНО НА ЧИСЛА) ───────────
const propertySchema = z.object({
  name: z.string().min(1, "Обов'язкове"),
  address: z.string().min(1, "Обов'язкове"),
  city: z.string().min(1, "Обов'язкове"),
  type: z.coerce.number().default(0), // 0 = Residential, і т.д.
  totalArea: z.coerce.number().min(1, "Має бути більше 0"),
  floorsCount: z.coerce.number().min(1, "Має бути хоча б 1 поверх"),
  ownerId: z.string().min(1, "Оберіть власника"),
  status: z.coerce.number().default(0), // 0 = Available, і т.д.
});
type PropertyFormData = z.infer<typeof propertySchema>;

// ─── СХЕМА ДЛЯ ПРИМІЩЕННЯ (UNIT) ─────────────────────────────
const unitSchema = z.object({
  number: z.string().min(1, "Обов'язковий номер"),
  floor: z.coerce.number().min(-5, "Некоректний поверх"),
  area: z.coerce.number().min(1, "Площа має бути більше 0"),
  roomsCount: z.coerce.number().min(1, "Мінімум 1 кімната"),
  baseRentPrice: z.coerce.number().min(0, "Ціна не може бути від'ємною"),
  description: z.string().optional(),
});
type UnitFormData = z.infer<typeof unitSchema>;

const propertyDefaultValues: any = {
  name: '',
  address: '',
  city: 'Львів',
  type: 0,
  totalArea: undefined as any,
  floorsCount: 1,
  ownerId: '',
  status: 0,
};

const unitDefaultValues: UnitFormData = {
  number: '',
  floor: 1,
  area: undefined as any,
  roomsCount: 1,
  baseRentPrice: undefined as any,
  description: '',
};

const STATUS_LABELS: Record<string, { label: string; cls: string }> = {
  '0': { label: 'Доступний', cls: 'bg-green-100 text-green-700' },
  'Available': { label: 'Доступний', cls: 'bg-green-100 text-green-700' },
  '1': { label: 'Зайнятий', cls: 'bg-blue-100 text-blue-700' },
  'Rented': { label: 'Зайнятий', cls: 'bg-blue-100 text-blue-700' },
  '2': { label: 'Ремонт', cls: 'bg-yellow-100 text-yellow-700' },
  'UnderMaintenance': { label: 'Ремонт', cls: 'bg-yellow-100 text-yellow-700' },
  '3': { label: 'Зарезервовано', cls: 'bg-purple-100 text-purple-700' },
  'Reserved': { label: 'Зарезервовано', cls: 'bg-purple-100 text-purple-700' },
};

export default function PropertiesPage() {
  const navigate = useNavigate();
  const [search, setSearch] = useState('');
  const [page, setPage] = useState(1);
  const qc = useQueryClient();

  // Стан для модалки об'єкта
  const [propertyModalOpen, setPropertyModalOpen] = useState(false);
  const [editing, setEditing] = useState<PropertyDto | null>(null);

  // Стан для модалки додавання приміщення (Unit)
  const [unitModalOpen, setUnitModalOpen] = useState(false);
  const [selectedPropertyForUnit, setSelectedPropertyForUnit] = useState<PropertyDto | null>(null);

  // Завантаження даних
  const { data, isLoading } = useQuery({
    queryKey: ['properties', page, search],
    queryFn: () => propertiesApi.getAll({ page, pageSize: 12, search: search || undefined }),
  });

  const { data: ownersData } = useQuery({
    queryKey: ['owners-list'],
    queryFn: () => ownersApi.getAll(),
  });
  const owners = ownersData?.items ?? (Array.isArray(ownersData) ? ownersData : []);

  // Форми
  const propertyForm = useForm<PropertyFormData>({
    resolver: zodResolver(propertySchema),
    defaultValues: propertyDefaultValues,
  });

  const unitForm = useForm<UnitFormData>({
    resolver: zodResolver(unitSchema),
    defaultValues: unitDefaultValues,
  });

  // Мутація збереження об'єкта
  const savePropertyMutation = useMutation({
    mutationFn: (d: PropertyFormData) => {
      const payload = {
        name: d.name,
        address: d.address,
        city: d.city,
        totalArea: d.totalArea,
        floorsCount: d.floorsCount,
        status: d.status,
        type: d.type,
        ownerId: d.ownerId,
        description: '', // Передаємо порожній опис про всяк випадок для бекенду
      };

      if (editing) {
        return propertiesApi.update(editing.id, payload);
      }
      return propertiesApi.create(payload);
    },
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['properties'] });
      closePropertyModal();
    },
    onError: (error: any) => {
      // Якщо знов вилетить 400 помилка, ми виведемо точні поля, які не пройшли валідацію
      const validationErrors = error?.response?.data?.errors;
      if (validationErrors) {
        const errorMessages = Object.entries(validationErrors)
          .map(([field, messages]) => `${field}: ${(messages as string[]).join(', ')}`)
          .join('\n');
        alert(`Помилка валідації на бекенді:\n${errorMessages}`);
      } else {
        alert(`Помилка: ${error?.response?.data?.title || error.message || 'Не вдалося зберегти об\'єкт'}`);
      }
    }
  });

  // Мутація для створення приміщення (Unit)
  const createUnitMutation = useMutation({
    mutationFn: (d: UnitFormData) => {
      if (!selectedPropertyForUnit) throw new Error("Property not selected");
      return propertiesApi.createUnit({
        ...d,
        propertyId: selectedPropertyForUnit.id
      });
    },
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['properties'] });
      closeUnitModal();
    },
  });

  // Видалення об'єкта
  const deleteMutation = useMutation({
    mutationFn: propertiesApi.delete,
    onSuccess: () => qc.invalidateQueries({ queryKey: ['properties'] }),
  });

  // Керування модалками
  const openCreateProperty = () => {
    setEditing(null);
    propertyForm.reset(propertyDefaultValues);
    setPropertyModalOpen(true);
  };

  const openEditProperty = (e: React.MouseEvent, p: PropertyDto) => {
    e.stopPropagation();
    setEditing(p);

    // При редагуванні підставляємо числові значення (або дефолтні, якщо прийшов null/рядок)
    const currentStatus = typeof p.status === 'string' ? (Object.keys(STATUS_LABELS).indexOf(p.status) || 0) : (p.status ?? 0);

    propertyForm.reset({
      name: p.name,
      address: p.address,
      city: p.city,
      type: typeof p.type === 'number' ? p.type : 0,
      totalArea: p.totalArea,
      floorsCount: p.floorsCount ?? 1,
      ownerId: p.ownerId || '',
      status: currentStatus,
    });
    setPropertyModalOpen(true);
  };

  const closePropertyModal = () => {
    setPropertyModalOpen(false);
    setEditing(null);
    propertyForm.reset(propertyDefaultValues);
  };

  const openCreateUnit = (e: React.MouseEvent, p: PropertyDto) => {
    e.stopPropagation();
    setSelectedPropertyForUnit(p);
    unitForm.reset(unitDefaultValues);
    setUnitModalOpen(true);
  };

  const closeUnitModal = () => {
    setUnitModalOpen(false);
    setSelectedPropertyForUnit(null);
    unitForm.reset(unitDefaultValues);
  };

  const items = data?.items ?? [];

  return (
    <div className="space-y-5">
      {/* Шапка */}
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-xl font-semibold text-gray-900">Об'єкти нерухомості</h1>
          <p className="text-sm text-gray-500 mt-0.5">{data?.totalCount ?? items.length} об'єктів загалом</p>
        </div>
        <button
          onClick={openCreateProperty}
          className="flex items-center gap-2 bg-blue-600 hover:bg-blue-700 text-white text-sm font-medium px-4 py-2.5 rounded-xl transition-colors"
        >
          <Plus className="w-4 h-4" /> Додати об'єкт
        </button>
      </div>

      {/* Пошук */}
      <div className="relative">
        <Search className="absolute left-4 top-1/2 -translate-y-1/2 w-4 h-4 text-gray-400" />
        <input
          value={search}
          onChange={(e) => {
            setSearch(e.target.value);
            setPage(1);
          }}
          placeholder="Пошук за назвою або адресою..."
          className="w-full pl-11 pr-4 py-2.5 bg-white border border-gray-200 rounded-xl text-sm focus:outline-none focus:ring-2 focus:ring-blue-500"
        />
      </div>

      {/* Список об'єктів */}
      {isLoading ? (
        <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
          {Array.from({ length: 6 }).map((_, i) => (
            <div key={i} className="bg-white rounded-2xl border border-gray-100 p-5 animate-pulse h-52" />
          ))}
        </div>
      ) : !items.length ? (
        <EmptyState
          title="Об'єктів не знайдено"
          action={
            <button onClick={openCreateProperty} className="text-sm text-blue-600 hover:underline">
              Додати перший об'єкт
            </button>
          }
        />
      ) : (
        <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
          {items.map((p) => {
            const statusInfo = STATUS_LABELS[p.status] ?? { label: 'Доступний', cls: 'bg-green-100 text-green-700' };
            const propertyOwner = p.ownerName || owners.find((o: OwnerDto) => o.id === p.ownerId)?.fullName || '—';

            return (
              <div
                key={p.id}
                onClick={() => navigate(`/properties/${p.id}`)}
                className="cursor-pointer bg-white rounded-2xl border border-gray-100 p-5 hover:border-blue-200 hover:shadow-sm transition-all flex flex-col justify-between min-h-[220px]"
              >
                <div>
                  <div className="flex items-start justify-between mb-3">
                    <div className="w-10 h-10 bg-blue-50 rounded-xl flex items-center justify-center">
                      <Building2 className="w-5 h-5 text-blue-600" />
                    </div>
                    <span className={`text-xs font-medium px-2.5 py-1 rounded-full ${statusInfo.cls}`}>
                      {statusInfo.label}
                    </span>
                  </div>

                  <h3 className="font-medium text-gray-900 text-sm hover:text-blue-600 transition-colors">{p.name}</h3>
                  <p className="text-xs text-gray-400 mt-1 truncate flex items-center gap-1">
                    <MapPin className="w-3 h-3 flex-shrink-0" /> {p.address}, {p.city}
                  </p>
                  <p className="text-xs text-gray-500 mt-1 truncate flex items-center gap-1">
                    <User className="w-3 h-3 flex-shrink-0 text-gray-400" /> 
                    Власник: <span className="font-medium text-gray-700">{propertyOwner}</span>
                  </p>
                </div>

                <div className="mt-4 pt-3 border-t border-gray-50 flex items-center justify-between">
                  <div className="text-xs text-gray-500">
                    Площа: <span className="font-semibold text-gray-700">{p.totalArea} м²</span>
                  </div>
                  <div className="flex items-center gap-1">
                    <button
                      onClick={(e) => openCreateUnit(e, p)}
                      title="Додати площу / приміщення"
                      className="flex items-center gap-1 px-2.5 py-1.5 bg-gray-50 hover:bg-blue-50 text-gray-600 hover:text-blue-600 text-xs font-medium rounded-lg transition-colors mr-1"
                    >
                      <Plus className="w-3 h-3" /> Площа
                    </button>
                    
                    <button
                      onClick={(e) => openEditProperty(e, p)}
                      className="p-1.5 text-gray-400 hover:text-blue-600 hover:bg-blue-50 rounded-lg transition-colors"
                    >
                      <Pencil className="w-3.5 h-3.5" />
                    </button>
                    <button
                      onClick={(e) => {
                        e.stopPropagation();
                        if (confirm('Видалити цей об\'єкт?')) deleteMutation.mutate(p.id);
                      }}
                      className="p-1.5 text-gray-400 hover:text-red-600 hover:bg-red-50 rounded-lg transition-colors"
                    >
                      <Trash2 className="w-3.5 h-3.5" />
                    </button>
                  </div>
                </div>
              </div>
            );
          })}
        </div>
      )}

      {/* МОДАЛКА: СТВОРЕННЯ / РЕДАГУВАННЯ ОБ'ЄКТА */}
      <Modal
        open={propertyModalOpen}
        onClose={closePropertyModal}
        title={editing ? "Редагувати об'єкт" : "Новий об'єкт нерухомості"}
        size="lg"
      >
        <form onSubmit={propertyForm.handleSubmit((d) => savePropertyMutation.mutate(d))} className="space-y-4">
          <Field label="Назва об'єкта" error={propertyForm.formState.errors.name?.message} required>
            <input {...propertyForm.register('name')} placeholder="Напр. ЖК Магнолія" className={inputCls} />
          </Field>
          <div className="grid grid-cols-3 gap-3">
            <div className="col-span-2">
              <Field label="Адреса" error={propertyForm.formState.errors.address?.message} required>
                <input {...propertyForm.register('address')} className={inputCls} />
              </Field>
            </div>
            <Field label="Місто" error={propertyForm.formState.errors.city?.message} required>
              <input {...propertyForm.register('city')} className={inputCls} />
            </Field>
          </div>
          <div className="grid grid-cols-3 gap-3">
            <Field label="Загальна площа (м²)" error={propertyForm.formState.errors.totalArea?.message} required>
              <input {...propertyForm.register('totalArea')} type="number" className={inputCls} />
            </Field>
            <Field label="Кількість поверхів" error={propertyForm.formState.errors.floorsCount?.message} required>
              <input {...propertyForm.register('floorsCount')} type="number" min="1" className={inputCls} />
            </Field>
            <Field label="Тип нерухомості" error={propertyForm.formState.errors.type?.message} required>
              <select {...propertyForm.register('type')} className={inputCls}>
                <option value={0}>Житлова</option>
                <option value={1}>Комерційна</option>
                <option value={2}>Індустріальна</option>
                <option value={3}>Земельна ділянка</option>
              </select>
            </Field>
          </div>
          <div className="grid grid-cols-2 gap-3">
            <Field label="Статус об'єкта" error={propertyForm.formState.errors.status?.message} required>
              <select {...propertyForm.register('status')} className={inputCls}>
                <option value={0}>Доступний</option>
                <option value={1}>Зайнятий</option>
                <option value={2}>Ремонт</option>
                <option value={3}>Зарезервовано</option>
              </select>
            </Field>
            <Field label="Власник об'єкта" error={propertyForm.formState.errors.ownerId?.message} required>
              <select {...propertyForm.register('ownerId')} disabled={!!editing} className={inputCls}>
                <option value="">-- Оберіть власника --</option>
                {owners.map((o: OwnerDto) => (
                  <option key={o.id} value={o.id}>
                    {o.fullName}
                  </option>
                ))}
              </select>
            </Field>
          </div>
          <div className="flex gap-3 pt-2">
            <button
              type="button"
              onClick={closePropertyModal}
              className="flex-1 px-4 py-2.5 border border-gray-200 rounded-xl text-sm text-gray-600 hover:bg-gray-50"
            >
              Скасувати
            </button>
            <button
              type="submit"
              disabled={savePropertyMutation.isPending}
              className="flex-1 bg-blue-600 text-white rounded-xl text-sm font-medium flex items-center justify-center gap-2"
            >
              {savePropertyMutation.isPending && <Spinner />}Зберегти
            </button>
          </div>
        </form>
      </Modal>

      {/* МОДАЛКА: ДОДАННЯ ПРИМІЩЕННЯ (UNIT) */}
      <Modal
        open={unitModalOpen}
        onClose={closeUnitModal}
        title={`Додати приміщення до: ${selectedPropertyForUnit?.name}`}
        size="md"
      >
        <form onSubmit={unitForm.handleSubmit((d) => createUnitMutation.mutate(d))} className="space-y-4">
          <div className="grid grid-cols-2 gap-3">
            <Field label="Номер приміщення / Офісу" error={unitForm.formState.errors.number?.message} required>
              <input {...unitForm.register('number')} placeholder="Напр. 101, 4-B" className={inputCls} />
            </Field>
            <Field label="Поверх" error={unitForm.formState.errors.floor?.message} required>
              <input {...unitForm.register('floor')} type="number" className={inputCls} />
            </Field>
          </div>

          <div className="grid grid-cols-2 gap-3">
            <Field label="Площа приміщення (м²)" error={unitForm.formState.errors.area?.message} required>
              <input {...unitForm.register('area')} type="number" step="0.1" className={inputCls} />
            </Field>
            <Field label="Кількість кімнат" error={unitForm.formState.errors.roomsCount?.message} required>
              <input {...unitForm.register('roomsCount')} type="number" min="1" className={inputCls} />
            </Field>
          </div>

          <Field label="Базова ціна оренди (грн/міс)" error={unitForm.formState.errors.baseRentPrice?.message} required>
            <input {...unitForm.register('baseRentPrice')} type="number" placeholder="25000" className={inputCls} />
          </Field>

          <Field label="Опис / Нотатки" error={unitForm.formState.errors.description?.message}>
            <textarea {...unitForm.register('description')} rows={3} placeholder="Додаткові відомості про стан площі..." className={inputCls} />
          </Field>

          <div className="flex gap-3 pt-2">
            <button
              type="button"
              onClick={closeUnitModal}
              className="flex-1 px-4 py-2.5 border border-gray-200 rounded-xl text-sm text-gray-600 hover:bg-gray-50"
            >
              Скасувати
            </button>
            <button
              type="submit"
              disabled={createUnitMutation.isPending}
              className="flex-1 bg-blue-600 text-white rounded-xl text-sm font-medium flex items-center justify-center gap-2"
            >
              {createUnitMutation.isPending && <Spinner />}Створити площу
            </button>
          </div>
        </form>
      </Modal>
    </div>
  );
}