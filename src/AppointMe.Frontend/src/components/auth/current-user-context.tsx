import { useGetCurrentUserSuspense } from '@/api/appointme';
import { GetCurrentUserResponse } from '@/api/appointme.schemas';
import { type ReactNode, createContext, use } from 'react';

const CurrentUserContext = createContext<GetCurrentUserResponse | null>(null);

export const CurrentUserProvider = ({ children }: { children: ReactNode }) => {
    const { data } = useGetCurrentUserSuspense();
    if (!data) {
        return null;
    }

    return <CurrentUserContext value={data}>{children}</CurrentUserContext>;
};

export const useCurrentUser = () => {
    const context = use(CurrentUserContext);
    if (context === null) {
        throw new Error('useCurrentUser must be used within a CurrentUserProvider');
    }

    return context;
};
