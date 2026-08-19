import { Permission } from '@/api/appointme.schemas';
import { CalendarIcon, ChartPieIcon, LucideIcon, ShieldIcon, UserCogIcon, UsersIcon } from 'lucide-react';

export const navData: NavData = {
    navMain: [
        {
            title: 'Appointments',
            url: '/appointments',
            navId: 'appointments',
            icon: CalendarIcon,
            permission: 'appointments:view',
        },
        {
            title: 'Dashboard',
            url: '/dashboard',
            navId: 'dashboard',
            icon: ChartPieIcon,
            permission: ['appointments.statistics:view', 'customers.statistics:view'],
        },
        {
            title: 'Team',
            url: '/team',
            navId: 'team',
            icon: UserCogIcon,
            permission: 'employees:view',
        },
        {
            title: 'Customers',
            url: '/customers',
            navId: 'customers',
            icon: UsersIcon,
            permission: 'customers:view',
        },
    ],
    navSettings: [
        {
            title: 'Permissions',
            url: '/settings/permissions',
            navId: 'settings.permissions',
            icon: ShieldIcon,
            permission: 'permissions:manage',
        },
    ],
};

export interface NavData {
    navMain: NavItem[];
    navSettings: NavItem[];
}

export interface NavItem {
    title: string;
    url: string;
    navId: string;
    icon?: LucideIcon;
    /** A single permission, or an array meaning "any of these". */
    permission?: Permission | Permission[];
}
