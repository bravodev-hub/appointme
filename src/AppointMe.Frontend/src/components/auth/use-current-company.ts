import { CompanyMembership } from '@/api/appointme.schemas';
import { createContext, use } from 'react';

export interface CurrentCompanyContextValue {
    currentCompany: CompanyMembership;
    setCurrentCompany: (companyId: string) => Promise<void>;
}

export const CurrentCompanyContext = createContext<CurrentCompanyContextValue | null>(null);

export const useCurrentCompany = () => {
    const context = use(CurrentCompanyContext);
    if (context === null) {
        throw new Error('useCurrentCompany must be used within a CurrentCompanyProvider');
    }
    return context;
};
