import { CurrentUserProvider, useCurrentUser } from '@/components/auth';
import { Spinner } from '@/components/ui';
import { Suspense } from 'react';
import { Navigate, Outlet, useLocation } from 'react-router';

type UserState = 'anonymous' | 'needs-onboarding' | 'has-invitations' | 'active';

const useUserState = (): UserState => {
    const currentUser = useCurrentUser();

    if (!currentUser.isAuthenticated) {
        return 'anonymous';
    }

    if (currentUser.hasMembership) {
        return 'active';
    }

    if (currentUser.hasPendingInvitations) {
        return 'has-invitations';
    }

    return 'needs-onboarding';
};

const stateRoutes: Record<UserState, string> = {
    anonymous: '/auth/login',
    'needs-onboarding': '/onboarding',
    'has-invitations': '/invitations',
    active: '/',
};

const StateRouter = ({ allow }: { allow: UserState[] }) => {
    const state = useUserState();
    const location = useLocation();

    if (allow.includes(state)) {
        return <Outlet />;
    }

    return <Navigate to={stateRoutes[state]} replace state={{ from: location }} />;
};

/** Routes only accessible to unauthenticated users. */
export const AnonymousRoutes = () => <StateRouter allow={['anonymous']} />;

/** Routes only accessible to users without a company (onboarding). */
export const OnboardingRoutes = () => <StateRouter allow={['needs-onboarding']} />;

/** Routes only accessible to users with pending invitations. */
export const InvitationRoutes = () => <StateRouter allow={['has-invitations']} />;

/** Routes only accessible to users with an active company membership. */
export const AppRoutes = () => <StateRouter allow={['active']} />;

/** Root shell — loads the current user, then renders the matched route. */
export const AppShell = () => {
    return (
        <Suspense
            fallback={
                <div className='flex h-dvh items-center justify-center'>
                    <Spinner className='size-8' />
                </div>
            }
        >
            <CurrentUserProvider>
                <Outlet />
            </CurrentUserProvider>
        </Suspense>
    );
};
