import { Permission } from '@/api/appointme.schemas';
import { useUserAccess } from '@/components/auth/use-user-access';

export const usePermission = (permission: Permission) => {
    const { permissions } = useUserAccess();
    return permissions.includes(permission);
};
