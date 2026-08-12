import { AppProviders } from '@/app/providers';
import { Outlet } from 'react-router';

export const RootLayout = () => {
    return (
        <AppProviders>
            <Outlet />
        </AppProviders>
    );
};
