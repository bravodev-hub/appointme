import { GetCurrentUserAccessResponse } from '@/api/appointme.schemas';
import { createContext, use } from 'react';

export const UserAccessContext = createContext<GetCurrentUserAccessResponse | null>(null);

export const useUserAccess = () => {
    const context = use(UserAccessContext);
    if (context === null) {
        throw new Error('useUserAccess must be used within a UserAccessProvider');
    }

    return context;
};
