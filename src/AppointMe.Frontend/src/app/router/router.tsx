import { AnonymousRoutes, AppRoutes, AppShell, InvitationRoutes, OnboardingRoutes } from './app-shell';
import { Appointments } from '@/app/appointments';
import { Login } from '@/app/auth/login';
import { Signup, VerifyEmail } from '@/app/auth/signup';
import { CreateCustomer, CustomerProfile, Customers } from '@/app/customers';
import { Invitations } from '@/app/invitations';
import { Layout, RootLayout } from '@/app/layouts';
import { NotFound } from '@/app/not-found';
import { Onboarding } from '@/app/onboarding';
import { Permissions } from '@/app/settings/permissions';
import { Team } from '@/app/team';
import { CurrentCompanyProvider, UserAccessProvider } from '@/components/auth';
import { ErrorScreen } from '@/components/error';
import { ModalDialogProvider } from '@/components/modal-dialog';
import { Spinner } from '@/components/ui';
import { Suspense } from 'react';
import { Navigate, createBrowserRouter } from 'react-router';

export const router = createBrowserRouter([
    {
        element: <RootLayout />,
        errorElement: <ErrorScreen />,
        children: [
            {
                element: <AppShell />,
                children: [
                    { path: '/not-found', element: <NotFound /> },

                    // Anonymous-only routes
                    {
                        element: <AnonymousRoutes />,
                        children: [
                            { path: '/auth/login', element: <Login /> },
                            { path: '/auth/signup', element: <Signup /> },
                            { path: '/auth/verify-email/:email?', element: <VerifyEmail /> },
                        ],
                    },

                    // Onboarding (no company yet)
                    {
                        element: <OnboardingRoutes />,
                        children: [{ path: '/onboarding', element: <Onboarding /> }],
                    },

                    // Pending invitations
                    {
                        element: <InvitationRoutes />,
                        children: [{ path: '/invitations', element: <Invitations /> }],
                    },

                    // Main app (active company membership required)
                    {
                        element: <AppRoutes />,
                        children: [
                            {
                                element: (
                                    <CurrentCompanyProvider>
                                        <Suspense
                                            fallback={
                                                <div className='flex h-dvh items-center justify-center'>
                                                    <Spinner className='size-8' />
                                                </div>
                                            }
                                        >
                                            <UserAccessProvider>
                                                <ModalDialogProvider>
                                                    <Layout />
                                                </ModalDialogProvider>
                                            </UserAccessProvider>
                                        </Suspense>
                                    </CurrentCompanyProvider>
                                ),
                                path: '/',
                                children: [
                                    {
                                        index: true,
                                        element: <Navigate to='/appointments' replace />,
                                    },
                                    {
                                        path: 'appointments',
                                        element: <Appointments />,
                                        handle: { breadcrumb: 'Appointments', navId: 'appointments' },
                                    },
                                    {
                                        path: 'team',
                                        element: <Team />,
                                        handle: { breadcrumb: 'Team', navId: 'team' },
                                    },
                                    {
                                        path: 'customers',
                                        handle: { breadcrumb: 'Customers', navId: 'customers' },
                                        children: [
                                            { index: true, element: <Customers /> },
                                            {
                                                path: 'new',
                                                element: <CreateCustomer />,
                                                handle: { breadcrumb: 'New customer' },
                                            },
                                            {
                                                path: ':id',
                                                element: <CustomerProfile />,
                                                handle: {
                                                    breadcrumb: 'Profile',
                                                },
                                            },
                                        ],
                                    },
                                    {
                                        path: 'settings',
                                        handle: { breadcrumb: 'Settings', group: true },
                                        children: [
                                            {
                                                path: 'permissions',
                                                element: <Permissions />,
                                                handle: { breadcrumb: 'Permissions', navId: 'settings.permissions' },
                                            },
                                        ],
                                    },
                                    { path: '*', element: <NotFound /> },
                                ],
                            },
                            { path: '*', element: <Navigate to='/' replace /> },
                        ],
                    },
                ],
            },
        ],
    },
]);
