import { router } from '@/app/router';
import { ErrorBoundary } from '@/components/error';
import { queryClient } from '@/lib/query-client';
import { QueryClientProvider } from '@tanstack/react-query';
import { ReactQueryDevtools } from '@tanstack/react-query-devtools';
import { StrictMode } from 'react';
import { RouterProvider } from 'react-router/dom';

export const App = () => {
    return (
        <StrictMode>
            <QueryClientProvider client={queryClient}>
                <ErrorBoundary>
                    <RouterProvider router={router} />
                </ErrorBoundary>

                {import.meta.env.DEV && <ReactQueryDevtools initialIsOpen={false} />}
            </QueryClientProvider>
        </StrictMode>
    );
};
