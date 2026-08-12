import { ThemeProvider } from '@/components/theme';
import { NuqsAdapter } from 'nuqs/adapters/react-router/v8';
import { ReactNode } from 'react';

export const AppProviders = ({ children }: { children: ReactNode }) => {
    return (
        <NuqsAdapter>
            <ThemeProvider>{children}</ThemeProvider>
        </NuqsAdapter>
    );
};
