import { Permission } from '@/api/appointme.schemas';
import { useUserAccess } from '@/components/auth/use-user-access';

export const hasPermission = (granted: readonly string[], required: Permission | Permission[]): boolean =>
    Array.isArray(required) ? required.some(permission => granted.includes(permission)) : granted.includes(required);

export const usePermission = (permission: Permission | Permission[]) => {
    const { permissions } = useUserAccess();
    return hasPermission(permissions, permission);
};
