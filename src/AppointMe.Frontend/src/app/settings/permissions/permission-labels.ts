import { Permission } from '@/api/appointme.schemas.ts';

const RESOURCE_LABELS: Record<string, string> = {
    customers: 'Customers',
    employees: 'Team members',
    invitations: 'Invitations',
    permissions: 'Role permissions',
    appointments: 'Appointments',
    'appointments.statistics': 'Appointment statistics',
    'customers.statistics': 'Customer statistics',
};

const PERMISSION_LABELS: Record<Permission, string> = {
    'employees:view': 'View team members',
    'employees:invite': 'Invite employees',
    'employees:remove': 'Remove employees',
    'employees:update_roles': 'Manage employee roles',
    'employees:manage_owners': 'Manage company owners',
    'invitations:resend': 'Resend invitations',
    'invitations:cancel': 'Cancel invitations',
    'permissions:view': 'View role permissions',
    'permissions:manage': 'Manage role permissions',
    'customers:view': 'View customers',
    'customers:create': 'Create customers',
    'customers:update': 'Update customers',
    'customers:delete': 'Delete customers',
    'customers.statistics:view': 'View customer statistics',
    'appointments:view': 'View appointments',
    'appointments:schedule': 'Schedule appointments',
    'appointments:reschedule': 'Reschedule appointments',
    'appointments:cancel': 'Cancel appointments',
    'appointments.statistics:view': 'View appointment statistics',
};

export const labelForPermission = (key: string) => PERMISSION_LABELS[key as Permission] ?? key;

export const labelForResource = (name: string) => RESOURCE_LABELS[name] ?? name;
