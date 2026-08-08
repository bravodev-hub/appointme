import { UserAccessContext } from './use-user-access';
import { useGetCurrentUserAccessSuspense } from '@/api/appointme';
import { type ReactNode } from 'react';

export const UserAccessProvider = ({ children }: { children: ReactNode }) => {
    const { data } = useGetCurrentUserAccessSuspense();
    return <UserAccessContext value={data}>{children}</UserAccessContext>;
};
