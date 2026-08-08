import { Permission } from '@/api/appointme.schemas.ts';

const RESOURCE_LABELS: Record<string, string> = {
    customers: 'Customers',
    employees: 'Team members',
    invitations: 'Invitations',
    permissions: 'Role permissions',
    appointments: 'Appointments',
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
    'appointments:view': 'View appointments',
    'appointments:schedule': 'Schedule appointments',
    'appointments:reschedule': 'Reschedule appointments',
    'appointments:cancel': 'Cancel appointments',
};

export const labelForPermission = (key: string) => PERMISSION_LABELS[key as Permission] ?? key;

export const labelForResource = (name: string) => RESOURCE_LABELS[name] ?? name;
