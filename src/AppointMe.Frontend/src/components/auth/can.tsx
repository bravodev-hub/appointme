import { Permission } from '@/api/appointme.schemas';
import { usePermission } from '@/components/auth';
import { ReactNode } from 'react';

interface CanProps {
    permission: Permission | Permission[];
    children: ReactNode;
    fallback?: ReactNode;
}

export const Can = ({ permission, children, fallback = null }: CanProps) => {
    const allowed = usePermission(permission);
    return allowed ? children : fallback;
};
