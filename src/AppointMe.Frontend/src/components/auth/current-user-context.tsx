import { CurrentUserContext } from './use-current-user';
import { useGetCurrentUserSuspense } from '@/api/appointme';
import { type ReactNode } from 'react';

export const CurrentUserProvider = ({ children }: { children: ReactNode }) => {
    const { data } = useGetCurrentUserSuspense();
    if (!data) {
        return null;
    }

    return <CurrentUserContext value={data}>{children}</CurrentUserContext>;
};
