import { useState } from 'react';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { ownersApi } from '../../api';
import { Plus, Search, Pencil, Trash2, Phone, Mail } from 'lucide-react';
import { Badge, Modal, Field, inputCls, EmptyState, Pagination, Spinner, Table, Tr, Td } from '../../components/ui';
import type { OwnerDto } from '../../types';

const schema = z.object({
  firstName: z.string().min(1, "Обов'язкове"),
  lastName: z.string().min(1, "Обов'язкове"),
  middleName: z.string().optional(),
  taxCode: z.string().optional(),
  isCompany: z.boolean(),
  companyName: z.string().optional(),
  email: z.string().email('Невірний email'),
  phone: z.string().min(5, "Обов'язкове"),
  managementFeePercent: z.coerce.number().min(0).max(100).optional(),
});
type FormData = z.infer<typeof schema>;

const defaultValues: FormData = {
  firstName: '', lastName: '', middleName: '', taxCode: '',
  isCompany: false, companyName: '', email: '', phone: '', managementFeePercent: undefined,
};

export default function OwnersPage() {
  const [search, setSearch] = useState('');
  const [page, setPage] = useState(1);
  const [modalOpen, setModalOpen] = useState(false);
  const [editing, setEditing] = useState<OwnerDto | null>(null);
  const qc = useQueryClient();

  const { data, isLoading } = useQuery({
    queryKey: ['owners', page, search],
    queryFn: () => ownersApi.getAll(),
  });

  const { register, handleSubmit, reset, watch, formState: { errors, isSubmitting } } = useForm<FormData>({
    resolver: zodResolver(schema), defaultValues,
  });
  const isCompany = watch('isCompany');

  const createMutation = useMutation({
    mutationFn: (d: FormData) => ownersApi.create(d as any),
    onSuccess: () => { qc.invalidateQueries({ queryKey: ['owners'] }); closeModal(); },
  });

  const openCreate = () => { setEditing(null); reset(defaultValues); setModalOpen(true); };
  const openEdit = (o: OwnerDto) => {
    setEditing(o);
    reset({ firstName: o.firstName, lastName: o.lastName, email: o.email, phone: o.phone, isCompany: o.isCompany, companyName: o.companyName ?? '', managementFeePercent: o.managementFeePercent ?? undefined });
    setModalOpen(true);
  };
  const closeModal = () => { setModalOpen(false); setEditing(null); reset(defaultValues); };
  const onSubmit = (d: FormData) => createMutation.mutate(d);

  const items = data?.items ?? (data as any) ?? [];

  return (
    <div className="space-y-5">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-xl font-semibold text-gray-900">Власники</h1>
          <p className="text-sm text-gray-500 mt-0.5">{items.length} записів</p>
        </div>
        <button onClick={openCreate}
          className="flex items-center gap-2 bg-blue-600 hover:bg-blue-700 text-white text-sm font-medium px-4 py-2.5 rounded-xl transition-colors">
          <Plus className="w-4 h-4" /> Додати власника
        </button>
      </div>

      <div className="relative">
        <Search className="absolute left-4 top-1/2 -translate-y-1/2 w-4 h-4 text-gray-400" />
        <input value={search} onChange={e => setSearch(e.target.value)}
          placeholder="Пошук власника..."
          className="w-full pl-11 pr-4 py-2.5 bg-white border border-gray-200 rounded-xl text-sm focus:outline-none focus:ring-2 focus:ring-blue-500" />
      </div>

      {isLoading ? (
        <div className="flex justify-center py-16"><Spinner size="lg" /></div>
      ) : !items.length ? (
        <EmptyState title="Власників не знайдено" action={<button onClick={openCreate} className="text-sm text-blue-600 hover:underline">Додати власника</button>} />
      ) : (
        <Table headers={['Власник', 'Контакти', 'ІПН', "Об'єктів", 'Комісія', '']}>
          {items.filter((o: OwnerDto) => !search || o.fullName.toLowerCase().includes(search.toLowerCase())).map((o: OwnerDto) => (
            <Tr key={o.id}>
              <Td>
                <div>
                  <p className="font-medium text-gray-900">{o.fullName}</p>
                  {o.isCompany && <Badge variant="purple">Юр. особа</Badge>}
                </div>
              </Td>
              <Td>
                <div className="space-y-0.5">
                  <div className="flex items-center gap-1.5 text-xs text-gray-600"><Phone className="w-3 h-3" />{o.phone}</div>
                  <div className="flex items-center gap-1.5 text-xs text-gray-500"><Mail className="w-3 h-3" />{o.email}</div>
                </div>
              </Td>
              <Td><span className="text-gray-500 text-sm">{o.taxCode || '—'}</span></Td>
              <Td><Badge variant="blue">{o.propertiesCount}</Badge></Td>
              <Td><span className="text-gray-700 text-sm">{o.managementFeePercent ? `${o.managementFeePercent}%` : '—'}</span></Td>
              <Td>
                <div className="flex gap-1 justify-end">
                  <button onClick={() => openEdit(o)} className="p-1.5 text-gray-400 hover:text-blue-600 hover:bg-blue-50 rounded-lg transition-colors"><Pencil className="w-3.5 h-3.5" /></button>
                </div>
              </Td>
            </Tr>
          ))}
        </Table>
      )}

      <Modal open={modalOpen} onClose={closeModal} title={editing ? 'Редагувати власника' : 'Новий власник'} size="lg">
        <form onSubmit={handleSubmit(onSubmit)} className="space-y-4">
          <label className="flex items-center gap-2 cursor-pointer">
            <input type="checkbox" {...register('isCompany')} className="w-4 h-4 rounded" />
            <span className="text-sm text-gray-700">Юридична особа</span>
          </label>
          {isCompany && (
            <Field label="Назва компанії" error={errors.companyName?.message}>
              <input {...register('companyName')} className={inputCls} />
            </Field>
          )}
          <div className="grid grid-cols-3 gap-3">
            <Field label="Прізвище" error={errors.lastName?.message} required>
              <input {...register('lastName')} className={inputCls} />
            </Field>
            <Field label="Ім'я" error={errors.firstName?.message} required>
              <input {...register('firstName')} className={inputCls} />
            </Field>
            <Field label="По батькові">
              <input {...register('middleName')} className={inputCls} />
            </Field>
          </div>
          <div className="grid grid-cols-2 gap-3">
            <Field label="Email" error={errors.email?.message} required>
              <input {...register('email')} type="email" className={inputCls} />
            </Field>
            <Field label="Телефон" error={errors.phone?.message} required>
              <input {...register('phone')} className={inputCls} />
            </Field>
          </div>
          <div className="grid grid-cols-2 gap-3">
            <Field label="ІПН / ЄДРПОУ">
              <input {...register('taxCode')} className={inputCls} />
            </Field>
            <Field label="Комісія управління (%)" error={errors.managementFeePercent?.message}>
              <input {...register('managementFeePercent')} type="number" step="0.1" min="0" max="100" className={inputCls} />
            </Field>
          </div>
          <div className="flex gap-3 pt-2">
            <button type="button" onClick={closeModal} className="flex-1 px-4 py-2.5 border border-gray-200 rounded-xl text-sm text-gray-600 hover:bg-gray-50 transition-colors">Скасувати</button>
            <button type="submit" disabled={isSubmitting} className="flex-1 bg-blue-600 hover:bg-blue-700 disabled:opacity-50 text-white text-sm font-medium px-4 py-2.5 rounded-xl transition-colors flex items-center justify-center gap-2">
              {isSubmitting && <Spinner />}{editing ? 'Зберегти' : 'Додати'}
            </button>
          </div>
        </form>
      </Modal>
    </div>
  );
}
