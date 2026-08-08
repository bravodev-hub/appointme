import { useCurrentCompany } from '@/components/auth/use-current-company';

export const useTimeZone = (): string => {
    const { currentCompany } = useCurrentCompany();
    return currentCompany.timeZone;
};
