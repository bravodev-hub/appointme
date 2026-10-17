import { GetCurrentUserResponse } from '@/api/client.schemas';
import { createContext, use } from 'react';

export const CurrentUserContext = createContext<GetCurrentUserResponse | null>(null);

export const useCurrentUser = () => {
    const context = use(CurrentUserContext);
    if (context === null) {
        throw new Error('useCurrentUser must be used within a CurrentUserProvider');
    }

    return context;
};
