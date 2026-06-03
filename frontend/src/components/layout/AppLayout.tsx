import { NavLink, Outlet, useNavigate } from 'react-router-dom';
import { useAuthStore } from '../../store/authStore';
import { Building2, Home, FileText, Users, Wrench, BarChart3, LogOut, BellRing, CreditCard } from 'lucide-react';
import clsx from 'clsx';

const nav = [
  { to: '/dashboard',  icon: Home,       label: 'Дашборд' },
  { to: '/properties', icon: Building2,  label: "Об'єкти" },
  { to: '/contracts',  icon: FileText,   label: 'Договори' },
  { to: '/crm',        icon: Users,      label: 'CRM' },
  { to: '/finance',    icon: CreditCard, label: 'Фінанси' },
  { to: '/utilities',  icon: Wrench,     label: 'Комунальні' },
  { to: '/reports',    icon: BarChart3,  label: 'Звіти' },
];

export default function AppLayout() {
  const { user, logout } = useAuthStore();
  const navigate = useNavigate();

  return (
    <div className="flex h-screen bg-gray-50 overflow-hidden">
      <aside className="w-56 bg-white border-r border-gray-100 flex flex-col shrink-0">
        <div className="flex items-center gap-3 px-5 h-16 border-b border-gray-100">
          <div className="w-8 h-8 bg-blue-600 rounded-lg flex items-center justify-center">
            <Building2 className="w-4 h-4 text-white" />
          </div>
          <div>
            <p className="text-xs font-semibold text-gray-900 leading-tight">Rental</p>
            <p className="text-xs text-gray-400 leading-tight">Management</p>
          </div>
        </div>
        <nav className="flex-1 p-2.5 space-y-0.5 overflow-y-auto">
          {nav.map(({ to, icon: Icon, label }) => (
            <NavLink key={to} to={to}
              className={({ isActive }) => clsx(
                'flex items-center gap-3 px-3 py-2.5 rounded-xl text-sm transition-colors',
                isActive ? 'bg-blue-50 text-blue-700 font-medium' : 'text-gray-500 hover:bg-gray-50 hover:text-gray-800'
              )}>
              <Icon className="w-4 h-4 shrink-0" />
              {label}
            </NavLink>
          ))}
        </nav>
        <div className="p-2.5 border-t border-gray-100">
          <div className="flex items-center gap-3 px-2 py-2">
            <div className="w-7 h-7 bg-blue-100 rounded-full flex items-center justify-center text-xs font-semibold text-blue-600">
              {user?.fullName?.charAt(0) ?? 'U'}
            </div>
            <div className="flex-1 min-w-0">
              <p className="text-xs font-medium text-gray-900 truncate">{user?.fullName}</p>
              <p className="text-xs text-gray-400 truncate">{user?.role}</p>
            </div>
            <button onClick={() => { logout(); navigate('/login'); }} className="text-gray-400 hover:text-gray-600 transition-colors" title="Вийти">
              <LogOut className="w-4 h-4" />
            </button>
          </div>
        </div>
      </aside>
      <main className="flex-1 flex flex-col overflow-hidden">
        <header className="h-14 bg-white border-b border-gray-100 flex items-center justify-end px-6 shrink-0">
          <button className="relative text-gray-400 hover:text-gray-600 transition-colors">
            <BellRing className="w-5 h-5" />
            <span className="absolute -top-0.5 -right-0.5 w-2 h-2 bg-red-500 rounded-full" />
          </button>
        </header>
        <div className="flex-1 overflow-auto p-6">
          <Outlet />
        </div>
      </main>
    </div>
  );
}
