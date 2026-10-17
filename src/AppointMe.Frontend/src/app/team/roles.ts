import { Role } from '@/api/client.schemas.ts';
import * as z from 'zod';

export const EMPLOYEE_ROLES = [
    { value: Role.Owner, description: 'Full access to all company settings and data' },
    { value: Role.Manager, description: 'Can manage employees, schedules, and appointments' },
    { value: Role.Staff, description: 'Can view schedules and manage own appointments' },
    { value: Role.Receptionist, description: 'Can manage bookings and customer check-ins' },
] as const;

export const roleSchema = z.enum(Object.values(Role) as [Role, ...Role[]]);
